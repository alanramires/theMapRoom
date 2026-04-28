using System.Collections.Generic;
using UnityEngine;

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

// UnitRole está em Assets/Scripts/Units/UnitRole.cs
// valores: None, Capturador, Assalto, Transportador, Logistica, FogoIndireto

[System.Serializable]
public class SlotNeed
{
    public UnitRole Role;
    public bool     Filled;
    public int      AssignedUnitId = -1;
}

[System.Serializable]
public class SectorObjective
{
    [Header("Alvo")]
    public ConstructionSector Sector;
    public TeamId             AssignedTeam;
    public ObjectiveStatus    Status;
    public int                Priority;

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
}
