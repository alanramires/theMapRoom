using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveGameData
{
    public int version = 5;
    public string sceneName;
    public long savedAtUtcTicks;
    public int currentTurn;
    public int activeTeamId;
    public bool includeNeutralTeam;
    public bool economyEnabled = true;
    public bool victoryStarsEnabled = true;
    public int victoryStarsToWin = 5;
    public bool hasVictoryWinner;
    public int victoryWinnerTeamId = (int)TeamId.Neutral;
    public List<MatchPlayerSaveData> players = new List<MatchPlayerSaveData>();
    public List<MatchVictoryStarSaveData> victoryStars = new List<MatchVictoryStarSaveData>();
    public List<UnitSaveData> units = new List<UnitSaveData>();
    public List<ConstructionSaveData> constructions = new List<ConstructionSaveData>();
    public PlanningConfigSaveData planningConfig = new PlanningConfigSaveData();
    public List<RallyPointSaveData> rallyPoints = new List<RallyPointSaveData>();
    public List<RallyAssignmentSaveData> rallyAssignments = new List<RallyAssignmentSaveData>();
    public int fogCacheTeamId = int.MinValue;
    public List<FogCellContributorSaveData> fogVisibleContributorsByCell = new List<FogCellContributorSaveData>();
    public List<FogUnitVisibilitySaveData> fogUnitVisibilityByCacheIndex = new List<FogUnitVisibilitySaveData>();
    public AIPlannerMultiTeamSaveData aiPlannerState;
    public List<AIObjectivePlanSaveData> aiObjectivePlans = new List<AIObjectivePlanSaveData>();
    public bool aiRuntimeActive;
    public int aiRuntimeTeamId = (int)TeamId.Neutral;
    public int aiRuntimeTurnNumber;
    public int aiRuntimeStage;
}

[Serializable]
public class PlanningConfigSaveData
{
    public int maxRallyPointsPerTeam = 5;
}

[Serializable]
public class RallyPointSaveData
{
    public int id;
    public string nome;
    public int hexX;
    public int hexY;
    public int teamOwner;
    public bool ativo;
}

[Serializable]
public class RallyAssignmentSaveData
{
    public int rallyPointId;
    public int unitId;
}

[Serializable]
public class MatchStateSaveData
{
    public int currentTurn;
    public int activeTeamId;
    public bool includeNeutralTeam;
    public bool economyEnabled = true;
    public bool victoryStarsEnabled = true;
    public int victoryStarsToWin = 5;
    public bool hasVictoryWinner;
    public int victoryWinnerTeamId = (int)TeamId.Neutral;
    public List<MatchPlayerSaveData> players = new List<MatchPlayerSaveData>();
    public List<MatchVictoryStarSaveData> victoryStars = new List<MatchVictoryStarSaveData>();
}

[Serializable]
public class MatchPlayerSaveData
{
    public int teamId;
    public bool flipX;
    public bool isAI;
    public bool commandServiceAutomatic;
    public int startMoney;
    public int actualMoney;
    public int incomePerTurn;
    public bool startMoneyApplied;
}

[Serializable]
public class MatchVictoryStarSaveData
{
    public int teamId;
    public int stars;
}

[Serializable]
public class AIObjectivePlanSaveData
{
    public int teamId;
    public List<AIObjectiveSaveData> objectives = new List<AIObjectiveSaveData>();
    public List<int> rogueUnitIds = new List<int>();
    public List<int> handoffVacaterIds = new List<int>();
    public List<int> vacaterForwardSectors = new List<int>();
}

[Serializable]
public class AIObjectiveSaveData
{
    public int sector;
    public int assignedTeam;
    public int status;
    public int objectiveType;
    public int priority;
    public int budgetReserved;
    public bool handoffEligible;
    public int preferredHandoffFromUnitId = -1;
    public List<AISlotNeedSaveData> slots = new List<AISlotNeedSaveData>();
}

[Serializable]
public class AISlotNeedSaveData
{
    public int role;
    public bool filled;
    public int assignedUnitId = -1;
}

[Serializable]
public class UnitSaveData
{
    public int instanceId;
    public string unitId;
    public bool isActiveInHierarchy = true;
    public int teamId;
    public int cellX;
    public int cellY;
    public float worldX;
    public float worldY;
    public int currentHP;
    public bool isDead;
    public int deadWhenTurn = -1;
    public string deadByReason;
    public string diedByUnit;
    public bool hasMerged;
    public int mergedWhenTurn = -1;
    public string mergedWithUnit;
    public int currentAmmo;
    public int currentFuel;
    public int remainingMovementPoints;
    public bool hasActed;
    public bool receivedSuppliesThisTurn;
    public bool isUnderRepair;
    public bool isEmbarked;
    public int transporterInstanceId;
    public int transporterSlotIndex;
    public int domain;
    public int heightLevel;
    public bool aiHasAssignedPlan;
    public string aiAssignedPlanKey;
    public string aiAssignedPlanName;
    public string aiAssignedPlanBadge;
    public int aiAssignedPlanRole = 0;
    public bool aiAssignedPlanBadgeVisible;
    public List<int> embarkedWeaponAmmo = new List<int>();
    public List<RuntimeSupplySaveData> embarkedSupplies = new List<RuntimeSupplySaveData>();
}

