using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class FogOfWarController : MonoBehaviour
{
    [System.Serializable]
    public sealed class FogUnitContributorsView
    {
        public string targetUnit;
        public int targetInstanceId;
        public bool visibleForActiveTeam;
        public int contributorsCount;
        [TextArea(1, 5)] public string contributors;
    }

    [SerializeField] private MatchController matchController;
    [Header("Presentation")]
    [SerializeField] [Range(0f, 1f)] private float fogOfWarAlpha = 0.65f;
    [SerializeField, HideInInspector] private bool fogOfWarAlphaInitialized;
    [SerializeField] private bool autoRefreshOnFogUpdate = false;
    [SerializeField] private bool includeInvisibleTargets = true;
    [SerializeField] [TextArea(2, 20)] private string lastDump;
    [SerializeField] private List<FogUnitContributorsView> snapshot = new List<FogUnitContributorsView>();

    [System.NonSerialized] private readonly List<MatchController.FogUnitContributorDebugInfo> runtimeBuffer = new List<MatchController.FogUnitContributorDebugInfo>();

    public IReadOnlyList<FogUnitContributorsView> Snapshot => snapshot;
    public string LastDump => lastDump;
    public float FogOfWarAlpha => Mathf.Clamp01(fogOfWarAlpha);

    private void OnEnable()
    {
        EnsureAlphaMigrated();
        MatchController.OnFogOfWarUpdated += HandleFogOfWarUpdated;
    }

    private void OnDisable()
    {
        MatchController.OnFogOfWarUpdated -= HandleFogOfWarUpdated;
    }

    private void OnValidate()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        EnsureAlphaMigrated();
    }

    public void InitializeAlphaFromLegacy(float legacyAlpha)
    {
        if (fogOfWarAlphaInitialized)
            return;

        fogOfWarAlpha = Mathf.Clamp01(legacyAlpha);
        fogOfWarAlphaInitialized = true;
    }

    public void SetAlphaPercent(int alphaPercent)
    {
        fogOfWarAlpha = Mathf.Clamp(alphaPercent, 0, 100) / 100f;
        fogOfWarAlphaInitialized = true;
    }

    private void EnsureAlphaMigrated()
    {
        if (fogOfWarAlphaInitialized || matchController == null)
            return;
        InitializeAlphaFromLegacy(matchController.LegacyFogOfWarAlpha);
    }

    public void RebuildSnapshot()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (matchController == null)
            return;

        runtimeBuffer.Clear();
        matchController.BuildFogUnitContributorDebugSnapshot(runtimeBuffer);

        snapshot.Clear();
        StringBuilder dumpBuilder = new StringBuilder(2048);
        dumpBuilder.AppendLine($"[FoW][Contributors] targets={runtimeBuffer.Count}");

        for (int i = 0; i < runtimeBuffer.Count; i++)
        {
            MatchController.FogUnitContributorDebugInfo info = runtimeBuffer[i];
            if (info == null || info.targetUnit == null)
                continue;
            if (!includeInvisibleTargets && !info.isVisibleForActiveTeam)
                continue;

            FogUnitContributorsView row = new FogUnitContributorsView();
            row.targetUnit = ResolveUnitLabel(info.targetUnit);
            row.targetInstanceId = info.targetUnit.InstanceId > 0 ? info.targetUnit.InstanceId : info.targetUnit.GetEntityId().GetHashCode();
            row.visibleForActiveTeam = info.isVisibleForActiveTeam;
            row.contributorsCount = info.contributors != null ? info.contributors.Count : 0;
            row.contributors = BuildContributorsLine(info.contributors);
            snapshot.Add(row);

            dumpBuilder.Append("- ")
                .Append(row.targetUnit)
                .Append(" [id=")
                .Append(row.targetInstanceId)
                .Append("] visible=")
                .Append(row.visibleForActiveTeam)
                .Append(" contributors=")
                .Append(row.contributorsCount)
                .AppendLine();
            if (!string.IsNullOrWhiteSpace(row.contributors))
                dumpBuilder.AppendLine("  " + row.contributors);
        }

        lastDump = dumpBuilder.ToString();
    }


    public void RefreshFogOfWarForTeam(TeamId observerTeamId)
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (matchController == null)
            return;

        matchController.RefreshFogOfWarForTeam(observerTeamId);
        RebuildSnapshot();
    }
    public void DumpSnapshotToConsole()
    {
        if (string.IsNullOrWhiteSpace(lastDump))
            RebuildSnapshot();

        if (!string.IsNullOrWhiteSpace(lastDump))
            Debug.Log(lastDump, this);
    }

    public void ClearSnapshot()
    {
        snapshot.Clear();
        lastDump = string.Empty;
    }

    private void HandleFogOfWarUpdated()
    {
        if (!autoRefreshOnFogUpdate)
            return;
        RebuildSnapshot();
    }

    private static string ResolveUnitLabel(UnitManager unit)
    {
        if (unit == null)
            return "-";
        if (!string.IsNullOrWhiteSpace(unit.UnitDisplayName))
            return unit.UnitDisplayName;
        return unit.name;
    }

    private static string BuildContributorsLine(List<UnitManager> contributors)
    {
        if (contributors == null || contributors.Count <= 0)
            return string.Empty;

        StringBuilder builder = new StringBuilder(128);
        for (int i = 0; i < contributors.Count; i++)
        {
            UnitManager unit = contributors[i];
            if (unit == null)
                continue;

            if (builder.Length > 0)
                builder.Append(" | ");
            builder.Append(ResolveUnitLabel(unit));
            int id = unit.InstanceId > 0 ? unit.InstanceId : unit.GetEntityId().GetHashCode();
            builder.Append(" (#").Append(id).Append(')');
        }

        return builder.ToString();
    }
}

