using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public enum AIPlanRuntimeIntent
{
    None = 0,
    Capture = 1,
    Pressure = 2,
    FireSupport = 3,
    AntiAir = 4,
    AirSurveillance = 5,
    Repair = 6,
    Supply = 7,
    Restock = 8,

    /// <summary>
    /// Promessa de resgate: este TRANSPORTADOR comprometeu uma viagem para
    /// buscar o passageiro em <c>AIDesignatedMissionTargetUnitInstanceId</c>.
    ///
    /// Valor novo entra sempre NO FIM: o save grava o enum como int, e
    /// renumerar transforma missao antiga em missao trocada.
    /// </summary>
    Transport = 9
}

[ExecuteAlways]
public class UnitManager : MonoBehaviour
{
    public static readonly List<UnitManager> AllActive = new List<UnitManager>();
    private static int activeTeamChangedHandlerCount;
    private static double activeTeamChangedHandlerTotalMs;

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private UnitHudController unitHud;
    [SerializeField] private SpriteRenderer actedLockRenderer;
    [SerializeField] private UnitDatabase unitDatabase;
    [SerializeField] private MatchController matchController;
    [SerializeField] private Tilemap boardTilemap;
    [SerializeField] private bool snapToCellCenter = true;
    [SerializeField] private bool autoSnapWhenMovedInEditor = true;
    [SerializeField] private Vector3Int currentCellPosition = Vector3Int.zero;
    [Header("Runtime Flags")]
    [SerializeField] private bool hasActed;
    [SerializeField, HideInInspector] private bool hasFiredThisTurn;
    [SerializeField] private bool receivedSuppliesThisTurn;
    [SerializeField] private bool tookOffRecently;
    [SerializeField] private bool surfacedForSupplyThisTurn;
    [SerializeField] private bool aircraftForcedLandingAwaitingRefuel;
    [SerializeField] private bool isUnderRepair;
    [SerializeField] private bool hasMerged;
    [SerializeField, HideInInspector] private bool aiForcedToRepair;
    [SerializeField] private int mergedWhenTurn = -1;
    [SerializeField] private string mergedWithUnit = string.Empty;
    [SerializeField] private TeamId teamId = TeamId.Green;
    [Tooltip("Slot do MatchController que controla este time. -1 = sem slot (TeamId fixo).")]
    [SerializeField] private int slotIndex = -1;
    [SerializeField] private string unitId;
    [SerializeField] private int instanceId;
    [SerializeField] private Vector3 currentPosition = Vector3.zero;
    [SerializeField] private string unitDisplayName;
    [SerializeField] private int currentHP;
    [SerializeField] private bool isDead;
    [SerializeField] private string diedByUnit = string.Empty;
    [SerializeField] private int deadWhenTurn = -1;
    [SerializeField] private string deadByReason = string.Empty;
    [SerializeField] private bool updateIsDeadInUpdate = true;
    [Header("Runtime Flags View")]
    [SerializeField] private bool flagHasActed;
    [SerializeField] private bool flagIsDead;
    [SerializeField] private int flagDeadWhenTurn = -1;
    [SerializeField] private string flagDeadByReason = string.Empty;
    [SerializeField] private string flagDiedByUnit = string.Empty;
    [SerializeField] private bool flagIsEmbarked;
    [SerializeField] private string flagEmbarkedAtUnit = string.Empty;
    [SerializeField] private bool flagReceivedSupplies;
    [SerializeField] private bool flagTookOffRecently;
    [SerializeField] private bool flagSurfacedForSupplyThisTurn;
    [SerializeField] private bool flagAircraftForcedLandingAwaitingRefuel;
    [SerializeField] private bool flagHasMerged;
    [SerializeField] private int flagMergedWhenTurn = -1;
    [SerializeField] private string flagMergedWithUnit = string.Empty;
    [SerializeField] private int currentAmmo = 3;
    [SerializeField] private int maxAmmo = 3;
    [SerializeField] private int currentFuel = 99;
    [SerializeField] private int maxFuel = 99;
    [SerializeField, Min(0)] private int remainingMovementPoints;
    [SerializeField, HideInInspector] private bool usedRoadBoostOnLastMove;
    [SerializeField, Min(1)] private int visao = 3;
    [Header("Embarked Weapons Runtime")]
    [SerializeField] private List<UnitEmbarkedWeapon> embarkedWeaponsRuntime = new List<UnitEmbarkedWeapon>();
    [Header("Supplier Runtime")]
    [SerializeField] private List<UnitEmbarkedSupply> embarkedResourcesRuntime = new List<UnitEmbarkedSupply>();
    [SerializeField] private List<ServiceData> embarkedServicesRuntime = new List<ServiceData>();
    [Header("Supplier Stock Alerts")]
    [SerializeField] private Image supplyTop;
    [SerializeField] private Image supplyMiddle;
    [SerializeField] private Image supplyBottom;
    [SerializeField] [Range(0.01f, 0.99f)] private float supplierStockAlertThreshold = 0.5f;
    [SerializeField, HideInInspector] private bool appliedHasActed;
    [SerializeField, HideInInspector] private int appliedActiveTeamId = int.MinValue;
    [SerializeField] private bool isEmbarked;
    [SerializeField] private string embarkedAtUnit = string.Empty;
    [SerializeField] private int embarkedVisualPreviewDepth;
    [SerializeField] private bool isSelected;
    [SerializeField, HideInInspector] private bool isPreviewDimmed;
    [SerializeField, HideInInspector] private bool hasTemporarySortingOverride;
    [System.NonSerialized] private bool temporaryFogTraversalVisual;
    [System.NonSerialized] private bool temporaryFogDetectionPresentation;
    [System.NonSerialized] private bool temporaryFogDetectionWasHidden;
    [System.NonSerialized] private int temporaryFogSpriteLayerId;
    [System.NonSerialized] private int temporaryFogSpriteOrder;
    [System.NonSerialized] private bool temporaryFogWasHidden;
    [System.NonSerialized] private Canvas[] temporaryFogHudCanvases;
    [System.NonSerialized] private int[] temporaryFogHudLayerIds;
    [System.NonSerialized] private int[] temporaryFogHudOrders;
    [System.NonSerialized] private SpriteRenderer fogDetectedContactRenderer;
    [SerializeField, HideInInspector] private bool hiddenByFogOfWar;
    [SerializeField, HideInInspector] private int cachedSpriteSortingOrder;
    [SerializeField, HideInInspector] private int cachedActedLockSortingOrder;
    [SerializeField] private bool enableSelectionBlink = true;
    [SerializeField] [Range(0.05f, 1f)] private float selectionBlinkInterval = 0.16f;
    [SerializeField] [Range(0.05f, 1f)] private float selectionBlinkActiveDuration = 0.16f;
    [SerializeField] [Range(0.05f, 1f)] private float selectionBlinkInactiveDuration = 0.16f;
    [SerializeField] [Range(0f, 1f)] private float actedDarkenFactor = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float actedGrayBlend = 0.6f;
    [SerializeField] [Range(0f, 1f)] private float previewDimDarkenFactor = 0.55f;
    [SerializeField] [Range(0f, 1f)] private float previewDimGrayBlend = 0.75f;
    [SerializeField] private Color actedGlowColor = Color.white;
    [SerializeField] [Range(0.1f, 6f)] private float actedGlowSize = 1.5f;
    [SerializeField] [Range(0f, 4f)] private float actedGlowStrength = 1.25f;
    [Header("Layer State")]
    [SerializeField] private Domain currentDomain = Domain.Land;
    [SerializeField] private HeightLevel currentHeightLevel = HeightLevel.Surface;
    [SerializeField] private int currentLayerModeIndex = 0;
    [SerializeField] private bool layerStateInitialized;
    [SerializeField] private bool useExplicitPreferredAirHeightRuntime;
    [SerializeField] private HeightLevel preferredAirHeightRuntime = HeightLevel.AirLow;
    [SerializeField] private bool useExplicitPreferredNavalHeightRuntime;
    [SerializeField] private HeightLevel preferredNavalHeightRuntime = HeightLevel.Submerged;
    [Header("Layer Lock")]
    [SerializeField] private bool hasForcedLayerLock;
    [SerializeField] private Domain forcedLayerLockDomain = Domain.Land;
    [SerializeField] private HeightLevel forcedLayerLockHeight = HeightLevel.Surface;
    [SerializeField, Min(0)] private int forcedLayerLockTurnsRemaining;
    [SerializeField] private bool layerLockCountdownStarted;
    [Header("Transport Runtime")]
    [SerializeField] private List<UnitTransportSeatRuntime> transportedUnitSlots = new List<UnitTransportSeatRuntime>();
    [SerializeField, HideInInspector] private UnitManager embarkedTransporter;
    [SerializeField, HideInInspector] private int embarkedTransporterSlotIndex = -1;
    [Header("Stealth Runtime")]
    [SerializeField, HideInInspector] private List<int> currentlyObservedByTeamIds = new List<int>();
    [Header("AI Plan Runtime")]
    [Tooltip("Plano/objetivo atribuido pela AI. Este e o estado visual/cache da unidade; o plano canonico fica no ObjectiveManager.")]
    [SerializeField] private bool aiHasAssignedPlan;
    [Tooltip("Chave persistida do objetivo, normalmente o nome do setor.")]
    [SerializeField] private string aiAssignedPlanKey = string.Empty;
    [Tooltip("Nome exibido do objetivo, normalmente Alpha/Bravo/etc.")]
    [SerializeField] private string aiAssignedPlanName = string.Empty;
    [Tooltip("Badge curto mostrado no HUD da unidade, por exemplo A/B/C/D.")]
    [SerializeField] private string aiAssignedPlanBadge = string.Empty;
    [Tooltip("Role da unidade dentro do plano. Usa os valores de UnitRole.")]
    [SerializeField] private int aiAssignedPlanRole = 0;
    [SerializeField] private bool aiAssignedPlanBadgeVisible;
    // O objetivo de captura NAO tem campo proprio: ele e a missao com intent
    // Capture. Enquanto foram dois armazenamentos, a mesma peca podia dizer
    // "estou capturando (0,0)" num campo e "Mission Intent: None" no outro, na
    // mesma tela — duas verdades sobre o que a unidade esta fazendo.
    [Tooltip("Missao individual persistente: captura, transporte, agenda retomada apos desembarque.")]
    [SerializeField] private AIPlanRuntimeIntent aiDesignatedMissionIntent = AIPlanRuntimeIntent.None;
    [SerializeField] private int aiDesignatedMissionTargetUnitInstanceId = -1;
    [SerializeField] private int aiDesignatedMissionTargetConstructionInstanceId = -1;
    [SerializeField] private Vector3Int aiDesignatedMissionTargetCell = Vector3Int.zero;
    [Tooltip("Turno em que a unidade comecou a esperar por uma carona que nao veio. 0 = nao esta esperando. E CARIMBO, nao contador: a espera e derivada (turno atual - carimbo), entao ler mil vezes no mesmo turno da o mesmo numero. Zero como sentinela mantem save antigo correto, porque turno comeca em 1.")]
    [SerializeField] private int aiRideWaitSinceTurn;
    [Tooltip("Quero carona? Fato publicado pela propria unidade, uma vez por turno, contra a propria missao. Nao e resposta de pergunta de transportador — o QueroCarona e par a par e cada transportador responde diferente.")]
    [SerializeField] private bool aiWantsRide;
    [Tooltip("Ha quantos turnos ela espera. Espelho legivel do carimbo acima, materializado na publicacao: o carimbo sozinho mostra o TURNO de entrada, que se le errado como contagem.")]
    [SerializeField] private int aiRideWaitTurns;
    [Header("AI Eixo Runtime")]
    [Tooltip("Eixo ao qual a unidade pertence: 1, 2 ou 3 = eixos regulares; 4 = invasão final. 0 = nenhum (rogue / fora de eixo).")]
    [Range(0, 4)]
    [SerializeField] private int aiEixo = 0;
    [Header("AI Stance Runtime")]
    [SerializeField] private bool aiHasStance;
    [SerializeField] private int aiStance = 0;
    [SerializeField] private bool aiStanceVisible;
    private Sprite aiStanceIcon;
    [SerializeField] private bool aiMaintenanceActive;

    private Vector3 _cohabitationOffset = Vector3.zero;
    private Vector3 _cohabitationPreScale;
    private bool _hasCohabitationVisual;

