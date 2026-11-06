using System.Collections.Generic;
using UnityEngine;

public enum PressaoCaronaCategoria
{
    // Capturador do plano sem rota propria ate o alvo: so chega de carona AEREA.
    IlhadoComPlano,
    // Capturador do plano que quer carona mas tem chao ate o alvo (carona encurta, nao viabiliza).
    ComPlano,
    // IsUnderRepair: passageiro de hospital, nao pede onibus novo.
    Emergencia,
    // Capturador sem objetivo (ex.: corpo da conscricao nascido numa ilha ja fechada).
    Rogue,
    // Nao captura (AAA/SAM rogue aceitando carona). Disputa banco, nao justifica onibus.
    NaoCapturador,
}

public sealed class PressaoCaronaPedido
{
    public UnitManager Unit;
    public PressaoCaronaCategoria Categoria;
    public QueroCaronaResult Result;
}

public sealed class PressaoCaronaOnibus
{
    public UnitManager Unit;
    public int Used;
    public int Capacity;
    public bool Air;
}

public sealed class PressaoCaronaLeitura
{
    public int Slot;
    public readonly List<PressaoCaronaOnibus> Buses = new List<PressaoCaronaOnibus>();
    public readonly List<PressaoCaronaPedido> Pedidos = new List<PressaoCaronaPedido>();
    public int Aboard;
    public int AirSeatsTotal;
    public int AirSeatsFree;
    public int LandSeatsTotal;
    public int LandSeatsFree;
    public int Errors;

    public int SeatsTotal => AirSeatsTotal + LandSeatsTotal;
    public int SeatsFree => AirSeatsFree + LandSeatsFree;

    public int Count(PressaoCaronaCategoria categoria)
    {
        int n = 0;
        foreach (PressaoCaronaPedido p in Pedidos)
            if (p.Categoria == categoria)
                n++;
        return n;
    }

    // A pressao que a COMPRA pode ler: capturador do plano ilhado contra banco AEREO livre.
    // APC nao atravessa agua, entao banco terrestre nao abate esta fila.
    public int StrandedAirSeatShortage =>
        Mathf.Max(0, Count(PressaoCaronaCategoria.IlhadoComPlano) - AirSeatsFree);
}

/// <summary>
/// Consumidor (camada do meio): pergunta ao QueroCaronaService UMA vez por unidade do slot,
/// com o MESMO pedido que o capturador monta, e cruza com os bancos livres dos transportes.
/// Nao decide nada: a compra (piso de Chinook) e a tela "Tools/Transporte/Pressao Carona"
/// leem esta mesma leitura.
///
/// Limite: banco e capacidade bruta do transporte, sem compatibilidade de slot (quem cabe em
/// qual banco e do PodeEmbarcar).
/// </summary>
public static class PressaoCaronaService
{
    public static PressaoCaronaLeitura Read(AIController ai, int slotIndex)
    {
        var leitura = new PressaoCaronaLeitura { Slot = slotIndex };
        if (ai == null)
            return leitura;

        TeamObjectivePlan plan = ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(slotIndex));
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == null || u.IsDead || u.SlotIndex != slotIndex
                || !u.TryGetUnitData(out UnitData d) || d == null)
                continue;

            if (d.isTransporter && d.transportSlots != null && d.transportSlots.Count > 0)
            {
                int cap = 0;
                for (int i = 0; i < d.transportSlots.Count; i++)
                    cap += u.GetCapacityForTransportSlot(i);
                int used = 0;
                if (u.TransportedUnitSlots != null)
                    foreach (UnitTransportSeatRuntime s in u.TransportedUnitSlots)
                        if (s != null && s.embarkedUnit != null)
                            used++;
                bool air = d.domain == Domain.Air;
                int free = Mathf.Max(0, cap - used);
                if (air) { leitura.AirSeatsTotal += cap; leitura.AirSeatsFree += free; }
                else { leitura.LandSeatsTotal += cap; leitura.LandSeatsFree += free; }
                leitura.Buses.Add(new PressaoCaronaOnibus { Unit = u, Used = used, Capacity = cap, Air = air });
                continue;
            }
            if (u.IsEmbarked)
            {
                leitura.Aboard++;
                continue;
            }
            if (d.domain == Domain.Air)
                continue;

            QueroCaronaResult r;
            bool comPlano;
            try
            {
                QueroCaronaRequest req = ai.BuildRidePressureRequest(u, plan, out comPlano);
                r = QueroCaronaService.Evaluate(req);
            }
            catch (System.Exception e)
            {
                leitura.Errors++;
                Debug.LogWarning($"[Pressao Carona] #{u.InstanceId}: {e.Message}");
                continue;
            }
            if (r == null || !r.wantsRide)
                continue;

            bool capturer = UnitRoleCompatibility.ResolveCompositionRole(d) == UnitRole.Capturador;
            PressaoCaronaCategoria categoria =
                r.isEmergency ? PressaoCaronaCategoria.Emergencia
                : !capturer ? PressaoCaronaCategoria.NaoCapturador
                : !comPlano ? PressaoCaronaCategoria.Rogue
                : r.isStranded ? PressaoCaronaCategoria.IlhadoComPlano
                : PressaoCaronaCategoria.ComPlano;
            leitura.Pedidos.Add(new PressaoCaronaPedido { Unit = u, Categoria = categoria, Result = r });
        }
        return leitura;
    }
}
