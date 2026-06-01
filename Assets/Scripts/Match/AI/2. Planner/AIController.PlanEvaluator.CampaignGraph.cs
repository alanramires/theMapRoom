using System.Collections.Generic;

public partial class AIController
{
    private struct CampaignSectorEdge
    {
        public ConstructionSector From;
        public ConstructionSector To;
        public bool Primary;

        public CampaignSectorEdge(ConstructionSector from, ConstructionSector to, bool primary)
        {
            From = from;
            To = to;
            Primary = primary;
        }
    }

    private static readonly CampaignSectorEdge[] GreenCampaignEdges =
    {
        new CampaignSectorEdge(ConstructionSector.Alpha, ConstructionSector.Bravo, true),
        new CampaignSectorEdge(ConstructionSector.Alpha, ConstructionSector.Charlie, true),
        new CampaignSectorEdge(ConstructionSector.Bravo, ConstructionSector.Echo, true),
        new CampaignSectorEdge(ConstructionSector.Charlie, ConstructionSector.Echo, true),
        new CampaignSectorEdge(ConstructionSector.Charlie, ConstructionSector.Delta, false),
        new CampaignSectorEdge(ConstructionSector.Bravo, ConstructionSector.Foxtrot, false),
        new CampaignSectorEdge(ConstructionSector.Echo, ConstructionSector.Golf, true),
        new CampaignSectorEdge(ConstructionSector.Golf, ConstructionSector.Delta, true),
    };

    private static readonly CampaignSectorEdge[] RedCampaignEdges =
    {
        new CampaignSectorEdge(ConstructionSector.Delta, ConstructionSector.Golf, true),
        new CampaignSectorEdge(ConstructionSector.Golf, ConstructionSector.Echo, true),
        new CampaignSectorEdge(ConstructionSector.Echo, ConstructionSector.Bravo, true),
        new CampaignSectorEdge(ConstructionSector.Echo, ConstructionSector.Charlie, true),
        new CampaignSectorEdge(ConstructionSector.Echo, ConstructionSector.Foxtrot, false),
        new CampaignSectorEdge(ConstructionSector.Bravo, ConstructionSector.Alpha, true),
        new CampaignSectorEdge(ConstructionSector.Charlie, ConstructionSector.Alpha, true),
    };

    private static CampaignSectorEdge[] GetCampaignEdges(TeamId aiTeam)
    {
        return aiTeam == TeamId.Red ? RedCampaignEdges : GreenCampaignEdges;
    }

    private static bool TryGetCampaignPredecessor(
        ConstructionSector sector,
        TeamObjectivePlan plan,
        TeamId aiTeam,
        out ConstructionSector predecessor,
        out bool primary)
    {
        predecessor = ConstructionSector.None;
        primary = false;
        CampaignSectorEdge[] edges = GetCampaignEdges(aiTeam);

        for (int pass = 0; pass < 2; pass++)
        {
            bool wantPrimary = pass == 0;
            for (int i = 0; i < edges.Length; i++)
            {
                CampaignSectorEdge edge = edges[i];
                if (edge.To != sector || edge.Primary != wantPrimary)
                    continue;

                if (!IsCampaignSectorAvailable(edge.From, plan, aiTeam))
                    continue;

                predecessor = edge.From;
                primary = edge.Primary;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetRequiredCampaignPredecessor(
        ConstructionSector sector,
        TeamId aiTeam,
        out ConstructionSector predecessor)
    {
        predecessor = ConstructionSector.None;
        CampaignSectorEdge[] edges = GetCampaignEdges(aiTeam);

        for (int pass = 0; pass < 2; pass++)
        {
            bool wantPrimary = pass == 0;
            for (int i = 0; i < edges.Length; i++)
            {
                CampaignSectorEdge edge = edges[i];
                if (edge.To != sector || edge.Primary != wantPrimary)
                    continue;

                predecessor = edge.From;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetPrimaryCampaignSuccessor(
        ConstructionSector sector,
        TeamId aiTeam,
        out ConstructionSector successor)
    {
        successor = ConstructionSector.None;
        CampaignSectorEdge[] edges = GetCampaignEdges(aiTeam);
        for (int i = 0; i < edges.Length; i++)
        {
            CampaignSectorEdge edge = edges[i];
            if (edge.From == sector && edge.Primary)
            {
                successor = edge.To;
                return true;
            }
        }

        return false;
    }

    private static int GetCampaignAdvancePriorityBonus(
        ConstructionSector sector,
        TeamObjectivePlan plan,
        TeamId aiTeam)
    {
        if (!TryGetCampaignPredecessor(sector, plan, aiTeam, out _, out bool primary))
            return 0;

        return primary ? 24 : 6;
    }

    private static bool IsCampaignSectorAvailable(
        ConstructionSector sector,
        TeamObjectivePlan plan,
        TeamId aiTeam)
    {
        if (sector == ConstructionSector.None)
            return false;

        if (TryGetAnySectorInfo(sector, out SectorManager.SectorInfo info)
            && IsOwnedDefensibleSector(info, aiTeam))
            return true;

        SectorObjective obj = plan != null ? plan.GetObjectiveForSector(sector) : null;
        return obj != null
            && obj.Status != ObjectiveStatus.Complete
            && obj.Status != ObjectiveStatus.Abandoned
            && obj.Status != ObjectiveStatus.Defending;
    }
}