[Serializable]
public class RuntimeSupplySaveData
{
    public string supplyId;
    public int quantity;
}

[Serializable]
public class ConstructionSiteRuntimeSaveData
{
    public bool isPlayerHeadQuarter;
    public bool isVictoryBuilding;
    public bool isCapturable;
    public int capturePointsMax;
    public int capturedIncoming;
    public int sellingRule;
    public bool canProvideSupplies;
    public List<string> offeredUnitIds = new List<string>();
    public List<string> offeredServiceIds = new List<string>();
    public List<RuntimeSupplySaveData> offeredSupplies = new List<RuntimeSupplySaveData>();
}

[Serializable]
public class ConstructionSaveData
{
    public int instanceId;
    public string constructionId;
    public bool isActiveInHierarchy = true;
    public bool isVisible = true;
    public bool isForwardObserverSpot;
    public bool isRallyPoint;
    public int teamId;
    public int sector;
    public int rallyTargetSlotIndex = -1;
    public List<int> rallyTargetSlotIndexes = new List<int>();
    public bool isAnchorSector;
    public int anchorSectorSlotIndex = -1;
    public int cellX;
    public int cellY;
    public float worldX;
    public float worldY;
    public int currentCapturePoints;
    public int originalOwnerTeamId;
    public bool hasOriginalOwner;
    public int firstOwnerTeamId;
    public bool hasFirstOwner;
    public bool hasInfiniteSuppliesOverride;
    public ConstructionSiteRuntimeSaveData siteRuntime;
}

[Serializable]
public class ReplaySaveData
{
    public List<ReplayTurnRecordSaveData> matchHistory = new List<ReplayTurnRecordSaveData>();
    public bool hasCurrentRecord;
    public ReplayTurnRecordSaveData currentRecord;
    public int selectedTurnIndex = -1;
    public int observerTeamId = (int)TeamId.Neutral;
    public int visionMode = (int)ReplayVisionMode.Omniscient;
    public ActionStack actionStack = new ActionStack();
}

[Serializable]
public class ReplayTurnRecordSaveData
{
    public int turnNumber;
    public int actingTeamId = (int)TeamId.Neutral;
    public TurnStartSnapshot startSnapshot;
    public List<ReplayStepSaveData> steps = new List<ReplayStepSaveData>();
}

[Serializable]
public class ReplayStepSaveData
{
    public int stepIndex;
    public int stepType;
    public string debugLabel;
    public string commandJson;
}


[Serializable]
public class FogCellContributorSaveData
{
    public int x;
    public int y;
    public int z;
    public int contributors;
}

[Serializable]
public class FogUnitVisibilitySaveData
{
    public int cacheIndex;
    public bool isVisible;
}






[Serializable]
public class AIPlannerMultiTeamSaveData
{
    public int dataVersion = 2;
    public List<TeamPlannerSaveBlock> teams = new List<TeamPlannerSaveBlock>();
}

[Serializable]
public class TeamPlannerSaveBlock
{
    public int teamId;
    public bool hasRestoredStatePendingUse;
    public int restoredTurn = -1;
    public List<SavedCatalogPlan> catalogPlans = new List<SavedCatalogPlan>();
    public List<SavedPlan> activePlans = new List<SavedPlan>();
    public List<SavedAssignment> assignments = new List<SavedAssignment>();
    public List<SavedPreviousAssignmentMemory> previousAssignments = new List<SavedPreviousAssignmentMemory>();
}

[Serializable]
public class SavedCatalogPlan
{
    public string planKey;
    public string displayName;
    public string sector;
    public bool isFixedPlan;
    public string fixedPlanKind;
    public int status;
    public string statusName;
    public bool conquered;
    public bool sectorClear;
    public string controllingTeam;
    public int progressCurrent;
    public int progressMax;
    public int lastActivationTurn = -1;
    public int lastCompletionTurn = -1;
    public int tacticalRiskScore;
    public string selectionReason;
}

[Serializable]
public class SavedPlan
{
    public string planKey;
    public string displayName;
    public string sector;
    public string badgeSymbol;
    public int tacticalRiskScore;
    public string selectionReason;
    public bool hasCaptureTarget;
    public int captureX;
    public int captureY;
    public string captureLabel;
}

[Serializable]
public class SavedAssignment
{
    public int unitInstanceId;
    public string planKey;
    public int role;
    public string roleName;
}

[Serializable]
public class SavedPreviousAssignmentMemory
{
    public int unitInstanceId;
    public string planKey;
    public int role;
    public string roleName;
    public int lastProgressTurn;
    public int lastDistanceToTarget;
}
