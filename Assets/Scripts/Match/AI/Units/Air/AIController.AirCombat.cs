using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class AIController
{
    private sealed class AirCombatTacticalCandidate
    {
        public Vector3Int AttackCell;
        public UnitManager Target;
        public PodeMirarTargetOption AttackOption;
        public bool TargetIsAircraft;
        public BazookaTargetPriority TargetPreference;
    }

    private PlayerAction TryDecideAirCombatAction(UnitManager unit, AIWorldSnapshot snapshot)
    {
        if (!IsAirCombatUnit(unit) || snapshot == null)
            return null;

        bool wasGrounded = unit.IsAircraftGrounded;
        List<int> takeoffMoveOptions = null;
        if (wasGrounded && !TryGetAITakeoffMoveOptions(unit, out takeoffMoveOptions, out string takeoffReason))
        {
            Debug.Log($"{TL("AirCombat")} {unit.InstanceId} sem acao: decolagem indisponivel ({takeoffReason})");
            return null;
        }

        if (wasGrounded)
            unit.SetAircraftGrounded(false);

        try
        {
            return DecideRogueAirCombatAction(unit, snapshot, takeoffMoveOptions);
        }
        finally
        {
            if (wasGrounded)
                unit.SetAircraftGrounded(true);
        }
    }

    /// <summary>
    /// Versao somente-tiro usada pela agenda de Vigilancia. Reaproveita os
    /// mesmos candidatos, PodeMirar, prioridades e regras de decolagem do
    /// combate aereo, mas nao materializa a patrulha/fallback de movimento: se
    /// nao existe ataque legal, MelhorVisao conserva a autoridade sobre a
    /// posicao da unidade.
    /// </summary>
    private PlayerAction TryDecideAirCombatAttackOnly(
        UnitManager unit,
        AIWorldSnapshot snapshot)
    {
        if (!IsAirCombatUnit(unit) || snapshot == null)
            return null;

        bool wasGrounded = unit.IsAircraftGrounded;
        List<int> takeoffMoveOptions = null;
        if (wasGrounded
            && !TryGetAITakeoffMoveOptions(
                unit,
                out takeoffMoveOptions,
                out _))
        {
            return null;
        }

        if (wasGrounded)
            unit.SetAircraftGrounded(false);

        try
        {
            Vector3Int fromCell = unit.CurrentCellPosition;
            fromCell.z = 0;
            Dictionary<Vector3Int, List<Vector3Int>> paths =
                UnitMovementPathRules.CalcularCaminhosValidos(
                    boardTilemap,
                    unit,
                    Mathf.Max(0, unit.RemainingMovementPoints),
                    terrainDatabase);
            if (paths == null || paths.Count == 0)
                return null;

            HashSet<Vector3Int> occupied = BuildAirOccupied(unit);
            List<UnitManager> visibleEnemies =
                CollectVisibleAirCombatEnemies(unit, snapshot);
            Vector3Int anchor = ResolveAirCombatFallbackAnchor(
                unit,
                snapshot,
                fromCell,
                visibleEnemies,
                out _);
            anchor.z = 0;
            if (!TryFindAirCombatAttack(
                    unit,
                    snapshot,
                    fromCell,
                    paths,
                    occupied,
                    anchor,
                    takeoffMoveOptions,
                    visibleEnemies,
                    out Vector3Int attackCell,
                    out UnitManager target,
                    out string attackReason))
            {
                return null;
            }

            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            Debug.Log(
                $"{TL("Vigilancia")} {unit.InstanceId} " +
                $"ataca via {attackCell} -> " +
                $"{target.UnitDisplayName}#{target.InstanceId} " +
                $"({attackReason})");
            return BuildAttackBatch(
                unit,
                snapshot.AITeam,
                fromCell,
                attackCell,
                target.InstanceId.ToString(),
                targetCell,
                paths);
        }
        finally
        {
            if (wasGrounded)
                unit.SetAircraftGrounded(true);
        }
    }

    /// <summary>
    /// Aeronave que consulta o pipeline de combate aereo.
    ///
    /// Testava `roles[0] == RaidAntiSub` — papel que NENHUMA ficha carrega desde
    /// que as cinco unidades de vigilancia viraram `Vigilancia`. Ramo morto, e o
    /// efeito era concreto: o Super Tucano deixou de ser reconhecido como
    /// combatente aereo.
    ///
    /// O substituto nao e outro papel: e CAPACIDADE. Vigilancia aerea ARMADA
    /// consulta o pipeline; quem nao tem arma nao. Radar e EWACS continuam de
    /// fora sozinhos, sem precisar de excecao por nome, porque nao tem arma.
    ///
    /// Isto apenas AUTORIZA a consulta. A legalidade do tiro continua inteira no
    /// PodeMirar.
    /// </summary>
    private static bool IsAirCombatUnit(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return false;

        if (data.domain != Domain.Air || data.roles == null || data.roles.Count == 0)
            return false;

        return data.roles[0] == UnitRole.AtaqueAereo
            || data.roles[0] == UnitRole.Interceptador
            || IsArmedSurveillance(unit, data);
    }

    private static bool IsOffensiveAirCombatUnit(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return false;

        return data.roles != null
            && data.roles.Count > 0
            && (data.roles[0] == UnitRole.AtaqueAereo
                || IsArmedSurveillance(unit, data));
    }

    /// <summary>
    /// Vigilancia que pode atirar. E o que devolve o Super Tucano ao combate sem
    /// ressuscitar um papel: o antissubmarino e vigilancia `Submarine/Submerged`
    /// com arma, nao uma categoria propria.
    /// </summary>
    private static bool IsArmedSurveillance(UnitManager unit, UnitData data)
    {
        return UnitRoleCompatibility.CanSatisfy(data, UnitRole.Vigilancia)
            && HasUsableWeapon(unit);
    }

    private static bool HasUsableWeapon(UnitManager unit)
    {
        IReadOnlyList<UnitEmbarkedWeapon> weapons =
            unit != null ? unit.GetEmbarkedWeapons() : null;
        if (weapons == null)
            return false;

        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i]?.weapon != null && weapons[i].squadAmmunition > 0)
                return true;
        }
        return false;
    }

    private PlayerAction DecideRogueAirCombatAction(UnitManager unit, AIWorldSnapshot snapshot, List<int> takeoffMoveOptions = null)
    {
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildAirOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        List<UnitManager> visibleEnemies =
            CollectVisibleAirCombatEnemies(unit, snapshot);
        Vector3Int anchor = ResolveAirCombatFallbackAnchor(
            unit,
            snapshot,
            fromCell,
            visibleEnemies,
            out string anchorTier);
        anchor.z = 0;

        if (TryFindAirCombatAttack(
                unit,
                snapshot,
                fromCell,
                paths,
                occupied,
                anchor,
                takeoffMoveOptions,
                visibleEnemies,
                out Vector3Int attackCell, out UnitManager target, out string attackReason))
        {
            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            Debug.Log($"{TL("AirCombat")} {unit.InstanceId} rogue ataca via {attackCell} -> {target.UnitDisplayName}#{target.InstanceId} ({attackReason})");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackCell,
                target.InstanceId.ToString(), targetCell, paths);
        }

        if (TryBuildAirPlatformRuntimeAction(
                unit,
                snapshot,
                anchor,
                paths,
                landingSnapshot: null,
                ewacsRecovery: null,
                minimumMissionGain:
                    ResolveAirPlatformMinimumMissionGain(unit),
                acceptOnlyRecovery: true,
                maximumRecoveryRegression: 1f,
                out PlayerAction platformAction,
                out string platformReason))
        {
            Debug.Log(
                $"{TL("AirCombat")} {unit.InstanceId} " +
                $"plataforma: {platformReason}");
            return platformAction;
        }

        bool preferCapturerEscortBand =
            anchorTier.StartsWith("CapturerMagnet:");
        bool usesHalfMovementCaptainPatrol =
            preferCapturerEscortBand
            && unit.TryGetUnitData(out UnitData patrolData)
            && patrolData != null
            && patrolData.roles != null
            && patrolData.roles.Count > 0
            && (patrolData.roles[0] == UnitRole.AtaqueAereo
                || patrolData.roles[0] == UnitRole.Interceptador);
        if (usesHalfMovementCaptainPatrol)
        {
            int patrolRadius =
                Mathf.Max(2, unit.MaxMovementPoints / 2);
            bool hasPatrolGeometry =
                TryBuildAirCombatPatrolGeometry(
                    unit,
                    snapshot,
                    anchor,
                    out List<Vector3Int> patrolCombatants,
                    out List<Vector3Int> patrolEnemies,
                    out Vector3Int patrolFrontAnchor,
                    out AIBacklineSettings patrolSettings,
                    out AIBacklineResult patrolGeometry);
            List<Vector3Int> patrolAllyCells =
                CollectAirCombatPatrolAllyCells(unit);
            bool hasPreferredPatrolZone =
                hasPatrolGeometry
                && HasAirCombatPreferredPatrolCell(
                    unit,
                    paths,
                    occupied,
                    takeoffMoveOptions,
                    patrolCombatants,
                    patrolEnemies,
                    patrolFrontAnchor,
                    patrolSettings,
                    patrolGeometry);

            // Ataque aereo/bombardeiro nao atravessa a propria politica de
            // postura so porque nenhum posto seguro cabe no movimento desta
            // rodada. Antes, hasPreferredPatrolZone=false reabria todas as
            // celulas e permitia que a Progressao escolhesse Vanguarda. O
            // passageiro aereo agora aguarda ate Flanco/Retaguarda ser
            // materializavel; interceptadores conservam o fallback de
            // progressao para formar cobertura adiante.
            if (hasPatrolGeometry
                && IsOffensiveAirCombatUnit(unit)
                && !hasPreferredPatrolZone)
            {
                string currentPatrolZone =
                    ResolveAirCombatPatrolZone(
                        fromCell,
                        hasPatrolGeometry,
                        patrolCombatants,
                        patrolEnemies,
                        patrolFrontAnchor,
                        patrolSettings,
                        patrolGeometry);
                Debug.Log(
                    $"{TL("AirCombat")} {unit.InstanceId} patrulha capitao " +
                    $"tier={anchorTier} raio={patrolRadius}h " +
                    $"zonaAtual={currentPatrolZone}: " +
                    "sem Flanco/Retaguarda materializavel; mantem posicao.");
                return BuildMoveBatch(
                    unit,
                    snapshot.AITeam,
                    fromCell,
                    fromCell,
                    paths);
            }

            if (TryFindBestToolProgressionCell(
                    unit,
                    snapshot,
                    fromCell,
                    anchor,
                    paths,
                    occupied,
                    ToolProgressionIntent.AssaultPressure,
                    out Vector3Int progressionCell,
                    out ToolProgressionCandidate progressionCandidate,
                    out string progressionReason,
                    allowCell: cell =>
                        IsAITakeoffDestinationAllowed(
                            paths,
                            cell,
                            takeoffMoveOptions)
                        && (!hasPreferredPatrolZone
                            || IsAirCombatPreferredPatrolCell(
                                unit,
                                cell,
                                patrolCombatants,
                                patrolEnemies,
                                patrolFrontAnchor,
                                patrolSettings,
                                patrolGeometry)),
                    tacticalScore: (cell, candidate) =>
                        ScoreAirCombatPatrolCell(
                            unit,
                            cell,
                            anchor,
                            patrolRadius,
                            hasPatrolGeometry,
                            patrolCombatants,
                            patrolEnemies,
                            patrolFrontAnchor,
                            patrolSettings,
                            patrolGeometry,
                            patrolAllyCells)))
            {
                string patrolZone =
                    ResolveAirCombatPatrolZone(
                        progressionCell,
                        hasPatrolGeometry,
                        patrolCombatants,
                        patrolEnemies,
                        patrolFrontAnchor,
                        patrolSettings,
                        patrolGeometry);
                Debug.Log(
                    $"{TL("AirCombat")} {unit.InstanceId} patrulha capitao " +
                    $"via progressao oficial {progressionCell} " +
                    $"tier={anchorTier} " +
                    $"raio={patrolRadius}h zona={patrolZone} " +
                    $"tool={progressionCandidate.ToolScore} " +
                    $"({progressionReason})");
                return BuildMoveBatch(
                    unit,
                    snapshot.AITeam,
                    fromCell,
                    progressionCell,
                    paths);
            }

            Debug.Log(
                $"{TL("AirCombat")} {unit.InstanceId} patrulha capitao: " +
                "progressao oficial sem movimento; mantem posicao.");
            return BuildMoveBatch(
                unit,
                snapshot.AITeam,
                fromCell,
                fromCell,
                paths);
        }

        Vector3Int moveCell = FindAirCombatAdvanceMove(
            fromCell,
            anchor,
            paths,
            occupied,
            snapshot.AITeam,
            takeoffMoveOptions,
            preferCapturerEscortBand);
        Debug.Log(
            $"{TL("AirCombat")} {unit.InstanceId} rogue avanca via {moveCell} " +
            $"alvo={anchor} tier={anchorTier} visibleEnemies={visibleEnemies.Count}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
    }

    private bool TryBuildAirCombatPatrolGeometry(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fallbackAnchor,
        out List<Vector3Int> combatants,
        out List<Vector3Int> enemies,
        out Vector3Int frontAnchor,
        out AIBacklineSettings settings,
        out AIBacklineResult geometry)
    {
        combatants = null;
        enemies = null;
        frontAnchor = fallbackAnchor;
        frontAnchor.z = 0;
        settings = BuildBacklineSettings();
        geometry = null;

        if (!TryBuildBacklineContext(
                unit,
                snapshot,
                out combatants,
                out enemies))
        {
            return false;
        }

        if (enemies != null && enemies.Count > 0)
        {
            frontAnchor = ResolveBacklineAnchor(
                enemies,
                fallbackAnchor);
        }
        else if (snapshot != null && snapshot.EnemyHQ != null)
        {
            frontAnchor = snapshot.EnemyHQ.CurrentCellPosition;
            frontAnchor.z = 0;
        }
        else
        {
            return false;
        }
        geometry = AIBacklineAnalyzer.Analyze(
            combatants,
            enemies,
            frontAnchor,
            settings);
        return geometry != null && geometry.Success;
    }

    private static List<Vector3Int>
        CollectAirCombatPatrolAllyCells(UnitManager unit)
    {
        var cells = new List<Vector3Int>();
        if (unit == null)
            return cells;

        foreach (UnitManager ally in UnitManager.AllActive)
        {
            if (ally == null
                || ally == unit
                || ally.IsDead
                || ally.IsEmbarked
                || ally.IsAircraftGrounded
                || ally.SlotIndex != unit.SlotIndex
                || !IsAirCombatUnit(ally))
            {
                continue;
            }

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            cells.Add(allyCell);
        }

        return cells;
    }

    private bool HasAirCombatPreferredPatrolCell(
        UnitManager unit,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        List<int> takeoffMoveOptions,
        IReadOnlyList<Vector3Int> combatants,
        IReadOnlyList<Vector3Int> enemies,
        Vector3Int frontAnchor,
        AIBacklineSettings settings,
        AIBacklineResult geometry)
    {
        if (paths == null || geometry == null)
            return false;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell
                || (occupied != null && occupied.Contains(cell))
                || !IsAITakeoffDestinationAllowed(
                    paths,
                    cell,
                    takeoffMoveOptions))
            {
                continue;
            }

            if (IsAirCombatPreferredPatrolCell(
                    unit,
                    cell,
                    combatants,
                    enemies,
                    frontAnchor,
                    settings,
                    geometry))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAirCombatPreferredPatrolCell(
        UnitManager unit,
        Vector3Int cell,
        IReadOnlyList<Vector3Int> combatants,
        IReadOnlyList<Vector3Int> enemies,
        Vector3Int frontAnchor,
        AIBacklineSettings settings,
        AIBacklineResult geometry)
    {
        if (unit == null || geometry == null)
            return true;

        AIBacklineScore position =
            AIBacklineAnalyzer.ScoreCell(
                combatants,
                enemies,
                cell,
                frontAnchor,
                settings,
                geometry);
        return IsOffensiveAirCombatUnit(unit)
            ? !position.IsVanguard
            : !position.InRearSlice;
    }

    private float ScoreAirCombatPatrolCell(
        UnitManager unit,
        Vector3Int cell,
        Vector3Int captainCell,
        int patrolRadius,
        bool hasPatrolGeometry,
        IReadOnlyList<Vector3Int> combatants,
        IReadOnlyList<Vector3Int> enemies,
        Vector3Int frontAnchor,
        AIBacklineSettings settings,
        AIBacklineResult geometry,
        IReadOnlyList<Vector3Int> patrolAllyCells)
    {
        float distance =
            SectorManager.HexDistance(cell, captainCell);
        float radialError =
            Mathf.Abs(distance - patrolRadius);
        float outsideBand =
            Mathf.Max(0f, radialError - 1f);

        // O anel e a missao ociosa: dentro de +/-1h, a geometria da frente
        // decide o posto de patrulha. Fora dele, aproximar-se do anel domina.
        float score =
            -outsideBand * 20000f
            -radialError * 250f;

        if (hasPatrolGeometry)
        {
            AIBacklineScore position =
                AIBacklineAnalyzer.ScoreCell(
                    combatants,
                    enemies,
                    cell,
                    frontAnchor,
                    settings,
                    geometry);
            bool offensive =
                IsOffensiveAirCombatUnit(unit);
            if (offensive)
            {
                // Ataque aereo/bombardeiro nunca e a tela da formacao.
                // Flancos e retaguarda sao postos validos; vanguarda perde.
                score += position.IsVanguard
                    ? -30000f
                    : position.InRearSlice
                        ? 7000f
                        : 6500f;
            }
            else
            {
                // Interceptador forma a cobertura adiante e nos flancos.
                // Retaguarda continua materializavel, mas e o ultimo posto.
                score += position.IsVanguard
                    ? 7000f
                    : position.InRearSlice
                        ? -2500f
                        : 6500f;
            }
        }

        // Evita transformar o anel em outro montinho. Como cada batch e
        // confirmado antes da proxima decisao, a separacao acompanha os
        // postos ocupados no snapshot corrente sem reserva paralela.
        float nearestPatrolAlly = float.MaxValue;
        if (patrolAllyCells != null)
        {
            for (int i = 0; i < patrolAllyCells.Count; i++)
            {
                nearestPatrolAlly = Mathf.Min(
                    nearestPatrolAlly,
                    SectorManager.HexDistance(
                        cell,
                        patrolAllyCells[i]));
            }
        }
        if (nearestPatrolAlly <= 1f)
            score -= 2500f;
        else if (nearestPatrolAlly <= 2f)
            score -= 600f;

        return score;
    }

    private static string ResolveAirCombatPatrolZone(
        Vector3Int cell,
        bool hasPatrolGeometry,
        IReadOnlyList<Vector3Int> combatants,
        IReadOnlyList<Vector3Int> enemies,
        Vector3Int frontAnchor,
        AIBacklineSettings settings,
        AIBacklineResult geometry)
    {
        if (!hasPatrolGeometry)
            return "SemGeometria";

        AIBacklineScore position =
            AIBacklineAnalyzer.ScoreCell(
                combatants,
                enemies,
                cell,
                frontAnchor,
                settings,
                geometry);
        return position.IsVanguard
            ? "Vanguarda"
            : position.InRearSlice
                ? "Retaguarda"
                : "Flanco";
    }

    private bool TryGetAITakeoffMoveOptions(UnitManager unit, out List<int> moveOptions, out string reason)
    {
        moveOptions = null;
        reason = string.Empty;

        Tilemap boardMap = boardTilemap != null ? boardTilemap : unit != null ? unit.BoardTilemap : null;
        PodeDecolarReport takeoff = PodeDecolarSensor.Evaluate(
            unit,
            boardMap,
            terrainDatabase,
            allowSameTeamAirBlockerForMovementTakeoff: true);
        reason = takeoff != null ? takeoff.explicacao : "sensor indisponivel";

        if (takeoff == null || takeoff.takeoffMoveOptions == null || takeoff.takeoffMoveOptions.Count == 0)
            return false;

        if (!takeoff.status)
            return false;

        moveOptions = new List<int>(takeoff.takeoffMoveOptions);
        return true;
    }

    private List<UnitManager> CollectVisibleAirCombatEnemies(
        UnitManager attacker,
        AIWorldSnapshot snapshot)
    {
        var visibleEnemies = new List<UnitManager>();
        if (attacker == null || snapshot == null)
            return visibleEnemies;

        MatchController matchController = GetMatchController();
        PlayerSlotId aiSlot =
            PlayerSlotId.FromIndex(snapshot.AISlotIndex);
        List<UnitManager> allUnits = UnitManager.AllActive;
        for (int i = 0; i < allUnits.Count; i++)
        {
            UnitManager enemy = allUnits[i];
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked
                || !enemy.gameObject.activeInHierarchy
                || !PlayerSlotRelations.AreEnemies(attacker, enemy))
            {
                continue;
            }

            // Consulta a visibilidade confirmada/cacheada da vez ativa uma
            // unica vez por inimigo. A variante NoCache era repetida para
            // cada combinacao de celula e alvo.
            if (matchController != null
                && !matchController.IsUnitVisibleForSlot(enemy, aiSlot))
            {
                continue;
            }

            visibleEnemies.Add(enemy);
        }

        AIDecisionPerf.AddCount(
            "AirCombatVisibleEnemies",
            visibleEnemies.Count);
        return visibleEnemies;
    }

    private Vector3Int ResolveAirCombatFallbackAnchor(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        List<UnitManager> visibleEnemies,
        out string tier)
    {
        tier = "None";
        bool isInterceptor = unit != null
            && unit.TryGetUnitData(out UnitData airData)
            && airData != null
            && airData.roles != null
            && airData.roles.Count > 0
            && airData.roles[0] == UnitRole.Interceptador;

        bool hasCaptain = TryResolveCapturerMagnet(
                unit,
                snapshot,
                fromCell,
                out UnitManager capturer,
                out Vector3Int capturerCell);

        // Interceptadores protegem o elo mais proximo da rede: EWACS ou
        // capitao. Ataque aereo e vigilancia armada continuam magnetizados apenas
        // pela cabeca de ponte.
        if (isInterceptor
            && TryResolveNearestEwacsMagnet(
                unit,
                snapshot,
                fromCell,
                out UnitManager ewacs,
                out Vector3Int ewacsCell)
            && (!hasCaptain
                || AIActionReachCoordinator.CubicDistance(
                    fromCell,
                    ewacsCell)
                <= AIActionReachCoordinator.CubicDistance(
                    fromCell,
                    capturerCell)))
        {
            tier = $"AirSurveillance:EWACS#{ewacs.InstanceId}";
            return ewacsCell;
        }

        if (hasCaptain)
        {
            tier = $"CapturerMagnet:#{capturer.InstanceId}";
            return capturerCell;
        }
        UnitManager nearestEnemy = null;
        int nearestEnemyDistance = int.MaxValue;
        if (visibleEnemies != null)
        {
            for (int i = 0; i < visibleEnemies.Count; i++)
            {
                UnitManager enemy = visibleEnemies[i];
                if (enemy == null || enemy.IsDead || enemy.IsEmbarked)
                    continue;

                Vector3Int cell = enemy.CurrentCellPosition;
                cell.z = 0;
                int distance =
                    AIActionReachCoordinator.CubicDistance(fromCell, cell);
                if (distance < nearestEnemyDistance
                    || (distance == nearestEnemyDistance
                        && nearestEnemy != null
                        && enemy.InstanceId < nearestEnemy.InstanceId))
                {
                    nearestEnemyDistance = distance;
                    nearestEnemy = enemy;
                }
            }
        }

        if (nearestEnemy != null)
        {
            tier = "Operational:CubicNearestEnemy";
            Vector3Int cell = nearestEnemy.CurrentCellPosition;
            cell.z = 0;
            return cell;
        }

        if (snapshot != null && snapshot.EnemyBuildings != null && snapshot.EnemyBuildings.Count > 0)
        {
            ConstructionManager best = null;
            int bestDist = int.MaxValue;
            foreach (ConstructionManager building in snapshot.EnemyBuildings)
            {
                if (building == null) continue;
                Vector3Int cell = building.CurrentCellPosition;
                cell.z = 0;
                int dist =
                    AIActionReachCoordinator.CubicDistance(fromCell, cell);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = building;
                }
            }

            if (best != null)
            {
                tier = "Strategic:CubicNearestBuilding";
                Vector3Int cell = best.CurrentCellPosition;
                cell.z = 0;
                return cell;
            }
        }

        return fromCell;
    }

    private static bool TryResolveNearestEwacsMagnet(
        UnitManager follower,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        out UnitManager ewacs,
        out Vector3Int anchor)
    {
        ewacs = null;
        anchor = fromCell;
        anchor.z = 0;
        if (snapshot == null || snapshot.MyUnits == null)
            return false;

        int bestDistance = int.MaxValue;
        for (int i = 0; i < snapshot.MyUnits.Count; i++)
        {
            UnitManager candidate = snapshot.MyUnits[i];
            if (candidate == null
                || candidate == follower
                || candidate.IsDead
                || candidate.IsEmbarked
                || candidate.IsUnderRepair
                || !candidate.gameObject.activeInHierarchy
                || !candidate.TryGetUnitData(out UnitData data)
                || data == null
                || data.domain != Domain.Air
                || !IsAirSurveillanceUnit(candidate))
            {
                continue;
            }

            Vector3Int cell = candidate.CurrentCellPosition;
            cell.z = 0;
            int distance =
                AIActionReachCoordinator.CubicDistance(fromCell, cell);
            if (distance < bestDistance
                || (distance == bestDistance
                    && (ewacs == null
                        || candidate.InstanceId < ewacs.InstanceId)))
            {
                bestDistance = distance;
                ewacs = candidate;
                anchor = cell;
            }
        }

        return ewacs != null;
    }

    private bool TryFindAirCombatAttack(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int anchor,
        List<int> takeoffMoveOptions,
        List<UnitManager> visibleEnemies,
        out Vector3Int bestCell,
        out UnitManager bestTarget,
        out string reason)
    {
        bestCell = fromCell;
        bestTarget = null;
        reason = "";
        float bestScore = float.MinValue;
        List<AirCombatTacticalCandidate> candidates =
            CollectAirCombatTacticalCandidates(
                unit,
                snapshot,
                fromCell,
                paths,
                occupied,
                takeoffMoveOptions,
                visibleEnemies);
        ResolveAirCombatCandidateGates(
            candidates,
            out bool hasAttackableAircraft,
            out bool hasPreferredAttackableAircraft);

        for (int i = 0; i < candidates.Count; i++)
        {
            AirCombatTacticalCandidate candidate = candidates[i];
            UnitManager enemy = candidate.Target;
            Vector3Int cell = candidate.AttackCell;
            PodeMirarTargetOption attackOption = candidate.AttackOption;
            bool isAirEnemy = candidate.TargetIsAircraft;
            if (!ShouldConsiderAirCombatTarget(
                    unit,
                    enemy,
                    hasAttackableAircraft,
                    hasPreferredAttackableAircraft))
            {
                continue;
            }
                // Aerial targets bypass PassesAttackDecision — a fighter always engages air threats.
            string attackDecisionReason = "atkDecision=airPriority";
            if (!isAirEnemy
                && !PassesAttackDecision(
                    unit,
                    enemy,
                    cell,
                    false,
                    out attackDecisionReason))
            {
                continue;
            }

            float combatScore = 0f;
            string combatScoreReason = "";
            if (TrySimulateAttackForAI(
                    unit,
                    enemy,
                    cell,
                    out CombatEvaluationResult simSummary))
            {
                combatScore =
                    (simSummary.Simulation.killGuaranteed ? 26000f : 0f)
                    + simSummary.TargetDamagePercent * 420f
                    + simSummary.TargetDamage * 1100f
                    - simSummary.AttackerLossPercent * 260f
                    - simSummary.AttackerLoss * 900f;
                combatScoreReason =
                    $" combatScore={combatScore:F0} kill={simSummary.Simulation.killGuaranteed} dmg={simSummary.TargetDamage}/{simSummary.TargetDamagePercent}% loss={simSummary.AttackerLoss}/{simSummary.AttackerLossPercent}%";
            }

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            BazookaTargetPriority targetPreference =
                candidate.TargetPreference;
            bool attacksFromCurrentCell = cell == fromCell;
            float targetValueScore =
                ScoreAirCombatTargetValue(enemy, attackOption, unit);
            float postureScore = ScoreAirCombatAttackPosture(
                unit,
                snapshot,
                fromCell,
                cell,
                enemyCell,
                anchor,
                isAirEnemy);
            float score =
                GetAirCombatTargetPreferenceScore(targetPreference)
                + targetValueScore
                + combatScore
                + Mathf.Max(0, 20 - enemy.CurrentHP) * 700f
                + (attacksFromCurrentCell ? 5000f : 0f)
                + postureScore
                - GetPathStepCount(paths, cell) * 8f
                - enemy.InstanceId * 0.001f;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
                bestTarget = enemy;
                string weaponName = attackOption.weapon != null
                    ? ResolveWeaponName(attackOption.weapon)
                    : "semArma";
                reason =
                    $"score={score:F0} pref={targetPreference} " +
                    $"value={targetValueScore:F0} posture={postureScore:F0} " +
                    $"weapon={weaponName} hp={enemy.CurrentHP} " +
                    $"noMove={attacksFromCurrentCell}{combatScoreReason} " +
                    $"{attackDecisionReason}";
            }
        }

        return bestTarget != null;
    }

    private List<AirCombatTacticalCandidate>
        CollectAirCombatTacticalCandidates(
            UnitManager unit,
            AIWorldSnapshot snapshot,
            Vector3Int fromCell,
            Dictionary<Vector3Int, List<Vector3Int>> paths,
            HashSet<Vector3Int> occupied,
            List<int> takeoffMoveOptions,
            List<UnitManager> visibleEnemies)
    {
        using var perf =
            new AIDecisionPerfScope(unit, "airCombatTacticalScan");
        var candidates = new List<AirCombatTacticalCandidate>();
        if (unit == null || snapshot == null || paths == null
            || visibleEnemies == null || visibleEnemies.Count == 0)
        {
            AIDecisionPerf.AddCount(
                "AirCombatTacticalCells",
                paths != null ? paths.Count : 0);
            AIDecisionPerf.AddCount(
                "AirCombatDistancePrunedCells",
                paths != null ? paths.Count : 0);
            Debug.Log(
                $"{TL("AirCombat")} " +
                $"{(unit != null ? unit.InstanceId : 0)} Tactical scan: " +
                $"cells={(paths != null ? paths.Count : 0)} " +
                "prunedByCubic=all sensorCalls=0 candidates=0 " +
                "visibleEnemies=0");
            return candidates;
        }

        int stationaryMaxRange = ResolveAirCombatMaxWeaponRange(
            unit,
            SensorMovementMode.MoveuParado);
        int movedMaxRange = ResolveAirCombatMaxWeaponRange(
            unit,
            SensorMovementMode.MoveuAndando);
        var visibleEnemyIds = new HashSet<int>();
        for (int i = 0; i < visibleEnemies.Count; i++)
        {
            UnitManager enemy = visibleEnemies[i];
            if (enemy != null)
                visibleEnemyIds.Add(enemy.InstanceId);
        }

        int eligibleCells = 0;
        int distancePrunedCells = 0;
        int sensorCalls = 0;
        var sensorTargets = new List<PodeMirarTargetOption>();
        var targetsAddedAtCell = new HashSet<int>();

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell
                && occupied != null
                && occupied.Contains(cell))
            {
                continue;
            }
            if (!IsAITakeoffDestinationAllowed(
                    paths,
                    cell,
                    takeoffMoveOptions))
            {
                continue;
            }

            eligibleCells++;
            SensorMovementMode movementMode = cell == fromCell
                ? SensorMovementMode.MoveuParado
                : SensorMovementMode.MoveuAndando;
            int maxRange = movementMode == SensorMovementMode.MoveuParado
                ? stationaryMaxRange
                : movedMaxRange;
            if (!HasVisibleEnemyWithinCubicRange(
                    visibleEnemies,
                    cell,
                    maxRange))
            {
                distancePrunedCells++;
                continue;
            }

            sensorCalls++;
            sensorTargets.Clear();
            if (!PodeMirarSensor.CollectTargets(
                    unit,
                    boardTilemap,
                    terrainDatabase,
                    movementMode,
                    sensorTargets,
                    weaponPriorityData: turnStateManager != null
                        ? turnStateManager.WeaponPriorityDataRef
                        : null,
                    dpqAirHeightConfig: turnStateManager != null
                        ? turnStateManager.DpqAirHeightConfigRef
                        : null,
                    fromCell: cell))
            {
                continue;
            }

            targetsAddedAtCell.Clear();
            for (int i = 0; i < sensorTargets.Count; i++)
            {
                PodeMirarTargetOption attackOption = sensorTargets[i];
                UnitManager enemy =
                    attackOption != null ? attackOption.targetUnit : null;
                if (enemy == null
                    || !visibleEnemyIds.Contains(enemy.InstanceId)
                    || !targetsAddedAtCell.Add(enemy.InstanceId))
                {
                    continue;
            }

                bool isAircraft =
                    enemy.TryGetUnitData(out UnitData enemyData)
                    && enemyData != null
                    && enemyData.domain == Domain.Air;
                candidates.Add(new AirCombatTacticalCandidate
                {
                    AttackCell = cell,
                    Target = enemy,
                    AttackOption = attackOption,
                    TargetIsAircraft = isAircraft,
                    TargetPreference =
                        ResolveAirCombatTargetPreference(unit, enemy)
                });
            }
        }

        AIDecisionPerf.AddCount(
            "AirCombatTacticalCells",
            eligibleCells);
        AIDecisionPerf.AddCount(
            "AirCombatDistancePrunedCells",
            distancePrunedCells);
        AIDecisionPerf.AddCount(
            "AirCombatSensorCalls",
            sensorCalls);
        AIDecisionPerf.AddCount(
            "AirCombatCandidates",
            candidates.Count);
        Debug.Log(
            $"{TL("AirCombat")} {unit.InstanceId} Tactical scan: " +
            $"cells={eligibleCells} prunedByCubic={distancePrunedCells} " +
            $"sensorCalls={sensorCalls} candidates={candidates.Count} " +
            $"visibleEnemies={visibleEnemies.Count}");
        return candidates;
    }

    private static bool HasVisibleEnemyWithinCubicRange(
        List<UnitManager> visibleEnemies,
        Vector3Int origin,
        int maxRange)
    {
        if (visibleEnemies == null || maxRange < 0)
            return false;

        for (int i = 0; i < visibleEnemies.Count; i++)
        {
            UnitManager enemy = visibleEnemies[i];
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked)
                continue;

            Vector3Int targetCell = enemy.CurrentCellPosition;
            targetCell.z = 0;
            if (AIActionReachCoordinator.CubicDistance(
                    origin,
                    targetCell) <= maxRange)
            {
                return true;
            }
        }

        return false;
    }

    private static int ResolveAirCombatMaxWeaponRange(
        UnitManager unit,
        SensorMovementMode movementMode)
    {
        int maxRange = -1;
        IReadOnlyList<UnitEmbarkedWeapon> weapons =
            unit != null ? unit.GetEmbarkedWeapons() : null;
        if (weapons == null)
            return maxRange;

        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked == null
                || embarked.weapon == null
                || embarked.squadAmmunition <= 0)
            {
                continue;
            }
            if (!PodeMirarSensor.TryResolveWeaponRangeCandidate(
                    embarked,
                    movementMode,
                    requireAmmo: true,
                    out _,
                    out int weaponMaxRange))
            {
                continue;
            }

            maxRange = Mathf.Max(maxRange, weaponMaxRange);
        }

        return maxRange;
    }

    private static void ResolveAirCombatCandidateGates(
        List<AirCombatTacticalCandidate> candidates,
        out bool hasAttackableAircraft,
        out bool hasPreferredAttackableAircraft)
    {
        hasAttackableAircraft = false;
        hasPreferredAttackableAircraft = false;
        if (candidates == null)
            return;

        for (int i = 0; i < candidates.Count; i++)
        {
            AirCombatTacticalCandidate candidate = candidates[i];
            if (candidate == null || !candidate.TargetIsAircraft)
                continue;

            hasAttackableAircraft = true;
            if (candidate.TargetPreference
                != BazookaTargetPriority.Tertiary)
            {
                hasPreferredAttackableAircraft = true;
            }
        }
    }

    private float ScoreAirCombatAttackPosture(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int attackCell,
        Vector3Int enemyCell,
        Vector3Int enemyAnchor,
        bool targetIsAircraft)
    {
        if (!targetIsAircraft)
        {
            return
                - SectorManager.HexDistance(enemyCell, enemyAnchor) * 350f
                - SectorManager.HexDistance(attackCell, enemyAnchor) * 40f;
        }

        float threat = CalculateThreatLevel(attackCell, snapshot.AITeam);
        float moveDistance = SectorManager.HexDistance(fromCell, attackCell);
        float targetDistance = SectorManager.HexDistance(attackCell, enemyCell);
        float score =
            - threat * 900f
            - moveDistance * 110f
            - targetDistance * 45f;

        if (snapshot.MyHQ != null)
        {
            Vector3Int hq = snapshot.MyHQ.CurrentCellPosition;
            hq.z = 0;
            score -= SectorManager.HexDistance(attackCell, hq) * 35f;
        }

        if (snapshot.Stance == AIStance.Offensive && IsOffensiveAirCombatUnit(unit))
            score -= SectorManager.HexDistance(attackCell, enemyAnchor) * 12f;

        return score;
    }

    private static bool ShouldConsiderAirCombatTarget(
        UnitManager attacker,
        UnitManager target,
        bool hasAttackableAircraft,
        bool hasPreferredAttackableAircraft)
    {
        if (target == null || !target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return true;

        bool targetIsAircraft = targetData.domain == Domain.Air;
        if (hasAttackableAircraft && !targetIsAircraft)
            return false;

        if (hasPreferredAttackableAircraft
            && (!targetIsAircraft || ResolveAirCombatTargetPreference(attacker, target) == BazookaTargetPriority.Tertiary))
            return false;

        return true;
    }

    private float ScoreAirCombatTargetValue(UnitManager target, PodeMirarTargetOption option, UnitManager attacker)
    {
        if (target == null || !target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return 0f;

        float score = targetData.cost * 1.2f + targetData.eliteLevel * 6000f;

        bool isAirAttack = targetData.roles != null
            && targetData.roles.Count > 0
            && targetData.roles[0] == UnitRole.AtaqueAereo;
        bool isInterceptor = targetData.roles != null
            && targetData.roles.Count > 0
            && targetData.roles[0] == UnitRole.Interceptador;
        bool isTransport =
            UnitRoleCompatibility.ResolveCompositionRole(targetData) == UnitRole.Transportador;

        if (targetData.domain == Domain.Air)
            score += 26000f;
        if (targetData.domain == Domain.Air && isAirAttack)
            score += targetData.eliteLevel >= 1 ? 30000f : 18000f;
        if (targetData.domain == Domain.Air && isInterceptor)
            score += targetData.eliteLevel >= 1 ? 24000f : 15000f;
        if (targetData.domain == Domain.Air && targetData.unitClass == GameUnitClass.Helicopter)
            score += 22000f;
        if (targetData.domain == Domain.Air && targetData.unitClass == GameUnitClass.Plane)
            score += 26000f;
        if (targetData.domain == Domain.Air && targetData.unitClass == GameUnitClass.Jet)
            score += 18000f;
        if (targetData.domain == Domain.Air && isTransport)
            score -= 3000f;

        WeaponPriorityData weaponPriorityData = turnStateManager != null
            ? turnStateManager.WeaponPriorityDataRef
            : null;

        if (option != null && option.weapon != null
            && PodeMirarSensor.IsPreferredWeaponForTarget(weaponPriorityData, option.weapon, targetData.unitClass))
        {
            score += 6500f;
        }

        return score;
    }

    private static string ResolveWeaponName(WeaponData weapon)
    {
        if (weapon == null)
            return "-";
        if (!string.IsNullOrWhiteSpace(weapon.apelido))
            return weapon.apelido.Trim();
        if (!string.IsNullOrWhiteSpace(weapon.displayName))
            return weapon.displayName.Trim();
        if (!string.IsNullOrWhiteSpace(weapon.id))
            return weapon.id.Trim();
        return weapon.name;
    }

    private static BazookaTargetPriority ResolveAirCombatTargetPreference(UnitManager attacker, UnitManager target)
    {
        if (attacker == null || target == null)
            return BazookaTargetPriority.Tertiary;
        if (!attacker.TryGetUnitData(out UnitData attackerData) || attackerData == null)
            return BazookaTargetPriority.Tertiary;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return BazookaTargetPriority.Tertiary;

        return attackerData.ResolveAiTargetPriorityForTargetClass(targetData.unitClass);
    }

    private static float GetAirCombatTargetPreferenceScore(BazookaTargetPriority priority)
    {
        switch (priority)
        {
            case BazookaTargetPriority.Primary:
                return 30000f;
            case BazookaTargetPriority.Secondary:
                return 15000f;
            default:
                return 0f;
        }
    }

    private Vector3Int FindAirCombatAdvanceMove(
        Vector3Int fromCell,
        Vector3Int targetCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        TeamId aiTeam,
        List<int> takeoffMoveOptions = null,
        bool preferOneHexEscortBand = false)
    {
        Vector3Int bestCell = fromCell;
        float startDist = SectorManager.HexDistance(fromCell, targetCell);
        float bestScore = float.MinValue;

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell || occupied.Contains(cell))
                continue;
            if (!IsAITakeoffDestinationAllowed(paths, cell, takeoffMoveOptions))
                continue;

            float dist = SectorManager.HexDistance(cell, targetCell);
            float progress = startDist - dist;
            if (progress <= 0f)
                continue;

            float threat = CalculateThreatLevel(cell, aiTeam);
            float routeLineDeviation = DistanceFromHexLine(cell, fromCell, targetCell);
            int pathSteps = GetPathStepCount(paths, cell);

            float score =
                progress * 2400f
                - dist * 180f
                - routeLineDeviation * 420f
                - threat * 120f
                - pathSteps * 3f;

            // Faixa de escolta, nao proibicao: 1h deixa a unidade-capita
            // legivel no tabuleiro. Distancia zero perde no ranking quando
            // existe alternativa boa, mas continua valida se for o unico
            // avanco materializavel.
            if (preferOneHexEscortBand)
            {
                if (Mathf.Abs(dist - 1f) <= 0.01f)
                    score += 5000f;
                else if (dist <= 0.01f)
                    score -= 5000f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
            }
        }

        if (bestCell == fromCell && occupied != null && occupied.Contains(fromCell))
            return FindAirVacateMove(fromCell, paths, occupied, aiTeam);

        return bestCell;
    }

    private static float DistanceFromHexLine(Vector3Int cell, Vector3Int lineStart, Vector3Int lineEnd)
    {
        Vector2 p = new Vector2(cell.x, cell.y);
        Vector2 a = new Vector2(lineStart.x, lineStart.y);
        Vector2 b = new Vector2(lineEnd.x, lineEnd.y);
        Vector2 ab = b - a;
        float abLenSq = ab.sqrMagnitude;
        if (abLenSq <= 0.0001f)
            return Vector2.Distance(p, a);

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLenSq);
        Vector2 projection = a + ab * t;
        return Vector2.Distance(p, projection);
    }

    private static bool IsAITakeoffDestinationAllowed(
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Vector3Int cell,
        List<int> takeoffMoveOptions)
    {
        if (takeoffMoveOptions == null || takeoffMoveOptions.Count == 0)
            return true;

        if (takeoffMoveOptions.Contains(9) || takeoffMoveOptions.Contains(-1))
            return true;

        int movementHexes = 0;
        if (paths != null && paths.TryGetValue(cell, out List<Vector3Int> path) && path != null)
            movementHexes = Mathf.Max(0, path.Count - 1);

        return takeoffMoveOptions.Contains(movementHexes);
    }
}
