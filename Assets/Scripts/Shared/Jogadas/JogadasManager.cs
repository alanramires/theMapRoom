using System.Collections.Generic;
using System.Linq;
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

    public void RegistrarAtaque(int turno, int team, int cx, int cy, int dx, int dy, string sigla, int uid, int uidAlvo)
    {
        log.Registrar(new Jogada
        {
            turno = turno, team = team, acao = "Ataque",
            cx = cx, cy = cy, dx = dx, dy = dy,
            unidadeSigla = sigla, uid = uid, uid2 = uidAlvo
        });
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

    public void RegistrarCapturar(int turno, int team, int cx, int cy, int dx, int dy, string sigla, int uid)
    {
        log.Registrar(new Jogada
        {
            turno = turno, team = team, acao = "Capturar",
            cx = cx, cy = cy, dx = dx, dy = dy,
            unidadeSigla = sigla, uid = uid
        });
    }

    public List<Jogada> UltimasRodadas(int n)
    {
        if (log.jogadas == null || log.jogadas.Count == 0) return new List<Jogada>();
        int turnoAtual = log.jogadas.Max(j => j.turno);
        return log.jogadas.Where(j => j.turno > turnoAtual - n).ToList();
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
            manager.RegistrarAtaque(turno, team, from.x, from.y, tCell.x, tCell.y, sigla, uid, uidAlvo);
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
            manager.RegistrarCapturar(turno, team, from.x, from.y, to.x, to.y, sigla, uid);
        else
            manager.RegistrarMover(turno, team, from.x, from.y, to.x, to.y, sigla, uid);
    }
}
