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
    public int AISlotIndex = -1;
    public int TurnNumber;
    public AIStance Stance;
    // Macro-estado ORTOGONAL à postura: a IA pode estar Offensive E invadindo ao mesmo tempo.
    // Por isso é uma flag, não um valor de AIStance — invasão não substitui a postura ofensiva
    // (substituí-la quebraria os gates "== Offensive"). True enquanto uma operação GoGreen estiver
    // em andamento (janela de supressão). Persiste no save via rallyGoGreenTurns.
    public bool IsInvading;

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

    public static AIWorldSnapshot Build(PlayerSlotId aiSlot, MatchController match)
    {
        var snap = new AIWorldSnapshot();
        snap.AITeam       = match != null ? match.GetVisualTeamForSlot(aiSlot) : TeamId.Neutral;
        snap.AISlotIndex  = aiSlot.Value;
        snap.TurnNumber   = match != null ? match.CurrentTurn : 0;
        snap.Budget       = match != null ? match.GetActualMoney(aiSlot) : 0;
        snap.IncomePerTurn = match != null ? match.GetIncomePerTurn(aiSlot) : 0;

        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.IsDead || u.IsEmbarked) continue;

            Vector3Int p = u.CurrentCellPosition; p.z = 0;
            snap.OccupiedCells.Add(p);

            if (u.SlotIndex == snap.AISlotIndex) snap.MyUnits.Add(u);
            else if (match == null || match.IsUnitVisibleForSlotNoCache(u, aiSlot)) snap.EnemyUnits.Add(u);
        }

        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (c.SlotIndex == snap.AISlotIndex)
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
        snap.IsInvading = AIController.GetGoGreenInvasionForInspection(aiSlot, snap.TurnNumber).Active;
        return snap;
    }

    /// <summary>
    /// Versão leve para o loop por-unidade da Fase 2.
    /// Preenche apenas os campos consumidos pelos handlers de role;
    /// omite MyUnits, EnemyUnits, OccupiedCells, Stance e IncomePerTurn.
    /// </summary>
    public static AIWorldSnapshot BuildLight(PlayerSlotId aiSlot, MatchController match)
    {
        var snap = new AIWorldSnapshot();
        snap.AITeam = match != null ? match.GetVisualTeamForSlot(aiSlot) : TeamId.Neutral;
        snap.AISlotIndex = aiSlot.Value;
        snap.TurnNumber = match != null ? match.CurrentTurn : 0;
        snap.Budget     = match != null ? match.GetActualMoney(PlayerSlotId.FromIndex(snap.AISlotIndex)) : 0;

        // MyUnits: necessário para o handler de Logistics (FindLogisticsServiceTarget,
        // TryBuildLogisticsSupplyAction, CalculateLogisticsRearAreaScore, etc.).
        // Omitimos OccupiedCells e EnemyUnits (com seu custo de fog-of-war) — nenhum
        // handler de role lê essas listas do snapshot.
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.IsDead || u.IsEmbarked) continue;
            if (u.SlotIndex == snap.AISlotIndex) snap.MyUnits.Add(u);
        }

        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (c.SlotIndex == snap.AISlotIndex)
            {
                if (c.IsPlayerHeadQuarter && snap.MyHQ == null) snap.MyHQ = c;
            }
            else if (c.TeamId != TeamId.Neutral)
            {
                snap.EnemyBuildings.Add(c);
                if (c.IsPlayerHeadQuarter && snap.EnemyHQ == null) snap.EnemyHQ = c;
            }
        }

        // IsInvading é barato (lookup no rallyGoGreenTurns) e é consumido por handlers da Fase 2
        // (ex.: gate de embarque de artilharia). Diferente de Stance, vale popular no Light.
        snap.IsInvading = AIController.GetGoGreenInvasionForInspection(aiSlot, snap.TurnNumber).Active;
        return snap;
    }

    private static AIStance CalculateStance(AIWorldSnapshot snap)
    {
        // Defensiva: ameaça REAL ao QG — inimigo a ≤4 células cuja força (HP) supera a defesa
        // local. Só a presença de um inimigo próximo não basta: se há defensores cobrindo, a
        // base está segura e a stance segue pela vantagem global (não trava em Defensiva por um
        // batedor passando perto enquanto a AI domina o mapa).
        if (snap.MyHQ != null && snap.EnemyUnits.Count > 0)
        {
            Vector3Int hq = snap.MyHQ.CurrentCellPosition; hq.z = 0;
            int enemyHpNearHQ = 0;
            foreach (UnitManager enemy in snap.EnemyUnits)
            {
                Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
                if (ChebyshevDistance(hq, ec) <= 4) enemyHpNearHQ += enemy.CurrentHP;
            }
            if (enemyHpNearHQ > 0)
            {
                int friendlyHpNearHQ = 0;
                foreach (UnitManager u in snap.MyUnits)
                {
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    if (ChebyshevDistance(hq, uc) <= 4) friendlyHpNearHQ += u.CurrentHP;
                }
                // Só Defensiva se a ameaça próxima supera a defesa local (base insegura).
                if (enemyHpNearHQ > friendlyHpNearHQ)
                    return AIStance.Defensive;
            }
        }

        // Ofensiva: AI tem ≥65% do HP total combinado
        if (snap.EnemyUnits.Count == 0 && HasBlindIntelPressure(snap))
            return AIStance.Tactical;

        int myHp = 0, enemyHp = 0;
        foreach (UnitManager u in snap.MyUnits)    myHp    += u.CurrentHP;
        foreach (UnitManager u in snap.EnemyUnits) enemyHp += u.CurrentHP;

        // As duas fotos no MESMO ponto do ciclo. O inimigo já comprou na vez dele; eu ainda
        // não — sem isto, quem joga em segundo vê 3 contra 6 no T2 de toda partida (razão 0,33)
        // e cai em Defensiva por ordem de turno, não por desvantagem. Marca do AI Step, 2026-11-04.
        // Depois da compra a projeção zera sozinha: a unidade nova ocupa o produtor e o caixa cai.
        // SÓ para quem joga DEPOIS de algum inimigo na rodada: para quem abre a rodada as fotos
        // já estão no mesmo ponto (os dois compraram N-1 vezes), e projetar daria vantagem de
        // graça — slot 0 viraria Ofensivo no T1 com 30 contra 0.
        if (HasEnemyActedBeforeMeThisRound(snap))
            myHp += ProjectOwnProductionHp(snap);

        if (myHp + enemyHp > 0)
        {
            float ratio = (float)myHp / (myHp + enemyHp);
            if (ratio > 0.65f) return AIStance.Offensive;
            if (ratio < 0.35f) return AIStance.Defensive;
        }

        return AIStance.Tactical;
    }

    /// <summary>
    /// A rodada passa pelos slots em ordem de índice: um inimigo de índice menor já jogou (e
    /// comprou) nesta rodada. Lê o slot das unidades inimigas vistas — o mesmo universo que a
    /// razão de HP usa, então a correção nunca considera um inimigo que a conta não contou.
    /// </summary>
    private static bool HasEnemyActedBeforeMeThisRound(AIWorldSnapshot snap)
    {
        if (snap == null || snap.EnemyUnits == null)
            return false;
        foreach (UnitManager enemy in snap.EnemyUnits)
            if (enemy != null && enemy.SlotIndex >= 0 && enemy.SlotIndex < snap.AISlotIndex)
                return true;
        return false;
    }

    /// <summary>
    /// HP que a IA ainda vai pôr no tabuleiro neste turno: cada produtor livre (célula de spawn
    /// desocupada) leva o corpo mais barato que ele oferece, enquanto o caixa pagar. É estimativa
    /// de massa, não previsão do carrinho — o shopping pode comprar mais caro; a postura só precisa
    /// saber que a resposta existe. É conserto de MEDIDA, por isso vale em toda dificuldade.
    /// </summary>
    private static int ProjectOwnProductionHp(AIWorldSnapshot snap)
    {
        if (snap == null || snap.MyBuildings == null || snap.Budget <= 0)
            return 0;

        int budget = snap.Budget;
        int hp = 0;
        foreach (ConstructionManager building in snap.MyBuildings)
        {
            if (building == null || building.OfferedUnits == null
                || !building.CanProduceUnitsForSlot(snap.AISlotIndex))
                continue;
            Vector3Int cell = building.CurrentCellPosition; cell.z = 0;
            if (snap.OccupiedCells.Contains(cell))
                continue;

            UnitData cheapest = null;
            foreach (UnitData unit in building.OfferedUnits)
                if (unit != null && unit.cost > 0 && (cheapest == null || unit.cost < cheapest.cost))
                    cheapest = unit;
            if (cheapest == null || cheapest.cost > budget)
                continue;

            budget -= cheapest.cost;
            hp += Mathf.Max(1, cheapest.maxHP);
        }
        return hp;
    }

    private static bool HasBlindIntelPressure(AIWorldSnapshot snap)
    {
        if (snap == null)
            return false;

        JogadasManager jogadas = JogadasManager.EnsureInstance();
        if (jogadas == null || jogadas.log == null || jogadas.log.jogadas == null || jogadas.log.jogadas.Count == 0)
            return false;

        int lookback = AIShoppingPlanner.Instance != null ? Mathf.Max(1, AIShoppingPlanner.Instance.IntelShoppingLookbackTurns) : 4;
        AIIntelReport intel = AIIntelAnalyzer.BuildReport(jogadas.log, snap.AITeam, lookback, 5, snap.TurnNumber);
        if (intel == null)
            return false;

        float topHot = 0f;
        if (intel.sectors != null && intel.sectors.Count > 0 && intel.sectors[0] != null)
            topHot = intel.sectors[0].hotScore;

        return intel.enemyMomentum >= 5f
            || intel.capturePressure >= 6f
            || topHot >= 10f;
    }

    // Aproximação rápida de distância hex por Chebyshev (suficiente para trigger de stance)
    private static int ChebyshevDistance(Vector3Int a, Vector3Int b) =>
        Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
}
