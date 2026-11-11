using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private bool rallyFormationVisible;
    private int rallyFormationRequestedSlot = -1;
    private Vector2 rallyFormationScroll;
    private readonly Dictionary<int, Vector3Int> rallyObservedMoves = new Dictionary<int, Vector3Int>();
    private readonly List<RallyFormationReport> rallyFormationReports = new List<RallyFormationReport>();
    private string rallyFormationMessage;
    public bool RallyFormationVisible => rallyFormationVisible;

    public string SetRallyFormationVisible(bool visible, int slot = -1)
    {
        rallyFormationVisible = visible;
        rallyFormationRequestedSlot = slot;
        CursorController.OnBoardSettledAtNeutral -= RefreshRallyAfterNeutral;
        rallyFormationReports.Clear();
        if (turnStateManager != null) turnStateManager.SetRallyDebugReports(null);
        if (!visible)
        {

            return "Rally oculto.";
        }
        if (!Application.isPlaying) { rallyFormationVisible = false; return "Rally: disponivel em Play."; }
        if (turnStateManager == null) turnStateManager = FindAnyObjectByType<TurnStateManager>();
        CursorController.OnBoardSettledAtNeutral += RefreshRallyAfterNeutral;
        aiReviewNoteFocused = false;
        RefreshRallyFormation(force: true);
        return "Rally visivel (somente leitura). " + rallyFormationMessage;
    }

    private void RefreshRallyAfterNeutral() => RefreshRallyFormation(force: true);
    private void RefreshRallyFormation(bool force)
    {
        if (!rallyFormationVisible || !force) return;
        if (turnStateManager != null && turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
        { rallyFormationMessage = "Aguardando tabuleiro confirmado em Neutral."; return; }
        if (matchController == null || boardTilemap == null || terrainDatabase == null)
        { rallyFormationMessage = "Sem contexto de partida/tabuleiro."; return; }
        int slot = rallyFormationRequestedSlot >= 0 ? rallyFormationRequestedSlot : matchController.ActiveSlotId.Value;
        if (!matchController.IsValidPlayerSlotIndex(slot)) { rallyFormationMessage = "Slot invalido."; return; }
        TeamId team = matchController.GetVisualTeamForSlot(PlayerSlotId.FromIndex(slot));
        var allies = new List<UnitManager>(); var enemies = new List<UnitManager>();
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.IsDead || unit.gameObject.scene != gameObject.scene) continue;
            if (unit.SlotIndex == slot) allies.Add(unit);
            else if (!unit.IsEmbarked && PlayerSlotRelations.AreEnemies(slot, unit.SlotIndex)
                && matchController.IsUnitVisibleForSlot(unit, PlayerSlotId.FromIndex(slot))) enemies.Add(unit);
        }
        UnitDatabase database = FindAnyObjectByType<UnitSpawner>()?.UnitDatabase;
        if (database == null) foreach (UnitManager unit in allies)
            if (unit.UnitDatabase != null) { database = unit.UnitDatabase; break; }
        QuadranteData quadrant = QuadranteController.Active?.BuiltQuadrant;
        InvasionAxisMap axisMap = InvasionAxisMap.Build(PlayerSlotId.FromIndex(slot), boardTilemap);
        rallyFormationReports.Clear();
        foreach (ConstructionManager rally in ConstructionManager.AllActive)
        {
            if (rally == null || rally.gameObject.scene != gameObject.scene || !IsRallyOwnedBySlot(rally, team, slot)) continue;
            UnitData model = ResolveRallyFormationModel(rally, quadrant, database, out bool fallback);
            Vector3Int? axisOrigin = null;
            foreach (InvasionAxisMap.Axis axis in axisMap.Axes)
                if (!axis.IsInvasionAxis && axis.RallySector == rally.Sector) { axisOrigin = axis.HqCell; break; }
            RallyArea area = RallyAreaService.Build(rally, model, team, slot, boardTilemap, terrainDatabase, allies, enemies, axisOrigin);
            rallyFormationReports.Add(RallyFormationReport.BuildForSlot(this, slot, area, allies, rallyObservedMoves, fallback));
        }
        rallyFormationMessage = $"Slot {slot}, {rallyFormationReports.Count} rally(s). Atualizado no Neutral confirmado.";
        turnStateManager?.SetRallyDebugReports(rallyFormationReports);
    }

    // Ponte de leitura para o consumidor; a formula continua no dono original.
    public RallyReadinessReport ReadRallyFormationReadiness(ConstructionManager rally, int slot, SectorObjective objective)
    {
        if (matchController == null) return new RallyReadinessReport { Status = "Sem partida", Missing = "-" };
        AIRallyReadiness current = EvaluateRallyReadiness(rally,
            matchController.GetVisualTeamForSlot(PlayerSlotId.FromIndex(slot)), matchController.CurrentTurn,
            objective != null ? objective.RallyAssemblyStartedTurn : -1, null);
        return new RallyReadinessReport { Status = current.Status, Missing = current.Missing,
            Hold = current.HoldReady, Breakthrough = current.BreakthroughReady,
            Artillery = current.ArtilleryReady, Force = current.ForceReady };
    }

    private UnitData ResolveRallyFormationModel(ConstructionManager rally, QuadranteData quadrant, UnitDatabase database, out bool fallback)
    {
        bool naval = terrainDatabase.TryGetByPaletteTile(boardTilemap.GetTile(rally.CurrentCellPosition), out TerrainTypeData terrain)
            && (terrain.domain == Domain.Naval || terrain.domain == Domain.Submarine);
        UnitData model = quadrant == null ? null : naval ? quadrant.rallyModelNaval : quadrant.rallyModelLand;
        fallback = model == null;
        if (fallback) database?.TryGetById("Soldado", out model);
        return model;
    }

    private void DrawRallyFormationWindowContents(int windowId)
    {
        GUILayout.BeginArea(new Rect(8f, 28f, aiReviewWindowRect.width - 16f, aiReviewWindowRect.height - 52f));
        GUILayout.Label(rallyFormationMessage ?? "Aguardando Neutral.", new GUIStyle(GUI.skin.label) { wordWrap = true });
        GUILayout.Label("Core: azul | Approach: laranja | Vanguard: vermelho | Rear: violeta\nRota verde: destino na area; cinza: fora.", new GUIStyle(GUI.skin.label) { wordWrap = true });
        rallyFormationScroll = GUILayout.BeginScrollView(rallyFormationScroll);
        var style = new GUIStyle(GUI.skin.label) { wordWrap = true };
        foreach (RallyFormationReport report in rallyFormationReports)
        {
            foreach (string line in report.ToDisplayText().Split('\n')) GUILayout.Label(line, style);
            GUILayout.Space(12);
        }
        GUILayout.EndScrollView();
        if (GUILayout.Button("Fechar relatorio (sem mudar pausa ou AI Step)")) SetRallyFormationVisible(false);
        GUILayout.EndArea();
        GUI.DragWindow(new Rect(0, 0, aiReviewWindowRect.width, 25f));
    }
}
