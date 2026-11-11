using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private bool rallyFormationVisible;
    private ConstructionManager rallyFormationSelected;
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
        rallyFormationSelected = null;
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
        var allies = new List<UnitManager>();
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.IsDead || unit.gameObject.scene != gameObject.scene) continue;
            if (unit.SlotIndex == slot) allies.Add(unit);
        }
        UnitDatabase database = FindAnyObjectByType<UnitSpawner>()?.UnitDatabase;
        if (database == null) foreach (UnitManager unit in allies)
            if (unit.UnitDatabase != null) { database = unit.UnitDatabase; break; }
        QuadranteData quadrant = QuadranteController.Active?.BuiltQuadrant;
        rallyFormationReports.Clear();
        foreach (ConstructionManager rally in ConstructionManager.AllActive)
        {
            if (rally == null || rally.gameObject.scene != gameObject.scene || !IsRallyOwnedBySlot(rally, team, slot)) continue;
            UnitData model = ResolveRallyFormationModel(rally, quadrant, database, out bool fallback);
            RallyArea area = GetRallyArea(rally, team);
            if (area == null) continue;
            rallyFormationReports.Add(RallyFormationReport.BuildForSlot(this, slot, area, allies, rallyObservedMoves, fallback));
        }
        rallyFormationMessage = $"Slot {slot}, {rallyFormationReports.Count} rally(s). Atualizado no Neutral confirmado.";
        rallyFormationReports.Sort((a, b) => RallyDisplayOrder(a).CompareTo(RallyDisplayOrder(b)));
        if (rallyFormationSelected != null && !rallyFormationReports.Exists(r => r.Area.Rally == rallyFormationSelected)) rallyFormationSelected = null;
        PublishRallyDisplay();
    }

    private static int RallyDisplayOrder(RallyFormationReport report)
    {
        if (report.Area.Rally.SlotIndex != report.Slot) return 2;
        return report.Readiness.Status != null && report.Readiness.Status.StartsWith("ASSEMBLING") ? 0 : 1;
    }

    private void PublishRallyDisplay()
    {
        turnStateManager?.SetRallyDebugReports(rallyFormationSelected == null ? rallyFormationReports
            : rallyFormationReports.FindAll(r => r.Area.Rally == rallyFormationSelected));
    }

    // Ponte de leitura para o consumidor; a formula continua no dono original.
    public RallyReadinessReport ReadRallyFormationReadiness(ConstructionManager rally, int slot, SectorObjective objective)
    {
        if (matchController == null) return new RallyReadinessReport { Status = "Sem partida", Missing = "-" };
        AIRallyReadiness current = EvaluateRallyReadiness(rally,
            matchController.GetVisualTeamForSlot(PlayerSlotId.FromIndex(slot)), matchController.CurrentTurn,
            null);
        return new RallyReadinessReport { Status = current.Status, Missing = current.Missing,
            Hold = current.HoldReady, Breakthrough = current.BreakthroughReady,
            Artillery = current.ArtilleryReady, Force = current.ForceReady,
            HoldValues = (current.Capturers, RallyMinimumCapturers),
            BreakthroughValues = (current.Assault, RallyMinimumBreakthrough),
            ArtilleryValues = (current.Artillery, 0f),
            ForceValues = (current.ForceScore, current.RequiredForce) };
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

    private static readonly UnitForce[] RallyDisplayForces = {
        UnitForce.Ruptura, UnitForce.Dissuasao, UnitForce.Ocupacao, UnitForce.Apoio
    };

    private void DrawRallyFormationWindowContents(int windowId)
    {
        GUILayout.BeginArea(new Rect(8f, 28f, aiReviewWindowRect.width - 16f, aiReviewWindowRect.height - 52f));
        var style = new GUIStyle(GUI.skin.label) { wordWrap = false };
        var bold = new GUIStyle(style) { fontStyle = FontStyle.Bold };
        GUILayout.Label(rallyFormationMessage ?? "Aguardando Neutral.", style);
        GUILayout.BeginHorizontal();
        foreach (RallyFormationReport item in rallyFormationReports)
        {
            bool selected = item.Area.Rally == rallyFormationSelected;
            if (GUILayout.Button((selected ? "[" : "") + item.Area.Rally.Sector + (selected ? "]" : "")))
            {
                rallyFormationSelected = selected ? null : item.Area.Rally;
                rallyFormationScroll = Vector2.zero;
                PublishRallyDisplay();
                if (rallyFormationSelected != null) FocusAIReviewCell(rallyFormationSelected.CurrentCellPosition);
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.Label("Clique no rally para isolar; clique novamente para mostrar todos.", style);
        rallyFormationScroll = GUILayout.BeginScrollView(rallyFormationScroll);
        foreach (RallyFormationReport report in rallyFormationReports)
        {
            if (rallyFormationSelected != null && report.Area.Rally != rallyFormationSelected) continue;
            GUILayout.BeginHorizontal();
            Vector3Int cell = report.Area.Rally.CurrentCellPosition;
            string model = (report.ModelFallback ? "fallback: " : "") + (report.Area.Model?.displayName ?? "indisponivel");
            GUILayout.Label(new GUIContent($"{report.Area.Rally.name} | {report.Area.Rally.Sector} | ({cell.x},{cell.y}) | slot {report.Slot} | {model}"), style);
            GUILayout.FlexibleSpace();
            string status = report.Readiness.Status ?? "WAIT";
            var statusStyle = new GUIStyle(bold) { alignment = TextAnchor.MiddleRight };
            statusStyle.normal.textColor = status.StartsWith("GO_GREEN") || status == "READY" ? Color.green
                : status.StartsWith("ASSEMBLING") ? Color.yellow : Color.red;
            GUILayout.Label(status, statusStyle);
            GUILayout.EndHorizontal();
            DrawRallyReadinessBar("Hold (cap.)", report.Readiness.HoldValues, report.Readiness.Hold, style);
            DrawRallyReadinessBar("Ruptura", report.Readiness.BreakthroughValues, report.Readiness.Breakthrough, style);
            DrawRallyReadinessBar("Suporte", report.Readiness.ArtilleryValues, report.Readiness.Artillery, style, optional: true);
            DrawRallyReadinessBar("Forca (pts)", report.Readiness.ForceValues, report.Readiness.Force, style);
            GUILayout.Label("Hold: quatro capturadores; Suporte soma forca e nao trava a saida.", style);
            GUILayout.Label("Missing: " + report.Readiness.Missing, style);
            GUILayout.Label($"Vanguarda vermelha: {report.Area.Vanguard.Count} | Retaguarda violeta: {report.Area.Rear.Count} | {report.Area.Orientation}", style);
            if (!string.IsNullOrEmpty(report.Area.Error)) GUILayout.Label("Geometria: " + report.Area.Error, style);
            GUILayout.Label("DEMANDA DO OBJETIVO", bold);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Forca", bold, GUILayout.Width(100f));
            GUILayout.Label("Papel", bold, GUILayout.Width(170f));
            GUILayout.Label("Preenchidas", bold, GUILayout.MinWidth(100f));
            GUILayout.Label("Vazias", bold, GUILayout.MinWidth(100f));
            GUILayout.EndHorizontal();
            if (report.Objective?.Slots == null) GUILayout.Label("Sem objetivo RallyAssembly publicado no plano.", style);
            else foreach (UnitForce force in RallyDisplayForces)
            {
                var roles = new Dictionary<UnitRole, (int filled, int empty)>();
                foreach (SlotNeed need in report.Objective.Slots)
                {
                    if (UnitRoleCompatibility.ResolveForce(UnitRoleCompatibility.ResolveFamily(need.Role)) != force) continue;
                    roles.TryGetValue(need.Role, out var slots);
                    if (need.Filled) slots.filled++; else slots.empty++;
                    roles[need.Role] = slots;
                }
                if (roles.Count == 0) continue;
                bool first = true;
                foreach (var role in roles)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(first ? force.ToString() : "", bold, GUILayout.Width(100f));
                    first = false;
                    GUILayout.Label(role.Key.ToString(), style, GUILayout.Width(170f));
                    GUILayout.Label(role.Value.filled > 0 ? new string('■', role.Value.filled) : "·", style, GUILayout.MinWidth(100f));
                    GUILayout.Label(role.Value.empty > 0 ? new string('□', role.Value.empty) : "·", style, GUILayout.MinWidth(100f));
                    GUILayout.EndHorizontal();
                }
                GUILayout.Space(4f);
            }

            // Passageiros sao apresentados na linha do transporte; o relatorio continua intacto.
            var rows = new List<RallyFormationParticipant>();
            var seen = new HashSet<int>();
            var aboardCounts = new Dictionary<int, int>();
            foreach (UnitManager passenger in UnitManager.AllActive)
                if (passenger != null && passenger.IsEmbarked && passenger.EmbarkedTransporter != null)
                {
                    int carrierId = passenger.EmbarkedTransporter.InstanceId;
                    aboardCounts.TryGetValue(carrierId, out int count);
                    aboardCounts[carrierId] = count + 1;
                }
            foreach (RallyFormationParticipant participant in report.Participants)
            {
                UnitManager unit = participant.Unit;
                if (unit == null) continue;
                if (unit.IsEmbarked && unit.EmbarkedTransporter != null)
                {
                    UnitManager carrier = unit.EmbarkedTransporter;
                    RallyFormationParticipant carrierRow = report.Participants.Find(p => p.Unit == carrier);
                    if (carrierRow == null)
                    {
                        carrierRow = new RallyFormationParticipant { Unit = carrier,
                            Position = participant.Position, Location = participant.Location };
                        if (rallyObservedMoves.TryGetValue(carrier.InstanceId, out Vector3Int destination)) carrierRow.PlannedDestination = destination;
                        if (carrier.AIHasDesignatedMission) carrierRow.MissionDestination = carrier.AIDesignatedMissionTargetCell;
                        if (carrierRow.Destination.HasValue)
                        {
                            carrierRow.DestinationLocation = RallyFormationReport.LocateParticipant(report.Area, carrierRow.Destination.Value, carrier);
                            carrierRow.GoingToArea = carrierRow.Destination.Value != carrierRow.Position
                                && carrierRow.DestinationLocation != RallyLocation.Outside;
                        }
                    }
                    if (seen.Add(carrier.InstanceId)) rows.Add(carrierRow);
                }
                else if (seen.Add(unit.InstanceId)) rows.Add(participant);
            }
            int[] counts = new int[3];
            foreach (var row in rows) counts[(int)row.Location]++;
            GUILayout.Label("PARTICIPANTES", bold);
            GUILayout.BeginHorizontal();
            GUILayout.Space(210f);
            GUILayout.Label($"Core ({counts[0]})", bold, GUILayout.MinWidth(80f));
            GUILayout.Label($"Approach ({counts[1]})", bold, GUILayout.MinWidth(80f));
            GUILayout.Label($"Fora ({counts[2]})", bold, GUILayout.MinWidth(80f));
            GUILayout.EndHorizontal();
            foreach (UnitForce force in RallyDisplayForces)
            {
                bool heading = false;
                var roles = new List<UnitRole>();
                foreach (var row in rows)
                    if (row.Unit.TryGetUnitData(out UnitData data) && UnitRoleCompatibility.ResolveForce(data) == force
                        && data.roles != null && data.roles.Count > 0 && !roles.Contains(data.roles[0])) roles.Add(data.roles[0]);
                foreach (UnitRole role in roles)
                {
                    if (!heading) { GUILayout.Label(force.ToString(), bold); heading = true; }
                    GUILayout.Label("    " + role, style);
                    foreach (var row in rows)
                    {
                        if (!row.Unit.TryGetUnitData(out UnitData data) || data.roles == null || data.roles.Count == 0 || data.roles[0] != role) continue;
                        aboardCounts.TryGetValue(row.Unit.InstanceId, out int aboard);
                        string name = $"{row.Unit.UnitDisplayName} #{row.Unit.InstanceId}" + (aboard > 0 ? $" ({aboard} a bordo)" : "");
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button(new GUIContent(name, $"Posicao: {row.Position}; decisao: {row.PlannedDestination}; missao: {row.MissionDestination}; esperado: {row.Expected}"), GUILayout.Width(210f)))
                        {
                            GUI.FocusControl(null);
                            FocusAIReviewCell(row.Position);
                        }
                        for (int column = 0; column < 3; column++)
                        {
                            string marker = (int)row.Location == column ? "●" : "";
                            if (row.Destination.HasValue && (row.GoingToArea || row.DestinationLocation == RallyLocation.Outside)
                                && (int)row.DestinationLocation == column) marker += " → " + (column == 0 ? "Core" : column == 1 ? "Approach" : "Fora");
                            GUILayout.Label(marker, style, GUILayout.MinWidth(80f));
                        }
                        GUILayout.EndHorizontal();
                    }
                }
            }
            GUILayout.Space(12f);
        }
        GUILayout.EndScrollView();
        if (GUILayout.Button("Fechar relatorio (sem mudar pausa ou AI Step)")) SetRallyFormationVisible(false);
        GUILayout.EndArea();
        GUI.DragWindow(new Rect(0, 0, aiReviewWindowRect.width, 25f));
    }

    private static void DrawRallyReadinessBar(string label, (float atual, float exigido) values, bool ready, GUIStyle style, bool optional = false)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, style, GUILayout.Width(90f));
        Rect rect = GUILayoutUtility.GetRect(80f, 16f, GUILayout.ExpandWidth(true));
        Color previous = GUI.color;
        GUI.color = new Color(0.12f, 0.14f, 0.17f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        float ratio = values.exigido > 0f ? Mathf.Clamp01(values.atual / values.exigido) : 1f;
        rect.width *= ratio;
        GUI.color = ready ? new Color(0.2f, 0.75f, 0.35f) : new Color(0.8f, 0.6f, 0.15f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
        GUILayout.Label(optional ? $"{values.atual:0.#} (opcional)" : $"{values.atual:0.#} / {values.exigido:0.#}", style, GUILayout.Width(95f));
        GUILayout.EndHorizontal();
    }
}
