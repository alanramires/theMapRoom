using System.Collections.Generic;
using UnityEngine;
// --------------------------------------------------------------------------------------------
// Gerencia os objetivos de captura para cada equipe, incluindo status, slots de unidade e or�amento reservado.
// O ObjectiveManager � um singleton acess�vel globalmente, permitindo que os sistemas 
// de IA consultem e atualizem os planos de objetivos de equipe conforme necess�rio.
// -------------------------------------------------------------------------------------------- 
public enum ObjectiveStatus
{
    Pending,               // setor escolhido, nenhuma unidade alocada ainda
    Pursuing,              // unidade(s) a caminho
    Capturing,             // capturador está no prédio
    PartialReadyForHandoff,// captura parcial; slot transferido para substituto mais saudável
    Defending,             // setor controlado, mantendo guarda
    Complete,              // totalmente controlado, sem pressão
    Abandoned,             // descartado por realocação ou pressão insuportável
}

public enum AIObjectiveType
{
    CaptureSector,
    RallyAssembly,
    InvasionAttack
}

public enum AIRallyAssemblyState
{
    None,
    WaitHold,
    Assembling,
    Ready,
    GoGreen,
    Expired
}

// UnitRole está em Assets/Scripts/Units/UnitRole.cs
// valores: None, Capturador, Assalto, Transportador, Logistica, FogoIndireto

[System.Serializable]
public class SlotNeed
{
    public UnitRole Role;
    public bool     Filled;
    public int      AssignedUnitId = -1;
    // PM reais (terreno) da unidade até o objetivo; -1 = desconhecido.
    // Unidades embarcadas propagam a posição do transportador.
    public int      DistanceToObjective = -1;
}

[System.Serializable]
public class SectorObjective
{
    [Header("Alvo")]
    public ConstructionSector Sector;
    public TeamId             AssignedTeam;
    public ObjectiveStatus    Status;
    public AIObjectiveType    ObjectiveType;
    public int                Priority;

    [Header("Rally")]
    public AIRallyAssemblyState RallyState = AIRallyAssemblyState.None;
    public int                 RallyAssemblyStartedTurn = -1;
    public int                 RallyGoGreenTurn = -1;
    public string              RallyReadinessReason;

    [Header("Slots de unidade")]
    public List<SlotNeed> Slots = new List<SlotNeed>();

    [Header("Orçamento reservado")]
    public int BudgetReserved;

    [Header("Handoff")]
    public bool HandoffEligible;
    public int  PreferredHandoffFromUnitId = -1;

    public bool HasOpenSlot(UnitRole role)
    {
        foreach (SlotNeed s in Slots)
            if (s.Role == role && !s.Filled) return true;
        return false;
    }

    public bool TryFillSlot(UnitRole role, int unitId)
    {
        foreach (SlotNeed s in Slots)
        {
            if (s.Role == role && !s.Filled)
            {
                s.Filled         = true;
                s.AssignedUnitId = unitId;
                return true;
            }
        }
        return false;
    }
}

[System.Serializable]
public class TeamObjectivePlan
{
    public TeamId                Team;
    public List<SectorObjective> Objectives   = new List<SectorObjective>();
    public List<int>             RogueUnitIds = new List<int>();

    public List<int>                     HandoffVacaterIds      = new List<int>();
    public HashSet<ConstructionSector>   VacaterForwardSectors  = new HashSet<ConstructionSector>();

    public int TotalReserved
    {
        get
        {
            int sum = 0;
            foreach (SectorObjective obj in Objectives) sum += obj.BudgetReserved;
            return sum;
        }
    }

    public SectorObjective GetObjectiveForSector(ConstructionSector sector)
    {
        foreach (SectorObjective obj in Objectives)
            if (obj.Sector == sector) return obj;
        return null;
    }
}

public class ObjectiveManager : MonoBehaviour
{
    private static ObjectiveManager instance;
    public static ObjectiveManager Instance => EnsureInstance();

    [SerializeField] private List<TeamObjectivePlan> plans = new List<TeamObjectivePlan>();

    public IReadOnlyList<TeamObjectivePlan> Plans => plans;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() => EnsureInstance();

