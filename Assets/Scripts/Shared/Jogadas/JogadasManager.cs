using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

// Attach to the "Jogadas" GameObject in the scene.
public class JogadasManager : MonoBehaviour
{
    public static JogadasManager Instance { get; private set; }

    [HideInInspector] public JogadasLog log = new JogadasLog();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public static JogadasManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        JogadasManager existing = FindFirstObjectByType<JogadasManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject go = new GameObject("Jogadas");
        Instance = go.AddComponent<JogadasManager>();
        return Instance;
    }

    public void RegistrarServicoComando(int turno, int team)
    {
        log.Registrar(new Jogada { turno = turno, team = team, acao = "ServicoComando" });
    }

    public void RegistrarCompra(int turno, int team, int cx, int cy, string sigla, int uid)
    {
        log.Registrar(new Jogada
        {
            turno = turno, team = team, acao = "Compra",
            cx = cx, cy = cy, unidadeSigla = sigla, uid = uid
        });
    }

    public void RegistrarDestruir(int turno, int team, int cx, int cy, string sigla, int uid)
    {
        log.Registrar(new Jogada
        {
            turno = turno, team = team, acao = "Destruir",
            cx = cx, cy = cy, unidadeSigla = sigla, uid = uid
        });
    }

    public void RegistrarSuprir(int turno, int team, int cx, int cy, int dx, int dy, string sigla, int uid)
    {
        log.Registrar(new Jogada
        {
            turno = turno, team = team, acao = "Suprir",
            cx = cx, cy = cy, dx = dx, dy = dy,
            unidadeSigla = sigla, uid = uid
        });
    }

    public void RegistrarSuprindo(int turno, int team, int cx, int cy, string sigla, int uid)
    {
        log.Registrar(new Jogada
        {
            turno = turno, team = team, acao = "Suprindo",
            cx = cx, cy = cy,
            unidadeSigla = sigla, uid = uid
        });
    }

    public void RegistrarTransferir(int turno, int team, int cx, int cy, string sigla, int uid)
    {
        log.Registrar(new Jogada
        {
            turno = turno, team = team, acao = "Transferir",
            cx = cx, cy = cy, unidadeSigla = sigla, uid = uid
        });
    }

    public void RegistrarEstacionario(int turno, int team, int cx, int cy, string sigla, int uid)
    {
        log.Registrar(new Jogada
        {
            turno = turno, team = team, acao = "Estacionario",
            cx = cx, cy = cy,
            unidadeSigla = sigla, uid = uid
        });
    }

    public void RegistrarFusao(int turno, int team, int cx, int cy, int dx, int dy, string sigla, int uid, int uidAlvo)
    {
        log.Registrar(new Jogada
        {
            turno = turno, team = team, acao = "Fusao",
            cx = cx, cy = cy, dx = dx, dy = dy,
            unidadeSigla = sigla, uid = uid, uid2 = uidAlvo
        });
    }

    public void RegistrarDesembarque(int turno, int team, int cx, int cy, int dx, int dy, string sigla, int uid)
    {
        log.Registrar(new Jogada
        {
            turno = turno, team = team, acao = "Desembarque",
            cx = cx, cy = cy, dx = dx, dy = dy,
            unidadeSigla = sigla, uid = uid
        });
    }

    public void RegistrarDesembarcando(int turno, int team, int cx, int cy, int dx, int dy, string sigla, int uid)
    {
        log.Registrar(new Jogada
        {
            turno = turno, team = team, acao = "Desembarcando",
            cx = cx, cy = cy, dx = dx, dy = dy,
            unidadeSigla = sigla, uid = uid
        });
    }

    public void RegistrarAtaque(
        int turno, int team, int cx, int cy, int dx, int dy, string sigla, int uid, int uidAlvo,
        CombatLogResult combatResult = null)
    {
        var jogada = new Jogada
        {
            turno = turno, team = team, acao = "Ataque",
            cx = cx, cy = cy, dx = dx, dy = dy,
            unidadeSigla = sigla, uid = uid, uid2 = uidAlvo
        };
        if (combatResult != null)
        {
            jogada.hasCombatResult = true;
            jogada.unidadeSigla = combatResult.AttackerSigla;
            jogada.unidadeSigla2 = combatResult.DefenderSigla;
            jogada.team = combatResult.AttackerTeam;
            jogada.team2 = combatResult.DefenderTeam;
            jogada.hpAntes = combatResult.AttackerHpBefore;
            jogada.hpDepois = combatResult.AttackerHpAfter;
            jogada.hp2Antes = combatResult.DefenderHpBefore;
            jogada.hp2Depois = combatResult.DefenderHpAfter;
            jogada.combatCargo = combatResult.CargoResults ?? new List<CombatCargoResult>();
            jogada.obs = combatResult.ToString();
        }
        log.Registrar(jogada);
    }

    public void RegistrarEmbarque(int turno, int team, int cx, int cy, int dx, int dy, string sigla, int uid, int uidTransporte)
    {
        log.Registrar(new Jogada
        {
            turno = turno, team = team, acao = "Embarque",
            cx = cx, cy = cy, dx = dx, dy = dy,
            unidadeSigla = sigla, uid = uid, uid2 = uidTransporte
        });
    }

    public void RegistrarMover(int turno, int team, int cx, int cy, int dx, int dy, string sigla, int uid)
    {
        log.Registrar(new Jogada
        {
            turno = turno, team = team, acao = "Mover",
            cx = cx, cy = cy, dx = dx, dy = dy,
            unidadeSigla = sigla, uid = uid
        });
    }

    public void RegistrarCapturar(int turno, int team, int cx, int cy, int dx, int dy, string sigla, int uid, string obs = null)
    {
        log.Registrar(new Jogada
        {
            turno = turno, team = team, acao = "Capturar",
            cx = cx, cy = cy, dx = dx, dy = dy,
            unidadeSigla = sigla, uid = uid, obs = obs
        });
    }

    public List<Jogada> UltimasRodadas(int n)
    {
        if (log.jogadas == null || log.jogadas.Count == 0) return new List<Jogada>();
        int turnoAtual = log.jogadas.Max(j => j.turno);
        return log.jogadas.Where(j => j.turno > turnoAtual - n).ToList();
    }

    // -----------------------------------------------------------------------
    // Export das jogadas
    // -----------------------------------------------------------------------
    public enum JogadasExportFormat { Csv, Texto }

    // Escreve todas as jogadas registradas em um arquivo. Retorna o caminho gerado.
    // Sem path → salva em Application.persistentDataPath com timestamp.
    public string ExportToFile(JogadasExportFormat format = JogadasExportFormat.Csv, string path = null)
    {
        string ext = format == JogadasExportFormat.Csv ? "csv" : "txt";
        if (string.IsNullOrEmpty(path))
            path = Path.Combine(Application.persistentDataPath, $"jogadas_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}");

        string content = format == JogadasExportFormat.Csv
            ? BuildCsv(log?.jogadas)
            : BuildTexto(log?.jogadas);

        File.WriteAllText(path, content, new UTF8Encoding(true));
        Debug.Log($"[Jogadas] exportado ({format}) {log?.jogadas?.Count ?? 0} jogada(s) em: {path}");
        return path;
    }

    public static string BuildCsv(IEnumerable<Jogada> jogadas)
    {
        var sb = new StringBuilder();
        sb.Append("jogadaId,turno,team,timeNome,acao,sigla,uid,hpAntes,hpDepois,team2,time2Nome,sigla2,uid2,hp2Antes,hp2Depois,cx,cy,coordTipo,dx,dy,destinoTipo,carga,obs\n");
        if (jogadas != null)
            foreach (Jogada j in jogadas.OrderBy(j => j.jogadaId))
            {
                sb.Append(j.jogadaId).Append(',')
                  .Append(j.turno).Append(',')
                  .Append(j.team).Append(',')
                  .Append(CsvCampo(NomeTime(j.team))).Append(',')
                  .Append(CsvCampo(j.acao)).Append(',')
                  .Append(CsvCampo(j.unidadeSigla)).Append(',')
                  .Append(j.uid).Append(',')
                  .Append(j.hasCombatResult ? j.hpAntes : -1).Append(',')
                  .Append(j.hasCombatResult ? j.hpDepois : -1).Append(',')
                  .Append(j.hasCombatResult ? j.team2 : -1).Append(',')
                  .Append(CsvCampo(j.hasCombatResult ? NomeTime(j.team2) : "")).Append(',')
                  .Append(CsvCampo(j.unidadeSigla2)).Append(',')
                  .Append(j.uid2).Append(',')
                  .Append(j.hasCombatResult ? j.hp2Antes : -1).Append(',')
                  .Append(j.hasCombatResult ? j.hp2Depois : -1).Append(',')
                  .Append(j.cx).Append(',')
                  .Append(j.cy).Append(',')
                  .Append(CsvCampo(j.TemCoordenada ? TipoConstrucaoNaCelula(j.cx, j.cy) : "")).Append(',')
                  .Append(j.dx).Append(',')
                  .Append(j.dy).Append(',')
                  .Append(CsvCampo(j.TemDestino ? DestinoLabel(j.dx, j.dy) : "")).Append(',')
                  .Append(CsvCampo(FormatCargoResults(j.combatCargo))).Append(',')
                  .Append(CsvCampo(j.obs)).Append('\n');
            }
        return sb.ToString();
    }

    public static string BuildTexto(IEnumerable<Jogada> jogadas)
    {
        var list = (jogadas ?? Enumerable.Empty<Jogada>()).OrderBy(j => j.jogadaId).ToList();
        var sb = new StringBuilder();
        sb.Append("# Jogadas da Partida\n");
        sb.Append($"# Exportado em {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        sb.Append($"# Total: {list.Count} jogada(s)\n\n");

        foreach (int turno in list.Select(j => j.turno).Distinct().OrderBy(t => t))
        {
            sb.Append($"== Turno {turno} ==\n");
            foreach (Jogada j in list.Where(j => j.turno == turno))
                sb.Append("  ").Append(DescreverJogada(j)).Append('\n');
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static string DescreverJogada(Jogada j)
    {
        string sigla = string.IsNullOrEmpty(j.unidadeSigla) ? "-" : j.unidadeSigla;
        string ator  = j.uid > 0 ? $"{sigla}#{j.uid}" : sigla;
        string ct    = j.TemCoordenada ? TipoConstrucaoNaCelula(j.cx, j.cy) : "";
        string dt    = j.TemDestino ? DestinoLabel(j.dx, j.dy) : "";
        string coord = j.TemCoordenada ? $"({j.cx},{j.cy}{(string.IsNullOrEmpty(ct) ? "" : " " + ct)})" : "";
        string dest  = j.TemDestino ? $" → ({j.dx},{j.dy}{(string.IsNullOrEmpty(dt) ? "" : " " + dt)})" : "";
        string alvo = j.uid2 > 0
            ? j.hasCombatResult
                ? $" vs {(string.IsNullOrEmpty(j.unidadeSigla2) ? "-" : j.unidadeSigla2)}#{j.uid2}"
                : $" alvo#{j.uid2}"
            : "";
        string hp = j.hasCombatResult
            ? $" {j.hpAntes}→{j.hpDepois} vs {j.hp2Antes}→{j.hp2Depois}"
            : "";
        string cargo = FormatCargoResults(j.combatCargo);
        string details = string.IsNullOrEmpty(cargo)
            ? ""
            : $" [{cargo}]";
        string obs = !j.hasCombatResult && !string.IsNullOrEmpty(j.obs)
            ? $" [{j.obs}]"
            : "";
        return $"#{j.jogadaId,-4} [{NomeTime(j.team)}] {j.acao,-14} {ator}{hp} {coord}{dest}{alvo}{details}{obs}".TrimEnd();
    }

    private static string NomeTime(int team)
        => Enum.IsDefined(typeof(TeamId), team) ? ((TeamId)team).ToString() : team.ToString();

    private static string CsvCampo(string s)
    {
        s ??= "";
        return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? "\"" + s.Replace("\"", "\"\"") + "\""
            : s;
    }

    // -----------------------------------------------------------------------
    // Resolução da construção numa célula (compartilhada por tabela e export).
    // Só resolve em Play mode, quando ConstructionManager.AllActive está populado.
    // -----------------------------------------------------------------------

    // Rótulo da construção na célula de origem (ex.: "HQ B", "Fáb B", "City D"). Vazio se não houver.
    public static string TipoConstrucaoNaCelula(int cx, int cy)
    {
        ConstructionManager c = ConstrucaoNaCelula(cx, cy);
        return c != null ? LabelComBadge(c) : "";
    }

    // Rótulo do destino: só para alvos capturáveis (cidades/HQ), com badge do setor
    // (ex.: "City A"). Terreno e estruturas não-capturáveis retornam vazio.
    public static string DestinoLabel(int dx, int dy)
    {
        ConstructionManager c = ConstrucaoNaCelula(dx, dy);
        if (c == null || (!c.IsCapturable && !c.IsVictoryBuilding))
            return "";
        return LabelComBadge(c);
    }

    private static string LabelComBadge(ConstructionManager c)
        => $"{TipoCurto(c)} {SectorBadge(c.Sector)}".Trim();

    // Obs preciso vindo da execução da captura (TurnStateManager.Capture sabe o tipo de operação).
    // Pendurado por unidade até o registro central da jogada consumir.
    private static readonly Dictionary<int, string> _captureObsPorUnidade = new Dictionary<int, string>();
    private static readonly Dictionary<int, CombatLogResult> _combatResultPorAtacante =
        new Dictionary<int, CombatLogResult>();

    public sealed class CombatLogResult
    {
        public string AttackerSigla;
        public int AttackerTeam;
        public int AttackerHpBefore;
        public int AttackerHpAfter;
        public string DefenderSigla;
        public int DefenderTeam;
        public int DefenderHpBefore;
        public int DefenderHpAfter;
        public List<CombatCargoResult> CargoResults = new List<CombatCargoResult>();

        public override string ToString()
        {
            string cargo = FormatCargoResults(CargoResults);
            return $"{AttackerSigla} {AttackerHpBefore}→{AttackerHpAfter} vs " +
                   $"{DefenderSigla} {DefenderHpBefore}→{DefenderHpAfter}" +
                   (string.IsNullOrEmpty(cargo) ? "" : $" [{cargo}]");
        }
    }

    public sealed class RuntimeCargoSnapshot
    {
        public UnitManager Unit;
        public CombatCargoResult Result;
    }

    public static List<RuntimeCargoSnapshot> CaptureCombatCargoSnapshot(
        UnitManager attacker,
        UnitManager defender)
    {
        var result = new List<RuntimeCargoSnapshot>();
        CollectCargoSnapshotRecursive(attacker, attacker != null ? attacker.InstanceId : 0, 0, 1, result);
        if (defender != null && defender != attacker)
            CollectCargoSnapshotRecursive(defender, defender.InstanceId, 0, 1, result);
        return result;
    }

    private static void CollectCargoSnapshotRecursive(
        UnitManager transporter,
        int rootUid,
        int parentUid,
        int depth,
        List<RuntimeCargoSnapshot> result)
    {
        if (transporter == null || result == null)
            return;
        IReadOnlyList<UnitTransportSeatRuntime> seats = transporter.TransportedUnitSlots;
        if (seats == null)
            return;

        var processed = new HashSet<int>();
        foreach (UnitTransportSeatRuntime seat in seats)
        {
            UnitManager child = seat != null ? seat.embarkedUnit : null;
            if (child == null || child.InstanceId <= 0 || !processed.Add(child.InstanceId))
                continue;
            child.TryGetUnitData(out UnitData data);
            result.Add(new RuntimeCargoSnapshot
            {
                Unit = child,
                Result = new CombatCargoResult
                {
                    rootUid = rootUid,
                    parentUid = parentUid > 0 ? parentUid : transporter.InstanceId,
                    depth = depth,
                    uid = child.InstanceId,
                    team = (int)child.TeamId,
                    sigla = ResolveUnitSigla(child),
                    hpAntes = Mathf.Max(0, child.CurrentHP),
                    cost = data != null ? data.cost : 0,
                    eliteLevel = data != null ? data.eliteLevel : 0,
                    unitClass = data != null ? data.unitClass : GameUnitClass.Infantry,
                }
            });
            CollectCargoSnapshotRecursive(child, rootUid, child.InstanceId, depth + 1, result);
        }
    }

    public static void SetUltimoAtaqueResultado(
        UnitManager attacker,
        UnitManager defender,
        int attackerHpBefore,
        int defenderHpBefore,
        List<RuntimeCargoSnapshot> cargoSnapshot = null)
    {
        if (attacker == null || defender == null || attacker.InstanceId <= 0)
            return;
        _combatResultPorAtacante[attacker.InstanceId] = new CombatLogResult
        {
            AttackerSigla = ResolveUnitSigla(attacker),
            AttackerTeam = (int)attacker.TeamId,
            AttackerHpBefore = Mathf.Max(0, attackerHpBefore),
            AttackerHpAfter = Mathf.Max(0, attacker.CurrentHP),
            DefenderSigla = ResolveUnitSigla(defender),
            DefenderTeam = (int)defender.TeamId,
            DefenderHpBefore = Mathf.Max(0, defenderHpBefore),
            DefenderHpAfter = Mathf.Max(0, defender.CurrentHP),
        };
        if (cargoSnapshot != null)
        {
            foreach (RuntimeCargoSnapshot snapshot in cargoSnapshot)
            {
                if (snapshot?.Result == null)
                    continue;
                snapshot.Result.hpDepois = snapshot.Unit != null
                    ? Mathf.Max(0, snapshot.Unit.CurrentHP)
                    : 0;
                if (snapshot.Result.hpDepois == snapshot.Result.hpAntes)
                    continue;
                snapshot.Result.cause = snapshot.Result.hpDepois <= 0
                    ? "TransportDestroyed"
                    : "TransportDamage";
                _combatResultPorAtacante[attacker.InstanceId].CargoResults.Add(snapshot.Result);
            }
        }
    }

    private static string FormatCargoResults(List<CombatCargoResult> cargo)
    {
        if (cargo == null || cargo.Count == 0)
            return "";
        var parts = new List<string>();
        foreach (CombatCargoResult item in cargo)
            parts.Add($"{new string('>', Mathf.Max(1, item.depth))}{item.sigla}#{item.uid} " +
                      $"{item.hpAntes}→{item.hpDepois}");
        return string.Join("; ", parts);
    }

    private static CombatLogResult ConsumirAtaqueResultado(int attackerUid)
    {
        if (attackerUid > 0
            && _combatResultPorAtacante.TryGetValue(attackerUid, out CombatLogResult result))
        {
            _combatResultPorAtacante.Remove(attackerUid);
            return result;
        }
        return null;
    }

    private static string ResolveUnitSigla(UnitManager unit)
        => unit != null && unit.TryGetUnitData(out UnitData data) && data != null
            ? data.apelido
            : "-";

    public static void SetUltimaCapturaObs(int capturerUid, string obs)
    {
        if (capturerUid > 0 && !string.IsNullOrEmpty(obs))
            _captureObsPorUnidade[capturerUid] = obs;
    }

    private static string ConsumirCapturaObs(int capturerUid)
    {
        if (capturerUid > 0 && _captureObsPorUnidade.TryGetValue(capturerUid, out string obs))
        {
            _captureObsPorUnidade.Remove(capturerUid);
            return obs;
        }
        return null;
    }

    // Fallback heurístico (estado pós-ação) quando a execução não forneceu o obs preciso:
    //  - inimigo/neutro parcial → "cur/max" (ex.: "10/20");
    //  - captura concluída (agora é nosso, cheio) → "capturado";
    //  - recuperando construção própria → "reparado".
    public static string ObsCaptura(int dx, int dy, int actingTeam)
    {
        ConstructionManager c = ConstrucaoNaCelula(dx, dy);
        if (c == null) return "";
        int max = c.CapturePointsMax;
        if (max <= 0) return "";

        int cur = Mathf.Clamp(c.CurrentCapturePoints, 0, max);
        bool dono = (int)c.TeamId == actingTeam;
        if (!dono) return $"{cur}/{max}";   // captura parcial de inimigo/neutro
        return cur >= max ? "capturado"     // concluiu a captura → nosso e cheio
                          : "reparado";      // recuperando construção própria
    }

    private static ConstructionManager ConstrucaoNaCelula(int x, int y)
    {
        List<ConstructionManager> all = ConstructionManager.AllActive;
        if (all == null) return null;

        var cell = new Vector3Int(x, y, 0);
        foreach (ConstructionManager c in all)
        {
            if (c == null) continue;
            Vector3Int cc = c.CurrentCellPosition; cc.z = 0;
            if (cc == cell) return c;
        }
        return null;
    }

    private static string TipoCurto(ConstructionManager c)
    {
        string tipo = "";
        if (c.TryResolveConstructionData(out ConstructionData d) && d != null)
        {
            if (!string.IsNullOrEmpty(d.sufixo)) tipo = d.sufixo; // preenchido manualmente
            else if (d.isAirport) tipo = "Aero";
            else if (d.isHarbor)  tipo = "Porto";
        }
        if (string.IsNullOrEmpty(tipo))
        {
            if (c.IsVictoryBuilding) tipo = "HQ";
            else if (c.OfferedUnits != null && c.OfferedUnits.Count > 0) tipo = "Fáb";
            else if (c.IsCapturable) tipo = "Cidade";
        }

        // Flag: refina pelo papel runtime da construção.
        if (string.Equals(tipo, "Flag", StringComparison.OrdinalIgnoreCase))
        {
            if (c.IsForwardObserverSpot) return "Spot";
            if (c.IsAnchorSector)        return "Anchor";
            if (c.IsRallyPoint)          return "Rally";
        }
        return tipo;
    }

    private static string SectorBadge(ConstructionSector sector)
    {
        if (sector == ConstructionSector.None || ConstructionSectorHelper.IsBase(sector))
            return "";
        string name = sector.ToString();
        return name.Length > 0 ? name[0].ToString().ToUpper() : "";
    }

    // Registra qualquer PlayerAction (AI ou humano) no Jogadas log.
    // Chamado por AIController.Phases após cada ação de Fase 2,
    // e por ReplayManager.PromoteCurrentBuffer para ações humanas.
    public static void RegistrarPlayerAction(PlayerAction action)
    {
        JogadasManager manager = EnsureInstance();
        if (manager == null) { Debug.LogWarning("[Jogadas] RegistrarPlayerAction: falha ao criar JogadasManager."); return; }
        if (action == null) return;
        if (action.ActionType != PlayerActionType.UnitAction) return;

        bool isCaptura     = action.SensorAction == SensorActionType.Capture;
        bool isEmbarque    = action.SensorAction == SensorActionType.Embark;
        bool isAtaque      = action.SensorAction == SensorActionType.Attack;
        bool isDesembarque = action.SensorAction == SensorActionType.Disembark;
        bool isFusao       = action.SensorAction == SensorActionType.Merge;
        bool isSuprir      = action.SensorAction == SensorActionType.Supply;
        bool isTransferir  = action.SensorAction == SensorActionType.Transfer;
        bool isEstacionario = !isCaptura && !isEmbarque && !isAtaque && !isDesembarque && !isFusao && !isSuprir && !isTransferir
                              && action.HasMoveTo && action.HasMoveFrom
                              && action.MoveTo == action.MoveFrom
                              && string.IsNullOrEmpty(action.TargetInstanceId);
        bool isMover = !isCaptura && !isEmbarque && !isAtaque && !isDesembarque && !isFusao && !isSuprir && !isTransferir && !isEstacionario
                       && action.HasMoveTo && action.HasMoveFrom
                       && action.MoveTo != action.MoveFrom
                       && string.IsNullOrEmpty(action.TargetInstanceId);
        if (!isCaptura && !isEmbarque && !isAtaque && !isDesembarque && !isFusao && !isSuprir && !isTransferir && !isEstacionario && !isMover)
        {
            Debug.Log($"[Jogadas] RegistrarPlayerAction ignorada: ActionType={action.ActionType} SensorAction={action.SensorAction} HasMoveTo={action.HasMoveTo} HasMoveFrom={action.HasMoveFrom} MoveFrom={action.MoveFrom} MoveTo={action.MoveTo}");
            return;
        }

        int turno = action.TurnNumber;
        int team  = (int)action.ActingTeam;

        int.TryParse(action.UnitInstanceId, out int uid);
        UnitManager unit = uid > 0 ? UnitManager.AllActive.Find(u => u != null && u.InstanceId == uid) : null;
        string sigla = unit != null && unit.TryGetUnitData(out UnitData ud) && ud != null ? ud.apelido : "-";

        Vector3Int from = action.MoveFrom; from.z = 0;

        if (isEmbarque)
        {
            Vector3Int tCell = action.TargetHex; tCell.z = 0;
            int.TryParse(action.TargetInstanceId, out int uidTransporte);
            manager.RegistrarEmbarque(turno, team, from.x, from.y, tCell.x, tCell.y, sigla, uid, uidTransporte);
            return;
        }

        if (isAtaque)
        {
            Vector3Int tCell = action.TargetHex; tCell.z = 0;
            int.TryParse(action.TargetInstanceId, out int uidAlvo);
            CombatLogResult combatResult = ConsumirAtaqueResultado(uid);
            manager.RegistrarAtaque(
                turno, team, from.x, from.y, tCell.x, tCell.y, sigla, uid, uidAlvo, combatResult);
            return;
        }

        if (isSuprir)
        {
            Vector3Int supplyCell = action.MoveTo; supplyCell.z = 0;
            manager.RegistrarSuprir(turno, team, from.x, from.y, supplyCell.x, supplyCell.y, sigla, uid);
            if (action.SubSteps != null)
            {
                foreach (PlayerActionSubStep step in action.SubSteps)
                {
                    if (step == null || !int.TryParse(step.TargetInstanceId, out int uidAlvo)) continue;
                    Vector3Int alvoCell = step.TargetHex; alvoCell.z = 0;
                    UnitManager alvo = UnitManager.AllActive.Find(u => u != null && u.InstanceId == uidAlvo);
                    string alvoSigla = alvo != null && alvo.TryGetUnitData(out UnitData ad) && ad != null ? ad.apelido : "-";
                    manager.RegistrarSuprindo(turno, team, alvoCell.x, alvoCell.y, alvoSigla, uidAlvo);
                }
            }
            return;
        }

        if (isTransferir)
        {
            manager.RegistrarTransferir(turno, team, from.x, from.y, sigla, uid);
            return;
        }

        if (isEstacionario)
        {
            manager.RegistrarEstacionario(turno, team, from.x, from.y, sigla, uid);
            return;
        }

        if (isFusao)
        {
            PlayerActionSubStep fusaoStep = action.SubSteps != null && action.SubSteps.Count > 0 ? action.SubSteps[0] : null;
            Vector3Int fusaoTarget = fusaoStep != null && fusaoStep.HasTargetHex ? fusaoStep.TargetHex : action.MoveTo;
            fusaoTarget.z = 0;
            int uidAlvo = fusaoStep != null && int.TryParse(fusaoStep.TargetInstanceId, out int parsed) ? parsed : 0;
            manager.RegistrarFusao(turno, team, from.x, from.y, fusaoTarget.x, fusaoTarget.y, sigla, uid, uidAlvo);
            return;
        }

        if (isDesembarque)
        {
            Vector3Int dropCell = action.MoveTo; dropCell.z = 0;
            manager.RegistrarDesembarque(turno, team, from.x, from.y, dropCell.x, dropCell.y, sigla, uid);
            if (action.SubSteps != null)
            {
                foreach (PlayerActionSubStep step in action.SubSteps)
                {
                    if (step == null || !int.TryParse(step.TargetInstanceId, out int uidPassageiro)) continue;
                    Vector3Int landCell = step.TargetHex; landCell.z = 0;
                    UnitManager passenger = UnitManager.AllActive.Find(u => u != null && u.InstanceId == uidPassageiro);
                    string passengerSigla = passenger != null && passenger.TryGetUnitData(out UnitData pd) && pd != null
                        ? pd.apelido : "-";
                    manager.RegistrarDesembarcando(turno, team, dropCell.x, dropCell.y, landCell.x, landCell.y,
                        passengerSigla, uidPassageiro);
                }
            }
            return;
        }

        Vector3Int to = action.MoveTo; to.z = 0;
        if (isCaptura)
            manager.RegistrarCapturar(turno, team, from.x, from.y, to.x, to.y, sigla, uid,
                ConsumirCapturaObs(uid) ?? ObsCaptura(to.x, to.y, team));
        else
            manager.RegistrarMover(turno, team, from.x, from.y, to.x, to.y, sigla, uid);
    }
}
