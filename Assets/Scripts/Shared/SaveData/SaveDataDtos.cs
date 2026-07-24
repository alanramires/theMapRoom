using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveGameData
{
    public int version = 13;
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
    public int victoryWinnerSlotIndex = -1;
    public List<MatchPlayerSaveData> players = new List<MatchPlayerSaveData>();
    public List<TeamCapturedBuildingSaveData> capturedBuildingHistory = new List<TeamCapturedBuildingSaveData>();
    public List<MatchVictoryStarSaveData> victoryStars = new List<MatchVictoryStarSaveData>();
    public List<UnitSaveData> units = new List<UnitSaveData>();
    public List<ConstructionSaveData> constructions = new List<ConstructionSaveData>();
    public PlanningConfigSaveData planningConfig = new PlanningConfigSaveData();
    public List<RallyPointSaveData> rallyPoints = new List<RallyPointSaveData>();
    public List<RallyAssignmentSaveData> rallyAssignments = new List<RallyAssignmentSaveData>();
    public int fogCacheTeamId = int.MinValue;
    public List<FogCellContributorSaveData> fogVisibleContributorsByCell = new List<FogCellContributorSaveData>();
    public List<FogUnitVisibilitySaveData> fogUnitVisibilityByCacheIndex = new List<FogUnitVisibilitySaveData>();
    public List<TeamExploredCellsSaveData> fogExploredCellsByTeam = new List<TeamExploredCellsSaveData>();
    public List<FogConstructionMemorySaveData> fogConstructionMemory = new List<FogConstructionMemorySaveData>();
    public AIPlannerMultiTeamSaveData aiPlannerState;
    public List<AIObjectivePlanSaveData> aiObjectivePlans = new List<AIObjectivePlanSaveData>();
    public bool aiRuntimeActive;
    public int aiRuntimeSlotIndex = -1;
    public int aiRuntimeTeamId = (int)TeamId.Neutral;
    public int aiRuntimeTurnNumber;
    public int aiRuntimeStage;
    public List<AIIntelLedgerSaveData> aiIntelLedgers = new List<AIIntelLedgerSaveData>();
    // Dificuldade da IA (flags do AIController). aiDifficultySaved distingue save novo de
    // save antigo sem estes campos (JsonUtility zera bools ausentes) — save antigo mantem
    // os defaults serializados na cena, comportamento de antes.
    public bool aiDifficultySaved;
    public bool aiEasyMode;
    public bool aiHardMode;
    public bool aiConscriptionWhenLosing;
    public bool aiConscriptionDoctrine;
    // Histerese da Fase de Massacre (valvula da conscricao). Default false em save
    // antigo e inofensivo: a fase reentra na proxima avaliacao de shopping se o
    // ratio ainda estiver acima do limiar de entrada.
    public bool aiMassacrePhase;
    // Jornal do Comandante: eventos acumulados entre os turnos de cada time
    // (contato perdido, tiro da nevoa, conquista perdida...), drenados no
    // inicio do turno do time destinatario. Ordem cronologica (deterministica).
    public List<TurnBriefingEventSaveData> turnBriefingEvents = new List<TurnBriefingEventSaveData>();
    // Ultimo Jornal ja apresentado por equipe. Diferente do ledger pendente:
    // serve apenas para reabrir o mesmo resumo depois de salvar/carregar.
    public List<TurnBriefingReportLineSaveData> turnBriefingReportLines = new List<TurnBriefingReportLineSaveData>();
}

[Serializable]
public class TurnBriefingEventSaveData
{
    public int slotIndex = -1; // identidade do destinatario
    public int teamId;       // destinatario do evento
    public int category;     // TurnBriefingCategory
    public string subjectName;
    public string detail;    // texto extra JA resolvido fog-honesto no registro
    public int cellX;
    public int cellY;
    public int turnNumber;
}