    public TeamId TeamId => teamId;
    public int SlotIndex => slotIndex;
    public void SetSlotIndex(int index)
    {
        int previousSlot = slotIndex;
        slotIndex = index;
        ThreatRevisionTracker.NotifyUnitSlotChanged(previousSlot, slotIndex);
        ResolveTeamIdFromSlot();
        UpdateDynamicName();
        if (Application.isPlaying && previousSlot != slotIndex)
        {
            Vector3Int cell = currentCellPosition;
            cell.z = 0;
            UnitOccupancyRules.NotifyUnitOccupancyChanged(
                this,
                cell,
                cell);
        }
    }
    public Tilemap BoardTilemap => boardTilemap;
    public Vector3Int CurrentCellPosition => currentCellPosition;
    public string UnitId => unitId;
    public int InstanceId => instanceId;
    public Vector3 CurrentPosition => currentPosition;
    public string UnitDisplayName => unitDisplayName;
    // Renomeia a unidade (ex.: "Ryan" no tutorial). Chamar depois do Apply(data),
    // que sobrescreve unitDisplayName com o displayName do UnitData.
    public void SetUnitDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return;
        unitDisplayName = displayName.Trim();
        UpdateDynamicName();
    }
    public int CurrentHP => currentHP;
    public bool IsDead => isDead;
    public int DeadWhenTurn => deadWhenTurn;
    public string DeadByReason => deadByReason;
    public string DiedByUnit => diedByUnit;
    public bool HasMerged => hasMerged;
    public bool AIForcedToRepair => aiForcedToRepair;
    public int MergedWhenTurn => mergedWhenTurn;
    public string MergedWithUnit => mergedWithUnit;
    public string EmbarkedAtUnit => embarkedAtUnit;
    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmo;
    public int CurrentFuel => currentFuel;
    public int MaxFuel => maxFuel;
    public int MaxMovementPoints => Mathf.Max(0, GetMovementRange());
    public int RemainingMovementPoints => Mathf.Clamp(remainingMovementPoints, 0, MaxMovementPoints);
    public int Visao => Mathf.Max(1, visao);
    public bool HasActed => hasActed;
    public bool HasFiredThisTurn => hasFiredThisTurn;
    public bool ReceivedSuppliesThisTurn => receivedSuppliesThisTurn;
    public bool TookOffRecently => tookOffRecently;
    public bool SurfacedForSupplyThisTurn => surfacedForSupplyThisTurn;
    public bool AircraftForcedLandingAwaitingRefuel =>
        GetAircraftType() != AircraftType.None &&
        aircraftForcedLandingAwaitingRefuel;
    public bool IsUnderRepair => isUnderRepair;
    public bool IsEmbarked => isEmbarked;
    public bool IsEmbarkedVisualPreviewActive => embarkedVisualPreviewDepth > 0;
    public bool IsSelected => isSelected;
    public bool IsHiddenByFogOfWar => hiddenByFogOfWar;
    public bool IsTemporaryFogDetectionPresentationActive => temporaryFogDetectionPresentation;
    public UnitDatabase UnitDatabase => unitDatabase;
    public bool IsAircraftGrounded => GetAircraftType() != AircraftType.None && currentDomain != Domain.Air;
    public bool IsAircraftEmbarkedInCarrier => isEmbarked;
    [System.Obsolete("Use LayerLockTurnsRemaining. This alias is kept only for legacy save compatibility.")]
    public int AircraftOperationLockTurns => LayerLockTurnsRemaining;
    public bool HasForcedLayerLock => hasForcedLayerLock && forcedLayerLockTurnsRemaining > 0;
    // Lock forcado ainda nao aplicado: a camada atual difere da travada porque o
    // hex nao permitia a transicao no momento do efeito (ex.: navio na superficie
    // sobre o submarino forcado a emergir). Enquanto pendente a unidade fica
    // revelada (anula stealth no FoW) e o tempo do lock nao corre; o upkeep do
    // dono ou o fim do proximo movimento aplica a camada quando o hex permitir.
    public bool HasPendingForcedLayerLock =>
        HasForcedLayerLock &&
        (currentDomain != forcedLayerLockDomain || currentHeightLevel != forcedLayerLockHeight);
    public Domain ForcedLayerLockDomain => forcedLayerLockDomain;
    public HeightLevel ForcedLayerLockHeight => forcedLayerLockHeight;
    public int ForcedLayerLockTurnsRemaining => Mathf.Max(0, forcedLayerLockTurnsRemaining);
    public int LayerLockTurnsRemaining => Mathf.Max(0, forcedLayerLockTurnsRemaining);
    public bool LayerLockCountdownStarted => layerLockCountdownStarted;
    public IReadOnlyList<UnitTransportSeatRuntime> TransportedUnitSlots => transportedUnitSlots;
    public UnitManager EmbarkedTransporter => embarkedTransporter;
    public int EmbarkedTransporterSlotIndex => embarkedTransporterSlotIndex;
    public IReadOnlyList<int> CurrentlyObservedByTeamIds => currentlyObservedByTeamIds;
    public bool UsedRoadBoostOnLastMove => usedRoadBoostOnLastMove;
    public bool AIHasAssignedPlan => aiHasAssignedPlan;
    public string AIAssignedPlanKey => aiAssignedPlanKey ?? string.Empty;
    public string AIAssignedPlanName => aiAssignedPlanName ?? string.Empty;
    public string AIAssignedPlanBadge => aiAssignedPlanBadge ?? string.Empty;
    public int AIAssignedPlanRole => aiAssignedPlanRole;
    public bool AIAssignedPlanBadgeVisible => aiAssignedPlanBadgeVisible;
    // Derivados da missao. Nao ha o que sincronizar porque nao ha dois donos.
    public bool AIHasDesignatedCaptureTarget =>
        aiDesignatedMissionIntent == AIPlanRuntimeIntent.Capture;
    public int AIDesignatedCaptureTargetInstanceId =>
        AIHasDesignatedCaptureTarget
            ? aiDesignatedMissionTargetConstructionInstanceId
            : -1;
    public Vector3Int AIDesignatedCaptureTargetCell =>
        AIHasDesignatedCaptureTarget
            ? aiDesignatedMissionTargetCell
            : Vector3Int.zero;
    // DERIVADO do verbo, nao armazenado. O enum ja tem o valor None, entao um
    // bool separado seria uma segunda verdade para o mesmo fato — e duas verdades
    // acabam divergindo. Escolher um intent JA e designar a missao.
    public bool AIHasDesignatedMission =>
        aiDesignatedMissionIntent != AIPlanRuntimeIntent.None;
    public AIPlanRuntimeIntent AIDesignatedMissionIntent => aiDesignatedMissionIntent;
    public int AIDesignatedMissionTargetUnitInstanceId => aiDesignatedMissionTargetUnitInstanceId;
    public int AIDesignatedMissionTargetConstructionInstanceId => aiDesignatedMissionTargetConstructionInstanceId;
    public Vector3Int AIDesignatedMissionTargetCell => aiDesignatedMissionTargetCell;

    /// <summary>
    /// Turno em que esta unidade comecou a esperar por carona. 0 = nao espera.
    /// </summary>
    public int AIRideWaitSinceTurn => aiRideWaitSinceTurn;

    /// <summary>
    /// Esta unidade quer carona? FATO PUBLICADO pela propria unidade, uma vez
    /// por turno, contra a PROPRIA missao — nunca resposta de pergunta alheia.
    ///
    /// Antes disto o valor era efeito colateral do planejamento dos outros: o
    /// QueroCarona e uma pergunta par a par (cada transportador com a sua
    /// banda, o seu horizonte, o seu tier) e quem perguntasse por ultimo
    /// gravava a ficha. Dois transportadores davam respostas diferentes sobre a
    /// mesma unidade no mesmo turno, e o campo dancava no Inspector.
    ///
    /// Nao era cosmetico: o degrau de aninhamento gateia aqui, entao a
    /// capacidade de um APC subir no navio dependia de qual transportador o
    /// tinha avaliado antes na ordem de iniciativa.
    /// </summary>
    public bool AIWantsRide => aiWantsRide;

    /// <summary>Esta unidade esta na fila da carona?</summary>
    public bool AIIsWaitingForRide => aiWantsRide;

    /// <summary>
    /// Ha quantos turnos ela espera, MATERIALIZADO na publicacao para ser
    /// legivel no Inspector. O carimbo (aiRideWaitSinceTurn) continua sendo a
    /// verdade da antiguidade; este campo e o espelho dele.
    ///
    /// Existe porque o carimbo sozinho engana quem le a ficha: esperando desde
    /// o turno 1, no turno 4 ele mostra "1", que se le como "esperei 1 turno"
    /// quando sao 3.
    /// </summary>
    public int AIRideWaitTurns => aiRideWaitTurns;

    /// <summary>
    /// Publica a resposta do turno. Unico escritor do par (quero, ha quanto
    /// tempo); a antiguidade continua idempotente, entao republicar "sim" no
    /// turno seguinte NAO reinicia a espera.
    /// </summary>
    public void PublishAIRideNeed(bool wantsRide, int currentTurn)
    {
        aiWantsRide = wantsRide;
        if (!wantsRide)
        {
            aiRideWaitSinceTurn = 0;
            aiRideWaitTurns = 0;
            return;
        }

        MarkAIRideWaitStart(currentTurn);
        aiRideWaitTurns = ResolveAIRideWaitTurns(currentTurn);
    }

    /// <summary>
    /// Ha quantos turnos ela espera. Zero quando nao esta na fila.
    ///
    /// E derivado do carimbo, nunca incrementado: o pedido de carona e
    /// reavaliado muitas vezes por turno (dezenas, num planejamento de
    /// transporte), e um contador incremental viraria contagem dupla na
    /// primeira reentrada.
    /// </summary>
    public int ResolveAIRideWaitTurns(int currentTurn)
    {
        if (aiRideWaitSinceTurn <= 0)
            return 0;
        return Mathf.Max(0, currentTurn - aiRideWaitSinceTurn);
    }

    /// <summary>
    /// Entra na fila da carona. Idempotente: chamar de novo NAO reinicia a
    /// espera — quem ja esperava continua com a antiguidade dele, que e o
    /// ponto todo da fila.
    /// </summary>
    public void MarkAIRideWaitStart(int currentTurn)
    {
        if (currentTurn <= 0 || aiRideWaitSinceTurn > 0)
            return;
        aiRideWaitSinceTurn = currentTurn;
    }

    /// <summary>
    /// Sai da fila: embarcou, chegou ao objetivo, ou parou de querer carona.
    /// </summary>
    public void ClearAIRideWait()
    {
        aiRideWaitSinceTurn = 0;
        aiWantsRide = false;
        aiRideWaitTurns = 0;
    }

    /// <summary>
    /// Restauracao de save. Nao passa pela regra de idempotencia.
    ///
    /// Só o carimbo é serializado: "quero carona" deriva dele (esperando =
    /// queria), e a contagem é republicada na primeira Fase 2 depois do load.
    /// Nada de campo novo no DTO para um valor que o carimbo já contém.
    /// </summary>
    public void RestoreAIRideWaitSinceTurn(int sinceTurn)
    {
        aiRideWaitSinceTurn = Mathf.Max(0, sinceTurn);
        aiWantsRide = aiRideWaitSinceTurn > 0;
        aiRideWaitTurns = 0;
    }

    public int AIEixo => aiEixo;
    public void SetAIEixo(int eixo)
    {
        aiEixo = Mathf.Clamp(eixo, 0, 4);
        RefreshAIAssignedPlanBadge();
    }
    public UnitCombatClassification CombatClassification
        => TryGetUnitData(out UnitData data) && data != null
            ? data.CombatClassification
            : UnitCombatClassification.Civil;

    public void RefreshAIAssignedPlanDebugBadge()
    {
        RefreshAIAssignedPlanBadge();
    }

    public void SetAIForcedToRepair(bool value) => aiForcedToRepair = value;
    public void SetIsUnderRepair(bool value, bool recordTransition = true)
    {
        if (isUnderRepair == value)
            return;
        bool before = isUnderRepair;
        isUnderRepair = value;
        if (recordTransition)
            JogadasManager.RegistrarEstadoReparo(this, before, value);
    }
    public void SetAIAssignedPlan(string planKey, string planName, string badge, int role, bool badgeVisible)
    {
        aiHasAssignedPlan = !string.IsNullOrWhiteSpace(planKey) || !string.IsNullOrWhiteSpace(planName);
        aiAssignedPlanKey = planKey ?? string.Empty;
        aiAssignedPlanName = planName ?? string.Empty;
        aiAssignedPlanBadge = badge ?? string.Empty;
        aiAssignedPlanRole = role;
        aiAssignedPlanBadgeVisible = badgeVisible;
        RefreshAIAssignedPlanBadge();
    }

    public void ClearAIAssignedPlan()
    {
        aiHasAssignedPlan = false;
        aiAssignedPlanKey = string.Empty;
        aiAssignedPlanName = string.Empty;
        aiAssignedPlanBadge = string.Empty;
        aiAssignedPlanRole = 0;
        aiAssignedPlanBadgeVisible = false;
        // NAO zera aiEixo: ele persiste como memoria de eixo entre o handoff e a proxima
        // atribuicao, para a unidade ser tentada a voltar ao MESMO eixo (estabilidade).
        // O HUD ja esconde o badge pelo gate (aiAssignedPlanBadgeVisible=false).
        RefreshAIAssignedPlanBadge();
    }

    /// <summary>
    /// Grava o alvo de captura COMO missao. Nao pisa em missao de outro dono —
    /// mesma guarda que CommitRidePromise usa: sobrescrever a agenda alheia
    /// para anotar uma captura seria trocar um problema por outro.
    ///
    /// Enquanto eram dois campos, a colisao nao existia porque os dois nunca
    /// precisavam concordar. Unificar obriga a decidir, e a decisao e: quem ja
    /// tem missao de outro verbo mantem a dela.
    /// </summary>
    public bool SetAIDesignatedCaptureTarget(
        int constructionInstanceId,
        Vector3Int cell)
    {
        if (AIHasDesignatedMission
            && aiDesignatedMissionIntent != AIPlanRuntimeIntent.Capture)
        {
            return false;
        }

        if (constructionInstanceId < 0)
        {
            ClearAIDesignatedCaptureTarget();
            return true;
        }

        SetAIDesignatedMission(
            AIPlanRuntimeIntent.Capture,
            cell,
            targetConstructionInstanceId: constructionInstanceId);
        return true;
    }

    public void ClearAIDesignatedCaptureTarget()
    {
        // So limpa o que e captura: baixa de captura nao pode apagar Transport
        // nem Restock de quem estiver com outra agenda.
        if (!AIHasDesignatedCaptureTarget)
            return;

        ClearAIDesignatedMission();
    }

    public void SetAIDesignatedMission(
        AIPlanRuntimeIntent intent,
        Vector3Int targetCell,
        int targetUnitInstanceId = -1,
        int targetConstructionInstanceId = -1)
    {
        targetCell.z = 0;
        aiDesignatedMissionIntent = intent;
        aiDesignatedMissionTargetUnitInstanceId = targetUnitInstanceId;
        aiDesignatedMissionTargetConstructionInstanceId = targetConstructionInstanceId;
        aiDesignatedMissionTargetCell = targetCell;
    }

    public void ClearAIDesignatedMission()
    {
        aiDesignatedMissionIntent = AIPlanRuntimeIntent.None;
        aiDesignatedMissionTargetUnitInstanceId = -1;
        aiDesignatedMissionTargetConstructionInstanceId = -1;
        aiDesignatedMissionTargetCell = Vector3Int.zero;
    }

    public void SetAIStance(int stance, Sprite icon = null, bool visible = false)
    {
        aiHasStance = true;
        aiStance = stance;
        aiStanceVisible = visible;
        aiStanceIcon = icon;
        RefreshAIAssignedPlanBadge();
    }

    public void ClearAIStance()
    {
        aiHasStance = false;
        aiStance = 0;
        aiStanceVisible = false;
        aiStanceIcon = null;
        RefreshAIAssignedPlanBadge();
    }

    public void SetAIMaintenanceActive(bool active)
    {
        aiMaintenanceActive = active;
        RefreshAIAssignedPlanBadge();
    }

    public static void ResetActiveTeamChangedPerfCounters()
    {
        activeTeamChangedHandlerCount = 0;
        activeTeamChangedHandlerTotalMs = 0d;
    }

    public static void GetActiveTeamChangedPerfCounters(out int count, out double totalMs)
    {
        count = activeTeamChangedHandlerCount;
        totalMs = activeTeamChangedHandlerTotalMs;
    }

    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private static readonly int GlowSizeId = Shader.PropertyToID("_GlowSize");
    private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");

    private Material defaultSpriteMaterial;
    private MaterialPropertyBlock spritePropertyBlock;
    private static Material actedGlowMaterial;
    private Coroutine selectionBlinkRoutine;
    private int supplierStockAlertSignature = int.MinValue;

    private void Awake()
    {
        EnsureDefaults();
        TryAutoAssignHud();
        TryAutoAssignSupplyAlertSlots();
        TryAutoAssignLockRenderer();
        TryAutoAssignBoardTilemap();
        TryAutoAssignMatchController();
        ResolveTeamIdFromSlot();
        DisableLegacyOutlineObjects();
        CacheSpriteMaterial();
        SyncPositionState();
        appliedHasActed = hasActed;
        appliedActiveTeamId = matchController != null ? matchController.ActiveTeamId : int.MinValue;
        RefreshActedVisual();
        RefreshSupplierStockAlerts(force: true);
    }
    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (updateIsDeadInUpdate)
            SyncDeadFlagFromHp();

        SyncRuntimeFlagInspectorView();
        RefreshSupplierStockAlerts(force: false);
    }

    private void LateUpdate()
    {
        if (Application.isPlaying)
        {
            TryAutoAssignMatchController();
            return;
        }

#if UNITY_EDITOR
        if (!autoSnapWhenMovedInEditor)
            return;

        if (boardTilemap == null)
            TryAutoAssignBoardTilemap();

        if (boardTilemap == null || !transform.hasChanged)
            return;

        transform.hasChanged = false;
        PullCellFromTransform();
        SnapToCellCenter();
#endif
    }

    private void Start()
    {
        TryAutoAssignMatchController();
        TryAutoAssignSupplyAlertSlots();
        appliedHasActed = hasActed;
        appliedActiveTeamId = matchController != null ? matchController.ActiveTeamId : int.MinValue;
        RefreshActedVisual();
        RefreshDetectedIndicator();
        RefreshAIAssignedPlanBadge();
        RefreshSupplierStockAlerts(force: true);
    }

    private void OnEnable()
    {
        if (Application.isPlaying && !AllActive.Contains(this))
            AllActive.Add(this);
        MatchController.OnActiveTeamChanged += HandleActiveTeamChanged;
        MatchController.OnUnitActedStateChanged += HandleUnitActedStateChanged;
        MatchController.OnFogOfWarUpdated += HandleFogOfWarUpdated;
        MatchController.OnSlotConfigChanged += HandleSlotConfigChanged;
        if (Application.isPlaying)
        {
            Vector3Int cell = currentCellPosition;
            cell.z = 0;
            UnitOccupancyRules.NotifyUnitOccupancyChanged(this, cell, cell);
            RefreshDetectedIndicator();
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            Vector3Int cell = currentCellPosition;
            cell.z = 0;
            UnitOccupancyRules.NotifyUnitOccupancyChanged(this, cell, cell);
        }
        AllActive.Remove(this);
        MatchController.OnActiveTeamChanged -= HandleActiveTeamChanged;
        MatchController.OnUnitActedStateChanged -= HandleUnitActedStateChanged;
        MatchController.OnFogOfWarUpdated -= HandleFogOfWarUpdated;
        MatchController.OnSlotConfigChanged -= HandleSlotConfigChanged;
        ThreatRevisionTracker.NotifyUnitDisabled(this, isEmbarked);
        StopSelectionBlinkRoutine();
        ClearTemporarySortingOrder();
        SetSpriteVisible(true);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        TryAutoAssignHud();
        TryAutoAssignSupplyAlertSlots();
        TryAutoAssignLockRenderer();
        EnsureDefaults();
        TryAutoAssignBoardTilemap();
        TryAutoAssignMatchController();
        DisableLegacyOutlineObjects();
        CacheSpriteMaterial();

        if (IsEditingPrefabContext())
            return;

        SyncPositionState();
        SyncDeadFlagFromHp();
        UpdateDynamicName();
        SyncRuntimeFlagInspectorView();

        RefreshActedVisual();
        RefreshSupplierStockAlerts(force: true);
    }
