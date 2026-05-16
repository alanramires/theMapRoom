using System.Collections.Generic;
using UnityEngine;

public enum AIStance { Tactical, Offensive, Defensive }

/// <summary>
/// Foto do estado do jogo no início do turno da IA.
/// Reconstruída a cada turno; não persiste entre rodadas.
/// </summary>
public class AIWorldSnapshot
{
    public TeamId AITeam;
    public int TurnNumber;
    public AIStance Stance;

    public List<UnitManager> MyUnits          = new List<UnitManager>();
    public List<UnitManager> EnemyUnits       = new List<UnitManager>();
    public List<ConstructionManager> MyBuildings      = new List<ConstructionManager>();
    public List<ConstructionManager> NeutralBuildings = new List<ConstructionManager>();
    public List<ConstructionManager> EnemyBuildings   = new List<ConstructionManager>();
    public HashSet<Vector3Int> OccupiedCells  = new HashSet<Vector3Int>();

    public ConstructionManager MyHQ;
    public ConstructionManager EnemyHQ;
    public int Budget;
    public int IncomePerTurn;

    public static AIWorldSnapshot Build(TeamId aiTeam, MatchController match)
    {
        var snap = new AIWorldSnapshot();
        snap.AITeam       = aiTeam;
        snap.TurnNumber   = match != null ? match.CurrentTurn : 0;
        snap.Budget       = match != null ? match.GetActualMoney(aiTeam) : 0;
        snap.IncomePerTurn = match != null ? match.GetIncomePerTurn(aiTeam) : 0;

        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.IsDead || u.IsEmbarked) continue;

            Vector3Int p = u.CurrentCellPosition; p.z = 0;
            snap.OccupiedCells.Add(p);

            if (u.TeamId == aiTeam) snap.MyUnits.Add(u);
            else if (!u.IsHiddenByFogOfWar) snap.EnemyUnits.Add(u);
        }

        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (c.TeamId == aiTeam)
            {
                snap.MyBuildings.Add(c);
                if (c.IsPlayerHeadQuarter && snap.MyHQ == null) snap.MyHQ = c;
            }
            else if (c.TeamId == TeamId.Neutral)
            {
                snap.NeutralBuildings.Add(c);
            }
            else
            {
                snap.EnemyBuildings.Add(c);
                if (c.IsPlayerHeadQuarter && snap.EnemyHQ == null) snap.EnemyHQ = c;
            }
        }

        snap.Stance = CalculateStance(snap);
        return snap;
    }

    /// <summary>
    /// Versão leve para o loop por-unidade da Fase 2.
    /// Preenche apenas os campos consumidos pelos handlers de role;
    /// omite MyUnits, EnemyUnits, OccupiedCells, Stance e IncomePerTurn.
    /// </summary>
    public static AIWorldSnapshot BuildLight(TeamId aiTeam, MatchController match)
    {
        var snap = new AIWorldSnapshot();
        snap.AITeam     = aiTeam;
        snap.TurnNumber = match != null ? match.CurrentTurn : 0;
        snap.Budget     = match != null ? match.GetActualMoney(aiTeam) : 0;

        // MyUnits: necessário para o handler de Logistics (FindLogisticsServiceTarget,
        // TryBuildLogisticsSupplyAction, CalculateLogisticsRearAreaScore, etc.).
        // Omitimos OccupiedCells e EnemyUnits (com seu custo de fog-of-war) — nenhum
        // handler de role lê essas listas do snapshot.
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.IsDead || u.IsEmbarked) continue;
            if (u.TeamId == aiTeam) snap.MyUnits.Add(u);
        }

        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (c.TeamId == aiTeam)
            {
                if (c.IsPlayerHeadQuarter && snap.MyHQ == null) snap.MyHQ = c;
            }
            else if (c.TeamId != TeamId.Neutral)
            {
                snap.EnemyBuildings.Add(c);
                if (c.IsPlayerHeadQuarter && snap.EnemyHQ == null) snap.EnemyHQ = c;
            }
        }

        return snap;
    }

    private static AIStance CalculateStance(AIWorldSnapshot snap)
    {
        // Defensiva: inimigo a ≤4 células do nosso QG
        if (snap.MyHQ != null && snap.EnemyUnits.Count > 0)
        {
            Vector3Int hq = snap.MyHQ.CurrentCellPosition; hq.z = 0;
            foreach (UnitManager enemy in snap.EnemyUnits)
            {
                Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
                if (ChebyshevDistance(hq, ec) <= 4)
                    return AIStance.Defensive;
            }
        }

        // Ofensiva: AI tem ≥65% do HP total combinado
        int myHp = 0, enemyHp = 0;
        foreach (UnitManager u in snap.MyUnits)    myHp    += u.CurrentHP;
        foreach (UnitManager u in snap.EnemyUnits) enemyHp += u.CurrentHP;

        if (myHp + enemyHp > 0)
        {
            float ratio = (float)myHp / (myHp + enemyHp);
            if (ratio > 0.65f) return AIStance.Offensive;
            if (ratio < 0.35f) return AIStance.Defensive;
        }

        return AIStance.Tactical;
    }

    // Aproximação rápida de distância hex por Chebyshev (suficiente para trigger de stance)
    private static int ChebyshevDistance(Vector3Int a, Vector3Int b) =>
        Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
}