[Serializable]
public class TurnBriefingReportLineSaveData
{
    public int teamId;
    public string unitName;
    public int autonomyConsumed;
    public int fuelBefore;
    public int fuelAfter;
    public int fuelMax;
    public int cellX;
    public int cellY;
    public float colorR = 1f;
    public float colorG = 1f;
    public float colorB = 1f;
    public float colorA = 1f;
    public string customText;
    public int severityTier = 2;
    public int stableOrder;
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
    public int victoryWinnerSlotIndex = -1;
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
public class TeamCapturedBuildingSaveData
{
    public int slotIndex = -1;
    public int teamId;
    public List<string> buildingKeys = new List<string>();
}

[Serializable]
public class MatchVictoryStarSaveData
{
    public int slotIndex = -1;
    public int teamId;
    public int stars;
}

[Serializable]
public class AIObjectivePlanSaveData
{
    public int slotIndex = -1;
    public int teamId;
    public List<AIObjectiveSaveData> objectives = new List<AIObjectiveSaveData>();
    public List<int> rogueUnitIds = new List<int>();
    public List<int> handoffVacaterIds = new List<int>();
    public List<int> vacaterForwardSectors = new List<int>();
    // Estado da operação GoGreen/Invasão. Após o GoGreen o objetivo RallyAssembly é removido do
    // plano (a massa marcha para a base inimiga), então a invasão "em andamento" não vive em
    // nenhum SectorObjective — precisa ser persistida à parte para sobreviver ao save/load.
    public List<AIGoGreenTurnSaveData> goGreenTurns = new List<AIGoGreenTurnSaveData>();
    // Monitor de desfecho da invasão (re-montagem por fracasso). -1 = sem medição.
    public int invasionBestDistance = -1;
    public int invasionStallCounter = 0;
}

[Serializable]
public class AIGoGreenTurnSaveData
{
    public int sector;
    public int turn;
}

[Serializable]
public class AIObjectiveSaveData
{
    public int sector;
    public int assignedTeam;
    public int status;
    public int objectiveType;
    public int priority;
    public int rallyState;
    public int rallyAssemblyStartedTurn = -1;
    public int rallyGoGreenTurn = -1;
    public string rallyReadinessReason;
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
    public bool goGreenFallbackAssignment;
}

[Serializable]
public class UnitSaveData
{
    public int instanceId;
    public string unitId;
    public bool isActiveInHierarchy = true;
    public int teamId;
    public int slotIndex = -1;
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
    public bool tookOffRecently;
    public bool isUnderRepair;
    public bool isEmbarked;
    public int transporterInstanceId;
    public int transporterSlotIndex;
    public int domain;
    public int heightLevel;
    public bool isAircraftGrounded;
    public int aircraftOperationLockTurns;
    // Lock de camada forcada completo (emersao por ataque/dano). Substitui o
    // legado aircraftOperationLockTurns, mantido para saves antigos.
    public bool hasForcedLayerLock;
    public int forcedLayerLockDomain;
    public int forcedLayerLockHeight;
    public int forcedLayerLockTurns;
    public bool hasLayerLockCountdownState;
    public bool layerLockCountdownStarted;
    public bool aiHasAssignedPlan;
    public string aiAssignedPlanKey;
    public string aiAssignedPlanName;
    public string aiAssignedPlanBadge;
    public int aiAssignedPlanRole = 0;
    public bool aiAssignedPlanBadgeVisible;
    public int aiEixo = 0;
    public List<int> embarkedWeaponAmmo = new List<int>();
    public List<RuntimeSupplySaveData> embarkedSupplies = new List<RuntimeSupplySaveData>();
}

[Serializable]
public class RuntimeSupplySaveData
{
    public string supplyId;
    public int quantity;
    // Marca d'agua do estoque (max dinamico das construcoes). Unidades ignoram.
    public int peakQuantity;
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
    public int forwardObserverSpotUsage;
    public bool isRallyPoint;
    public int teamId;
    public int slotIndex = -1;
    public int sector;
    public int rallyOwnerSlotIndex = -1;
    public bool isAnchorSector;
    public int anchorSectorSlotIndex = -1;
    public int cellX;
    public int cellY;
    public float worldX;
    public float worldY;
    public int currentCapturePoints;
    public int originalOwnerSlotIndex = -1;
    public bool hasOriginalOwner;
    public int firstOwnerSlotIndex = -1;
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
public class TeamExploredCellsSaveData
{
    public int slotIndex = -1;
    public int teamId;
    public List<Vector3Int> cells = new List<Vector3Int>();
}

[Serializable]
public class FogConstructionMemorySaveData
{
    public int observerSlotIndex = -1;
    public int observerTeamId;
    public int x;
    public int y;
    public string constructionDataId;
    public int knownOwnerTeamId;
    public bool flipX;
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