    private static ObjectiveManager EnsureInstance()
    {
        if (instance != null) return instance;
        instance = FindAnyObjectByType<ObjectiveManager>();
        if (instance != null) return instance;
        GameObject go = new GameObject(nameof(ObjectiveManager));
        instance = go.AddComponent<ObjectiveManager>();
        DontDestroyOnLoad(go);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public static TeamObjectivePlan GetPlanForTeam(TeamId team)
    {
        ObjectiveManager m = EnsureInstance();
        foreach (TeamObjectivePlan p in m.plans)
            if (p.Team == team) return p;
        return null;
    }

    public static TeamObjectivePlan GetOrCreatePlanForTeam(TeamId team)
    {
        ObjectiveManager m = EnsureInstance();
        foreach (TeamObjectivePlan p in m.plans)
            if (p.Team == team) return p;
        TeamObjectivePlan newPlan = new TeamObjectivePlan { Team = team };
        m.plans.Add(newPlan);
        return newPlan;
    }

    public static void ClearPlanForTeam(TeamId team)
    {
        ObjectiveManager m = EnsureInstance();
        foreach (TeamObjectivePlan p in m.plans)
        {
            if (p.Team != team) continue;
            p.Objectives.Clear();
            p.RogueUnitIds.Clear();
            return;
        }
    }

    public static List<AIObjectivePlanSaveData> BuildSaveData()
    {
        ObjectiveManager m = EnsureInstance();
        var result = new List<AIObjectivePlanSaveData>();
        foreach (TeamObjectivePlan plan in m.plans)
        {
            if (plan == null)
                continue;

            var savedPlan = new AIObjectivePlanSaveData
            {
                teamId = (int)plan.Team,
                rogueUnitIds = plan.RogueUnitIds != null ? new List<int>(plan.RogueUnitIds) : new List<int>(),
                handoffVacaterIds = plan.HandoffVacaterIds != null ? new List<int>(plan.HandoffVacaterIds) : new List<int>()
            };

            if (plan.VacaterForwardSectors != null)
            {
                foreach (ConstructionSector sector in plan.VacaterForwardSectors)
                    savedPlan.vacaterForwardSectors.Add((int)sector);
            }

            if (plan.Objectives != null)
            {
                foreach (SectorObjective obj in plan.Objectives)
                {
                    if (obj == null)
                        continue;

                    var savedObj = new AIObjectiveSaveData
                    {
                        sector = (int)obj.Sector,
                        assignedTeam = (int)obj.AssignedTeam,
                        status = (int)obj.Status,
                        objectiveType = (int)obj.ObjectiveType,
                        priority = obj.Priority,
                        budgetReserved = obj.BudgetReserved,
                        handoffEligible = obj.HandoffEligible,
                        preferredHandoffFromUnitId = obj.PreferredHandoffFromUnitId
                    };

                    if (obj.Slots != null)
                    {
                        foreach (SlotNeed slot in obj.Slots)
                        {
                            if (slot == null)
                                continue;

                            savedObj.slots.Add(new AISlotNeedSaveData
                            {
                                role = (int)slot.Role,
                                filled = slot.Filled,
                                assignedUnitId = slot.AssignedUnitId
                            });
                        }
                    }

                    savedPlan.objectives.Add(savedObj);
                }
            }

            result.Add(savedPlan);
        }

        return result;
    }

    public static void RestoreSaveData(List<AIObjectivePlanSaveData> savedPlans)
    {
        ObjectiveManager m = EnsureInstance();
        m.plans.Clear();
        if (savedPlans == null)
            return;

        foreach (AIObjectivePlanSaveData savedPlan in savedPlans)
        {
            if (savedPlan == null)
                continue;

            var plan = new TeamObjectivePlan
            {
                Team = (TeamId)savedPlan.teamId
            };

            if (savedPlan.rogueUnitIds != null)
                plan.RogueUnitIds.AddRange(savedPlan.rogueUnitIds);
            if (savedPlan.handoffVacaterIds != null)
                plan.HandoffVacaterIds.AddRange(savedPlan.handoffVacaterIds);
            if (savedPlan.vacaterForwardSectors != null)
            {
                foreach (int sector in savedPlan.vacaterForwardSectors)
                    plan.VacaterForwardSectors.Add((ConstructionSector)sector);
            }

            if (savedPlan.objectives != null)
            {
                foreach (AIObjectiveSaveData savedObj in savedPlan.objectives)
                {
                    if (savedObj == null)
                        continue;

                    var obj = new SectorObjective
                    {
                        Sector = (ConstructionSector)savedObj.sector,
                        AssignedTeam = (TeamId)savedObj.assignedTeam,
                        Status = (ObjectiveStatus)savedObj.status,
                        ObjectiveType = System.Enum.IsDefined(typeof(AIObjectiveType), savedObj.objectiveType)
                            ? (AIObjectiveType)savedObj.objectiveType
                            : AIObjectiveType.CaptureSector,
                        Priority = savedObj.priority,
                        BudgetReserved = savedObj.budgetReserved,
                        HandoffEligible = savedObj.handoffEligible,
                        PreferredHandoffFromUnitId = savedObj.preferredHandoffFromUnitId
                    };

                    if (savedObj.slots != null)
                    {
                        foreach (AISlotNeedSaveData savedSlot in savedObj.slots)
                        {
                            if (savedSlot == null)
                                continue;

                            obj.Slots.Add(new SlotNeed
                            {
                                Role = (UnitRole)savedSlot.role,
                                Filled = savedSlot.filled,
                                AssignedUnitId = savedSlot.assignedUnitId
                            });
                        }
                    }

                    plan.Objectives.Add(obj);
                }
            }

            m.plans.Add(plan);
        }
    }
}


