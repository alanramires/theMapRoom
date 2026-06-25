using System.Collections.Generic;
using UnityEngine;
// --------------------------------------------------------------------------------------------
// Necessidades Táticas da IA: Definição de Requisitos de Unidades para Operações Específicas
// O AITacticalNeed representa uma necessidade tática específica para uma operação, como defesa de base,
// captura de setor ou apoio aéreo. Ele inclui o tipo de necessidade, a fase atual da operação,
//os setores envolvidos, a equipe responsável, a prioridade, os slots de unidade necessários
// e outras informações relevantes. O AITacticalNeed é usado para guiar a alocação de unidades,
// atribuição de tarefas e a tomada de decisões táticas da IA, garantindo que as operações
// sejam apoiadas por recursos adequados e coordenados de forma eficaz.
// --------------------------------------------------------------------------------------------
public enum AINeedKind
{
    Capturer,
    Assault,
    FireSupport,
    Artillery,
    AAA,
    SAM,
    GroundTransport,
    AirTransport,
    FighterB,
    FighterA,
    Apache,
    AirTanker,
}

public enum AITacticalNeedType
{
    BaseDefense,
    SectorDefense,
    GroundCapture,
    AirliftCapture,
    AirInterception,
    AirRefuelSupport,
    PreventiveDefense,
    Reserve,
}

public enum AITacticalNeedPhase
{
    Forming,
    Moving,
    Engaging,
    Capturing,
    Holding,
    Complete,
    Aborted,
}

public class AITacticalSlotNeed
{
    public AINeedKind Kind;
    public bool Filled;
    public int AssignedUnitId = -1;
}

public class AITacticalNeed
{
    public int Id;
    public AITacticalNeedType Type;
    public AITacticalNeedPhase Phase;
    public ConstructionSector Sector;
    public TeamId Team;
    public int Priority;

    public Vector3Int AnchorCell;
    public Vector3Int TargetCell;

    public List<AITacticalSlotNeed> RequiredSlots = new List<AITacticalSlotNeed>();
    public List<int> AssignedUnitIds = new List<int>();

    public bool IsUrgent;
    public bool IsPreventive;
    public bool HasScreen;
    public int ScreenUnitId = -1;
    public float ScreenDistanceToTarget = -1f;
    public string CohesionReason = "";
    public int CreatedTurn;
    public int LastUpdatedTurn;

    public SectorObjective LinkedObjective;

    public int CountOpenSlots(AINeedKind kind)
    {
        int count = 0;
        foreach (AITacticalSlotNeed slot in RequiredSlots)
            if (slot.Kind == kind && !slot.Filled)
                count++;
        return count;
    }

    public bool HasOpenSlot(AINeedKind kind)
    {
        foreach (AITacticalSlotNeed slot in RequiredSlots)
            if (slot.Kind == kind && !slot.Filled)
                return true;
        return false;
    }

    public void AddSlots(AINeedKind kind, int count)
    {
        for (int i = 0; i < count; i++)
            RequiredSlots.Add(new AITacticalSlotNeed { Kind = kind });
    }
}

public struct TacticalDeficit
{
    public AITacticalNeed Operation;
    public AINeedKind Kind;
    public int Count;
}

public class AIShoppingDemand
{
    public UnitRole Role;
    public UnitRole ExactRole = UnitRole.None;
    public Domain? Domain;
    public GameUnitClass? TargetClass;
    public WeaponCategory? RequiredWeaponCategory;
    public int Count;
    public int Priority;
    public bool Urgent;
    public int MinEliteLevel;
    public int MaxEliteLevel = int.MaxValue;
    public string Origin;
    public string Reason;
}
