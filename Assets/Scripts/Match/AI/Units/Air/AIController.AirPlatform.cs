using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // Quantos TURNOS de voo proprio a plataforma precisa economizar para o
    // rebasing valer. Um: embarcar custa a rodada inteira (mover, embarcar, e
    // decolar de novo depois), entao a plataforma so compensa se ela adianta
    // mais do que a aeronave adiantaria voando sozinha no mesmo tempo.
    private const float AirPlatformRebasingGainInTacticalTurns = 1f;

    /// <summary>
    /// Ganho minimo de posicionamento para aceitar rebasing, em hexes, medido
    /// como BANDA da aeronave avaliada — nao numero fixo.
    ///
    /// Era 2f cravado na chamada, e por isso um caca de 9 MP embarcava por 3
    /// hexes de ganho: menos de meio turno de voo, pago com o turno inteiro.
    /// Com a banda, o mesmo 3 e recusado e um aviao lento (ou com pouco MP
    /// restante, que voaria pouco de qualquer jeito) volta a aceitar carona
    /// por ganhos pequenos — que para ele nao sao pequenos.
    /// </summary>
    private static float ResolveAirPlatformMinimumMissionGain(
        UnitManager aircraft)
    {
        int tactical = aircraft != null
            ? Mathf.Max(0, aircraft.RemainingMovementPoints)
            : 0;
        return Mathf.Max(
            0.1f,
            tactical * AirPlatformRebasingGainInTacticalTurns);
    }

    /// <summary>
    /// Consome a intenção pura do QueroCaronaAerea e materializa somente o
    /// trecho Tactical permitido nesta rodada. Compatibilidade e vaga
    /// continuam pertencendo a MelhorPouso/PodePousar/PodeEmbarcar.
    /// </summary>
    private bool TryBuildAirPlatformRuntimeAction(
        UnitManager aircraft,
        AIWorldSnapshot snapshot,
        Vector3Int missionFocus,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        MelhorPousoResult landingSnapshot,
        EwacsRecoverySnapshot ewacsRecovery,
        float minimumMissionGain,
        bool acceptOnlyRecovery,
        float maximumRecoveryRegression,
        out PlayerAction action,
        out string reason)
    {
        action = null;
        reason = string.Empty;
        if (aircraft == null
            || snapshot == null
            || paths == null
            || paths.Count == 0)
        {
            return false;
        }

        if (!HasCompatibleAirPlatform(
                aircraft,
                landingSnapshot))
        {
            reason = "nenhuma plataforma aliada com slot compativel";
            return false;
        }

        missionFocus.z = 0;
        QueroCaronaAereaResult ride =
            QueroCaronaAereaService.Evaluate(
                new QueroCaronaAereaRequest
                {
                    aircraft = aircraft,
                    map = boardTilemap,
                    terrainDatabase = terrainDatabase,
                    operationalTurns = 2,
                    emulateUnderRepairFromUnitData = true,
                    landingSnapshot = landingSnapshot,
                    hasMissionFocus = true,
                    missionFocus = missionFocus,
                    minimumMissionDistanceGain =
                        Mathf.Max(0.1f, minimumMissionGain),
                    acceptPlatformWhenOnlyRecovery =
                        acceptOnlyRecovery,
                    maximumMissionRegressionForRecovery =
                        Mathf.Max(0f, maximumRecoveryRegression)
                });

        if (!ride.wantsRide
            || ride.bestPlatform == null
            || ride.bestPlatform.platform == null)
        {
            reason = ride.reason;
            return false;
        }

        UnitManager platform = ride.bestPlatform.platform;
        Vector3Int fromCell = aircraft.CurrentCellPosition;
        fromCell.z = 0;
        TeamObjectivePlan plan =
            ObjectiveManager.GetPlanForSlot(
                PlayerSlotId.FromIndex(snapshot.AISlotIndex));

        // Tactical tenta concluir no batch oficial mover -> embarcar. O
        // método revalida slot, custo, ocupação e PodeEmbarcar no ponto de
        // materialização.
        if (TryBuildRepairEvacExtendedEmbarkAction(
                aircraft,
                snapshot,
                plan,
                snapshot.AITeam,
                fromCell,
                platform,
                paths,
                out action,
                logCategory: "AirPlatform",
                logVerb: "REBASING"))
        {
            reason =
                $"plataforma #{platform.InstanceId} " +
                $"tier={ride.bestPlatform.tier} embarque Tactical; " +
                ride.reason;
            return true;
        }

        Vector3Int platformCell = platform.CurrentCellPosition;
        platformCell.z = 0;
        HashSet<Vector3Int> occupied = BuildAirOccupied(aircraft);
        float fromDistance =
            SectorManager.HexDistance(fromCell, platformCell);
        Vector3Int bestCell = fromCell;
        float bestScore = float.MinValue;
        string bestRecovery = string.Empty;

        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> pair
                 in paths)
        {
            Vector3Int cell = pair.Key;
            cell.z = 0;
            if (cell == fromCell
                || (occupied != null && occupied.Contains(cell)))
            {
                continue;
            }

            float distance =
                SectorManager.HexDistance(cell, platformCell);
            float progress = fromDistance - distance;
            if (progress <= 0f)
                continue;

            if (!IsEwacsRecoveryCellSafe(
                    aircraft,
                    cell,
                    pair.Value,
                    ewacsRecovery,
                    out string recoveryReason))
            {
                continue;
            }

            int pathCost = GetPathStepCount(paths, cell);
            float threat =
                CalculateThreatLevel(cell, snapshot.AITeam);
            float score =
                progress * 2400f
                - distance * 180f
                - threat * 240f
                - pathCost * 12f;
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestCell = cell;
            bestRecovery = recoveryReason;
        }

        if (bestCell == fromCell)
        {
            reason =
                $"aceita plataforma #{platform.InstanceId}, mas sem " +
                $"aproximacao Tactical segura; {ride.reason}";
            return false;
        }

        action = BuildMoveBatch(
            aircraft,
            snapshot.AITeam,
            fromCell,
            bestCell,
            paths);
        reason =
            $"progride para plataforma #{platform.InstanceId} " +
            $"{platformCell} via={bestCell} " +
            $"recovery={bestRecovery}; {ride.reason}";
        return true;
    }

    private static bool HasCompatibleAirPlatform(
        UnitManager aircraft,
        MelhorPousoResult landingSnapshot)
    {
        if (landingSnapshot != null)
        {
            for (int i = 0;
                 i < landingSnapshot.options.Count;
                 i++)
            {
                MelhorPousoOption option =
                    landingSnapshot.options[i];
                if (option != null && option.IsPlatform)
                    return true;
            }
            return false;
        }

        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == null
                || candidate == aircraft
                || candidate.IsDead
                || candidate.IsEmbarked
                || !PlayerSlotRelations.AreAllies(
                    aircraft,
                    candidate))
            {
                continue;
            }

            if (PodePousarSensor.CanLandOnTransporter(
                    aircraft,
                    candidate,
                    out _,
                    out _))
            {
                return true;
            }
        }

        return false;
    }
}
