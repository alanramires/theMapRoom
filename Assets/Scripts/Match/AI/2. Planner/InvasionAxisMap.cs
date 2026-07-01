using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// -----------------------------------------------------------------------------
// InvasionAxisMap — classificacao runtime de "eixos de invasao".
//
// Fonte unica de verdade do leque angular: cada rally (do slot/time) e um eixo;
// cada setor de campo e atribuido ao rally cuja direcao (HQ->rally) e a mais
// proxima em angulo da direcao HQ->setor, em world space (sem distorcao do hex).
// Fatias disjuntas => nao cruzam nem repetem, e a ordenacao por distancia da o
// corredor "sempre pra frente". E exatamente a mesma geometria desenhada pelo
// SectorManagerEditor ("Desenhar eixos") — o editor agora consome esta classe.
//
// Esta camada e puramente descritiva (sem efeito de jogo): expoe setor -> eixo
// (1..N) e o corredor ordenado por eixo. Quem usa: o HUD de unidade (aiEixo) e,
// no futuro, o vies de prioridade / reforco do planner.
// -----------------------------------------------------------------------------
public class InvasionAxisMap
{
    public class Axis
    {
        public int EixoIndex;                 // 1..N, ordenado por angulo (esq->dir) a partir do HQ.
        public int RallyOwnerSlotIndex;       // slot dono do rally (para cor/identidade).
        public TeamId Team;
        public ConstructionSector RallySector;
        public Vector3Int HqCell;
        public Vector3Int RallyCell;
        public float RallyAngleDeg;           // direcao HQ->rally (world space) usada na ordenacao.
        // Corredor ordenado do mais perto do HQ ao mais longe (NAO inclui HQ nem o setor do rally).
        public List<ConstructionSector> Corridor = new List<ConstructionSector>();

        // Frente (leading edge): proximo setor do eixo ainda nao conquistado, caminhando o
        // corredor de HQ pra fora. E o "alvo de avanco" do eixo (R1) e onde reforcar (R3).
        public int FrontIndex = -1;                            // indice em Corridor da frente; == Corridor.Count se a frente ja e o rally; -1 indefinido.
        public ConstructionSector FrontSector = ConstructionSector.None; // setor da frente (None se eixo completo).
        public bool Complete;                                  // corredor inteiro + rally ja sao do time.
        public bool IsInvasionAxis;                            // 4o eixo sintetico HQ->QG inimigo (captura final).
    }

    private readonly Dictionary<ConstructionSector, int> sectorToEixo = new Dictionary<ConstructionSector, int>();
    private readonly List<Axis> axes = new List<Axis>();

    public TeamId Team { get; private set; }
    public IReadOnlyList<Axis> Axes => axes;
    public int AxisCount => axes.Count;

    // Retorna o eixo (1..N) do setor, ou 0 se o setor nao pertence a nenhum eixo (rogue/base/fora de alcance).
    public int GetEixo(ConstructionSector sector)
    {
        ConstructionSector key = sector;
        return sectorToEixo.TryGetValue(key, out int eixo) ? eixo : 0;
    }

    public bool TryGetAxis(int eixoIndex, out Axis axis)
    {
        for (int i = 0; i < axes.Count; i++)
        {
            if (axes[i].EixoIndex == eixoIndex) { axis = axes[i]; return true; }
        }
        axis = null;
        return false;
    }

    // Setor-alvo de transporte do eixo. Normalmente e a frente; mas se a frente ja esta SOB
    // CAPTURA (parcial), olha UM no a frente no corredor — pipelining do corredor: enquanto a
    // captura conclui, o APC ja prepara o proximo lance (a proxima unidade ja embarca na base).
    // None se o eixo esta completo ou se a frente e o rally (sem no seguinte para antecipar).
    public ConstructionSector GetTransportTargetSector(int eixoIndex)
    {
        if (!TryGetAxis(eixoIndex, out Axis axis) || axis.Complete)
            return ConstructionSector.None;
        ConstructionSector front = axis.FrontSector;
        if (front == ConstructionSector.None)
            return ConstructionSector.None;
        if (!FrontIsUnderCapture(front))
            return front;
        ConstructionSector next = NextCorridorSector(axis);
        return next != ConstructionSector.None ? next : front;
    }