#endif

    private void TryAutoAssignSupplyAlertSlots()
    {
        if (supplyTop == null) supplyTop = FindChildImageByName("supply_top");
        if (supplyMiddle == null) supplyMiddle = FindChildImageByName("supply_middle");
        if (supplyBottom == null) supplyBottom = FindChildImageByName("supply_bottom");
        SupplierStockAlertView.ConfigureSlot(supplyTop);
        SupplierStockAlertView.ConfigureSlot(supplyMiddle);
        SupplierStockAlertView.ConfigureSlot(supplyBottom);
    }

    private Image FindChildImageByName(string objectName)
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
            if (images[i] != null && images[i].name == objectName)
                return images[i];
        return null;
    }

    private void RefreshSupplierStockAlerts(bool force)
    {
        if (supplyTop == null || supplyMiddle == null || supplyBottom == null)
            TryAutoAssignSupplyAlertSlots();

        UnitData data = TryGetUnitData();
        bool isSupplier = data != null && data.isSupplier;

        // Somente supridores exibem a pilha de alerta de estoque; o resto fica oculto.
        if (!isSupplier)
        {
            if (force || supplierStockAlertSignature != 0)
            {
                supplierStockAlertSignature = 0;
                SupplierStockAlertView.HideStack(supplyBottom, supplyMiddle, supplyTop);
            }
            return;
        }

        int signature = 17;
        for (int i = 0; i < embarkedResourcesRuntime.Count; i++)
        {
            UnitEmbarkedSupply entry = embarkedResourcesRuntime[i];
            signature = unchecked(signature * 31 + (entry != null ? entry.amount : 0));
        }
        if (!force && signature == supplierStockAlertSignature)
            return;
        supplierStockAlertSignature = signature;

        List<SupplierStockAlert> alerts = new List<SupplierStockAlert>(3);
        if (data.supplierResources != null)
        {
            for (int i = 0; i < data.supplierResources.Count; i++)
            {
                UnitEmbarkedSupply baseline = data.supplierResources[i];
                if (baseline == null || baseline.supply == null || baseline.amount <= 0)
                    continue;
                int current = ResolveRuntimeSupplyAmount(baseline.supply);
                float ratio = Mathf.Clamp01((float)current / baseline.amount);
                if (ratio > supplierStockAlertThreshold)
                    continue;
                Sprite alertSprite = SupplierStockAlertView.ResolveAlertSprite(baseline.supply, current <= 0);
                if (alertSprite == null)
                    continue;
                alerts.Add(new SupplierStockAlert { ratio = ratio, empty = current <= 0, sprite = alertSprite });
            }
        }
        SupplierStockAlertView.SortMostCriticalFirst(alerts);
        // Pilha enche de baixo pra cima: o primeiro/mais critico ocupa o bottom.
        SupplierStockAlertView.ApplyStack(supplyBottom, supplyMiddle, supplyTop, alerts);
    }

    private int ResolveRuntimeSupplyAmount(SupplyData supply)
    {
        int total = 0;
        for (int i = 0; i < embarkedResourcesRuntime.Count; i++)
        {
            UnitEmbarkedSupply entry = embarkedResourcesRuntime[i];
            if (entry != null && entry.supply == supply)
                total += Mathf.Max(0, entry.amount);
        }
        return total;
    }

    public void Setup(UnitDatabase database, string id)
    {
        unitDatabase = database;
        unitId = id;
        EnsureDefaults();
        UpdateDynamicName();
    }

    public bool ApplyFromDatabase()
    {
        if (unitDatabase == null || string.IsNullOrWhiteSpace(unitId))
            return false;

        if (!unitDatabase.TryGetById(unitId, out UnitData data))
            return false;

        Apply(data);
        return true;
    }

    public void Apply(UnitData data)
    {
        if (data == null)
            return;

        unitId = data.id;
        unitDisplayName = string.IsNullOrWhiteSpace(data.displayName) ? data.id : data.displayName;

        if (currentHP <= 0 || currentHP > data.maxHP)
            currentHP = data.maxHP;

        SyncDeadFlagFromHp();

        maxFuel = Mathf.Max(1, data.autonomia);
        visao = Mathf.Max(1, data.visao);
        currentAmmo = Mathf.Clamp(currentAmmo, 0, GetMaxAmmo());
        currentFuel = Mathf.Clamp(currentFuel, 0, GetMaxFuel());
        if (!hasActed)
            remainingMovementPoints = Mathf.Max(0, data.movement);
        else
            remainingMovementPoints = Mathf.Clamp(remainingMovementPoints, 0, Mathf.Max(0, data.movement));
        SyncEmbarkedWeaponsFromData(data);
        SyncSupplierRuntimeFromData(data);
        SyncTransportRuntimeSlotsWithData(data);
        SyncCurrentLayerStateWithData(data, forceNativeDefault: true);
        SyncPreferredLayerPreferencesFromData(data);
        RefreshSpriteForCurrentLayer(data);

        currentPosition = transform.position;
        UpdateDynamicName();
        RefreshActedVisual();
        ThreatRevisionTracker.NotifyUnitDataApplied(this);
    }

    public void SetAutonomia(int autonomiaMax, bool refillCurrentFuel)
    {
        maxFuel = Mathf.Max(1, autonomiaMax);
        currentFuel = refillCurrentFuel ? maxFuel : Mathf.Clamp(currentFuel, 0, maxFuel);
        RefreshActedVisual();
    }

    public void SetCurrentHP(int value)
    {
        int max = GetMaxHP();
        currentHP = Mathf.Clamp(value, 0, max);
        SyncDeadFlagFromHp();
        RefreshActedVisual();
    }

    public void SetIsDead(bool value)
    {
        if (isDead == value)
            return;

        isDead = value;
        if (isDead)
        {
            if (deadWhenTurn < 0)
                deadWhenTurn = ResolveCurrentTurnNumber();
            if (string.IsNullOrWhiteSpace(deadByReason))
                deadByReason = "(unknown)";
            if (string.IsNullOrWhiteSpace(diedByUnit))
                diedByUnit = "(unknown)";
        }
        else
        {
            deadWhenTurn = -1;
            deadByReason = string.Empty;
            diedByUnit = string.Empty;
        }

        UpdateDynamicName();
        if (Application.isPlaying)
        {
            Vector3Int cell = currentCellPosition;
            cell.z = 0;
            UnitOccupancyRules.NotifyUnitOccupancyChanged(
                this,
                cell,
                cell);
        }
    }

    public void MarkDiedBy(UnitManager killer)
    {
        string killerId = ResolveKillerAuditId(killer);
        if (string.IsNullOrWhiteSpace(killerId))
            killerId = "(unknown)";
        MarkDead($"morto pela unidade {killerId}", killer);
    }

    public void MarkDead(string reason, UnitManager killer = null, int turnNumber = -1)
    {
        if (turnNumber < 0)
            turnNumber = ResolveCurrentTurnNumber();

        bool wasAlreadyDead = isDead;
        isDead = true;
        deadWhenTurn = turnNumber;
        deadByReason = string.IsNullOrWhiteSpace(reason) ? "(unknown)" : reason.Trim();
        diedByUnit = ResolveKillerAuditId(killer);
        UpdateDynamicName();
        if (Application.isPlaying)
        {
            Vector3Int cell = currentCellPosition;
            cell.z = 0;
            UnitOccupancyRules.NotifyUnitOccupancyChanged(this, cell, cell);
            if (!wasAlreadyDead)
                ReportDeathToTurnBriefing(killer);
        }
    }

    // Jornal do Comandante: morte durante turno ALHEIO vira "contato perdido"
    // no briefing do dono. Fog-honesto: o assassino so e nomeado se estava
    // visivel para o time do dono no momento — senao a unica informacao e a
    // ultima posicao reportada.
    private void ReportDeathToTurnBriefing(UnitManager killer)
    {
        TryAutoAssignMatchController();
        if (matchController == null || TeamId == TeamId.Neutral)
            return;
        // Morte no proprio turno do dono foi vista ao vivo; o Jornal cobre a ausencia.
        if ((int)TeamId == matchController.ActiveTeamId)
            return;

        Vector3Int cell = currentCellPosition;
        cell.z = 0;
        string detail = killer != null &&
                        matchController.IsUnitVisibleForSlot(killer, PlayerSlotId.FromIndex(SlotIndex))
            ? $"abatida por {(!string.IsNullOrWhiteSpace(killer.UnitDisplayName) ? killer.UnitDisplayName : killer.name)}"
            : "sem contato visual com o atacante";
        matchController.ReportTurnBriefingEvent(
            PlayerSlotId.FromIndex(SlotIndex),
            MatchController.TurnBriefingCategory.ContactLost,
            ResolveRuntimeUnitName(),
            detail,
            cell);
    }

    public void ClearDeathAudit()
    {
        deadWhenTurn = -1;
        deadByReason = string.Empty;
        diedByUnit = string.Empty;
        if (currentHP > 0)
            isDead = false;
        UpdateDynamicName();
    }

    public void RestoreLifecycleAudit(
        bool savedIsDead,
        int savedDeadWhenTurn,
        string savedDeadByReason,
        string savedDiedByUnit,
        bool savedHasMerged,
        int savedMergedWhenTurn,
        string savedMergedWithUnit)
    {
        isDead = savedIsDead;
        deadWhenTurn = savedIsDead ? savedDeadWhenTurn : -1;
        deadByReason = savedIsDead ? (savedDeadByReason ?? string.Empty) : string.Empty;
        diedByUnit = savedIsDead ? (savedDiedByUnit ?? string.Empty) : string.Empty;
        hasMerged = savedHasMerged;
        mergedWhenTurn = savedHasMerged ? savedMergedWhenTurn : -1;
        mergedWithUnit = savedHasMerged ? (savedMergedWithUnit ?? string.Empty) : string.Empty;
        UpdateDynamicName();
    }

    public void MarkAsDonorMergedInto(UnitManager receiver)
    {
        hasMerged = true;
        mergedWhenTurn = ResolveCurrentTurnNumber();
        mergedWithUnit = receiver != null ? receiver.ResolveRuntimeUnitName() : "(unknown)";
    }

    public void MarkMergedWith(IReadOnlyList<UnitManager> donors)
    {
        hasMerged = true;
        mergedWhenTurn = ResolveCurrentTurnNumber();
        if (donors == null || donors.Count <= 0)
        {
            mergedWithUnit = "(unknown)";
            UpdateDynamicName();
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < donors.Count; i++)
        {
            UnitManager donor = donors[i];
            if (donor == null)
                continue;

            if (sb.Length > 0)
                sb.Append(", ");

            sb.Append(donor.ResolveRuntimeUnitName());
        }

        mergedWithUnit = sb.Length > 0 ? sb.ToString() : "(unknown)";
        UpdateDynamicName();
    }

    public void ClearMergeAudit()
    {
        hasMerged = false;
        mergedWhenTurn = -1;
        mergedWithUnit = string.Empty;
        UpdateDynamicName();
    }

    public void SetCurrentAmmo(int value)
    {
        currentAmmo = Mathf.Clamp(value, 0, GetMaxAmmo());
        RefreshActedVisual();
    }

    public void SetCurrentFuel(int value)
    {
        currentFuel = Mathf.Clamp(value, 0, GetMaxFuel());
        RefreshActedVisual();
    }

    public SpriteRenderer GetMainSpriteRenderer()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        return spriteRenderer;
    }

    public void SetTemporarySortingOrder(int forcedSortingOrder = 999)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer == null)
            return;

        if (!hasTemporarySortingOverride)
        {
            cachedSpriteSortingOrder = spriteRenderer.sortingOrder;
            cachedActedLockSortingOrder = actedLockRenderer != null ? actedLockRenderer.sortingOrder : 0;
            hasTemporarySortingOverride = true;
        }

        spriteRenderer.sortingOrder = forcedSortingOrder;
        if (actedLockRenderer != null)
            actedLockRenderer.sortingOrder = forcedSortingOrder;
    }

    public void ClearTemporarySortingOrder()
    {
        if (!hasTemporarySortingOverride)
            return;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = cachedSpriteSortingOrder;

        if (actedLockRenderer != null)
            actedLockRenderer.sortingOrder = cachedActedLockSortingOrder;

        hasTemporarySortingOverride = false;
    }

    // Hex multicamada (ex.: navio Naval/Surface + submarino Submerged no mesmo
    // hex): a unidade do time OBSERVADOR salta pra frente do sprite inimigo,
    // para o dono ver onde a sua unidade esta. Bump persistente e bem abaixo do
    // 999 da selecao. Convive com o override temporario de selecao ajustando a
    // base cacheada quando ele estiver ativo (senao a deselecao restauraria a
    // ordem errada).
    private const int StackedHexFrontSortingBump = 40;
    private int stackedHexFrontBaseOrder;
    private bool stackedHexFrontApplied;

    public void SetStackedHexFrontRendering(bool front)
    {
        if (front == stackedHexFrontApplied)
            return;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
            return;

        if (hasTemporarySortingOverride)
        {
            // Selecao ativa: o renderer esta em 999; ajusta apenas a base que
            // sera restaurada no ClearTemporarySortingOrder.
            if (front)
            {
                stackedHexFrontBaseOrder = cachedSpriteSortingOrder;
                cachedSpriteSortingOrder = stackedHexFrontBaseOrder + StackedHexFrontSortingBump;
            }
            else
            {
                cachedSpriteSortingOrder = stackedHexFrontBaseOrder;
            }

            stackedHexFrontApplied = front;
            return;
        }

        if (front)
        {
            stackedHexFrontBaseOrder = spriteRenderer.sortingOrder;
            spriteRenderer.sortingOrder = stackedHexFrontBaseOrder + StackedHexFrontSortingBump;
        }
        else
        {
            spriteRenderer.sortingOrder = stackedHexFrontBaseOrder;
        }

        stackedHexFrontApplied = front;
    }

    public void BeginTemporaryFogTraversalVisual()
    {
        if (temporaryFogTraversalVisual) return;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null) return;

        temporaryFogTraversalVisual = true;
        temporaryFogSpriteLayerId = spriteRenderer.sortingLayerID;
        temporaryFogSpriteOrder = spriteRenderer.sortingOrder;
        temporaryFogWasHidden = hiddenByFogOfWar;
        int fogLayerId = SortingLayer.NameToID("FogOfWar");
        spriteRenderer.sortingLayerID = fogLayerId;
        spriteRenderer.sortingOrder = 90;

        temporaryFogHudCanvases = GetComponentsInChildren<Canvas>(true);
        temporaryFogHudLayerIds = new int[temporaryFogHudCanvases.Length];
        temporaryFogHudOrders = new int[temporaryFogHudCanvases.Length];
        for (int i = 0; i < temporaryFogHudCanvases.Length; i++)
        {
            Canvas canvas = temporaryFogHudCanvases[i];
            temporaryFogHudLayerIds[i] = canvas.sortingLayerID;
            temporaryFogHudOrders[i] = canvas.sortingOrder;
            canvas.overrideSorting = true;
            canvas.sortingLayerID = fogLayerId;
            canvas.sortingOrder = 91 + i;
        }

        hiddenByFogOfWar = false;
        ApplyFogOfWarVisibility();
    }

    public void EndTemporaryFogTraversalVisual(bool restorePreviousVisibility = true)
    {
        if (!temporaryFogTraversalVisual) return;
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingLayerID = temporaryFogSpriteLayerId;
            spriteRenderer.sortingOrder = temporaryFogSpriteOrder;
        }
        if (temporaryFogHudCanvases != null)
        {
            for (int i = 0; i < temporaryFogHudCanvases.Length; i++)
            {
                Canvas canvas = temporaryFogHudCanvases[i];
                if (canvas == null) continue;
                canvas.sortingLayerID = temporaryFogHudLayerIds[i];
                canvas.sortingOrder = temporaryFogHudOrders[i];
            }
        }
        temporaryFogTraversalVisual = false;
        if (restorePreviousVisibility)
            hiddenByFogOfWar = temporaryFogWasHidden;
        ApplyFogOfWarVisibility();
        temporaryFogHudCanvases = null;
        temporaryFogHudLayerIds = null;
        temporaryFogHudOrders = null;
    }

    /// <summary>
    /// Mantem sprite e HUD coerentes depois que uma unidade em movimento cruza uma
    /// area onde a apresentacao provisoria confirmou sua deteccao. Nao altera cache,
    /// memoria ou verdade confirmada do FOW.
    /// </summary>
    public void BeginTemporaryFogDetectionPresentation()
    {
        if (temporaryFogDetectionPresentation)
            return;

        temporaryFogDetectionPresentation = true;
        temporaryFogDetectionWasHidden = hiddenByFogOfWar;
        hiddenByFogOfWar = false;
        ApplyFogOfWarVisibility();
    }

    public void EndTemporaryFogDetectionPresentation(bool restorePreviousVisibility)
    {
        if (!temporaryFogDetectionPresentation)
            return;

        temporaryFogDetectionPresentation = false;
        if (restorePreviousVisibility)
        {
            hiddenByFogOfWar = temporaryFogDetectionWasHidden;
            ApplyFogOfWarVisibility();
        }
    }

    public void MarkAsActed()
    {
        if (hasActed)
            return;

        hasActed = true;
        appliedHasActed = hasActed;
        RefreshActedVisual();
        TryAutoAssignMatchController();
        matchController?.NotifyUnitReachedHasAct(this);
    }

    public void MarkAsFired()
    {
        hasFiredThisTurn = true;
    }

    public void SetFogOfWarVisibility(bool visible)
    {
        bool shouldHide = !visible;
        if (hiddenByFogOfWar == shouldHide)
            return;

        hiddenByFogOfWar = shouldHide;
        ApplyFogOfWarVisibility();
        
        if (visible && matchController != null &&
            !matchController.IsUnitOwnedByFogPresentationObserver(this))
        {
            TurnStateManager.NotifyUnitRevealedFromFog(this);
        }
    }

    public void ForceFogOfWarPresentationVisibility(bool visible)
    {
        hiddenByFogOfWar = !visible;
        if (visible)
        {
            ApplyFogOfWarVisibility();
            return;
        }

        StopSelectionBlinkRoutine();
        isSelected = false;
        SetSpriteVisible(false);
        SetHudVisible(false);
        SetOwnedUiVisualsVisible(false);
    }

    /// <summary>
    /// Eco exclusivamente visual para um contato confirmado cuja celula
    /// geografica continua sob a camada de FOW. Nao altera deteccao, memoria,
    /// ocupacao nem qualquer verdade confirmada do tabuleiro.
    /// </summary>
    public void SetFogDetectedContactPresentation(bool visible)
    {
        if (!visible || isDead || isEmbarked || !gameObject.activeInHierarchy)
        {
            if (fogDetectedContactRenderer != null)
                fogDetectedContactRenderer.enabled = false;
            return;
        }

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
            return;

        EnsureFogDetectedContactRenderer();
        if (fogDetectedContactRenderer == null)
            return;

        fogDetectedContactRenderer.sprite = spriteRenderer.sprite;
        fogDetectedContactRenderer.flipX = spriteRenderer.flipX;
        fogDetectedContactRenderer.flipY = spriteRenderer.flipY;
        fogDetectedContactRenderer.drawMode = spriteRenderer.drawMode;
        fogDetectedContactRenderer.size = spriteRenderer.size;
        fogDetectedContactRenderer.spriteSortPoint =
            spriteRenderer.spriteSortPoint;
        fogDetectedContactRenderer.maskInteraction =
            SpriteMaskInteraction.None;

        CacheSpriteMaterial();
        fogDetectedContactRenderer.sharedMaterial =
            defaultSpriteMaterial != null
                ? defaultSpriteMaterial
                : spriteRenderer.sharedMaterial;

        Color source = spriteRenderer.color;
        float luminance =
            source.r * 0.2126f +
            source.g * 0.7152f +
            source.b * 0.0722f;
        Color desaturated = Color.Lerp(
            source,
            new Color(luminance, luminance, luminance, source.a),
            0.72f);
        Color contact = Color.Lerp(
            desaturated, Color.white, 0.28f);
        contact.a = Mathf.Clamp01(source.a * 0.55f);
        fogDetectedContactRenderer.color = contact;
        fogDetectedContactRenderer.enabled = true;
    }

    private void EnsureFogDetectedContactRenderer()
    {
        if (fogDetectedContactRenderer != null || spriteRenderer == null)
            return;

        Transform existing =
            spriteRenderer.transform.Find("FogDetectedContact");
        if (existing != null)
        {
            fogDetectedContactRenderer =
                existing.GetComponent<SpriteRenderer>();
        }

        if (fogDetectedContactRenderer == null)
        {
            var contactObject = new GameObject("FogDetectedContact")
            {
                hideFlags = HideFlags.DontSave
            };
            contactObject.transform.SetParent(
                spriteRenderer.transform, false);
            fogDetectedContactRenderer =
                contactObject.AddComponent<SpriteRenderer>();
        }

        Transform contactTransform =
            fogDetectedContactRenderer.transform;
        contactTransform.localPosition = Vector3.zero;
        contactTransform.localRotation = Quaternion.identity;
        contactTransform.localScale = Vector3.one;
        fogDetectedContactRenderer.sortingLayerName = "FogOfWar";
        fogDetectedContactRenderer.sortingOrder = 20;
        fogDetectedContactRenderer.enabled = false;
    }

    public void ResetActed()
    {
        hasActed = false;
        hasFiredThisTurn = false;
        ResetRemainingMovement();
        appliedHasActed = hasActed;
        RefreshActedVisual();
        TryAutoAssignMatchController();
        matchController?.NotifyUnitReachedHasAct(this);
    }

    public void ResetForTeamTurnStart()
    {
        ResetActed();
        ClearReceivedSuppliesThisTurn();
        ClearTookOffRecently();
        SetSurfacedForSupplyThisTurn(false);
    }

    public void SetRemainingMovementPoints(int value)
    {
        remainingMovementPoints = Mathf.Clamp(value, 0, GetMovementRange());
        RefreshActedVisual();
    }

    public void ConsumeMovementPoints(int movementCost)
    {
        int clampedCost = Mathf.Max(0, movementCost);
        int maxMovement = Mathf.Max(0, GetMovementRange());
        int currentRemaining = Mathf.Clamp(remainingMovementPoints, 0, maxMovement);
        remainingMovementPoints = Mathf.Clamp(currentRemaining - clampedCost, 0, maxMovement);
        RefreshActedVisual();
    }

    public void ResetRemainingMovement()
    {
        remainingMovementPoints = Mathf.Max(0, GetMovementRange());
        RefreshActedVisual();
    }

    public void SetUsedRoadBoostOnLastMove(bool value)
    {
        usedRoadBoostOnLastMove = value;
    }

    public void MarkReceivedSuppliesThisTurn()
    {
        SetReceivedSuppliesThisTurn(true);
    }

    public void ClearReceivedSuppliesThisTurn()
    {
        SetReceivedSuppliesThisTurn(false);
    }

    public void MarkTookOffRecently()
    {
        SetTookOffRecently(true);
    }

    public void ClearTookOffRecently()
    {
        SetTookOffRecently(false);
    }

    public void RefreshRuntimeVisualState()
    {
        RefreshActedVisual();
    }

    public void RegisterStealthReveal(int detectorTeamId)
    {
        AddCurrentlyObservedByTeam(detectorTeamId);
    }

    public bool IsStealthRevealedForTeam(int viewerTeamId, int currentTurn)
    {
        if (currentlyObservedByTeamIds == null || currentlyObservedByTeamIds.Count <= 0)
            return false;

        return currentlyObservedByTeamIds.Contains(viewerTeamId);
    }

    public void SetSurfacedForSupplyThisTurn(bool value)
    {
        surfacedForSupplyThisTurn = value &&
            GetAircraftType() == AircraftType.None &&
            SupportsLayerMode(Domain.Submarine, HeightLevel.Submerged);
    }

    public bool IsCurrentlyObservedByOpponent()
    {
        if (currentlyObservedByTeamIds == null || currentlyObservedByTeamIds.Count <= 0)
            return false;

        for (int i = 0; i < currentlyObservedByTeamIds.Count; i++)
        {
            int observerTeamId = currentlyObservedByTeamIds[i];
            if (observerTeamId >= 0 && observerTeamId != SlotIndex)
                return true;
        }

        return false;
    }

    public void ClearStealthRevealState()
    {
        if (currentlyObservedByTeamIds != null)
            currentlyObservedByTeamIds.Clear();
        RefreshDetectedIndicator();
    }

    public bool AddCurrentlyObservedByTeam(int teamId)
    {
        if (currentlyObservedByTeamIds == null)
            currentlyObservedByTeamIds = new List<int>();
        if (teamId < -1 || teamId > 3)
            return false;
        if (currentlyObservedByTeamIds.Contains(teamId))
            return false;

        currentlyObservedByTeamIds.Add(teamId);
        RefreshDetectedIndicator();
        return true;
    }

    public bool RemoveCurrentlyObservedByTeam(int teamId)
    {
        if (currentlyObservedByTeamIds == null || currentlyObservedByTeamIds.Count <= 0)
            return false;

        bool removed = currentlyObservedByTeamIds.Remove(teamId);
        if (removed)
            RefreshDetectedIndicator();
        return removed;
    }

    public bool SyncCurrentlyObservedByTeams(IEnumerable<int> teamIds)
    {
        if (currentlyObservedByTeamIds == null)
            currentlyObservedByTeamIds = new List<int>();

        HashSet<int> desired = new HashSet<int>();
        if (teamIds != null)
        {
            foreach (int teamId in teamIds)
            {
                if (teamId < -1 || teamId > 3)
                    continue;
                desired.Add(teamId);
            }
        }

        bool changed = false;
        for (int i = currentlyObservedByTeamIds.Count - 1; i >= 0; i--)
        {
            int teamId = currentlyObservedByTeamIds[i];
            if (desired.Contains(teamId))
                continue;

            currentlyObservedByTeamIds.RemoveAt(i);
            changed = true;
        }

        foreach (int teamId in desired)
        {
            if (currentlyObservedByTeamIds.Contains(teamId))
                continue;

            currentlyObservedByTeamIds.Add(teamId);
            changed = true;
        }

        if (changed)
            RefreshDetectedIndicator();
        return changed;
    }

    public bool ClearCurrentlyObservedByTeams()
    {
        if (currentlyObservedByTeamIds == null || currentlyObservedByTeamIds.Count <= 0)
            return false;

        currentlyObservedByTeamIds.Clear();
        RefreshDetectedIndicator();
        return true;
    }

    public void SetReceivedSuppliesThisTurn(bool value)
    {
        if (receivedSuppliesThisTurn == value)
            return;

        receivedSuppliesThisTurn = value;
        UpdateDynamicName();
    }

    public void SetTookOffRecently(bool value)
    {
        if (tookOffRecently == value)
            return;

        tookOffRecently = value;
        UpdateDynamicName();
    }

    public void SetAircraftForcedLandingAwaitingRefuel(bool value)
    {
        aircraftForcedLandingAwaitingRefuel =
            GetAircraftType() != AircraftType.None && value;
    }

    public void SetSelected(bool selected)
    {
        if (selected && hiddenByFogOfWar)
        {
            isSelected = false;
            StopSelectionBlinkRoutine();
            ClearTemporarySortingOrder();
            ApplyFogOfWarVisibility();
            return;
        }

        if (isSelected == selected)
            return;

        isSelected = selected;
        if (isSelected)
            SetTemporarySortingOrder();
        else
            ClearTemporarySortingOrder();
        RefreshSelectionVisual();
    }

    public void SetPreviewDimmed(bool dimmed)
    {
        if (isPreviewDimmed == dimmed)
            return;

        isPreviewDimmed = dimmed;
        RefreshActedVisual();
    }

    public void SetSelectionBlinkInterval(float interval)
    {
        float clamped = Mathf.Clamp(interval, 0.05f, 1f);
        selectionBlinkInterval = clamped;
        selectionBlinkActiveDuration = clamped;
        selectionBlinkInactiveDuration = clamped;
    }

    public void SetSelectionBlinkDurations(float activeDuration, float inactiveDuration)
    {
        selectionBlinkActiveDuration = Mathf.Clamp(activeDuration, 0.05f, 1f);
        selectionBlinkInactiveDuration = Mathf.Clamp(inactiveDuration, 0.05f, 1f);
    }

    public int GetMaxHP()
    {
        if (unitDatabase != null && !string.IsNullOrWhiteSpace(unitId) && unitDatabase.TryGetById(unitId, out UnitData data))
            return Mathf.Max(1, data.maxHP);

        return Mathf.Max(1, currentHP);
    }

    public int GetMaxAmmo()
    {
        return Mathf.Max(1, maxAmmo);
    }

    public int GetMaxFuel()
    {
        return Mathf.Max(1, maxFuel);
    }

    public int GetMovementRange()
    {
        if (unitDatabase != null && !string.IsNullOrWhiteSpace(unitId) && unitDatabase.TryGetById(unitId, out UnitData data))
            return Mathf.Max(0, data.movement);

        return 0;
    }

    public Domain GetDomain()
    {
        return currentDomain;
    }

    public IReadOnlyList<UnitLayerMode> GetAllLayerModes()
    {
        UnitLayerMode[] modes = BuildLayerModesSnapshot();
        return modes;
    }

    public IReadOnlyList<UnitLayerMode> GetAdditionalLayerModes()
    {
        UnitLayerMode[] modes = BuildLayerModesSnapshot();
        if (modes.Length <= 1)
            return System.Array.Empty<UnitLayerMode>();

        UnitLayerMode[] additional = new UnitLayerMode[modes.Length - 1];
        for (int i = 1; i < modes.Length; i++)
            additional[i - 1] = modes[i];
        return additional;
    }

    public UnitLayerMode GetCurrentLayerMode()
    {
        return new UnitLayerMode(currentDomain, currentHeightLevel);
    }

    public bool TrySetCurrentLayerMode(int index)
    {
        UnitLayerMode[] modes = BuildLayerModesSnapshot();
        if (modes.Length == 0)
            return false;
        if (index < 0 || index >= modes.Length)
            return false;

        UnitLayerMode targetMode = modes[index];
        if (IsLayerChangeBlockedByForcedLock(targetMode.domain, targetMode.heightLevel, out _))
            return false;

        SetCurrentLayerState(index, targetMode);
        return true;
    }

    public bool TrySetCurrentLayerMode(Domain domain, HeightLevel heightLevel, bool ignoreForcedLock = false)
    {
        if (!ignoreForcedLock && IsLayerChangeBlockedByForcedLock(domain, heightLevel, out _))
            return false;

        Domain previousDomain = currentDomain;
        HeightLevel previousHeight = currentHeightLevel;
        UnitLayerMode[] modes = BuildLayerModesSnapshot();
        for (int i = 0; i < modes.Length; i++)
        {
            if (modes[i].domain == domain && modes[i].heightLevel == heightLevel)
            {
                SetCurrentLayerState(i, modes[i]);
                ThreatRevisionTracker.NotifyUnitLayerChanged(this, previousDomain, previousHeight, currentDomain, currentHeightLevel);
                return true;
            }
        }

        return false;
    }

    public bool SupportsLayerMode(Domain domain, HeightLevel heightLevel)
    {
        UnitLayerMode[] modes = BuildLayerModesSnapshot();
        for (int i = 0; i < modes.Length; i++)
        {
            if (modes[i].domain == domain && modes[i].heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    public bool TryGetForcedLayerLock(out Domain domain, out HeightLevel heightLevel, out int turnsRemaining)
    {
        if (!HasForcedLayerLock)
        {
            domain = currentDomain;
            heightLevel = currentHeightLevel;
            turnsRemaining = 0;
            return false;
        }

        domain = forcedLayerLockDomain;
        heightLevel = forcedLayerLockHeight;
        turnsRemaining = Mathf.Max(0, forcedLayerLockTurnsRemaining);
        return true;
    }

    public bool IsLayerChangeBlockedByForcedLock(Domain targetDomain, HeightLevel targetHeightLevel, out string reason)
    {
        if (!HasForcedLayerLock)
        {
            reason = string.Empty;
            return false;
        }

        bool sameLockedLayer = forcedLayerLockDomain == targetDomain && forcedLayerLockHeight == targetHeightLevel;
        if (sameLockedLayer)
        {
            reason = string.Empty;
            return false;
        }

        reason = PanelDialogController.ResolveDialogMessage(
            "layer.locked.by.weapon",
            "Camada travada em <domain>/<height> por <turns> turno(s).",
            new Dictionary<string, string>
            {
                { "unit", ResolveRuntimeUnitName() },
                { "domain", forcedLayerLockDomain.ToString() },
                { "height", forcedLayerLockHeight.ToString() },
                { "turns", forcedLayerLockTurnsRemaining.ToString() }
            });
        return true;
    }

    public void SetForcedLayerLock(Domain domain, HeightLevel heightLevel, int turns)
    {
        hasForcedLayerLock = true;
        forcedLayerLockDomain = domain;
        forcedLayerLockHeight = heightLevel;
        forcedLayerLockTurnsRemaining = Mathf.Max(1, turns);
        layerLockCountdownStarted = false;
    }

    public void RestoreLayerLock(Domain domain, HeightLevel heightLevel, int turns, bool countdownStarted)
    {
        SetForcedLayerLock(domain, heightLevel, turns);
        layerLockCountdownStarted = countdownStarted;
    }

    public void ClearForcedLayerLock()
    {
        hasForcedLayerLock = false;
        forcedLayerLockTurnsRemaining = 0;
        layerLockCountdownStarted = false;
    }

    public void ConsumeForcedLayerLockTurn()
    {
        if (!HasForcedLayerLock)
            return;

        // Lock pendente nao conta tempo: a janela de exposicao so corre depois
        // que a camada forcada e de fato aplicada. Senao, campear sob um navio
        // ate o lock expirar anularia a emersao forcada.
        if (HasPendingForcedLayerLock)
            return;

        // O valor configurado representa turnos jogaveis completos do dono.
        // No primeiro upkeep depois de receber a trava, apenas inicia a janela;
        // decrementar aqui faria "2 turnos" bloquear somente uma acao.
        if (!layerLockCountdownStarted)
        {
            layerLockCountdownStarted = true;
            return;
        }

        forcedLayerLockTurnsRemaining = Mathf.Max(0, forcedLayerLockTurnsRemaining - 1);
        if (forcedLayerLockTurnsRemaining <= 0)
            ClearForcedLayerLock();
    }

    // Debug utility: allows forcing a runtime layer state even when that exact
    // mode is not declared on UnitData (useful for gameplay investigation).
    public bool ForceLayerStateForDebug(Domain domain, HeightLevel heightLevel)
    {
        currentDomain = domain;
        currentHeightLevel = heightLevel;
        layerStateInitialized = true;

        currentLayerModeIndex = ResolveLayerModeIndex(domain, heightLevel);
        RefreshSpriteForCurrentLayer();
        RefreshActedVisual();
        return true;
    }

    // Debug step order used by editor buttons while playing:
    // Land/Surface -> Air/Low -> Air/High (up) and reverse (down).
    public bool TryStepLayerStateForDebug(int delta)
    {
        if (delta == 0)
            return false;

        Domain targetDomain = currentDomain;
        HeightLevel targetHeight = currentHeightLevel;

        if (delta < 0)
        {
            if (currentDomain == Domain.Air && currentHeightLevel == HeightLevel.AirHigh)
            {
                targetDomain = Domain.Air;
                targetHeight = HeightLevel.AirLow;
            }
            else if (currentDomain == Domain.Air && currentHeightLevel == HeightLevel.AirLow)
            {
                targetDomain = Domain.Land;
                targetHeight = HeightLevel.Surface;
            }
            else
            {
                return false;
            }
        }
        else
        {
            if (currentDomain != Domain.Air)
            {
                targetDomain = Domain.Air;
                targetHeight = HeightLevel.AirLow;
            }
            else if (currentHeightLevel == HeightLevel.AirLow)
            {
                targetDomain = Domain.Air;
                targetHeight = HeightLevel.AirHigh;
            }
            else
            {
                return false;
            }
        }

        return ForceLayerStateForDebug(targetDomain, targetHeight);
    }

    public MovementCategory GetMovementCategory()
    {
        if (unitDatabase != null && !string.IsNullOrWhiteSpace(unitId) && unitDatabase.TryGetById(unitId, out UnitData data))
            return data.movementCategory;

        return MovementCategory.Marcha;
    }

    public HeightLevel GetHeightLevel()
    {
        return currentHeightLevel;
    }

    public bool HasSkill(SkillData skill)
    {
        if (skill == null)
            return false;

        UnitData data = TryGetUnitData();
        if (data == null || data.skills == null)
            return false;

        if (data.skills.Contains(skill))
            return true;

        string requestedId = string.IsNullOrWhiteSpace(skill.id) ? string.Empty : skill.id.Trim();
        if (requestedId.Length == 0)
            return false;

        for (int i = 0; i < data.skills.Count; i++)
        {
            SkillData ownedSkill = data.skills[i];
            if (ownedSkill == null || string.IsNullOrWhiteSpace(ownedSkill.id))
                continue;

            if (ownedSkill.id.Trim() == requestedId)
                return true;
        }

        return false;
    }

    public bool TryGetUnitData(out UnitData data)
    {
        data = TryGetUnitData();
        return data != null;
    }

    public AircraftType GetAircraftType()
    {
        UnitData data = TryGetUnitData();
        if (data == null)
            return AircraftType.None;

        if (data.unitClass == GameUnitClass.Helicopter)
            return AircraftType.Helicopter;
        // Classificacao estrutural: qualquer unidade que suporte Domain.Air e aeronave.
        // Evita excluir hidroavioes e futuras unidades hibridas por depender de nome/classe.
        if (data.IsAircraft())
            return AircraftType.FixedWing;
        return AircraftType.None;
    }

    public HeightLevel GetPreferredAirHeight()
    {
        if (useExplicitPreferredAirHeightRuntime)
            return preferredAirHeightRuntime == HeightLevel.AirHigh ? HeightLevel.AirHigh : HeightLevel.AirLow;

        UnitData data = TryGetUnitData();
        if (data == null)
            return HeightLevel.AirLow;

        if (data.domain == Domain.Air && (data.heightLevel == HeightLevel.AirLow || data.heightLevel == HeightLevel.AirHigh))
            return data.heightLevel;

        if (data.aditionalDomainsAllowed != null)
        {
            for (int i = 0; i < data.aditionalDomainsAllowed.Count; i++)
            {
                UnitLayerMode mode = data.aditionalDomainsAllowed[i];
                if (mode.domain == Domain.Air && (mode.heightLevel == HeightLevel.AirLow || mode.heightLevel == HeightLevel.AirHigh))
                    return mode.heightLevel;
            }
        }

        return HeightLevel.AirLow;
    }

    public bool TryGetPreferredNavalLayerMode(out Domain domain, out HeightLevel heightLevel)
    {
        domain = Domain.Naval;
        heightLevel = HeightLevel.Surface;

        if (!useExplicitPreferredNavalHeightRuntime)
            return false;

        heightLevel = preferredNavalHeightRuntime == HeightLevel.Submerged
            ? HeightLevel.Submerged
            : HeightLevel.Surface;
        domain = heightLevel == HeightLevel.Submerged ? Domain.Submarine : Domain.Naval;
        return true;
    }

    public bool HasSkillId(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            return false;

        UnitData data = TryGetUnitData();
        if (data == null || data.skills == null)
            return false;

        string normalized = skillId.Trim();
        for (int i = 0; i < data.skills.Count; i++)
        {
            SkillData owned = data.skills[i];
            if (owned == null || string.IsNullOrWhiteSpace(owned.id))
                continue;

            if (string.Equals(owned.id.Trim(), normalized, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public void SetAircraftGrounded(bool grounded)
    {
        if (grounded)
        {
            if (currentDomain == Domain.Air || currentHeightLevel != HeightLevel.Surface)
                TrySetCurrentLayerMode(Domain.Land, HeightLevel.Surface);
            return;
        }

        if (currentDomain != Domain.Air)
            TrySetCurrentLayerMode(Domain.Air, GetPreferredAirHeight());
        SetAircraftForcedLandingAwaitingRefuel(false);
    }

    public void SetAircraftEmbarkedInCarrier(bool embarkedInCarrier)
    {
        SetEmbarked(embarkedInCarrier);
    }

    public void SetAircraftOperationLockTurns(int turns)
    {
        if (turns <= 0)
        {
            ClearForcedLayerLock();
            return;
        }

        // Compatibilidade com saves antigos: o contador legado ja estava em
        // andamento, portanto nao deve ganhar um turno extra ao ser restaurado.
        RestoreLayerLock(currentDomain, currentHeightLevel, turns, countdownStarted: true);
    }

    private string ResolveRuntimeUnitName()
    {
        if (!string.IsNullOrWhiteSpace(unitDisplayName))
            return unitDisplayName;
        if (!string.IsNullOrWhiteSpace(unitId))
            return unitId;
        return name;
    }

    public IReadOnlyList<UnitEmbarkedWeapon> GetEmbarkedWeapons()
    {
        return embarkedWeaponsRuntime;
    }

    public IReadOnlyList<UnitEmbarkedSupply> GetEmbarkedResources()
    {
        return embarkedResourcesRuntime;
    }

    public IReadOnlyList<ServiceData> GetEmbarkedServices()
    {
        return embarkedServicesRuntime;
    }

    public bool TryConsumeEmbarkedWeaponAmmo(int embarkedWeaponIndex, int amount = 1)
    {
        if (amount <= 0)
            amount = 1;

        if (embarkedWeaponIndex < 0 || embarkedWeaponIndex >= embarkedWeaponsRuntime.Count)
            return false;

        UnitEmbarkedWeapon embarked = embarkedWeaponsRuntime[embarkedWeaponIndex];
        if (embarked == null || embarked.squadAmmunition < amount)
            return false;

        embarked.squadAmmunition -= amount;
        RefreshActedVisual();
        return true;
    }

    public int GetOccupiedTransportSeatCountForSlot(int slotIndex)
    {
        UnitData data = TryGetUnitData();
        SyncTransportRuntimeSlotsWithData(data);

        int count = 0;
        for (int i = 0; i < transportedUnitSlots.Count; i++)
        {
            UnitTransportSeatRuntime seat = transportedUnitSlots[i];
            if (seat == null || seat.slotIndex != slotIndex || seat.embarkedUnit == null)
                continue;

            if (!seat.embarkedUnit.IsEmbarked)
            {
                seat.embarkedUnit = null;
                seat.embarkedOnTurn = -1;
                continue;
            }

            count++;
        }

        return count;
    }

    public int GetPassengerEmbarkedOnTurn(UnitManager passenger)
    {
        if (passenger == null || transportedUnitSlots == null)
            return -1;

        for (int i = 0; i < transportedUnitSlots.Count; i++)
        {
            UnitTransportSeatRuntime seat = transportedUnitSlots[i];
            if (seat != null && seat.embarkedUnit == passenger && passenger.IsEmbarked)
                return seat.embarkedOnTurn;
        }

        return -1;
    }

    public int GetCapacityForTransportSlot(int slotIndex)
    {
        UnitData data = TryGetUnitData();
        if (data == null || data.transportSlots == null || slotIndex < 0 || slotIndex >= data.transportSlots.Count)
            return 0;

        return Mathf.Max(1, data.transportSlots[slotIndex].capacity);
    }

    public bool TryEmbarkPassengerInSlot(
        UnitManager passenger,
        int slotIndex,
        out string reason,
        int embarkedOnTurnOverride = int.MinValue)
    {
        reason = string.Empty;
        if (passenger == null)
        {
            reason = "Passageiro invalido.";
            return false;
        }

        if (passenger == this)
        {
            reason = "Unidade nao pode embarcar em si mesma.";
            return false;
        }

        if (!TryGetUnitData(out UnitData data) || data == null || !data.isTransporter)
        {
            reason = "Unidade alvo nao eh transportadora.";
            return false;
        }

        if (data.transportSlots == null || slotIndex < 0 || slotIndex >= data.transportSlots.Count)
        {
            reason = "Slot de transporte invalido.";
            return false;
        }

        SyncTransportRuntimeSlotsWithData(data);
        if (!CanUseTransportSlotExclusivity(slotIndex, out reason))
            return false;

        UnitTransportSeatRuntime freeSeat = FindFirstFreeSeat(slotIndex);
        if (freeSeat == null)
        {
            reason = "Slot lotado.";
            return false;
        }

        UnitManager currentTransporter = passenger.EmbarkedTransporter;
        if (passenger.IsEmbarked && currentTransporter != null)
        {
            if (currentTransporter != this)
                currentTransporter.RemoveEmbarkedPassenger(passenger);
            else
                RemoveEmbarkedPassenger(passenger);
        }

        Vector3Int transporterCell = currentCellPosition;
        transporterCell.z = 0;
        passenger.SetCurrentCellPosition(transporterCell, enforceFinalOccupancyRule: false);
        passenger.AssignEmbarkTransport(this, slotIndex);
        if (!passenger.IsEmbarked)
            passenger.SetEmbarked(true);
        else
            passenger.SyncHierarchyForEmbarkedState();
        // Defensive refresh after reparent to transporter hierarchy:
        // guarantees embarked passenger visuals/HUD remain hidden.
        passenger.RefreshRuntimeVisualState();
        freeSeat.embarkedUnit = passenger;
        freeSeat.embarkedOnTurn = embarkedOnTurnOverride != int.MinValue
            ? embarkedOnTurnOverride
            : ResolveCurrentTurnNumber();
        RefreshSpriteForCurrentLayer(data);
        RefreshActedVisual();
        return true;
    }

    public bool TryEmbarkPassengerInSeat(
        UnitManager passenger,
        int slotIndex,
        int seatIndex,
        out string reason,
        int embarkedOnTurnOverride = int.MinValue)
    {
        reason = string.Empty;
        if (!TryGetUnitData(out UnitData data) || data == null || !data.isTransporter)
        {
            reason = "Unidade alvo nao eh transportadora.";
            return false;
        }

        if (passenger == null)
        {
            reason = "Passageiro invalido.";
            return false;
        }

        if (passenger == this)
        {
            reason = "Unidade nao pode embarcar em si mesma.";
            return false;
        }

        if (data.transportSlots == null || slotIndex < 0 || slotIndex >= data.transportSlots.Count)
        {
            reason = "Slot de transporte invalido.";
            return false;
        }

        SyncTransportRuntimeSlotsWithData(data);
        if (!CanUseTransportSlotExclusivity(slotIndex, out reason))
            return false;

        UnitTransportSeatRuntime targetSeat = FindSeat(slotIndex, seatIndex);
        if (targetSeat == null)
        {
            reason = "Vaga de transporte invalida.";
            return false;
        }

        if (targetSeat.embarkedUnit != null && targetSeat.embarkedUnit != passenger)
        {
            reason = "Vaga ocupada.";
            return false;
        }

        UnitManager currentTransporter = passenger.EmbarkedTransporter;
        if (passenger.IsEmbarked && currentTransporter != null)
        {
            if (currentTransporter != this)
                currentTransporter.RemoveEmbarkedPassenger(passenger);
            else
                RemoveEmbarkedPassenger(passenger);
        }

        Vector3Int transporterCell = currentCellPosition;
        transporterCell.z = 0;
        passenger.SetCurrentCellPosition(transporterCell, enforceFinalOccupancyRule: false);
        passenger.AssignEmbarkTransport(this, slotIndex);
        if (!passenger.IsEmbarked)
            passenger.SetEmbarked(true);
        else
            passenger.SyncHierarchyForEmbarkedState();
        // Defensive refresh after reparent to transporter hierarchy:
        // guarantees embarked passenger visuals/HUD remain hidden.
        passenger.RefreshRuntimeVisualState();
        targetSeat.embarkedUnit = passenger;
        targetSeat.embarkedOnTurn = embarkedOnTurnOverride != int.MinValue
            ? embarkedOnTurnOverride
            : ResolveCurrentTurnNumber();
        RefreshSpriteForCurrentLayer(data);
        RefreshActedVisual();
        return true;
    }

    public bool TryDisembarkPassengerFromSeat(int slotIndex, int seatIndex, out UnitManager passenger, out string reason)
    {
        passenger = null;
        reason = string.Empty;

        UnitData data = TryGetUnitData();
        SyncTransportRuntimeSlotsWithData(data);
        UnitTransportSeatRuntime seat = FindSeat(slotIndex, seatIndex);
        if (seat == null)
        {
            reason = "Vaga de transporte invalida.";
            return false;
        }

        passenger = seat.embarkedUnit;
        if (passenger == null)
        {
            reason = "Vaga ja esta livre.";
            return false;
        }

        seat.embarkedUnit = null;
        seat.embarkedOnTurn = -1;
        passenger.SetEmbarked(false);
        RefreshSpriteForCurrentLayer();
        RefreshActedVisual();
        return true;
    }

    public bool RemoveEmbarkedPassenger(UnitManager passenger)
    {
        if (passenger == null || transportedUnitSlots == null)
            return false;

        bool removed = false;
        for (int i = 0; i < transportedUnitSlots.Count; i++)
        {
            UnitTransportSeatRuntime seat = transportedUnitSlots[i];
            if (seat == null || seat.embarkedUnit != passenger)
                continue;

            seat.embarkedUnit = null;
            seat.embarkedOnTurn = -1;
            removed = true;
        }

        if (removed)
        {
            RefreshSpriteForCurrentLayer();
            RefreshActedVisual();
        }

        return removed;
    }

    public void SyncLayerStateFromData(bool forceNativeDefault)
    {
        SyncCurrentLayerStateWithData(forceNativeDefault);
    }

    public void RefreshTransportSlotsFromData()
    {
        SyncTransportRuntimeSlotsWithData(TryGetUnitData());
        RefreshSpriteForCurrentLayer();
        RefreshActedVisual();
    }

    public void RefreshSupplierRuntimeFromData()
    {
        SyncSupplierRuntimeFromData(TryGetUnitData());
        RefreshActedVisual();
    }

    // Zera as reservas logisticas mantendo as entradas (tipo de suprimento e teto
    // continuam vindo da ficha; so a quantidade vai a zero). Usado no spawn de compra
    // quando a ficha marca startsWithEmptySupplies: a unidade sai da fabrica vazia e
    // precisa ser carregada por outro elo da cadeia. NAO chamar em load/refresh —
    // apagaria estoque conquistado em jogo.
    public void ClearSupplierReservesForFreshSpawn()
    {
        if (embarkedResourcesRuntime == null)
            return;

        for (int i = 0; i < embarkedResourcesRuntime.Count; i++)
        {
            UnitEmbarkedSupply entry = embarkedResourcesRuntime[i];
            if (entry != null)
                entry.amount = 0;
        }

        RefreshSupplierStockAlerts(true);
    }

    private void SyncEmbarkedWeaponsFromData(UnitData data)
    {
        if (embarkedWeaponsRuntime == null)
            embarkedWeaponsRuntime = new List<UnitEmbarkedWeapon>();

        embarkedWeaponsRuntime.Clear();
        if (data == null || data.embarkedWeapons == null)
            return;

        for (int i = 0; i < data.embarkedWeapons.Count; i++)
        {
            UnitEmbarkedWeapon source = data.embarkedWeapons[i];
            if (source == null || source.weapon == null)
                continue;

            UnitEmbarkedWeapon copy = new UnitEmbarkedWeapon
            {
                weapon = source.weapon,
                squadAmmunition = Mathf.Max(0, source.squadAmmunition),
                operationRangeMin = source.GetRangeMin(),
                operationRangeMax = source.GetRangeMax(),
                selectedTrajectory = source.selectedTrajectory,
                canBeFireOnlyAtDomainHeigh = source.canBeFireOnlyAtDomainHeigh != null
                    ? new List<UnitLayerMode>(source.canBeFireOnlyAtDomainHeigh)
                    : new List<UnitLayerMode>()
            };
            copy.EnsureValidSelectedTrajectory();
            embarkedWeaponsRuntime.Add(copy);
        }
    }

    private void SyncSupplierRuntimeFromData(UnitData data)
    {
        if (embarkedResourcesRuntime == null)
            embarkedResourcesRuntime = new List<UnitEmbarkedSupply>();
        if (embarkedServicesRuntime == null)
            embarkedServicesRuntime = new List<ServiceData>();
        if (currentlyObservedByTeamIds == null)
            currentlyObservedByTeamIds = new List<int>();
        for (int i = currentlyObservedByTeamIds.Count - 1; i >= 0; i--)
        {
            int team = currentlyObservedByTeamIds[i];
            if (team < -1 || team > 3)
                currentlyObservedByTeamIds.RemoveAt(i);
        }

        if (data == null || !data.isSupplier)
        {
            embarkedResourcesRuntime.Clear();
            embarkedServicesRuntime.Clear();
            return;
        }

        embarkedResourcesRuntime.Clear();
        List<UnitEmbarkedSupply> sourceResources = data.supplierResources;

        if (sourceResources != null)
        {
            for (int i = 0; i < sourceResources.Count; i++)
            {
                UnitEmbarkedSupply source = sourceResources[i];
                if (source == null || source.supply == null)
                    continue;

                UnitEmbarkedSupply copy = new UnitEmbarkedSupply
                {
                    supply = source.supply,
                    amount = Mathf.Max(0, source.amount)
                };
                embarkedResourcesRuntime.Add(copy);
            }
        }

        embarkedServicesRuntime.Clear();
        if (data.supplierServicesProvided == null)
            return;

        for (int i = 0; i < data.supplierServicesProvided.Count; i++)
        {
            ServiceData service = data.supplierServicesProvided[i];
            if (service == null || embarkedServicesRuntime.Contains(service))
                continue;
            embarkedServicesRuntime.Add(service);
        }
    }

    private void SyncTransportRuntimeSlotsWithData(UnitData data, bool preserveSeatPassengers = false)
    {
        if (transportedUnitSlots == null)
            transportedUnitSlots = new List<UnitTransportSeatRuntime>();

        if (data == null || !data.isTransporter || data.transportSlots == null || data.transportSlots.Count == 0)
        {
            transportedUnitSlots.Clear();
            return;
        }

        Dictionary<string, UnitTransportSeatRuntime> existing =
            new Dictionary<string, UnitTransportSeatRuntime>();
        for (int i = 0; i < transportedUnitSlots.Count; i++)
        {
            UnitTransportSeatRuntime seat = transportedUnitSlots[i];
            if (seat == null || seat.embarkedUnit == null)
                continue;
            if (!preserveSeatPassengers && !seat.embarkedUnit.IsEmbarked)
                continue;

            string key = BuildTransportSeatKey(seat.slotIndex, seat.seatIndex);
            existing[key] = seat;
        }

        transportedUnitSlots.Clear();

        for (int slotIndex = 0; slotIndex < data.transportSlots.Count; slotIndex++)
        {
            UnitTransportSlotRule slot = data.transportSlots[slotIndex];
            if (slot == null)
                continue;

            int capacity = Mathf.Max(1, slot.capacity);
            string slotId = !string.IsNullOrWhiteSpace(slot.slotId) ? slot.slotId : $"slot_{slotIndex}";
            for (int seatIndex = 0; seatIndex < capacity; seatIndex++)
            {
                UnitTransportSeatRuntime runtimeSeat = new UnitTransportSeatRuntime
                {
                    slotIndex = slotIndex,
                    slotId = slotId,
                    seatIndex = seatIndex
                };

                string key = BuildTransportSeatKey(slotIndex, seatIndex);
                if (existing.TryGetValue(key, out UnitTransportSeatRuntime previous)
                    && previous?.embarkedUnit != null
                    && previous.embarkedUnit.IsEmbarked)
                {
                    runtimeSeat.embarkedUnit = previous.embarkedUnit;
                    runtimeSeat.embarkedOnTurn = previous.embarkedOnTurn;
                }

                transportedUnitSlots.Add(runtimeSeat);
            }
        }
    }

    private UnitTransportSeatRuntime FindFirstFreeSeat(int slotIndex)
    {
        if (transportedUnitSlots == null)
            return null;

        for (int i = 0; i < transportedUnitSlots.Count; i++)
        {
            UnitTransportSeatRuntime seat = transportedUnitSlots[i];
            if (seat == null || seat.slotIndex != slotIndex)
                continue;

            if (seat.embarkedUnit != null && !seat.embarkedUnit.IsEmbarked)
            {
                seat.embarkedUnit = null;
                seat.embarkedOnTurn = -1;
            }

            if (seat.embarkedUnit == null)
                return seat;
        }

        return null;
    }

    public bool CanUseTransportSlotExclusivity(int slotIndex, out string reason)
    {
        reason = string.Empty;
        UnitData data = TryGetUnitData();
        if (data == null || data.transportSlots == null || slotIndex < 0 || slotIndex >= data.transportSlots.Count)
        {
            reason = "Slot de transporte invalido.";
            return false;
        }

        SyncTransportRuntimeSlotsWithData(data);
        UnitTransportSlotRule requestedSlot = data.transportSlots[slotIndex];
        if (requestedSlot == null)
        {
            reason = "Slot de transporte invalido.";
            return false;
        }

        for (int i = 0; i < transportedUnitSlots.Count; i++)
        {
            UnitTransportSeatRuntime seat = transportedUnitSlots[i];
            if (seat == null || seat.embarkedUnit == null)
                continue;

            if (!seat.embarkedUnit.IsEmbarked)
            {
                seat.embarkedUnit = null;
                seat.embarkedOnTurn = -1;
                continue;
            }

            if (seat.slotIndex == slotIndex)
                continue;

            bool occupiedSlotIsExclusive = seat.slotIndex >= 0
                && seat.slotIndex < data.transportSlots.Count
                && data.transportSlots[seat.slotIndex] != null
                && data.transportSlots[seat.slotIndex].exclusiveSlot;

            if (requestedSlot.exclusiveSlot || occupiedSlotIsExclusive)
            {
                reason = requestedSlot.exclusiveSlot
                    ? "Slot exclusivo indisponivel enquanto houver carga em outra vaga."
                    : "Outra vaga exclusiva do transportador ja esta em uso.";
                return false;
            }
        }

        return true;
    }

    private UnitTransportSeatRuntime FindSeat(int slotIndex, int seatIndex)
    {
        if (transportedUnitSlots == null)
            return null;

        for (int i = 0; i < transportedUnitSlots.Count; i++)
        {
            UnitTransportSeatRuntime seat = transportedUnitSlots[i];
            if (seat == null)
                continue;
            if (seat.slotIndex == slotIndex && seat.seatIndex == seatIndex)
                return seat;
        }

        return null;
    }

    private static string BuildTransportSeatKey(int slotIndex, int seatIndex)
    {
        return slotIndex.ToString() + ":" + seatIndex.ToString();
    }

    private UnitLayerMode[] BuildLayerModesSnapshot()
    {
        if (unitDatabase == null || string.IsNullOrWhiteSpace(unitId) || !unitDatabase.TryGetById(unitId, out UnitData data) || data == null)
            return new[] { new UnitLayerMode(Domain.Land, HeightLevel.Surface) };

        int additionalCount = data.aditionalDomainsAllowed != null ? data.aditionalDomainsAllowed.Count : 0;
        UnitLayerMode[] modes = new UnitLayerMode[1 + additionalCount];
        modes[0] = new UnitLayerMode(data.domain, data.heightLevel);

        for (int i = 0; i < additionalCount; i++)
            modes[i + 1] = data.aditionalDomainsAllowed[i];

        return modes;
    }

    private void SyncCurrentLayerStateWithData(bool forceNativeDefault)
    {
        UnitLayerMode[] modes = BuildLayerModesSnapshot();
        SyncCurrentLayerStateWithModes(modes, forceNativeDefault);
    }

    private void SyncCurrentLayerStateWithData(UnitData data, bool forceNativeDefault)
    {
        UnitLayerMode[] modes = BuildLayerModesSnapshot(data);
        SyncCurrentLayerStateWithModes(modes, forceNativeDefault);
    }

    private void SyncCurrentLayerStateWithModes(UnitLayerMode[] modes, bool forceNativeDefault)
    {
        if (modes.Length == 0)
        {
            SetCurrentLayerState(0, new UnitLayerMode(Domain.Land, HeightLevel.Surface));
            return;
        }

        if (forceNativeDefault || !layerStateInitialized)
        {
            SetCurrentLayerState(0, modes[0]);
            return;
        }

        for (int i = 0; i < modes.Length; i++)
        {
            if (modes[i].domain == currentDomain && modes[i].heightLevel == currentHeightLevel)
            {
                SetCurrentLayerState(i, modes[i]);
                return;
            }
        }

        SetCurrentLayerState(0, modes[0]);
    }

    private static UnitLayerMode[] BuildLayerModesSnapshot(UnitData data)
    {
        if (data == null)
            return new[] { new UnitLayerMode(Domain.Land, HeightLevel.Surface) };

        int additionalCount = data.aditionalDomainsAllowed != null ? data.aditionalDomainsAllowed.Count : 0;
        UnitLayerMode[] modes = new UnitLayerMode[1 + additionalCount];
        modes[0] = new UnitLayerMode(data.domain, data.heightLevel);

        for (int i = 0; i < additionalCount; i++)
            modes[i + 1] = data.aditionalDomainsAllowed[i];

        return modes;
    }

    private int ResolveLayerModeIndex(Domain domain, HeightLevel heightLevel)
    {
        UnitLayerMode[] modes = BuildLayerModesSnapshot();
        for (int i = 0; i < modes.Length; i++)
        {
            if (modes[i].domain == domain && modes[i].heightLevel == heightLevel)
                return i;
        }

        return 0;
    }

    private void SetCurrentLayerState(int modeIndex, UnitLayerMode mode)
    {
        HeightBand previousBand = OccupancyResolver.GetHeightBand(currentDomain, currentHeightLevel);
        bool wasInitialized = layerStateInitialized;

        currentLayerModeIndex = Mathf.Max(0, modeIndex);
        currentDomain = mode.domain;
        currentHeightLevel = mode.heightLevel;
        layerStateInitialized = true;
        RefreshSpriteForCurrentLayer();
        RefreshActedVisual();

        // Mudanca de banda no mesmo hex (decolar/pousar/desfazer decolagem) precisa
        // re-disparar a ocupacao para o visual de coabitacao e listeners atualizarem.
        // Sem isso o "divide o hex ao meio" fica preso no estado anterior.
        if (Application.isPlaying && wasInitialized)
        {
            HeightBand newBand = OccupancyResolver.GetHeightBand(currentDomain, currentHeightLevel);
            if (newBand != previousBand)
            {
                Vector3Int cell = currentCellPosition;
                cell.z = 0;
                UnitOccupancyRules.NotifyUnitOccupancyChanged(this, cell, cell);
            }
        }
    }

    private UnitData TryGetUnitData()
    {
        if (unitDatabase == null || string.IsNullOrWhiteSpace(unitId))
            return null;
        if (!unitDatabase.TryGetById(unitId, out UnitData data))
            return null;
        return data;
    }

    public void RefreshSpriteForCurrentLayer()
    {
        RefreshSpriteForCurrentLayer(TryGetUnitData());
    }

    private void RefreshSpriteForCurrentLayer(UnitData data)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer == null || data == null)
            return;

        bool preferTransportSprite = ShouldUseTransportSprite(data);
        Sprite baseTeamSprite = TeamUtils.GetTeamSprite(data, teamId, preferTransportSprite);
        Sprite finalSprite = baseTeamSprite;
        if (currentLayerModeIndex > 0 && data.aditionalDomainsAllowed != null)
        {
            int additionalIndex = currentLayerModeIndex - 1;
            if (additionalIndex >= 0 && additionalIndex < data.aditionalDomainsAllowed.Count)
            {
                UnitLayerMode mode = data.aditionalDomainsAllowed[additionalIndex];
                Sprite layerSprite = TeamUtils.GetTeamSprite(mode, teamId, baseTeamSprite);
                if (layerSprite != null)
                    finalSprite = layerSprite;
            }
        }

        if (finalSprite != null)
            spriteRenderer.sprite = finalSprite;

        spriteRenderer.color = TeamUtils.GetColor(teamId);
        ApplyTeamVisualFlipFromMatchController();
    }

    private bool ShouldUseTransportSprite(UnitData data)
    {
        if (data == null || !data.isTransporter || data.spriteTransport == null)
            return false;
        return HasAnyEmbarkedPassenger(data);
    }

    private bool HasAnyEmbarkedPassenger(UnitData data)
    {
        return CountEmbarkedPassengers(data) > 0;
    }

    public int GetEmbarkedPassengerCount()
    {
        return CountEmbarkedPassengers(TryGetUnitData());
    }

    private int CountEmbarkedPassengers(UnitData data)
    {
        if (data == null || !data.isTransporter)
            return 0;

        SyncTransportRuntimeSlotsWithData(data, preserveSeatPassengers: !Application.isPlaying);
        int count = 0;
        for (int i = 0; i < transportedUnitSlots.Count; i++)
        {
            UnitTransportSeatRuntime seat = transportedUnitSlots[i];
            if (seat == null || seat.embarkedUnit == null)
                continue;

            if (seat.embarkedUnit.IsEmbarked)
            {
                count++;
                continue;
            }

            if (Application.isPlaying)
            {
                seat.embarkedUnit = null;
                seat.embarkedOnTurn = -1;
            }
        }

        return count;
    }

    public void SetCurrentPosition(Vector3 position)
    {
        currentPosition = position;
        transform.position = position;
        if (boardTilemap != null)
        {
            currentCellPosition = HexCoordinates.WorldToCell(boardTilemap, position);
            SyncEmbarkedPassengersCellPosition();
        }
    }

    private void HandleSlotConfigChanged()
    {
        ResolveTeamIdFromSlot();
    }

    // Resolve teamId a partir do slotIndex no MatchController.
    // Sem efeito se slotIndex == -1 (fixo) ou se nao ha MatchController na cena.
    private void ResolveTeamIdFromSlot()
    {
        if (slotIndex < 0)
            return;

        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();

        if (matchController == null)
            return;

        TeamId resolved = matchController.GetTeamIdForSlot(slotIndex);
        if (teamId == resolved)
            return;

        TeamId previousTeam = teamId;
        teamId = resolved;
        if (!ApplyFromDatabase())
            UpdateDynamicName();
        RefreshActedVisual();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public void SetTeamId(TeamId team)
    {
        TeamId previousTeam = teamId;
        teamId = team;
        if (!ApplyFromDatabase())
        {
            RefreshSpriteForCurrentLayer();
            UpdateDynamicName();
        }
        RefreshActedVisual();
        if (Application.isPlaying)
        {
            Vector3Int cell = currentCellPosition;
            cell.z = 0;
            UnitOccupancyRules.NotifyUnitOccupancyChanged(this, cell, cell);
        }
    }

    public void ApplyTeamVisualFlipX(bool flipX)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
            return;

        spriteRenderer.flipX = flipX;
    }

    public void AssignSpawnInstanceId(int id)
    {
        if (id <= 0)
            return;

        instanceId = id;
        UpdateDynamicName();
    }

    public void SetBoardTilemap(Tilemap tilemap)
    {
        boardTilemap = tilemap;
        SyncPositionState();
    }

    public void SetMatchController(MatchController mc)
    {
        matchController = mc;
    }

    public void SetCurrentCellPosition(Vector3Int cell, bool enforceFinalOccupancyRule = true)
    {
        Vector3Int previousCell = currentCellPosition;
        if (enforceFinalOccupancyRule && Application.isPlaying && boardTilemap != null)
        {
            Vector3Int target = cell;
            target.z = 0;
            if (UnitRulesDefinition.IsUnitCellOccupied(boardTilemap, target, this))
            {
                Debug.LogWarning($"[UnitManager] Destino bloqueado: hex ({target.x},{target.y},0) ja possui unidade.", this);
                return;
            }
        }

        currentCellPosition = cell;
        SnapToCellCenter();
        SyncEmbarkedPassengersCellPosition();
        ThreatRevisionTracker.NotifyUnitCellChanged(this, previousCell, currentCellPosition);
        if (Application.isPlaying)
            UnitOccupancyRules.NotifyUnitOccupancyChanged(this, previousCell, currentCellPosition);
    }

    private void SyncEmbarkedPassengersCellPosition()
    {
        if (transportedUnitSlots == null || transportedUnitSlots.Count <= 0)
            return;

        Vector3Int transporterCell = currentCellPosition;
        transporterCell.z = 0;

        for (int i = 0; i < transportedUnitSlots.Count; i++)
        {
            UnitTransportSeatRuntime seat = transportedUnitSlots[i];
            if (seat == null || seat.embarkedUnit == null)
                continue;

            UnitManager passenger = seat.embarkedUnit;
            if (!passenger.IsEmbarked || passenger.EmbarkedTransporter != this)
                continue;

            Vector3Int passengerCell = passenger.CurrentCellPosition;
            passengerCell.z = 0;
            if (passengerCell == transporterCell)
                continue;

            passenger.SetCurrentCellPosition(transporterCell, enforceFinalOccupancyRule: false);
        }
    }

    public void SetEmbarked(bool embarked)
    {
        bool previousEmbarked = isEmbarked;
        if (isEmbarked == embarked)
            return;

        isEmbarked = embarked;
        if (!isEmbarked)
        {
            embarkedVisualPreviewDepth = 0;
            embarkedAtUnit = string.Empty;
        }
        if (isEmbarked)
        {
            SetSelected(false);
            SetSpriteVisible(false);
            SetHudVisible(false);
            SetOwnedUiVisualsVisible(false);
            SyncHierarchyForEmbarkedState();
            if (actedLockRenderer != null)
                actedLockRenderer.enabled = false;
            RefreshDetectedIndicator();
            ThreatRevisionTracker.NotifyUnitEmbarkStateChanged(this, previousEmbarked, isEmbarked);
            if (Application.isPlaying)
            {
                Vector3Int cell = currentCellPosition;
                cell.z = 0;
                UnitOccupancyRules.NotifyUnitOccupancyChanged(this, cell, cell);
            }
            return;
        }

        if (embarkedTransporter != null)
            embarkedTransporter.RemoveEmbarkedPassenger(this);
        ClearEmbarkTransport();
        SyncHierarchyForEmbarkedState();

        SetSpriteVisible(true);
        SetHudVisible(true);
        SetOwnedUiVisualsVisible(true);
        RefreshActedVisual();
        RefreshDetectedIndicator();
        ThreatRevisionTracker.NotifyUnitEmbarkStateChanged(this, previousEmbarked, isEmbarked);
        if (Application.isPlaying)
        {
            Vector3Int cell = currentCellPosition;
            cell.z = 0;
            UnitOccupancyRules.NotifyUnitOccupancyChanged(this, cell, cell);
        }
    }

    private void AssignEmbarkTransport(UnitManager transporter, int slotIndex)
    {
        embarkedTransporter = transporter;
        embarkedTransporterSlotIndex = slotIndex;
        embarkedAtUnit = transporter != null ? transporter.ResolveRuntimeUnitName() : string.Empty;
        if (isEmbarked)
            SyncHierarchyForEmbarkedState();
    }

    private void ClearEmbarkTransport()
    {
        embarkedTransporter = null;
        embarkedTransporterSlotIndex = -1;
        embarkedAtUnit = string.Empty;
    }

    private void SyncHierarchyForEmbarkedState()
    {
        // Keep units independent in hierarchy even when embarked.
        // Embark linkage is controlled by runtime references/slots only.
        if (embarkedTransporter != null && transform.parent == embarkedTransporter.transform)
            transform.SetParent(null, true);
    }

    public void SnapToCellCenter()
    {
        if (boardTilemap == null)
        {
            currentPosition = transform.position;
            return;
        }

        Vector3 snapped = HexCoordinates.GetCellCenterWorld(boardTilemap, currentCellPosition);
        snapped += _cohabitationOffset;
        transform.position = snapped;
        currentPosition = snapped;
    }

    internal void ApplyCohabitationVisual(
        Vector3 positionOffset,
        Vector3 scale,
        float hudOffsetY)
    {
        if (!_hasCohabitationVisual)
            _cohabitationPreScale = transform.localScale;
        _hasCohabitationVisual = true;
        _cohabitationOffset = positionOffset;
        transform.localScale = scale;
        SnapToCellCenter();
        if (unitHud != null)
            unitHud.ApplyCohabitationHudOffset(hudOffsetY);
    }

    internal void ClearCohabitationVisual()
    {
        if (!_hasCohabitationVisual)
            return;
        _hasCohabitationVisual = false;
        transform.localScale = _cohabitationPreScale;
        _cohabitationOffset = Vector3.zero;
        SnapToCellCenter();
        if (unitHud != null)
            unitHud.ClearCohabitationHudOffset();
    }

    public void PullCellFromTransform()
    {
        currentPosition = transform.position;
        if (boardTilemap != null)
            currentCellPosition = HexCoordinates.WorldToCell(boardTilemap, currentPosition);
    }

    private void EnsureDefaults()
    {
        if ((int)teamId < -1 || (int)teamId > 3)
            teamId = TeamId.Green;

        if (string.IsNullOrWhiteSpace(unitId) && unitDatabase != null && unitDatabase.TryGetFirst(out UnitData first) && first != null)
            unitId = first.id;

        if (!IsFinite(currentPosition))
            currentPosition = Vector3.zero;

        if (instanceId < 0)
            instanceId = 0;

        maxAmmo = Mathf.Max(1, maxAmmo);
        maxFuel = Mathf.Max(1, maxFuel);
        visao = Mathf.Max(1, visao);
        currentAmmo = Mathf.Clamp(currentAmmo, 0, maxAmmo);
        currentFuel = Mathf.Clamp(currentFuel, 0, maxFuel);
        if (embarkedResourcesRuntime == null)
            embarkedResourcesRuntime = new List<UnitEmbarkedSupply>();
        if (embarkedServicesRuntime == null)
            embarkedServicesRuntime = new List<ServiceData>();

        UnitData data = TryGetUnitData();
        int maxMovement = data != null ? Mathf.Max(0, data.movement) : Mathf.Max(0, remainingMovementPoints);
        if (!hasActed)
            remainingMovementPoints = maxMovement;
        else
            remainingMovementPoints = Mathf.Clamp(remainingMovementPoints, 0, maxMovement);
        SyncTransportRuntimeSlotsWithData(data, preserveSeatPassengers: !Application.isPlaying);
        RestoreEditorEmbarkedStateFromSeats(data);
        SyncPreferredLayerPreferencesFromData(TryGetUnitData());
        SyncCurrentLayerStateWithData(forceNativeDefault: false);
    }

    private void RestoreEditorEmbarkedStateFromSeats(UnitData data)
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            return;
        if (data == null || !data.isTransporter || isEmbarked)
            return;

        for (int i = 0; i < transportedUnitSlots.Count; i++)
        {
            UnitTransportSeatRuntime seat = transportedUnitSlots[i];
            if (seat == null || seat.embarkedUnit == null || seat.embarkedUnit == this)
                continue;

            UnitManager passenger = seat.embarkedUnit;
            passenger.isEmbarked = true;
            passenger.AssignEmbarkTransport(this, seat.slotIndex);
            passenger.SyncHierarchyForEmbarkedState();
            passenger.SetSelected(false);
            passenger.SetSpriteVisible(false);
            if (passenger.unitHud != null)
                passenger.HideHudForEditorEmbarkedPreview();
            if (passenger.actedLockRenderer != null)
                passenger.actedLockRenderer.enabled = false;
            passenger.RefreshActedVisual();
        }
#endif
    }

#if UNITY_EDITOR
    private void HideHudForEditorEmbarkedPreview()
    {
        SetHudVisible(false);
    }
#endif

    private void SetHudVisible(bool visible)
    {
        if (unitHud == null)
            TryAutoAssignHud();

        // Passenger embarked must keep HUD hidden unless an explicit visual
        // preview is active (e.g. temporary supply animation preview).
        if (isEmbarked && visible && !IsEmbarkedVisualPreviewActive)
            visible = false;
        // FoW does not override an active visual preview (e.g. supply animation showing an embarked unit).
        if (hiddenByFogOfWar && visible && !(isEmbarked && IsEmbarkedVisualPreviewActive))
            visible = false;

        ApplyOwnedHudVisibility(visible);

        if (visible)
            RefreshHudWidgetsOnly();
    }

    private void ApplyOwnedHudVisibility(bool visible)
    {
        bool anyOwnedHud = false;
        UnitHudController[] ownedHuds = GetComponentsInChildren<UnitHudController>(true);
        for (int i = 0; i < ownedHuds.Length; i++)
        {
            UnitHudController hud = ownedHuds[i];
            if (hud == null)
                continue;

            UnitManager owner = hud.ResolveOwnerUnit();
            if (owner != this)
                continue;

            hud.gameObject.SetActive(visible);
            anyOwnedHud = true;
        }

        if (!anyOwnedHud && unitHud != null)
            unitHud.gameObject.SetActive(visible);
    }

    private void RefreshHudWidgetsOnly()
    {
        if (unitHud == null || (isEmbarked && !IsEmbarkedVisualPreviewActive))
            return;

        TryAutoAssignMatchController();
        UnitData unitData = TryGetUnitData();
        bool showTransportIndicator = HasAnyEmbarkedPassenger(unitData);
        bool showDetectedIndicator = ShouldShowDetectedIndicator(unitData);
        Color teamColor = TeamUtils.GetColor(teamId);
        unitHud.RefreshBindings();
        unitHud.Apply(
            currentHP,
            GetMaxHP(),
            currentAmmo,
            GetMaxAmmo(),
            currentFuel,
            GetMaxFuel(),
            teamColor,
            currentDomain,
            currentHeightLevel,
            showTransportIndicator,
            showDetectedIndicator);
    }

    private void SyncPreferredLayerPreferencesFromData(UnitData data)
    {
        if (data == null)
        {
            useExplicitPreferredAirHeightRuntime = false;
            preferredAirHeightRuntime = HeightLevel.AirLow;
            useExplicitPreferredNavalHeightRuntime = false;
            preferredNavalHeightRuntime = HeightLevel.Submerged;
            return;
        }

        useExplicitPreferredAirHeightRuntime = data.useExplicitPreferredAirHeight;
        preferredAirHeightRuntime = data.preferredAirHeight == HeightLevel.AirHigh ? HeightLevel.AirHigh : HeightLevel.AirLow;
        useExplicitPreferredNavalHeightRuntime = data.useExplicitPreferredNavalHeight;
        preferredNavalHeightRuntime = data.preferredNavalHeight == HeightLevel.Surface ? HeightLevel.Surface : HeightLevel.Submerged;
    }

    private void SyncPositionState()
    {
        if (boardTilemap == null)
        {
            TryAutoAssignBoardTilemap();
        }

        if (boardTilemap == null)
        {
            currentPosition = transform.position;
            return;
        }

        if (snapToCellCenter)
            SnapToCellCenter();
        else
            PullCellFromTransform();
    }

    private void TryAutoAssignBoardTilemap()
    {
        if (boardTilemap != null &&
            string.Equals(boardTilemap.name, "TileMap", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Avoid trying to bind scene references while editing the prefab asset itself.
        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            return;

        Tilemap namedBoard = FindTilemapByName("TileMap");
        if (namedBoard != null)
        {
            boardTilemap = namedBoard;
            return;
        }

        if (boardTilemap != null)
            return;

        Tilemap[] maps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] == null)
                continue;

            GridLayout.CellLayout layout = maps[i].layoutGrid != null ? maps[i].layoutGrid.cellLayout : GridLayout.CellLayout.Rectangle;
            if (layout == GridLayout.CellLayout.Hexagon)
            {
                boardTilemap = maps[i];
                return;
            }
        }
    }

    private static Tilemap FindTilemapByName(string expectedName)
    {
        if (string.IsNullOrWhiteSpace(expectedName))
            return null;

        Tilemap[] maps = FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;

            if (string.Equals(map.name, expectedName, System.StringComparison.OrdinalIgnoreCase))
                return map;
        }

        return null;
    }

    private void TryAutoAssignMatchController()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
    }

    private void ApplyTeamVisualFlipFromMatchController()
    {
        TryAutoAssignMatchController();
        if (matchController == null)
            return;

        ApplyTeamVisualFlipX(matchController.GetTeamFlipX(teamId));
    }

    private static bool IsFinite(Vector3 v)
    {
        return float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }

    private void UpdateDynamicName()
    {
        string baseName = !string.IsNullOrWhiteSpace(unitDisplayName)
            ? unitDisplayName.Trim()
            : (!string.IsNullOrWhiteSpace(unitId) ? unitId.Trim() : "unit");

        string slotSuffix = slotIndex >= 0 ? $"_T{slotIndex}" : string.Empty;
        string instanceSuffix = instanceId > 0 ? $"_U{instanceId}" : string.Empty;
        gameObject.name = $"{baseName}{slotSuffix}{instanceSuffix}";
    }

    private void SyncRuntimeFlagInspectorView()
    {
        flagHasActed = hasActed;
        flagIsDead = isDead;
        flagDeadWhenTurn = deadWhenTurn;
        flagDeadByReason = deadByReason;
        flagDiedByUnit = diedByUnit;
        flagIsEmbarked = isEmbarked;
        flagEmbarkedAtUnit = embarkedAtUnit;
        flagReceivedSupplies = receivedSuppliesThisTurn;
        flagTookOffRecently = tookOffRecently;
        flagSurfacedForSupplyThisTurn = surfacedForSupplyThisTurn;
        flagAircraftForcedLandingAwaitingRefuel = AircraftForcedLandingAwaitingRefuel;
        flagHasMerged = hasMerged;
        flagMergedWhenTurn = mergedWhenTurn;
        flagMergedWithUnit = mergedWithUnit;
    }
    private void SyncDeadFlagFromHp()
    {
        bool shouldBeDead = currentHP <= 0;
        if (isDead == shouldBeDead)
        {
            if (!shouldBeDead &&
                (!string.IsNullOrEmpty(diedByUnit) || !string.IsNullOrEmpty(deadByReason) || deadWhenTurn >= 0))
            {
                deadWhenTurn = -1;
                deadByReason = string.Empty;
                diedByUnit = string.Empty;
                UpdateDynamicName();
            }
            return;
        }

        isDead = shouldBeDead;
        if (isDead)
        {
            if (deadWhenTurn < 0)
                deadWhenTurn = ResolveCurrentTurnNumber();
            if (string.IsNullOrWhiteSpace(deadByReason))
                deadByReason = "(unknown)";
            if (string.IsNullOrWhiteSpace(diedByUnit))
                diedByUnit = "(unknown)";
        }
        else
        {
            deadWhenTurn = -1;
            deadByReason = string.Empty;
            diedByUnit = string.Empty;
        }

        UpdateDynamicName();
    }

    private int ResolveCurrentTurnNumber()
    {
        TryAutoAssignMatchController();
        return matchController != null ? matchController.CurrentTurn : -1;
    }

    private static string ResolveKillerAuditId(UnitManager killer)
    {
        if (killer == null)
            return string.Empty;

        string baseName = !string.IsNullOrWhiteSpace(killer.UnitDisplayName)
            ? killer.UnitDisplayName.Trim()
            : (!string.IsNullOrWhiteSpace(killer.UnitId) ? killer.UnitId.Trim() : "Unit");
        if (killer.SlotIndex >= 0 && killer.InstanceId > 0)
            return $"{baseName}_T{killer.SlotIndex}_U{killer.InstanceId}";
        if (killer.InstanceId > 0)
            return $"instance:{killer.InstanceId}";
        return string.Empty;
    }

    private void TryAutoAssignLockRenderer()
    {
        if (actedLockRenderer != null)
            return;

        Transform lockChild = transform.Find("ActedLock");
        if (lockChild == null)
            return;

        actedLockRenderer = lockChild.GetComponent<SpriteRenderer>();
    }

    private void TryAutoAssignHud()
    {
        if (unitHud != null)
            return;

        UnitHudController[] candidates = GetComponentsInChildren<UnitHudController>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            UnitHudController candidate = candidates[i];
            if (candidate == null)
                continue;

            UnitManager owner = candidate.GetComponentInParent<UnitManager>();
            if (owner == this)
            {
                unitHud = candidate;
                return;
            }
        }
    }

    private void CacheSpriteMaterial()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null && defaultSpriteMaterial == null)
            defaultSpriteMaterial = spriteRenderer.sharedMaterial;

        if (spritePropertyBlock == null)
            spritePropertyBlock = new MaterialPropertyBlock();
    }

    private static Material GetActedGlowMaterial()
    {
        if (actedGlowMaterial != null)
            return actedGlowMaterial;

        Shader shader = Shader.Find("Custom/SpriteGlowOutline");
        if (shader == null)
            return null;

        actedGlowMaterial = new Material(shader)
        {
            name = "Runtime_UnitActedGlow"
        };
        return actedGlowMaterial;
    }

    private void SetActedGlowEnabled(bool enabled)
    {
        if (spriteRenderer == null)
            return;

        CacheSpriteMaterial();
        Material glowMaterial = GetActedGlowMaterial();
        if (enabled && glowMaterial != null)
        {
            spriteRenderer.sharedMaterial = glowMaterial;
            spriteRenderer.GetPropertyBlock(spritePropertyBlock);
            spritePropertyBlock.SetColor(GlowColorId, actedGlowColor);
            spritePropertyBlock.SetFloat(GlowSizeId, actedGlowSize);
            spritePropertyBlock.SetFloat(GlowStrengthId, actedGlowStrength);
            spriteRenderer.SetPropertyBlock(spritePropertyBlock);
        }
        else
        {
            if (defaultSpriteMaterial != null)
            {
                spriteRenderer.sharedMaterial = defaultSpriteMaterial;
            }
            else if (spriteRenderer.sharedMaterial == glowMaterial)
            {
                // Fallback: volta para o material padrao do SpriteRenderer.
                spriteRenderer.sharedMaterial = null;
            }

            spriteRenderer.SetPropertyBlock(null);
        }
    }

    private void DisableLegacyOutlineObjects()
    {
        Transform legacy = transform.Find("ActedOutline");
        if (legacy != null && legacy.gameObject.activeSelf)
            legacy.gameObject.SetActive(false);

        for (int i = 0; i < 4; i++)
        {
            Transform old = transform.Find($"ActedOutline_{i}");
            if (old != null && old.gameObject.activeSelf)
                old.gameObject.SetActive(false);
        }
    }

    private void RefreshActedVisual()
    {
#if UNITY_EDITOR
        if (IsEditingPrefabContext())
            return;
#endif

        if (isEmbarked && !IsEmbarkedVisualPreviewActive)
        {
            SetActedGlowEnabled(false);
            SetSpriteVisible(false);
            SetHudVisible(false);
            SetOwnedUiVisualsVisible(false);
            if (actedLockRenderer != null)
                actedLockRenderer.enabled = false;
            return;
        }

        // Death presentation can disable renderers directly. When a snapshot revives
        // the unit, this guarantees sprite/hud come back before color/state updates.
        SetSpriteVisible(true);
        SetHudVisible(true);
        SetOwnedUiVisualsVisible(true);

        TryAutoAssignMatchController();
        Color teamColor = TeamUtils.GetColor(teamId);
        bool isActiveTeamUnit = matchController != null && (int)teamId == matchController.ActiveTeamId;
        UnitData unitData = TryGetUnitData();
        bool showTransportIndicator = HasAnyEmbarkedPassenger(unitData);
        bool showDetectedIndicator = ShouldShowDetectedIndicator(unitData);

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // Unidade fora do time ativo nunca escurece e nunca recebe glow de "ja agiu".
        if (!isActiveTeamUnit)
        {
            if (spriteRenderer != null)
                spriteRenderer.color = ResolvePreviewDimmedColor(teamColor);

            SetActedGlowEnabled(false);

            if (unitHud != null)
            {
                unitHud.Apply(
                    currentHP,
                    GetMaxHP(),
                    currentAmmo,
                    GetMaxAmmo(),
                    currentFuel,
                    GetMaxFuel(),
                    teamColor,
                    currentDomain,
                    currentHeightLevel,
                    showTransportIndicator,
                    showDetectedIndicator
                );
            }

            if (actedLockRenderer != null)
                actedLockRenderer.enabled = false;

            ApplyFogOfWarVisibility();
            return;
        }

        bool shouldHighlightActed = hasActed;

        if (spriteRenderer != null)
        {
            Color grayMixed = Color.Lerp(teamColor, Color.gray, Mathf.Clamp01(actedGrayBlend));
            Color unitColor = shouldHighlightActed
                ? new Color(grayMixed.r * Mathf.Clamp01(actedDarkenFactor), grayMixed.g * Mathf.Clamp01(actedDarkenFactor), grayMixed.b * Mathf.Clamp01(actedDarkenFactor), teamColor.a)
                : teamColor;
            spriteRenderer.color = ResolvePreviewDimmedColor(unitColor);
        }

        SetActedGlowEnabled(shouldHighlightActed);

        if (unitHud != null)
        {
            unitHud.Apply(
                currentHP,
                GetMaxHP(),
                currentAmmo,
                GetMaxAmmo(),
                currentFuel,
                GetMaxFuel(),
                teamColor,
                currentDomain,
                currentHeightLevel,
                showTransportIndicator,
                showDetectedIndicator
            );
        }

        if (actedLockRenderer != null)
            actedLockRenderer.enabled = false;

        ApplyFogOfWarVisibility();
    }

    private Color ResolvePreviewDimmedColor(Color baseColor)
    {
        if (!isPreviewDimmed)
            return baseColor;

        Color grayMixed = Color.Lerp(baseColor, Color.gray, Mathf.Clamp01(previewDimGrayBlend));
        return new Color(
            grayMixed.r * Mathf.Clamp01(previewDimDarkenFactor),
            grayMixed.g * Mathf.Clamp01(previewDimDarkenFactor),
            grayMixed.b * Mathf.Clamp01(previewDimDarkenFactor),
            baseColor.a);
    }

    private bool ShouldShowDetectedIndicator(UnitData unitData)
    {
        // INTENCIONAL: sobrecarga SEM camada. O Olho significa "unidade com skill
        // de ocultacao foi detectada", nao "foi detectada apesar do stealth ativo".
        // A camada e onde a skill foi feita para funcionar, nao condicao do aviso.
        // Caca F em Air/Low no alcance do SAM, ou pousado e visto por um soldado,
        // PRECISA do Olho: esconder ali faz o jogador se achar seguro sem estar.
        // Nao trocar por IsStealthUnit(GetDomain(), GetHeightLevel()).
        if (unitData == null || !unitData.IsStealthUnit())
            return false;

        if (currentlyObservedByTeamIds == null || currentlyObservedByTeamIds.Count <= 0)
            return false;

        int ownerTeamId = (int)teamId;
        for (int i = 0; i < currentlyObservedByTeamIds.Count; i++)
        {
            int observerTeamId = currentlyObservedByTeamIds[i];
            if (observerTeamId < -1 || observerTeamId > 3)
                continue;
            if (observerTeamId == ownerTeamId)
                continue;

            return true;
        }

        return false;
    }

    private void RefreshDetectedIndicator()
    {
        if (unitHud == null)
            TryAutoAssignHud();
        if (unitHud == null || !unitHud.gameObject.activeInHierarchy)
            return;

        bool shouldShow = ShouldShowDetectedIndicator(TryGetUnitData());
        unitHud.SetDetectedIndicatorVisible(shouldShow);
    }

    private void RefreshAIAssignedPlanBadge()
    {
        if (unitHud == null)
            TryAutoAssignHud();
        if (unitHud == null)
            return;

        // Stance icon is independent of the plan badge.
        unitHud.SetStanceIcon(aiHasStance && aiStanceVisible ? aiStanceIcon : null);
        bool isAIUnit = matchController != null &&
            matchController.IsPlayerAI(PlayerSlotId.FromIndex(SlotIndex));
        // O icone de manutencao e informacao de depuracao da IA, assim como o
        // badge de eixo. Nao deve vazar para o HUD normal quando a flag global
        // "Show AI Unit HUD" estiver desligada.
        unitHud.SetMaintenanceIconVisible(aiMaintenanceActive && isAIUnit && AIController.ShowAIHUD);

        // Plan badge: only show when a plan is actually assigned.
        bool hasPlan = aiHasAssignedPlan && aiAssignedPlanBadgeVisible
            && !string.IsNullOrWhiteSpace(aiAssignedPlanBadge);
        unitHud.SetPlanDebugBadge(hasPlan, hasPlan ? aiAssignedPlanBadge : string.Empty);

        // Eixo badge: o eixo PERSISTE como memoria entre objetivos (nao depende de ter plano
        // ativo), entao a bandeirola fica visivel enquanto a unidade tiver eixo, for da AI e o
        // flag global "Show AI Unit HUD" estiver ligado.
        bool showEixo = aiEixo >= 1 && aiEixo <= 3 && isAIUnit && AIController.ShowAIHUD;
        unitHud.SetEixoBadge(showEixo ? aiEixo : 0, showEixo);
    }

    private void HandleActiveTeamChanged(int newTeamId)
    {
        if (appliedActiveTeamId == newTeamId)
            return;

        double startMs = Time.realtimeSinceStartupAsDouble * 1000d;
        int previousActiveTeamId = appliedActiveTeamId;
        appliedActiveTeamId = newTeamId;

        // Units outside previous/new active teams keep the same "inactive team" visual.
        // Skip full HUD/material refresh for them to reduce per-turn fan-out cost.
        bool shouldRefreshFully =
            previousActiveTeamId == int.MinValue
            || (int)teamId == previousActiveTeamId
            || (int)teamId == newTeamId;
        if (shouldRefreshFully)
            RefreshActedVisual();

        activeTeamChangedHandlerCount++;
        activeTeamChangedHandlerTotalMs += (Time.realtimeSinceStartupAsDouble * 1000d) - startMs;
    }

    private void HandleUnitActedStateChanged(UnitManager changed)
    {
        if (changed != this)
            return;
        if (appliedHasActed == hasActed)
            return;

        appliedHasActed = hasActed;
        RefreshActedVisual();
    }

    private void HandleFogOfWarUpdated()
    {
        RefreshDetectedIndicator();
    }

    private void RefreshSelectionVisual()
    {
        if (!isSelected)
        {
            StopSelectionBlinkRoutine();
            SetSpriteVisible(true);
            ApplyFogOfWarVisibility();
            return;
        }

        if (!enableSelectionBlink)
        {
            SetSpriteVisible(true);
            ApplyFogOfWarVisibility();
            return;
        }

        if (!Application.isPlaying)
        {
            SetSpriteVisible(true);
            ApplyFogOfWarVisibility();
            return;
        }

        if (selectionBlinkRoutine == null)
            selectionBlinkRoutine = StartCoroutine(SelectionBlinkRoutine());
    }

    private IEnumerator SelectionBlinkRoutine()
    {
        while (isSelected)
        {
            SetSpriteVisible(false);
            yield return new WaitForSeconds(selectionBlinkInactiveDuration);
            SetSpriteVisible(true);
            yield return new WaitForSeconds(selectionBlinkActiveDuration);
        }

        selectionBlinkRoutine = null;
        SetSpriteVisible(true);
    }

    private void StopSelectionBlinkRoutine()
    {
        if (selectionBlinkRoutine == null)
            return;

        StopCoroutine(selectionBlinkRoutine);
        selectionBlinkRoutine = null;
    }

    private void SetSpriteVisible(bool visible)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (unitHud == null)
            TryAutoAssignHud();
        if (!visible && (isDead || isEmbarked) &&
            fogDetectedContactRenderer != null)
        {
            fogDetectedContactRenderer.enabled = false;
        }

        // Passenger embarked must stay visually hidden even if other
        // systems request visibility (selection cleanup, blink stop, etc).
        if (isEmbarked && visible && !IsEmbarkedVisualPreviewActive)
            visible = false;
        // FoW does not override an active visual preview (e.g. supply animation showing an embarked unit).
        if (hiddenByFogOfWar && visible && !(isEmbarked && IsEmbarkedVisualPreviewActive))
            visible = false;

        if (spriteRenderer != null && spriteRenderer.GetComponentInParent<UnitManager>() == this)
            spriteRenderer.enabled = visible;

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        Transform hudRoot = unitHud != null ? unitHud.transform : null;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;
            if (renderer == spriteRenderer)
                continue;
            if (renderer == fogDetectedContactRenderer)
                continue;

            UnitManager owner = renderer.GetComponentInParent<UnitManager>();
            if (owner != this)
                continue;

            // HUD sprites (altitude/detected/etc) sao controlados pelo UnitHudController.
            if (renderer.GetComponentInParent<UnitHudController>() != null)
                continue;

            if (hudRoot != null && renderer.transform.IsChildOf(hudRoot))
                continue;

            renderer.enabled = visible;
        }
    }

    private void ApplyFogOfWarVisibility()
    {
        bool visible = !hiddenByFogOfWar;
        if (!visible)
        {
            StopSelectionBlinkRoutine();
            isSelected = false;
            // Don't forcibly hide an embarked unit that has an active visual preview
            // (e.g. supply animation temporarily showing the unit above its transporter).
            if (isEmbarked && IsEmbarkedVisualPreviewActive)
                return;
        }

        SetSpriteVisible(visible);
        SetHudVisible(visible);
        SetOwnedUiVisualsVisible(visible);
    }

    private void SetOwnedUiVisualsVisible(bool visible)
    {
        if (unitHud == null)
            TryAutoAssignHud();
        Transform hudRoot = unitHud != null ? unitHud.transform : null;

        Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
                continue;

            UnitManager owner = canvas.GetComponentInParent<UnitManager>();
            if (owner != this)
                continue;
            if (hudRoot != null && canvas.transform.IsChildOf(hudRoot))
                continue;

            canvas.enabled = visible;
        }

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
                continue;

            UnitManager owner = graphic.GetComponentInParent<UnitManager>();
            if (owner != this)
                continue;
            if (hudRoot != null && graphic.transform.IsChildOf(hudRoot))
                continue;

            graphic.enabled = visible;
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            UnitManager owner = text.GetComponentInParent<UnitManager>();
            if (owner != this)
                continue;
            if (hudRoot != null && text.transform.IsChildOf(hudRoot))
                continue;

            text.enabled = visible;
        }
    }

    public void BeginEmbarkedVisualPreview()
    {
        embarkedVisualPreviewDepth = Mathf.Max(0, embarkedVisualPreviewDepth) + 1;
        RefreshActedVisual();
    }

    public void EndEmbarkedVisualPreview()
    {
        if (embarkedVisualPreviewDepth > 0)
            embarkedVisualPreviewDepth--;
        RefreshActedVisual();
    }

    [ContextMenu("Apply From Database")]
    private void ApplyFromDatabaseContext()
    {
        bool ok = ApplyFromDatabase();
        if (!ok)
            Debug.LogWarning("[UnitManager] Nao foi possivel aplicar UnitData (db/id).", this);
    }

    [ContextMenu("Snap To Cell Center")]
    private void SnapToCellCenterContext()
    {
        SnapToCellCenter();
    }

    [ContextMenu("Pull Cell From Transform")]
    private void PullCellFromTransformContext()
    {
        PullCellFromTransform();
    }

#if UNITY_EDITOR
    private bool IsEditingPrefabContext()
    {
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject))
            return true;

        UnityEditor.SceneManagement.PrefabStage stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject);
        return stage != null;
    }
#endif
}
