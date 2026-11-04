using System;
using System.Collections.Generic;
using UnityEngine;

// Dono do DiagnosticoLog da partida. Mesmo ciclo de vida do JogadasManager: um
// por cena, criado sob demanda, restaurado pelo load. Ver DiagnosticoLog.cs
// para o contrato das entradas.
public class DiagnosticoManager : MonoBehaviour
{
    public static DiagnosticoManager Instance { get; private set; }

    // Observacao pura (guia de modding §6): quem assina recebe a entrada depois
    // de gravada e nao deve altera-la.
    public static event Action<DiagnosticoEntrada> OnEntradaRegistrada;

    [HideInInspector] public DiagnosticoLog log = new DiagnosticoLog();

    private readonly Dictionary<(string operacao, int rodada, int slot, int uid, int codigo), DiagnosticoEntrada> agregadas =
        new Dictionary<(string, int, int, int, int), DiagnosticoEntrada>();

    private MatchController match;
    private static int ativoFrame = -1;
    private static bool ativoCache;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static DiagnosticoManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        DiagnosticoManager existing = FindFirstObjectByType<DiagnosticoManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject go = new GameObject("Diagnostico");
        Instance = go.AddComponent<DiagnosticoManager>();
        return Instance;
    }

    // Lido uma vez por frame: o caminho quente (decisao da IA) pergunta isto
    // milhares de vezes por turno.
    public static bool Ativo
    {
        get
        {
            if (!Application.isPlaying)
                return false;
            int frame = Time.frameCount;
            if (frame != ativoFrame)
            {
                ativoFrame = frame;
                ativoCache = DebugManager.IsDiagnosticoNoSaveAtivo();
            }
            return ativoCache;
        }
    }

    public int RodadaAtual
    {
        get
        {
            ResolveMatch();
            return match != null ? match.CurrentTurn : 0;
        }
    }

    public int SlotAtivo
    {
        get
        {
            ResolveMatch();
            if (match == null)
                return -1;
            PlayerSlotId slot = match.ActiveSlotId;
            return slot.IsValid ? slot.Value : -1;
        }
    }

    /// <summary>
    /// Grava a entrada. Rodada e slot ficam com os da partida quando a entrada
    /// nao trouxer (rodada 0 / slot -1).
    /// </summary>
    public static DiagnosticoEntrada Registrar(DiagnosticoEntrada entrada)
    {
        if (entrada == null || !Ativo)
            return null;

        DiagnosticoManager manager = EnsureInstance();
        manager.Gravar(entrada);
        return entrada;
    }

    /// <summary>
    /// Entrada agregada: a mesma (operacao, rodada, slot, uid, codigo) soma em
    /// contagem em vez de repetir linha. Sem alocacao quando ja existe.
    /// </summary>
    public static bool TryGetAgregada(string operacao, int rodada, int slot, int uid, int codigo, out DiagnosticoEntrada entrada)
    {
        entrada = null;
        return Instance != null
            && Instance.agregadas.TryGetValue((operacao, rodada, slot, uid, codigo), out entrada)
            && entrada != null;
    }

    public static void RegistrarAgregada(DiagnosticoEntrada entrada)
    {
        if (entrada == null || !Ativo)
            return;

        DiagnosticoManager manager = EnsureInstance();
        entrada.agregada = true;
        manager.Gravar(entrada);
        manager.agregadas[(entrada.operacao, entrada.rodada, entrada.slot, entrada.uid, entrada.codigo)] = entrada;
    }

    public void RestaurarLog(DiagnosticoLog restaurado)
    {
        log = restaurado ?? new DiagnosticoLog();
        log.entradas ??= new List<DiagnosticoEntrada>();
        ReconstruirAgregadas();
    }

    private void Gravar(DiagnosticoEntrada entrada)
    {
        if (entrada.rodada <= 0)
            entrada.rodada = RodadaAtual;
        if (entrada.slot < 0)
            entrada.slot = SlotAtivo;
        if (string.IsNullOrEmpty(entrada.origem))
            entrada.origem = "core";

        entrada.id = log.proximoId++;
        log.entradas.Add(entrada);
        AplicarTeto();
        OnEntradaRegistrada?.Invoke(entrada);
    }

    // Teto (guia §15): passou do limite, descarta os 10% mais antigos de uma
    // vez e conta quantos foram. O Save Inspector mostra o descarte.
    private void AplicarTeto()
    {
        int max = Mathf.Max(100, DebugManager.GetDiagnosticoMaxEntradas());
        if (log.entradas.Count <= max)
            return;

        int remover = log.entradas.Count - max + Mathf.Max(1, max / 10);
        remover = Mathf.Min(remover, log.entradas.Count);
        log.entradas.RemoveRange(0, remover);
        log.descartadas += remover;
        ReconstruirAgregadas();
    }

    private void ReconstruirAgregadas()
    {
        agregadas.Clear();
        for (int i = 0; i < log.entradas.Count; i++)
        {
            DiagnosticoEntrada e = log.entradas[i];
            if (e == null || !e.agregada)
                continue;
            agregadas[(e.operacao, e.rodada, e.slot, e.uid, e.codigo)] = e;
        }
    }

    private void ResolveMatch()
    {
        if (match == null)
            match = FindFirstObjectByType<MatchController>();
    }
}