    private static bool FrontIsUnderCapture(ConstructionSector front)
    {
        return SectorManager.TryGetSectorInfo(front, out SectorManager.SectorInfo info)
            && info != null && info.HasPartialCapture;
    }

    // Proximo no do corredor depois da frente: Corridor[FrontIndex+1], ou o setor do rally se
    // a frente e o ultimo no do corredor. None se a frente ja e o rally (FrontIndex == Count).
    private static ConstructionSector NextCorridorSector(Axis axis)
    {
        if (axis.FrontIndex < 0)
            return ConstructionSector.None;
        if (axis.FrontIndex < axis.Corridor.Count - 1)
            return axis.Corridor[axis.FrontIndex + 1];
        if (axis.FrontIndex == axis.Corridor.Count - 1)
            return axis.RallySector;
        return ConstructionSector.None;
    }

    // Constroi o mapa de eixos do time. tilemap pode ser null (resolve automaticamente).
    public static InvasionAxisMap Build(TeamId team, Tilemap tilemap = null)
    {
        var map = new InvasionAxisMap { Team = team };

        Tilemap board = tilemap != null ? tilemap : ResolveBoardTilemap();
        if (board == null)
            return map;

        // TODOS os HQs (de todos os times) e todos os rallys ativos. O dono do rally
        // e resolvido entre todos os HQs; so depois filtramos pelo time pedido — senao
        // um rally inimigo cairia no fallback "HQ mais proximo" e se prenderia ao nosso eixo.
        var allHqs = new List<ConstructionManager>();
        var rallies = new List<ConstructionManager>();
        foreach (ConstructionManager c in GetConstructions())
        {
            if (c == null) continue;
            if (c.IsPlayerHeadQuarter) allHqs.Add(c);
            if (c.IsRallyPoint) rallies.Add(c);
        }
        if (allHqs.Count == 0)
            return map;

        // Agrupa rallys pelo HQ-apex (dono). Espelha DrawInvasionAxes do editor:
        // resolve o dono entre todos os HQs e descarta se o dono nao for do time.
        var porHQ = new Dictionary<ConstructionManager, List<ConstructionManager>>();
        foreach (ConstructionManager rally in rallies)
        {
            ConstructionManager hq = FindAxisHQ(allHqs, rally, rally.RallyOwnerSlotIndex);
            if (hq == null) continue;
            if (hq.TeamId != team) continue; // rally de outro time
            if (!porHQ.TryGetValue(hq, out List<ConstructionManager> lista)) { lista = new List<ConstructionManager>(); porHQ[hq] = lista; }
            lista.Add(rally);
        }
        // Setores de campo (exclui base).
        var setores = new List<SectorManager.SectorInfo>();
        foreach (SectorManager.SectorInfo info in SectorManager.GetAllSectorInfos())
            if (info != null && !ConstructionSectorHelper.IsBase(info.Sector))
                setores.Add(info);

        // Roda o leque por grupo de HQ e acumula eixos crus.
        foreach (var kv in porHQ)
            map.BuildFan(board, kv.Key, kv.Value, setores);

        // Numeracao global por angulo (esq->dir): EixoIndex = 1..N. Preenche setor->eixo.
        map.axes.Sort((a, b) => a.RallyAngleDeg.CompareTo(b.RallyAngleDeg));
        for (int i = 0; i < map.axes.Count; i++)
        {
            Axis axis = map.axes[i];
            axis.EixoIndex = i + 1;
            map.sectorToEixo[axis.RallySector] = axis.EixoIndex; // o proprio setor do rally pertence ao eixo
            foreach (ConstructionSector sec in axis.Corridor)
                map.sectorToEixo[sec] = axis.EixoIndex;
            ComputeFront(axis, team);
        }

        // 4o eixo: INVASAO (HQ -> QG inimigo). Append depois da numeracao (rally = 1..N, invasao = N+1).
        map.AppendInvasionAxis(board, team, allHqs);

        return map;
    }

