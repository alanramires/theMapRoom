using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private const int FireSupportEmbarkSectorThreshold = 6;

    private PlayerAction TryDecideFireSupportEmbarkAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        SectorObjective assigned = null)
    {
        if (unit == null || snapshot == null)
            return null;

        // Unidade que precisa de reboque (skill "precisaReboque", ex.: Artilharia de Campanha) só
        // embarca no supridor durante a INVASÃO (IsInvading). Fora dela, embarcar/desembarcar
        // descoordenado confunde a AI; quando o GoGreen inicia a invasão, a operação é coordenada
        // e o tow funciona certinho. Apoio de fogo SEM a skill de reboque não é afetado. (Navio de
        // transporte é outro caso — tratar à parte quando chegar a hora.)
        if (!snapshot.IsInvading && UnitNeedsTow(unit))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} (reboque) não embarca no supridor: fora de invasão (IsInvading=false)");
            return null;
        }

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        if (assigned != null)
        {
            if (!TryGetAnySectorInfo(assigned.Sector, out SectorManager.SectorInfo info))
                return null;

            if (IsRallyAssemblyObjective(assigned)
                && !IsRallyAssemblyEstablishedForFireSupport(info, snapshot.AITeam))
            {
                Debug.Log($"{TL("FireSupport")} {unit.InstanceId} ignora embarque rally: {assigned.Sector} ainda sem cabeca de ponte");
                return null;
            }

            float sectorDistance = info.GetDistanceToHQ(snapshot.AITeam);
            if (sectorDistance < FireSupportEmbarkSectorThreshold)
            {
                Debug.Log($"{TL("FireSupport")} {unit.InstanceId} ignora embarque: {assigned.Sector} dist={sectorDistance:F0}h < {FireSupportEmbarkSectorThreshold}h");
                return null;
            }
        }
        else if (TryFindTowDeliveryTarget(unit, fromCell, snapshot, plan, out Vector3Int deliveryTarget)
            && SectorManager.HexDistance(fromCell, deliveryTarget) < FireSupportEmbarkSectorThreshold)
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} ignora embarque rogue: destino < {FireSupportEmbarkSectorThreshold}h");
            return null;
        }

        return TryDecideAssaultEmbarkAction(unit, snapshot, plan, FireSupportEmbarkSectorThreshold);
    }

    private static bool IsRallyAssemblyEstablishedForFireSupport(SectorManager.SectorInfo info, TeamId aiTeam)
    {
        if (info == null)
            return false;

        if (info.ControllingTeam == aiTeam)
            return true;

        if (info.IsFullyControlled && info.ControllingTeam == aiTeam)
            return true;

        int owned = 0;
        int total = 0;
        IReadOnlyList<SectorManager.SectorConstructionInfo> constructions = info.Constructions;
        if (constructions != null)
        {
            for (int i = 0; i < constructions.Count; i++)
            {
                SectorManager.SectorConstructionInfo construction = constructions[i];
                if (construction == null)
                    continue;

                total++;
                if (construction.OwnerTeam == aiTeam)
                    owned++;
            }
        }

        return total > 0 && owned * 2 > total;
    }
}