    // Eixo sintetico da captura final: existe desde o inicio como geometria HQ -> QG inimigo.
    // A base e excluida dos eixos rally e recebe este eixo descritivo dedicado.
    // fica "fora de eixo" (GetEixo==0) e o builder de transporte nao gera demanda — entao a leva do
    // A demanda de APC permanece separada e so abre durante GoGreen/IsInvading.
    private void AppendInvasionAxis(Tilemap board, TeamId team, List<ConstructionManager> allHqs)
    {
        if (board == null || allHqs == null)
            return;

        ConstructionManager hq = null;
        foreach (ConstructionManager h in allHqs)
            if (h != null && h.TeamId == team) { hq = h; break; }
        if (hq == null)
            return;

        Vector3Int hqCell = hq.CurrentCellPosition; hqCell.z = 0;

        ConstructionSector plannedBase = ConstructionSector.None;
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(team);
        if (plan != null && plan.Objectives != null)
            foreach (SectorObjective obj in plan.Objectives)
                if (obj != null && obj.ObjectiveType == AIObjectiveType.InvasionAttack)
                {
                    plannedBase = obj.Sector;
                    break;
                }

        ConstructionManager enemyHq = FindNearestEnemyHq(allHqs, team, hqCell, plannedBase);
        if (enemyHq == null && plannedBase != ConstructionSector.None)
            enemyHq = FindNearestEnemyHq(allHqs, team, hqCell, ConstructionSector.None);
        if (enemyHq == null || sectorToEixo.ContainsKey(enemyHq.Sector))
            return;

        ConstructionSector baseSector = enemyHq.Sector;
        Vector3Int baseCell = enemyHq.CurrentCellPosition; baseCell.z = 0;

        Vector3 hqW = board.GetCellCenterWorld(hqCell);
        Vector3 baseW = board.GetCellCenterWorld(baseCell);

        int idx = axes.Count + 1;
        var axis = new Axis
        {
            EixoIndex = idx,
            RallyOwnerSlotIndex = hq.SlotIndex,
            Team = team,
            RallySector = baseSector,
            HqCell = hqCell,
            RallyCell = baseCell,
            RallyAngleDeg = AngleDeg(hqW, baseW),
            FrontIndex = 0,
            FrontSector = baseSector,   // alvo de transporte = a base inimiga
            Complete = false,
            IsInvasionAxis = true,
        };
        axes.Add(axis);
        sectorToEixo[baseSector] = idx;
    }

    private static ConstructionManager FindNearestEnemyHq(
        List<ConstructionManager> allHqs,
        TeamId team,
        Vector3Int hqCell,
        ConstructionSector requiredSector)
    {
        ConstructionManager best = null;
        float bestDistance = float.MaxValue;
        foreach (ConstructionManager candidate in allHqs)
        {
            if (candidate == null || candidate.TeamId == team)
                continue;
            if (requiredSector != ConstructionSector.None && candidate.Sector != requiredSector)
                continue;

            Vector3Int candidateCell = candidate.CurrentCellPosition;
            candidateCell.z = 0;
            float distance = SectorManager.HexDistance(hqCell, candidateCell);
            if (distance >= bestDistance)
                continue;
            bestDistance = distance;
            best = candidate;
        }
        return best;
    }

    // Leque angular para um grupo de rallys que compartilham o mesmo HQ-apex.
    private void BuildFan(Tilemap map, ConstructionManager hq, List<ConstructionManager> rallies, List<SectorManager.SectorInfo> setores)
    {
        if (rallies.Count == 0) return;
        Vector3Int hqCell = hq.CurrentCellPosition; hqCell.z = 0;
        Vector3 hqW = map.GetCellCenterWorld(hqCell);

        int n = rallies.Count;
        var rallyAng = new float[n];
        var rallyDist = new float[n];
        var rallyCell = new Vector3Int[n];
        for (int i = 0; i < n; i++)
        {
            Vector3Int rc = rallies[i].CurrentCellPosition; rc.z = 0;
            rallyCell[i] = rc;
            Vector3 rw = map.GetCellCenterWorld(rc);
            rallyAng[i] = AngleDeg(hqW, rw);
            rallyDist[i] = Vector2.Distance(hqW, rw);
        }

        var byRally = new List<(SectorManager.SectorInfo info, float dist)>[n];
        for (int i = 0; i < n; i++) byRally[i] = new List<(SectorManager.SectorInfo, float)>();

        foreach (SectorManager.SectorInfo info in setores)
        {
            Vector3 repW = map.GetCellCenterWorld(info.RepresentativeCell);
            float sd = Vector2.Distance(hqW, repW);
            if (sd < 0.01f) continue;
            float sa = AngleDeg(hqW, repW);

            int best = 0; float bestDiff = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float diff = Mathf.Abs(Mathf.DeltaAngle(sa, rallyAng[i]));
                if (diff < bestDiff) { bestDiff = diff; best = i; }
            }
            if (sd > rallyDist[best] + 1f) continue;
            if (info.Sector == rallies[best].Sector) continue;
            byRally[best].Add((info, sd));
        }

        for (int i = 0; i < n; i++)
        {
            byRally[i].Sort((a, b) => a.dist.CompareTo(b.dist));
            var axis = new Axis
            {
                RallyOwnerSlotIndex = rallies[i].RallyOwnerSlotIndex,
                Team = Team,
                RallySector = rallies[i].Sector,
                HqCell = hqCell,
                RallyCell = rallyCell[i],
                RallyAngleDeg = rallyAng[i],
            };
            foreach (var (info, _) in byRally[i])
                axis.Corridor.Add(info.Sector);
            axes.Add(axis);
        }
    }

    // Caminha o corredor de HQ pra fora e acha a frente: o primeiro setor ainda nao
    // conquistado pelo time. Se todo o corredor ja e nosso, a frente e o setor do rally;
    // se ate o rally for nosso, o eixo esta completo.
    private static void ComputeFront(Axis axis, TeamId team)
    {
        axis.FrontIndex = -1;
        axis.FrontSector = ConstructionSector.None;
        axis.Complete = false;

        for (int i = 0; i < axis.Corridor.Count; i++)
        {
            if (!IsOwnedBy(axis.Corridor[i], team))
            {
                axis.FrontIndex = i;
                axis.FrontSector = axis.Corridor[i];
                return;
            }
        }

        if (!IsOwnedBy(axis.RallySector, team))
        {
            axis.FrontIndex = axis.Corridor.Count;
            axis.FrontSector = axis.RallySector;
            return;
        }

        axis.FrontIndex = axis.Corridor.Count;
        axis.Complete = true;
    }

    private static bool IsOwnedBy(ConstructionSector sector, TeamId team)
    {
        return SectorManager.TryGetSectorInfo(sector, out SectorManager.SectorInfo info)
            && info != null && info.ControllingTeam == team;
    }

    private static ConstructionManager FindAxisHQ(List<ConstructionManager> hqs, ConstructionManager rally, int slot)
    {
        Vector3Int rallyCell = rally.CurrentCellPosition; rallyCell.z = 0;
        ConstructionManager best = null; float bestD = float.MaxValue;
        foreach (ConstructionManager hq in hqs)
        {
            if (hq == null) continue;
            if (slot >= 0 && hq.SlotIndex == slot) return hq;   // dono explicito
            Vector3Int hqCell = hq.CurrentCellPosition; hqCell.z = 0;
            float d = SectorManager.HexDistance(rallyCell, hqCell);
            if (d < bestD) { bestD = d; best = hq; }
        }
        return best;
    }

    private static float AngleDeg(Vector3 from, Vector3 to)
        => Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;

    // Prefere o registro runtime; cai para FindObjectsByType em edit-mode (editor usa este caminho).
    private static IReadOnlyList<ConstructionManager> GetConstructions()
    {
        if (ConstructionManager.AllActive != null && ConstructionManager.AllActive.Count > 0)
            return ConstructionManager.AllActive;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            return Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                ?? (IReadOnlyList<ConstructionManager>)System.Array.Empty<ConstructionManager>();
#endif

        return Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            ?? (IReadOnlyList<ConstructionManager>)System.Array.Empty<ConstructionManager>();
    }

    private static Tilemap ResolveBoardTilemap()
    {
        CursorController cursor = Object.FindAnyObjectByType<CursorController>();
        if (cursor != null && cursor.BoardTilemap != null)
            return cursor.BoardTilemap;

        ConstructionManager construction = Object.FindAnyObjectByType<ConstructionManager>();
        if (construction != null && construction.BoardTilemap != null)
            return construction.BoardTilemap;

        return null;
    }
}
