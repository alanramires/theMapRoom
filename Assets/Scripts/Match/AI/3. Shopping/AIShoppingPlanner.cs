using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
// --------------------------------------------------------------------------------------------
// Planejamento de Compras da IA: Decis�o de Unidades a Comprar
// O AIShoppingPlanner � respons�vel por decidir quais unidades a IA deve comprar a cada turno,
// com base em uma an�lise abrangente do estado atual do jogo, incluindo a composi��o do ex�rcito,
// amea�as inimigas, necessidades operacionais e intelig�ncia de jogadas. Ele avalia a demanda por
// diferentes tipos de unidades (capturadores, assalto, transporte, suporte de fogo, combate a�reo, etc.),
// aplica regras de composi��o e prioridades estrat�gicas, e gera uma lista de ordens 
// de compra que guiar�o a fase de compras da IA.
// -------------------------------------------------------------------------------------------- 
public partial class AIShoppingPlanner : MonoBehaviour
{
    private const int DefensiveBaseThreatRange = 3;
    private const int DefensiveArmorThreatRange = 3;
    private const int DefensiveLowTroopCountThreshold = 7;

    public struct ShoppingOrder
    {
        public ConstructionManager Building;
        public UnitData UnitToBuy;
        public int SelectedIndex;
    }

    [Header("Debug")]
    [Tooltip("Usa a fila global por papeis. Desative apenas para comparar com o shopping legado.")]
    public bool UseRoleBasedShopping = true;

    [Header("Economia Exército")]
    [Range(0f, 20f)] public float SavingPercentualForElite = 15f;
    [Range(0f, 1f)]  public float EliteCapturerFillRatio   = 0.6f;
    [Range(0, 5)]    public int   MinFilledAssaultSlots     = 1;
    [HideInInspector] public int  MinArmySizeForElitePivot  = 12; // legado de serialização; não usado
    // Poupança de elite (turnos) e margem de manutenção (%) migraram para o AI Manager (AIController):
    // EliteSaveTurns / EliteMaintenanceReservePercent, com par por modo (normal/hard). Ver ComputeStrategicSavingReserve.
    [Tooltip("Pressao liquida na escala 0..1 do Unit Analysis a partir da qual a IA deixa de repetir counters baratos e passa a poupar para um counter elite.")]
    [Range(0.2f, 10f)] public float CounterEliteEscalationPressure = 3.2f;
    [HideInInspector] public float BasicCounterPressureCoverage = 4f; // legado; agora vem da matriz oficial
    // Razões de elite (normal/hard, pressão/cobertas) migraram para o AI Manager (AIController):
    // EliteRatioPressure / EliteRatioSafe, resolvidas por modo. Ver AddEliteQualityDemand.
    [Range(1, 12)]   public int   MinTurnForFireSupport     = 3;
    [Range(0, 8)]    public int   MinActiveCapturersForFireSupport = 2;
    [Range(0, 5)]    public int   MinActiveAssaultForFireSupport   = 1;
    [Tooltip("Massa mínima de capturadores ativos antes de liberar demanda de suporte (transporte terrestre e fire support de composição). Forma a base primeiro.")]
    [Range(0, 8)]    public int   MinCapturerMassForSupport        = 4;
    [Range(2, 8)]    public int   CapturersPerPreventiveTransport = 4;
    [Range(1, 4)]    public int   ProgressiveCapturerBatchSize = 2;
    [Range(1, 4)]    public int   AssaultPerFireSupportRatio = 2;
    [Range(0, 2)]    public int   MaxProactiveDefensiveFireSupport = 1;
    [Range(0, 5)]    public int   MaxProactiveAntiAirSAM          = 3;
    [Range(2, 10)]   public int   AntiAirCoverageRange             = 5;

    [Header("Defesa de Base")]
    [Range(0, 3)]    public int   MinBaseArtilharia               = 1;
    [Range(0, 3)]    public int   MinBaseAAA                      = 1;
    [Range(1, 12)]   public int   MinTurnBaseDefense              = 3;

    [Header("Intel de Jogadas")]
    public bool usarIntelJogadasNoShopping = true;
    [Range(1, 8)] public int IntelShoppingLookbackTurns = 4;
    [Range(0f, 10f)] public float IntelInfantryPressureAssaultThreshold = 1.5f;
    [Range(0f, 10f)] public float IntelAirThreatAntiAirThreshold = 2f;
    [Range(0f, 10f)] public float IntelArmorThreatDefenseThreshold = 2f;
    [Range(0f, 10f)] public float IntelCapturePressureDefenseThreshold = 2f;
    [Range(0f, 10f)] public float IntelNumericalPressureThreshold = 1.5f;
    [Range(0f, 30f)] public float IntelFireSupportGapHotThreshold = 8f;
    [Range(0f, 20f)] public float IntelFireSupportGapDamageThreshold = 3f;
    [Range(0f, 10f)] public float IntelOffensiveAntiInfantryFireThreshold = 2.5f;
    [Range(0f, 10f)] public float IntelStalemateElitePressureThreshold = 3.5f;
    [Range(0f, 10f)] public float IntelStalemateFireSupportThreshold = 6f;
    [Range(0f, 1f)] public float StalemateEliteCapturerFillRatio = 0.3f;
    [Range(1, 12)] public int StalemateEliteCapturerRange = 8;

    [Header("Logistica")]
    [Range(1, 8)] public int RepairsPerGroundSupplier = 2;
    [Tooltip("Doutrina da Conscrição: prioridade da demanda de logística quando há um ELITE ferido. Menor = mais cedo. Default 7 supera o counter-pressure-elite (8/9), garantindo o supridor móvel pra consertar o elite no campo enquanto as bases produzem massa.")]
    [Range(1, 20)] public int EliteRepairLogisticsPriority = 7;

    [Header("Economia Aeronáutica")]
    [Range(1, 8)]    public int   MaxAirTransporters               = 3;
    [Range(1, 12)]   public int   MinTurnForInterceptador          = 4;
    [Range(1, 6)]    public int   HelicopterosPorCacaB             = 3;
    [Range(0, 6)]    public int   MaxCacaB                         = 4;
    [Range(0, 4)]    public int   MaxCacaA                         = 2;
    [Range(1, 12)]   public int   MinTurnForAtaqueAereo            = 5;
    [Range(1, 4)]    public int   ChinooksPorApache                = 2;
    [Range(1, 6)]    public int   HelicopterosInimigosPorApache    = 3;
    [Range(1, 4)]    public int   ApachesParaBombardeiro           = 2;
    public bool                  ComprarApacheEmModoDefesa         = true;
    [Range(0, 4)]    public int   MinCacaBPresence                 = 1;
    [Range(0, 4)]    public int   MinApachePresence                = 1;
    [Range(0, 2)]    public int   MinBombaPresence                 = 0;
    [FormerlySerializedAs("MinTurnForIntel")]
    [Range(1, 12)]   public int   MinTurnForAirSurveillance        = 4;
    [FormerlySerializedAs("MaxAirIntel")]
    [Range(0, 3)]    public int   MaxAirSurveillance               = 1;
    [FormerlySerializedAs("MaxMobileAirIntel")]
    [FormerlySerializedAs("MaxGroundIntel")]
    [Range(0, 3)]    public int   MaxMobileAirSurveillance         = 1;

    private static AIShoppingPlanner instance;
    public static AIShoppingPlanner Instance => EnsureInstance();

    private static AIShoppingPlanner EnsureInstance()
    {
        if (instance != null) return instance;
        instance = FindAnyObjectByType<AIShoppingPlanner>();
        if (instance != null) return instance;
        GameObject go = new GameObject(nameof(AIShoppingPlanner));
        instance = go.AddComponent<AIShoppingPlanner>();
        DontDestroyOnLoad(go);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public static List<ShoppingOrder> Decide(AIWorldSnapshot snapshot)
    {
        if (Instance != null && Instance.UseRoleBasedShopping)
            return DecideRoleBased(snapshot);

        var orders = new List<ShoppingOrder>();
        if (snapshot == null) return orders;

        var occupied  = BuildProductionOccupiedCells();
        int remaining = snapshot.Budget;
        int openCapturerSlots   = CountOpenSlots(snapshot.AITeam, UnitRole.Capturador);
        int openAssaultSlots    = CountOpenSlots(snapshot.AITeam, UnitRole.Assalto);
        int openTransportSlots    = ComputeTransportDemand(snapshot, out bool urgentTransportDemand);
        int openAirTransportSlots = ComputeAirTransportDemand(snapshot, openCapturerSlots);
        int openFireSupportSlots = ComputeFireSupportDemand(snapshot, openCapturerSlots, openAssaultSlots, out bool preferDefensiveFireSupport);
        int openCacaBSlots  = ComputeCacaBDemand(snapshot);
        int openCacaASlots  = ComputeCacaADemand(snapshot);
        int openApacheSlots = ComputeApacheDemand(snapshot);
        int openBombaSlots  = ComputeBombaDemand(snapshot);
        int openAirTankerSlots = 0;
        AIIntelReport intelReport = BuildShoppingIntelReport(snapshot);
        ComputeAirSurveillanceDemand(
            snapshot, intelReport,
            out int openAirSurveillanceSlots,
            out int openMobileAirSurveillanceSlots);
        bool intelArmorThreat = false;
        bool offensiveAntiInfantryFireSupport = false;
        bool proactiveDefFireSupport = !preferDefensiveFireSupport
            && ComputeProactiveDefensiveFireSupportNeeded(snapshot);
        if (proactiveDefFireSupport)
        {
            openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
            preferDefensiveFireSupport = true;
            Debug.Log($"[AI Shopping] proactive_def_fire_support: abrindo slot preventivo (1 art. defensiva antes da ameaça)");
        }
        int openLogisticsSlots = ComputeLogisticsDemand(snapshot, out int repairDemandCount, out int activeLogisticsCount);
        bool proactiveAntiAir = ComputeProactiveAntiAirNeeded(snapshot, out int activeSAMs, out int activeAAAs);
        int maxSAMCap = Instance != null ? Instance.MaxProactiveAntiAirSAM : 3;
        bool forceSAMBypass = false;
        // SAM proactive: requires AAA in field (chain gate) and cap not reached.
        // AAA proactive: bypasses the IsAntiAirOnlyUnit gate via proactiveAntiAir flag — competes normally for open assault slots.
        bool proactiveSAM = proactiveAntiAir && activeAAAs >= 1 && activeSAMs < 1; // proactive: 1 SAM is enough; reactive path uses maxSAMCap
        if (proactiveSAM)
        {
            openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
            preferDefensiveFireSupport = true;
            Debug.Log($"[AI Shopping] proactive_anti_air: SAM proativo activeAAAs={activeAAAs} activeSAMs={activeSAMs}/{maxSAMCap} → slot fire_support defensivo aberto");
        }

        // Hard: o primeiro elite terrestre � a pe�a de ruptura (MBT), n�o o Obus M�dio.
        // Defesa antia�rea emergencial continua podendo furar esta regra mais abaixo.
        ComputeGuaranteedBaseDefense(snapshot, out int baseArtSlots, out bool forceBaseAAA);
        if (baseArtSlots > 0)
        {
            openFireSupportSlots = Mathf.Max(openFireSupportSlots, baseArtSlots);
            preferDefensiveFireSupport = true;
            proactiveDefFireSupport = true;
            Debug.Log($"[AI Shopping] base_defense: abrindo {baseArtSlots} slot(s) artilharia preventiva");
        }
        if (forceBaseAAA)
        {
            proactiveAntiAir = true;
            if (openAssaultSlots <= 0)
                openAssaultSlots = 1;
            Debug.Log($"[AI Shopping] base_defense: forçando AAA preventiva (proactiveAntiAir=true, assault_slot=1)");
        }
        int aaaCoverageRange = Instance != null ? Instance.AntiAirCoverageRange : 5;
        int aircraftNearHQ = CountVisibleEnemyAircraftNearHQ(snapshot, aaaCoverageRange);
        bool aaaThreat = aircraftNearHQ > 0;
        bool emergencyAirBaseThreat = aircraftNearHQ > 0
            && HasAnyVisibleEnemyNearOwnedBase(snapshot, DefensiveBaseThreatRange);
        int aaaCap = Mathf.CeilToInt(aircraftNearHQ / 2f);
        if (forceBaseAAA)
        {
            int minBaseAAA = Instance != null ? Instance.MinBaseAAA : 1;
            aaaCap = Mathf.Max(aaaCap, minBaseAAA + (emergencyAirBaseThreat ? 1 : 0));
        }
        if (aaaCap > 0 && activeAAAs < aaaCap && openAssaultSlots <= 0)
        {
            openAssaultSlots = 1;
            Debug.Log($"[AI Shopping] reactive_anti_air: aircraft={aircraftNearHQ} <= {aaaCoverageRange}h HQ -> aaaCap={aaaCap} activeAAAs={activeAAAs} emergency={emergencyAirBaseThreat} → slot assault aberto para AAA");
        }
        // Distant air contacts should pull fighter demand, not ground AAA. AAA is local base coverage.
        if (aaaCap == 0 && activeAAAs == 0 && openAssaultSlots <= 0)
        {
            int totalVisibleAir = CountTotalVisibleEnemyAircraft(snapshot);
            if (totalVisibleAir > 0)
            {
                Debug.Log($"[AI Shopping] reactive_anti_air_wide: {totalVisibleAir} aeronave(s) inimiga(s) visivel longe do HQ -> sem AAA terrestre");
            }
        }
        ApplyJogadasIntelBias(snapshot, intelReport,
            ref openAssaultSlots,
            ref openFireSupportSlots,
            ref openCacaBSlots,
            ref proactiveAntiAir,
            ref preferDefensiveFireSupport,
            ref proactiveDefFireSupport,
            ref intelArmorThreat,
            activeAAAs,
            activeSAMs);

        int visibleEnemyFireSupport = CountVisibleEnemyCombatFireSupport(snapshot);
        bool artilleryWallBreakthrough = ShouldApplyArtilleryWallBreakthrough(snapshot, intelReport, visibleEnemyFireSupport);
        if (artilleryWallBreakthrough)
        {
            int beforeAssault = openAssaultSlots;
            int beforeBomba = openBombaSlots;
            int ownedBombers = CountOwnedBombers(snapshot, includeUnderRepair: true);
            openAssaultSlots = Mathf.Max(openAssaultSlots, 1);
            if (ownedBombers <= 0)
                openBombaSlots = Mathf.Max(openBombaSlots, 1);
            Debug.Log($"[AI Shopping] ruptura_artilharia: enemyFire={visibleEnemyFireSupport} stalemate={intelReport?.stalemateElitePressure:F1} enemyArt={intelReport?.enemyArtilleryThreatScore:F1} units={snapshot.MyUnits?.Count ?? 0} bombers={ownedBombers} budget={remaining} -> ass={beforeAssault}->{openAssaultSlots} bomba={beforeBomba}->{openBombaSlots}");
        }

        int urgentCapturerFloor = 0;
        List<TacticalDeficit> opDeficits = AITacticalAnalyzer.Instance?.GetDeficits(
            PlayerSlotId.FromIndex(snapshot.AISlotIndex));
        if (opDeficits != null)
        {
            foreach (TacticalDeficit deficit in opDeficits)
            {
                if (deficit.Count <= 0) continue;
                bool elevated = false;
                switch (deficit.Kind)
                {
                    case AINeedKind.Capturer:
                        if (deficit.Operation != null && deficit.Operation.IsUrgent)
                        {
                            int before = urgentCapturerFloor;
                            urgentCapturerFloor = Mathf.Max(urgentCapturerFloor, deficit.Count);
                            elevated = urgentCapturerFloor != before;
                        }
                        else
                        {
                            int before = openCapturerSlots;
                            openCapturerSlots = Mathf.Max(openCapturerSlots, deficit.Count);
                            elevated = openCapturerSlots != before;
                        }
                        break;
                    case AINeedKind.Assault:
                        {
                            int before = openAssaultSlots;
                            openAssaultSlots = Mathf.Max(openAssaultSlots, deficit.Count);
                            elevated = openAssaultSlots != before;
                        }
                        break;
                    case AINeedKind.AAA:
                        {
                            proactiveAntiAir = true;
                            int before = openAssaultSlots;
                            openAssaultSlots = Mathf.Max(openAssaultSlots, deficit.Count);
                            elevated = openAssaultSlots != before;
                        }
                        break;
                    case AINeedKind.SAM:
                        {
                            proactiveAntiAir = true;
                            forceSAMBypass = true;
                            int before = openFireSupportSlots;
                            openFireSupportSlots = Mathf.Max(openFireSupportSlots, deficit.Count);
                            preferDefensiveFireSupport = true;
                            elevated = openFireSupportSlots != before;
                        }
                        break;
                    case AINeedKind.Artillery:
                        {
                            if (IsFireSupportSaturated(snapshot))
                            {
                                int beforeAssault = openAssaultSlots;
                                openAssaultSlots = Mathf.Max(openAssaultSlots, 1);
                                elevated = openAssaultSlots != beforeAssault;
                                if (elevated)
                                    Debug.Log($"[AI Shopping] op_deficit: {deficit.Operation?.Type}({deficit.Operation?.Sector}) {deficit.Kind}x{deficit.Count} saturado por artilharia ativa -> Assaultx1");
                            }
                            else
                            {
                                int before = openFireSupportSlots;
                                openFireSupportSlots += deficit.Count;
                                preferDefensiveFireSupport = true;
                                proactiveDefFireSupport = true;
                                elevated = openFireSupportSlots != before;
                            }
                        }
                        break;
                    case AINeedKind.FireSupport:
                        {
                            if (IsFireSupportSaturated(snapshot))
                            {
                                int beforeAssault = openAssaultSlots;
                                openAssaultSlots = Mathf.Max(openAssaultSlots, 1);
                                elevated = openAssaultSlots != beforeAssault;
                                if (elevated)
                                    Debug.Log($"[AI Shopping] op_deficit: {deficit.Operation?.Type}({deficit.Operation?.Sector}) {deficit.Kind}x{deficit.Count} saturado por artilharia ativa -> Assaultx1");
                            }
                            else
                            {
                                int before = openFireSupportSlots;
                                openFireSupportSlots += deficit.Count;
                                elevated = openFireSupportSlots != before;
                            }
                        }
                        break;
                    case AINeedKind.AirTransport:
                        {
                            int before = openAirTransportSlots;
                            openAirTransportSlots = Mathf.Max(openAirTransportSlots, deficit.Count);
                            elevated = openAirTransportSlots != before;
                        }
                        break;
                    case AINeedKind.FighterB:
                        {
                            int before = openCacaBSlots;
                            openCacaBSlots = Mathf.Max(openCacaBSlots, deficit.Count);
                            elevated = openCacaBSlots != before;
                        }
                        break;
                    case AINeedKind.FighterA:
                        {
                            int before = openCacaASlots;
                            openCacaASlots = Mathf.Max(openCacaASlots, deficit.Count);
                            elevated = openCacaASlots != before;
                        }
                        break;
                    case AINeedKind.Apache:
                        {
                            int before = openApacheSlots;
                            openApacheSlots = Mathf.Max(openApacheSlots, deficit.Count);
                            elevated = openApacheSlots != before;
                        }
                        break;
                    case AINeedKind.AirTanker:
                        {
                            int before = openAirTankerSlots;
                            openAirTankerSlots = Mathf.Max(openAirTankerSlots, deficit.Count);
                            elevated = openAirTankerSlots != before;
                        }
                        break;
                }

                if (elevated)
                    Debug.Log($"[AI Shopping] op_deficit: {deficit.Operation?.Type}({deficit.Operation?.Sector}) {deficit.Kind}x{deficit.Count}");
            }
        }
        bool previousProactiveSAM = proactiveSAM;
        proactiveSAM = (proactiveAntiAir && activeAAAs >= 1 && activeSAMs < 1)
            || (forceSAMBypass && activeSAMs < 1);
        if (proactiveSAM && !previousProactiveSAM)
        {
            openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
            preferDefensiveFireSupport = true;
            Debug.Log($"[AI Shopping] op_deficit: SAM bypass force={forceSAMBypass} activeAAAs={activeAAAs} activeSAMs={activeSAMs}/{maxSAMCap}");
        }
        // Defensive burst: when stance=Defensive and SectorDefense ops have no assigned units,
        // open fire support slots beyond the normal saturation cap so the AI actively buys
        // artillery for hot undefended owned sectors instead of sitting on cash.
        if (snapshot.Stance == AIStance.Defensive)
        {
            int unfilledDefOps = CountUnfilledDefenseOps(snapshot.AITeam);
            if (unfilledDefOps > 0 && openFireSupportSlots < unfilledDefOps)
            {
                if (IsFireSupportSaturated(snapshot))
                {
                    int beforeAssault = openAssaultSlots;
                    openAssaultSlots = Mathf.Max(openAssaultSlots, 1);
                    Debug.Log($"[AI Shopping] defensive_burst: stance=Defensive ops_sem_defesa={unfilledDefOps} saturado por artilharia ativa -> Assaultx{openAssaultSlots} fire_slots={openFireSupportSlots} ass_before={beforeAssault}");
                }
                else
                {
                    int before = openFireSupportSlots;
                    openFireSupportSlots = unfilledDefOps;
                    preferDefensiveFireSupport = true;
                    proactiveDefFireSupport = true;
                    if (openFireSupportSlots != before)
                        Debug.Log($"[AI Shopping] defensive_burst: stance=Defensive ops_sem_defesa={unfilledDefOps} → fire_slots={openFireSupportSlots}");
                }
            }
        }
        if (ComputeOffensiveAntiInfantryFireSupportDemand(snapshot, intelReport, out offensiveAntiInfantryFireSupport))
        {
            int before = openFireSupportSlots;
            openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
            if (!proactiveAntiAir && !intelArmorThreat && !HasAnyVisibleEnemyNearOwnedBase(snapshot, DefensiveBaseThreatRange))
                preferDefensiveFireSupport = false;
            if (openFireSupportSlots != before || !preferDefensiveFireSupport)
                Debug.Log($"[AI Shopping] offensive_anti_inf_fire: abrindo/priorizando fire ofensivo anti-infantaria fire={openFireSupportSlots} preferDef={preferDefensiveFireSupport}");
        }
        if (openAssaultSlots <= 0
            && !HasActivePrimaryRole(snapshot, UnitRole.Assalto)
            && CanAffordPurePrimaryRole(snapshot, UnitRole.Assalto, remaining))
        {
            openAssaultSlots = 1;
        }
        int rawOpenCapturerSlots = openCapturerSlots;
        openCapturerSlots = LimitCapturerDemandForProgression(snapshot, openCapturerSlots, openAssaultSlots, openTransportSlots, openLogisticsSlots, openFireSupportSlots);
        openCapturerSlots = RestoreCapturerDemandForIdleAirlift(snapshot, rawOpenCapturerSlots, openCapturerSlots);
        openCapturerSlots = RestoreStrategicPrimaryCapturerDemand(snapshot, rawOpenCapturerSlots, openCapturerSlots, ref openFireSupportSlots, ref preferDefensiveFireSupport);
        if (urgentCapturerFloor > 0)
            openCapturerSlots = Mathf.Max(openCapturerSlots, urgentCapturerFloor);

        // Numerical pressure: when the enemy has significantly more units, buy extra capturers
        // for body count regardless of plan objectives — same way a human player would.
        {
            int bulkFloor = ComputeNumericalBulkCapturerDemand(snapshot, intelReport);
            if (bulkFloor > 0 && openCapturerSlots < bulkFloor)
            {
                openCapturerSlots = bulkFloor;
                Debug.Log($"[AI Shopping] bulk_cap: pressaoNumerica={intelReport?.numericalPressure:F1} → cap_floor={bulkFloor}");
            }
        }

        // Cap assault demand to 1 per 2 active capturers (min 1). Prevents the plan from
        // creating 5+ assault slots in T1 when all sectors are high-risk and no units exist,
        // which would otherwise cause the shopping loop to buy 5 tanks before any capturer.
        {
            int activeCap = CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: false);
            openAssaultSlots = Mathf.Min(openAssaultSlots, Mathf.Max(1, activeCap / 2));
        }

        // Escalada via cadeia eliteFrom: level 2 só fica disponível quando level 1 já está em campo
        // (chain gate em FindEliteAssaultReserveTarget). Não há mais necessidade de branch por contagem.
        int activeEliteAssaultCount = CountActiveEliteAssaultUnits(snapshot);
        const int DreamTeamEliteAssaultThreshold = 2;
        UnitData eliteLevel2Candidate = FindEliteAssaultReserveTarget(snapshot, 2);
        UnitData eliteLevel1Candidate = FindEliteAssaultReserveTarget(snapshot, 1);
        UnitData eliteAssaultTarget = eliteLevel2Candidate != null
            && remaining >= eliteLevel2Candidate.cost
                ? eliteLevel2Candidate
                : eliteLevel1Candidate;
        bool matureEconomyEliteAssaultPivot = eliteAssaultTarget != null
            && activeEliteAssaultCount == 0
            && HasOperationalCore(snapshot)
            && remaining >= eliteAssaultTarget.cost;

        // Save the target for reserve purposes BEFORE the composition check may null it.
        // Reserve (saving money) applies even when composition isn't ready yet;
        // only the actual purchase is gated by composition.
        UnitData eliteAssaultTargetForReserve = eliteAssaultTarget;

        // Compra elite apenas quando a composição mínima do exército já foi atingida:
        // proporção dos slots de capturador preenchidos >= EliteCapturerFillRatio
        // E pelo menos MinFilledAssaultSlots slots de assault preenchidos.
        CountSlots(snapshot.AITeam, UnitRole.Capturador, out int totalCap, out int filledCap);
        if (eliteAssaultTarget != null)
        {
            CountSlots(snapshot.AITeam, UnitRole.Assalto, out int totalAss, out int filledAss);
            float capFill       = totalCap > 0 ? filledCap / (float)totalCap : 0f;
            float fillThreshold = Instance != null ? Instance.EliteCapturerFillRatio : 0.6f;
            bool stalemateCapturerReady = HasStalemateCapturerCommitment(snapshot, intelReport, out string stalemateCapturerReason);
            if (stalemateCapturerReady)
            {
                float stalemateThreshold = Instance != null
                    ? Mathf.Clamp01(Instance.StalemateEliteCapturerFillRatio)
                    : 0.3f;
                bool heavyStalematePressure = intelReport != null
                    && Instance != null
                    && intelReport.stalemateElitePressure >= Instance.IntelStalemateFireSupportThreshold;
                if (heavyStalematePressure)
                    stalemateThreshold = Mathf.Min(stalemateThreshold, 0.25f);
                fillThreshold = Mathf.Min(fillThreshold, stalemateThreshold);
            }
            bool offensiveElitePressure = remaining >= eliteAssaultTarget.cost
                && (snapshot.Stance == AIStance.Offensive
                    || snapshot.Stance == AIStance.Tactical
                    || HasAnyOffensiveObjective(snapshot.AITeam))
                && capFill >= 0.5f;
            if (offensiveElitePressure)
                fillThreshold = Mathf.Min(fillThreshold, 0.5f);
            int   minAssault    = Instance != null ? Instance.MinFilledAssaultSlots   : 1;
            if (artilleryWallBreakthrough && remaining >= eliteAssaultTarget.cost)
                minAssault = 0;
            // Invasão: a peça forte (elite assalto) é prioridade. A massa de capturador já foi
            // montada (GoGreen) e agora é o LEFTOVER do orçamento, não o pré-requisito — então não
            // gateia a elite atrás do preenchimento de capturador.
            if (snapshot.IsInvading && remaining >= eliteAssaultTarget.cost)
            {
                fillThreshold = 0f;
                minAssault = 0;
            }
            bool  capOk         = capFill >= fillThreshold;
            bool  assOk         = filledAss >= minAssault;
            if (matureEconomyEliteAssaultPivot)
            {
                capOk = true;
                assOk = true;
                openAssaultSlots = Mathf.Max(openAssaultSlots, 1);
            }
            string stalemateText = stalemateCapturerReady ? $" stalemateCap={stalemateCapturerReason}" : "";
            string offensiveText = matureEconomyEliteAssaultPivot
                ? $" qualityPivot=True coreOperacional=True cash={remaining}"
                : offensiveElitePressure ? " offensivePressure=True" : artilleryWallBreakthrough ? " artilleryWall=True" : "";
            string status       = (capOk && assOk) ? $"ELITE LIBERADO{stalemateText}{offensiveText}" : $"bloqueado ({(!capOk ? $"cap {filledCap}/{totalCap} {capFill:P0}<{fillThreshold:P0}" : "cap OK")} | {(!assOk ? $"ass {filledAss}<{minAssault}" : "ass OK")}){stalemateText}{offensiveText}";
            Debug.Log($"[AI Shopping] composição: cap={filledCap}/{totalCap} ({capFill:P0}) ass={filledAss}/{totalAss} — {status}");
            if (!capOk || !assOk)
                eliteAssaultTarget = null; // blocks purchase; eliteAssaultTargetForReserve still set
        }

        if (eliteAssaultTarget != null && openAssaultSlots <= 0 && remaining >= eliteAssaultTarget.cost)
        {
            openAssaultSlots = 1;
            Debug.Log($"[AI Shopping] elite excedente liberado: {eliteAssaultTarget.displayName} custo={eliteAssaultTarget.cost} cash={remaining}");
        }

        UnitData eliteFireSupportTarget = FindEliteFireSupportReserveTarget(snapshot, preferDefensiveFireSupport, remaining);
        // Proactive SAM: AAA in field satisfies chain gate — find SAM as elite fire support target.
        if (proactiveSAM)
        {
            UnitData samTarget = FindEliteFireSupportReserveTarget(
                snapshot,
                true,
                remaining,
                requireChain: true,
                antiAirOnly: true);
            if (samTarget != null && IsAntiAirOnlyUnit(samTarget)
                && (eliteFireSupportTarget == null || !IsAntiAirOnlyUnit(eliteFireSupportTarget)))
            {
                eliteFireSupportTarget = samTarget;
                Debug.Log($"[AI Shopping] proactive_anti_air: SAM target={samTarget.displayName} custo={samTarget.cost}");
            }
        }
        // Hard: o primeiro elite terrestre � a pe�a de ruptura (MBT), n�o o Obus M�dio.
        // Defesa antia�rea emergencial continua podendo furar esta regra mais abaixo.
        if (AIController.Instance != null
            && AIController.Instance.HardMode
            && activeEliteAssaultCount == 0
            && eliteAssaultTargetForReserve != null
            && eliteFireSupportTarget != null
            && !IsAntiAirOnlyUnit(eliteFireSupportTarget))
        {
            Debug.Log($"[AI Shopping] hard_blitz: adiando primeiro elite fire_support "
                + $"{eliteFireSupportTarget.displayName}; prioridade={eliteAssaultTargetForReserve.displayName}");
            eliteFireSupportTarget = null;
        }
        int activeFireSupportCount = CountActiveUnitsWithRole(snapshot, UnitRole.FogoIndireto, requirePrimary: false);
        bool criticalBaseAirThreat = aaaThreat || proactiveSAM || forceSAMBypass;

        // Dream team pivot: once the AI has N+ elite assault units in the field, stop
        // adding more elite tanks and invest in elite defensive fire support instead.
        // Uses field unit count, not plan slots — plan slots are empty in rogue mode.
        int activeEliteFireSupportCount = CountActiveEliteFireSupportUnits(snapshot);
        int desiredEliteFireSupport = activeEliteAssaultCount / DreamTeamEliteAssaultThreshold;
        bool dreamTeamPivot = activeEliteAssaultCount >= DreamTeamEliteAssaultThreshold
            && activeEliteFireSupportCount < desiredEliteFireSupport;
        if (dreamTeamPivot)
        {
            preferDefensiveFireSupport = true;
            eliteFireSupportTarget = FindEliteFireSupportReserveTarget(snapshot, preferDefensiveFireSupport: true, remaining, requireChain: false);
            Debug.Log($"[AI Shopping] dream_team_pivot: {activeEliteAssaultCount} elite assault em campo → target={eliteFireSupportTarget?.displayName ?? "nenhum"} custo={eliteFireSupportTarget?.cost ?? 0}");
        }

        bool eliteFireSupportReserveReady = IsEliteFireSupportReserveReady(snapshot);
        bool eliteFireSupportNowAffordable = eliteFireSupportTarget != null && remaining >= eliteFireSupportTarget.cost;
        float eliteFireCapFill = totalCap > 0 ? filledCap / (float)totalCap : 0f;
        float eliteFireFillThreshold = Instance != null ? Instance.EliteCapturerFillRatio : 0.6f;
        bool wantsEliteFireSupport = eliteFireSupportTarget != null
            && (eliteFireSupportReserveReady || eliteFireSupportNowAffordable)
            && (dreamTeamPivot
                // Invasão: a peça forte (elite fogo) é prioridade junto com o assalto; a massa é o
                // leftover, não o pré-requisito — não gateia atrás do preenchimento de capturador.
                || (snapshot.IsInvading && eliteFireSupportNowAffordable)
                || (activeFireSupportCount > 0 && eliteFireCapFill >= eliteFireFillThreshold));
        // Proactive SAM bypasses the composition gate — buy immediately if affordable.
        if (proactiveSAM && eliteFireSupportTarget != null
            && IsAntiAirOnlyUnit(eliteFireSupportTarget) && eliteFireSupportNowAffordable)
            wantsEliteFireSupport = true;
        int reserveForEliteFireSupport = 0;
        bool eliteFireSupportBought = false;
        bool emergencyProductionDefense = TryFindEmergencyProductionDefensePurchase(snapshot, remaining, out UnitData emergencyDefenseTarget, out int emergencyContestedOwned);
        if (emergencyProductionDefense)
        {
            eliteFireSupportTarget = emergencyDefenseTarget;
            openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
            preferDefensiveFireSupport = true;
            wantsEliteFireSupport = true;
            eliteFireSupportNowAffordable = true;
            Debug.Log($"[AI Shopping] emergencia fabrica: construcoes_contestadas={emergencyContestedOwned} cash={remaining} compra_prioritaria={emergencyDefenseTarget.displayName} custo={emergencyDefenseTarget.cost}");
        }
        if (wantsEliteFireSupport)
        {
            if (remaining >= eliteFireSupportTarget.cost)
            {
                openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
                Debug.Log($"[AI Shopping] elite fire_support liberado: {eliteFireSupportTarget.displayName} custo={eliteFireSupportTarget.cost} cash={remaining}");
            }
            else
            {
                reserveForEliteFireSupport = remaining;
                Debug.Log($"[AI Shopping] reserva elite fire_support {eliteFireSupportTarget.displayName} elite={eliteFireSupportTarget.eliteLevel} custo={eliteFireSupportTarget.cost} cash={remaining} income={snapshot.IncomePerTurn} reserva={reserveForEliteFireSupport}");
            }
        }

        int cheapestTransportCost    = openTransportSlots    > 0 ? FindCheapestAvailableTransportCost(snapshot)    : 0;
        int cheapestAirTransportCost = openAirTransportSlots > 0 ? FindCheapestAirTransportCost(snapshot)         : 0;
        int cheapestAirSurveillanceCost =
            openAirSurveillanceSlots > 0
                ? FindCheapestDedicatedAirSurveillanceCost(
                    snapshot, Domain.Air)
                : 0;
        int reserveForCapturerPassenger = 0;
        if (openCapturerSlots > 0)
        {
            int cheapestCapturerCost = FindCheapestPrimaryRoleLandCost(snapshot, UnitRole.Capturador);
            int capturerProductionSlots = CountAvailablePrimaryRoleLandProductionSlots(snapshot, UnitRole.Capturador, occupied);
            int capturerPassengerBuys = Mathf.Min(openCapturerSlots, capturerProductionSlots);
            reserveForCapturerPassenger = cheapestCapturerCost * capturerPassengerBuys;
        }
        int reserveForAirTransport   = 0;
        if (cheapestAirTransportCost > 0 && openAirTransportSlots > 0)
        {
            int airReserveBudget = Mathf.Max(0, remaining - reserveForCapturerPassenger);
            reserveForAirTransport = Mathf.Min(airReserveBudget, cheapestAirTransportCost * openAirTransportSlots);
            Debug.Log($"[AI Shopping] reserva_ar: air_slots={openAirTransportSlots} custo={cheapestAirTransportCost} reserva={reserveForAirTransport} cap_passageiro_reserva={reserveForCapturerPassenger}");
        }
        int reserveForAirSurveillance = 0;
        if (cheapestAirSurveillanceCost > 0
            && openAirSurveillanceSlots > 0)
        {
            int airSurveillanceReserveBudget = Mathf.Max(
                0,
                remaining
                - reserveForCapturerPassenger
                - reserveForAirTransport);
            reserveForAirSurveillance = Mathf.Min(
                airSurveillanceReserveBudget,
                cheapestAirSurveillanceCost
                * openAirSurveillanceSlots);
            Debug.Log(
                $"[AI Shopping] reserva_vigilancia_aerea: " +
                $"slots={openAirSurveillanceSlots} " +
                $"custo={cheapestAirSurveillanceCost} " +
                $"reserva={reserveForAirSurveillance}");
        }
        int anyAirCombatDemand    = openCacaBSlots + openCacaASlots + openApacheSlots + openBombaSlots;
        int cheapestAirCombatCost = anyAirCombatDemand > 0 ? FindCheapestAirCombatCost(snapshot) : 0;
        int cacaBReserveCost      = openCacaBSlots  > 0 ? FindCheapestAirCombatCost(snapshot, UnitRole.Interceptador, elite: false) : 0;
        int cacaAReserveCost      = openCacaASlots  > 0 ? FindCheapestAirCombatCost(snapshot, UnitRole.Interceptador, elite: true)  : 0;
        int apacheReserveCost     = openApacheSlots > 0 ? FindCheapestAirCombatCost(snapshot, UnitRole.AtaqueAereo,   elite: false) : 0;
        int bomberReserveCost     = openBombaSlots > 0 ? FindCheapestAirCombatCost(snapshot, UnitRole.AtaqueAereo, elite: true) : 0;
        int reserveForAirCombat   = 0;
        if (cheapestAirCombatCost > 0 && anyAirCombatDemand > 0)
        {
            int budgetAfterAirTransport = Mathf.Max(0, remaining - reserveForCapturerPassenger - reserveForAirTransport);
            int breakthroughArmorReserve = artilleryWallBreakthrough
                ? FindBreakthroughArmorCost(snapshot, budgetAfterAirTransport)
                : 0;
            if (breakthroughArmorReserve > 0)
                budgetAfterAirTransport = Mathf.Max(0, budgetAfterAirTransport - breakthroughArmorReserve);
            int minimumReserve = cheapestAirCombatCost * Mathf.Min(anyAirCombatDemand, 2);
            if (artilleryWallBreakthrough && bomberReserveCost > 0)
                minimumReserve = bomberReserveCost;
            else if (cacaBReserveCost > 0)
                minimumReserve = Mathf.Max(minimumReserve, cacaBReserveCost);
            if (cacaAReserveCost > 0)
                minimumReserve = Mathf.Max(minimumReserve, cacaAReserveCost);
            if (apacheReserveCost > 0)
                minimumReserve = Mathf.Max(minimumReserve, apacheReserveCost);
            if (bomberReserveCost > 0)
                minimumReserve = Mathf.Max(minimumReserve, bomberReserveCost);
            reserveForAirCombat = Mathf.Min(budgetAfterAirTransport, minimumReserve);
            Debug.Log($"[AI Shopping] reserva_combate_ar: slots={anyAirCombatDemand} custo={cheapestAirCombatCost} cacaB_custo={cacaBReserveCost} cacaA_custo={cacaAReserveCost} apache_custo={apacheReserveCost} bomber_custo={bomberReserveCost} ruptura={artilleryWallBreakthrough} armor_reserva={breakthroughArmorReserve} reserva={reserveForAirCombat} cap_passageiro_reserva={reserveForCapturerPassenger}");
        }

        Debug.Log(
            $"[AI Shopping] budget={remaining} " +
            $"cap_slots={openCapturerSlots} ass_slots={openAssaultSlots} " +
            $"trans_slots={openTransportSlots} " +
            $"trans_urgent={urgentTransportDemand} " +
            $"air_trans_slots={openAirTransportSlots} " +
            $"air_tanker_slots={openAirTankerSlots} " +
            $"vigilancia_aerea_slots={openAirSurveillanceSlots} " +
            $"vigilancia_movel_slots={openMobileAirSurveillanceSlots} " +
            $"log_slots={openLogisticsSlots} repairs={repairDemandCount} " +
            $"active_log={activeLogisticsCount} " +
            $"fire_slots={openFireSupportSlots} " +
            $"fire_def={preferDefensiveFireSupport} " +
            $"cacaB_slots={openCacaBSlots} cacaA_slots={openCacaASlots} " +
            $"apache_slots={openApacheSlots} bomba_slots={openBombaSlots} " +
            $"cheapest_transport={cheapestTransportCost} " +
            $"cheapest_air={cheapestAirTransportCost} " +
            $"cheapest_air_surveillance={cheapestAirSurveillanceCost} " +
            $"reserva_ar={reserveForAirTransport} " +
            $"reserva_vigilancia={reserveForAirSurveillance} " +
            $"cap_passageiro_reserva={reserveForCapturerPassenger} " +
            $"intel={(intelReport != null ? $"inf={intelReport.enemyInfantryPressureScore:F1} air={intelReport.enemyAirThreatScore:F1} armor={intelReport.enemyArmorThreatScore:F1} num={intelReport.numericalPressure:F1}" : "off")}");

        bool strategicEliteAssaultReserve = eliteAssaultTarget != null
            && !dreamTeamPivot
            && openCapturerSlots <= 0
            && openAssaultSlots <= 0
            && openTransportSlots <= 0
            && openLogisticsSlots <= 0
            && openFireSupportSlots <= 0
            && IsEliteAssaultReserveReady(snapshot);
        float reserveCapFill = totalCap > 0 ? filledCap / (float)totalCap : 0f;
        float reserveCapThreshold = (Instance != null ? Instance.EliteCapturerFillRatio : 0.6f) * 0.85f;
        bool nextTurnEliteAssaultReserve = eliteAssaultTargetForReserve != null
            && !dreamTeamPivot
            && remaining < eliteAssaultTargetForReserve.cost
            && remaining + Mathf.Max(0, snapshot.IncomePerTurn) >= eliteAssaultTargetForReserve.cost
            && reserveCapFill >= reserveCapThreshold
            && !(preferDefensiveFireSupport && openFireSupportSlots >= 2);
        bool defensiveEmergencyBlocksElite = snapshot.Stance == AIStance.Defensive
            && preferDefensiveFireSupport
            && openFireSupportSlots >= 2;
        if (defensiveEmergencyBlocksElite)
        {
            eliteAssaultTarget = null;
            eliteAssaultTargetForReserve = null;
            openCapturerSlots = 0;
            // Force elite fire support purchase when affordable — overrides the
            // "no elite on first buy" gate and the composition checks, so the AI
            // goes straight for the best fire support unit it can afford.
            if (eliteFireSupportTarget != null && remaining >= eliteFireSupportTarget.cost)
            {
                wantsEliteFireSupport = true;
                eliteFireSupportNowAffordable = true;
                openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
            }
            Debug.Log($"[AI Shopping] elite_assault_bloqueado: stance=Defensive fire_slots={openFireSupportSlots} → elite assault suprimido, cap_slots zerados, elite_fire={wantsEliteFireSupport}");
        }
        bool wantsEliteAssault = eliteAssaultTarget != null
            && (openAssaultSlots > 0 || strategicEliteAssaultReserve || nextTurnEliteAssaultReserve);
        if (strategicEliteAssaultReserve)
            Debug.Log($"[AI Shopping] reserva estrategica elite assalto: composicao completa alvo={eliteAssaultTarget.displayName} custo={eliteAssaultTarget.cost}");
        else if (nextTurnEliteAssaultReserve)
            Debug.Log($"[AI Shopping] reserva proximo turno elite assalto: alvo={eliteAssaultTargetForReserve.displayName} custo={eliteAssaultTargetForReserve.cost} cash={remaining} income={snapshot.IncomePerTurn}");
        bool eliteAssaultBought = false;
        bool defensiveBaseTankBought = false;
        int defensiveBaseResponseReserveCost = FindCheapestDefensiveBaseThreatPurchaseCost(snapshot);
        int defensiveBaseBasicMassCost = FindCheapestDefensiveBaseBasicMassPurchaseCost(snapshot);
        int defensiveArmorThreatCount = CountVisibleEnemyArmorNearOwnedBase(snapshot, DefensiveArmorThreatRange);
        bool intelArmorNearHome = HasIntelArmorThreatNearOwnBase(snapshot, intelReport, intelArmorThreat);
        bool defensiveArmorThreat = defensiveArmorThreatCount > 0 || intelArmorNearHome;
        bool strategicArmorThreat = intelArmorThreat && !defensiveArmorThreat;
        int activeArmorAssault = CountActiveArmoredAssaultUnits(snapshot);
        int visibleEnemyArmor = CountVisibleEnemyArmor(snapshot);
        int inferredEnemyArmor = Mathf.Max(
            visibleEnemyArmor,
            intelReport != null ? Mathf.CeilToInt(Mathf.Max(intelReport.enemyArmorForce, intelReport.enemyArmorThreatScore)) : 0);
        bool strategicArmorParity = !defensiveArmorThreat
            && inferredEnemyArmor > activeArmorAssault
            && (snapshot.Stance != AIStance.Defensive || visibleEnemyArmor > 0);
        if (strategicArmorParity && openAssaultSlots <= 0)
            openAssaultSlots = 1;
        if (defensiveArmorThreat)
            Debug.Log($"[AI Shopping] defesa blindada: visible={defensiveArmorThreatCount} <= {DefensiveArmorThreatRange}h base/HQ intelHome={intelArmorNearHome}");
        else if (strategicArmorThreat)
            Debug.Log($"[AI Shopping] blindado inimigo por intel: preparando resposta sem acionar defesa de base");
        if (strategicArmorParity)
            Debug.Log($"[AI Shopping] paridade blindada: enemy={inferredEnemyArmor} visible={visibleEnemyArmor} own={activeArmorAssault} -> assault_slots={openAssaultSlots}");
        int defensiveInfantryThreatCount = !defensiveArmorThreat
            ? CountVisibleEnemyInfantryNearOwnedBase(snapshot, DefensiveArmorThreatRange) : 0;
        int visibleEnemyCapturers = !defensiveArmorThreat
            ? CountVisibleEnemyCapturers(snapshot) : 0;
        float defensiveCapturePressure = intelReport != null
            ? Mathf.Max(intelReport.capturePressure, intelReport.landingPressure, intelReport.damageTakenScore)
            : 0f;
        float defensiveInfantryPressure = intelReport != null
            ? Mathf.Max(intelReport.enemyInfantryPressureScore, intelReport.enemyInfantryForce)
            : 0f;
        bool intelCaptureInfantryPressure = !defensiveArmorThreat
            && snapshot.Stance == AIStance.Defensive
            && Instance != null
            && defensiveCapturePressure >= Instance.IntelCapturePressureDefenseThreshold
            && defensiveInfantryPressure >= Instance.IntelInfantryPressureAssaultThreshold;
        bool intelInfantryNearHome = !defensiveArmorThreat
            && HasIntelInfantryThreatNearOwnBase(snapshot, intelReport);
        bool defensiveInfantryThreat = defensiveInfantryThreatCount >= 2
            || visibleEnemyCapturers >= 3
            || intelInfantryNearHome
            || intelCaptureInfantryPressure;
        if (defensiveInfantryThreat)
        {
            openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
            preferDefensiveFireSupport = true;
            offensiveAntiInfantryFireSupport = true;
            Debug.Log($"[AI Shopping] defesa anti-inf demand: nearBase={defensiveInfantryThreatCount} visibleCap={visibleEnemyCapturers} intelInf={defensiveInfantryPressure:F1} capture={defensiveCapturePressure:F1} criticalAir={criticalBaseAirThreat} -> fire={openFireSupportSlots}");
        }
        UnitData eliteDefensiveTankTarget = defensiveArmorThreat ? FindEliteDefensiveTankReserveTarget(snapshot) : null;
        int reserveForEliteDefensiveTank = 0;
        if (eliteDefensiveTankTarget != null && remaining < eliteDefensiveTankTarget.cost)
        {
            int nextTurnCash = remaining + Mathf.Max(0, snapshot.IncomePerTurn);
            if (nextTurnCash >= eliteDefensiveTankTarget.cost)
            {
                reserveForEliteDefensiveTank = Mathf.Min(remaining, Mathf.Max(0, eliteDefensiveTankTarget.cost - Mathf.Max(0, snapshot.IncomePerTurn)));
                Debug.Log($"[AI Shopping] reserva tank elite {eliteDefensiveTankTarget.displayName} custo={eliteDefensiveTankTarget.cost} cash={remaining} income={snapshot.IncomePerTurn} reserva={reserveForEliteDefensiveTank} gastoLivre={Mathf.Max(0, remaining - reserveForEliteDefensiveTank)}");
            }
        }
        UnitData supremeFireSupport = null;
        if (defensiveArmorThreat && !criticalBaseAirThreat)
        {
            supremeFireSupport = FindAffordableSupremeDefensiveFireSupportTarget(snapshot, remaining);
            if (supremeFireSupport != null)
            {
                eliteFireSupportTarget = supremeFireSupport;
                openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
                preferDefensiveFireSupport = true;
                wantsEliteFireSupport = true;
                reserveForEliteFireSupport = 0;
                Debug.Log($"[AI Shopping] defesa blindada suprema: priorizando fire_support elite{supremeFireSupport.eliteLevel} {supremeFireSupport.displayName} custo={supremeFireSupport.cost} cash={remaining}");
            }
        }
        if (defensiveInfantryThreat && !criticalBaseAirThreat)
        {
            UnitData antiInfTarget = FindAntiInfantryDefensiveTarget(snapshot, remaining);
            if (antiInfTarget != null)
            {
                eliteFireSupportTarget = antiInfTarget;
                openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
                preferDefensiveFireSupport = true;
                wantsEliteFireSupport = antiInfTarget.eliteLevel > 0;
                reserveForEliteFireSupport = 0;
                Debug.Log($"[AI Shopping] defesa anti-inf: {defensiveInfantryThreatCount} inf visíveis intelPressure={intelReport?.enemyInfantryPressureScore:F1}, alvo={antiInfTarget.displayName} custo={antiInfTarget.cost}");
            }
        }
        bool needsAffordableArmorFallback = defensiveArmorThreat
            && !criticalBaseAirThreat
            && supremeFireSupport == null
            && reserveForEliteDefensiveTank <= 0
            && !CanAffordEliteDefensiveTank(snapshot, remaining)
            && FindAffordableDefensiveBaseAssaultTankTarget(snapshot, remaining) == null;
        if (needsAffordableArmorFallback)
        {
            UnitData armorFallbackFireSupport = FindAffordableArmorFallbackFireSupportTarget(snapshot, remaining);
            if (armorFallbackFireSupport != null)
            {
                eliteFireSupportTarget = armorFallbackFireSupport;
                openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
                preferDefensiveFireSupport = true;
                wantsEliteFireSupport = true;
                reserveForEliteFireSupport = 0;
                Debug.Log($"[AI Shopping] defesa blindada fallback: sem caixa para elite tank, tentando apoio anti-blindado {armorFallbackFireSupport.displayName} custo={armorFallbackFireSupport.cost} cash={remaining}");
            }
        }
        int reserveForEliteAssault = 0;
        int eliteAssaultSafetyBuffer = 0;
        if (nextTurnEliteAssaultReserve)
        {
            int baseReserve = Mathf.Max(0, eliteAssaultTargetForReserve.cost - Mathf.Max(0, snapshot.IncomePerTurn));
            eliteAssaultSafetyBuffer = CalculateEliteAssaultSafetyBuffer(eliteAssaultTargetForReserve);
            reserveForEliteAssault = Mathf.Min(remaining, baseReserve + eliteAssaultSafetyBuffer);
        }

        if (reserveForEliteAssault > 0)
        {
            Debug.Log($"[AI Shopping] reserva elite assalto {eliteAssaultTargetForReserve.displayName} elite={eliteAssaultTargetForReserve.eliteLevel} custo={eliteAssaultTargetForReserve.cost} cash={remaining} income={snapshot.IncomePerTurn} reserva={reserveForEliteAssault} colchao={eliteAssaultSafetyBuffer} gastoLivre={Mathf.Max(0, remaining - reserveForEliteAssault)}");
        }

        // Separa edifícios por domínio das unidades que oferecem
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(snapshot.AISlotIndex));
        var landBuildings = new List<ConstructionManager>();
        var airBuildings  = new List<ConstructionManager>();
        foreach (ConstructionManager b in snapshot.MyBuildings)
        {
            if (b == null) continue;
            bool offersLand = false, offersAir = false;
            int offeredCount = 0;
            if (b.OfferedUnits != null)
                foreach (UnitData u in b.OfferedUnits)
                {
                    if (u == null) continue;
                    offeredCount++;
                    if (u.domain == Domain.Land) offersLand = true;
                    else if (u.domain == Domain.Air) offersAir = true;
                }
            if (offersLand) { landBuildings.Add(b); Vector3Int lc = b.CurrentCellPosition; lc.z = 0; Debug.Log($"[AI Shopping] produtor terrestre {b.ConstructionDisplayName}#{b.InstanceId} @ {lc} selling={b.SellingRule} offers={offeredCount}"); }
            else if (offersAir) { airBuildings.Add(b); Vector3Int ac = b.CurrentCellPosition; ac.z = 0; Debug.Log($"[AI Shopping] produtor aéreo {b.ConstructionDisplayName}#{b.InstanceId} @ {ac} selling={b.SellingRule} offers={offeredCount}"); }
            else
            {
                Vector3Int cell = b.CurrentCellPosition; cell.z = 0;
                Debug.Log($"[AI Shopping] produtor ignorado {b.ConstructionDisplayName}#{b.InstanceId} id={b.ConstructionId} @ {cell} team={b.TeamId} selling={b.SellingRule} offers={offeredCount} land={offersLand} air={offersAir}");
            }
        }

        if (airBuildings.Count > 0)
            Debug.Log($"[AI Shopping] aerodromos={airBuildings.Count} air_trans_slots={openAirTransportSlots} custo_heli={cheapestAirTransportCost}");

        int apcPassengerFollowupDemand = 0;
        int pendingGroundCapturerBuys = 0;

        // T1: sem reserva de elite — gasta tudo em capturadores e suporte básico.
        // reserveForAirTransport é mantida para que soldados não consumam o orçamento do chinook.
        if (snapshot.TurnNumber <= 1)
        {
            reserveForEliteAssault      = 0;
            reserveForEliteFireSupport  = 0;
            reserveForAirCombat         = 0;
            wantsEliteAssault           = false;
            wantsEliteFireSupport       = false;
        }

        var sortedBuildings = landBuildings;
        sortedBuildings.Sort((a, b) =>
        {
            bool baseDefenseProductionPending = criticalBaseAirThreat
                || defensiveArmorThreat
                || emergencyProductionDefense
                || proactiveDefFireSupport
                || openFireSupportSlots > 0
                || openAssaultSlots > 0;
            if (baseDefenseProductionPending)
            {
                int baseDefenseA = GetBaseDefenseProductionPriority(a, snapshot, criticalBaseAirThreat);
                int baseDefenseB = GetBaseDefenseProductionPriority(b, snapshot, criticalBaseAirThreat);
                if (baseDefenseA != baseDefenseB) return baseDefenseA.CompareTo(baseDefenseB);
            }

            int eliteA = wantsEliteAssault && !eliteAssaultBought && CanOfferUnit(a, eliteAssaultTarget) ? 0 : 1;
            int eliteB = wantsEliteAssault && !eliteAssaultBought && CanOfferUnit(b, eliteAssaultTarget) ? 0 : 1;
            if (eliteA != eliteB) return eliteA.CompareTo(eliteB);

            int eliteFireA = wantsEliteFireSupport && !eliteFireSupportBought && CanOfferUnit(a, eliteFireSupportTarget) ? 0 : 1;
            int eliteFireB = wantsEliteFireSupport && !eliteFireSupportBought && CanOfferUnit(b, eliteFireSupportTarget) ? 0 : 1;
            if (eliteFireA != eliteFireB) return eliteFireA.CompareTo(eliteFireB);

            bool manpowerShortage = HasDefensiveBaseManpowerShortage(snapshot);
            bool tankThreat = defensiveArmorThreat || manpowerShortage || defensiveInfantryThreat;
            bool supremeFirePending = defensiveArmorThreat
                && wantsEliteFireSupport
                && !eliteFireSupportBought
                && eliteFireSupportTarget != null
                && eliteFireSupportTarget.eliteLevel >= 2
                && IsDefensiveFireSupportPurchase(eliteFireSupportTarget)
                && remaining >= eliteFireSupportTarget.cost;
            int tankReserve = defensiveArmorThreat ? 0 : Mathf.Max(0, defensiveBaseBasicMassCost) * 2;
            int tankA = !supremeFirePending && tankThreat && (defensiveArmorThreat || HasVisibleEnemyNearBase(a, snapshot, DefensiveBaseThreatRange))
                && CanOfferAffordableDefensiveTank(a, remaining, tankReserve) ? 0 : 1;
            int tankB = !supremeFirePending && tankThreat && (defensiveArmorThreat || HasVisibleEnemyNearBase(b, snapshot, DefensiveBaseThreatRange))
                && CanOfferAffordableDefensiveTank(b, remaining, tankReserve) ? 0 : 1;
            if (tankA != tankB) return tankA.CompareTo(tankB);

            int transA = urgentTransportDemand && openTransportSlots > 0 && cheapestTransportCost > 0 && remaining >= cheapestTransportCost
                && CanOfferPrimaryRoleUnit(a, UnitRole.Transportador) ? 0 : 1;
            int transB = urgentTransportDemand && openTransportSlots > 0 && cheapestTransportCost > 0 && remaining >= cheapestTransportCost
                && CanOfferPrimaryRoleUnit(b, UnitRole.Transportador) ? 0 : 1;
            if (transA != transB) return transA.CompareTo(transB);

            int logA = openLogisticsSlots > 0 && CanOfferPrimaryRoleUnit(a, UnitRole.Logistica) ? 0 : 1;
            int logB = openLogisticsSlots > 0 && CanOfferPrimaryRoleUnit(b, UnitRole.Logistica) ? 0 : 1;
            if (logA != logB) return logA.CompareTo(logB);

            int fireA = openFireSupportSlots > 0 && CanOfferFireSupportUnit(a) ? 0 : 1;
            int fireB = openFireSupportSlots > 0 && CanOfferFireSupportUnit(b) ? 0 : 1;
            if (fireA != fireB) return fireA.CompareTo(fireB);

            return GetMinDistanceToOpenObjective(a, plan, snapshot.AITeam)
                .CompareTo(GetMinDistanceToOpenObjective(b, plan, snapshot.AITeam));
        });

        foreach (ConstructionManager building in sortedBuildings)
        {
            Vector3Int cell = building.CurrentCellPosition; cell.z = 0;
            if (!building.CanProduceUnitsForSlot(snapshot.AISlotIndex))
            {
                Debug.Log($"[AI Shopping] {building.ConstructionDisplayName} @ {cell} — não produz para o time, pulando");
                continue;
            }
            if (occupied.Contains(cell))
            {
                Debug.Log($"[AI Shopping] {building.ConstructionDisplayName} @ {cell} — célula ocupada, pulando");
                continue;
            }

            bool defensiveBaseThreat = HasVisibleEnemyNearBase(building, snapshot, DefensiveBaseThreatRange)
                || defensiveArmorThreat
                || emergencyProductionDefense;
            bool allowDefensiveEliteAssault = defensiveBaseThreat
                && wantsEliteAssault
                && !eliteAssaultBought
                && CanOfferUnit(building, eliteAssaultTarget)
                && remaining >= eliteAssaultTarget.cost + CalculateEliteAssaultSafetyBuffer(eliteAssaultTarget);
            bool defensiveBaseManpowerShortage = defensiveBaseThreat
                && (HasDefensiveBaseManpowerShortage(snapshot) || defensiveArmorThreat);
            int defensiveTankReserveCost = defensiveArmorThreat ? 0 : defensiveBaseResponseReserveCost;
            int defensiveMassReserveCost = defensiveArmorThreat ? 0 : defensiveBaseBasicMassCost;
            // Forced production: when enemies are within range of this building, or the army is
            // critically small in Defensive stance, spend the full remaining budget — no hoarding.
            const int ForcedProductionEnemyRange = 7;
            bool forcedProduction = snapshot.Stance == AIStance.Defensive
                && (HasVisibleEnemyNearCell(cell, snapshot, ForcedProductionEnemyRange)
                    || (snapshot.MyUnits != null && snapshot.MyUnits.Count <= 5));

            int spendBudget = remaining;
            if (forcedProduction)
            {
                // Zero all reserves — must produce something now.
                spendBudget = remaining;
                if (spendBudget != remaining)
                    Debug.Log($"[AI Shopping] {building.ConstructionDisplayName} @ {cell} — producao_forcada: inimigos próximos ou exercito critico, reservas ignoradas");
            }
            else if (defensiveArmorThreat
                && reserveForEliteDefensiveTank > 0
                && (eliteDefensiveTankTarget == null || remaining < eliteDefensiveTankTarget.cost))
            {
                spendBudget = Mathf.Max(0, remaining - reserveForEliteDefensiveTank);
            }
            if (!eliteAssaultBought && !forcedProduction)
            {
                bool canBuyEliteNow = wantsEliteAssault && !defensiveBaseThreat
                    && eliteAssaultTarget != null && remaining >= eliteAssaultTarget.cost;
                if (canBuyEliteNow)
                {
                    spendBudget = CanOfferUnit(building, eliteAssaultTarget)
                        ? remaining
                        : Mathf.Max(0, remaining - eliteAssaultTarget.cost);
                }
                else if (reserveForEliteAssault > 0
                         && (!defensiveBaseThreat || (!defensiveArmorThreat && !emergencyProductionDefense)))
                {
                    spendBudget = Mathf.Min(spendBudget, Mathf.Max(0, remaining - reserveForEliteAssault));
                }
            }
            if (wantsEliteFireSupport && !eliteFireSupportBought && !forcedProduction)
            {
                if (!defensiveBaseThreat)
                {
                    if (remaining >= eliteFireSupportTarget.cost)
                        spendBudget = CanOfferUnit(building, eliteFireSupportTarget)
                            ? spendBudget
                            : Mathf.Max(0, spendBudget - eliteFireSupportTarget.cost);
                    else if (reserveForEliteFireSupport > 0)
                        spendBudget = Mathf.Min(spendBudget, Mathf.Max(0, remaining - reserveForEliteFireSupport));
                }
                else if (!defensiveArmorThreat && !emergencyProductionDefense
                         && remaining < eliteFireSupportTarget.cost && reserveForEliteFireSupport > 0)
                {
                    spendBudget = Mathf.Min(spendBudget, Mathf.Max(0, remaining - reserveForEliteFireSupport));
                }
            }
            bool canBuyEliteBreakthroughHere = wantsEliteAssault
                && !eliteAssaultBought
                && eliteAssaultTarget != null
                && openAssaultSlots > 0
                && remaining >= eliteAssaultTarget.cost
                && CanOfferUnit(building, eliteAssaultTarget);
            bool canBuyEliteFireSupportHere = wantsEliteFireSupport
                && !eliteFireSupportBought
                && eliteFireSupportTarget != null
                && remaining >= eliteFireSupportTarget.cost
                && CanOfferUnit(building, eliteFireSupportTarget);
            // Reserva para transporte aéreo + combate aéreo. Defesa de base terrestre nao deve
            // drenar o caixa reservado quando ja existe demanda aerea e aerodromo disponivel.
            if ((reserveForAirTransport > 0
                    || reserveForAirCombat > 0
                    || reserveForAirSurveillance > 0)
                && !forcedProduction
                && !emergencyProductionDefense
                && !canBuyEliteBreakthroughHere
                && !canBuyEliteFireSupportHere)
            {
                int spendAfterAirReserves = Mathf.Max(
                    0,
                    remaining
                    - reserveForAirTransport
                    - reserveForAirCombat
                    - reserveForAirSurveillance);
                if (reserveForCapturerPassenger > 0 && openCapturerSlots > 0)
                    spendAfterAirReserves = Mathf.Max(spendAfterAirReserves, Mathf.Min(remaining, reserveForCapturerPassenger));
                spendBudget = Mathf.Min(spendBudget, spendAfterAirReserves);
            }
            else if (canBuyEliteBreakthroughHere || canBuyEliteFireSupportHere)
            {
                spendBudget = remaining;
                UnitData elitePriority = canBuyEliteBreakthroughHere
                    ? eliteAssaultTarget
                    : eliteFireSupportTarget;
                Debug.Log($"[AI Shopping] prioridade elite: {elitePriority.displayName} " +
                          $"libera reservas aereas neste produtor cash={remaining} ass_slots={openAssaultSlots}");
            }

            // Log das opções deste edifício
            {
                var offerLog = new System.Text.StringBuilder();
                offerLog.Append($"[AI Shopping] {building.ConstructionDisplayName} @ {cell} budget={spendBudget}/{remaining}:");
                if (building.OfferedUnits != null)
                    foreach (UnitData ou in building.OfferedUnits)
                        if (ou != null) offerLog.Append($" [{ou.displayName} ${ou.cost}]");
                Debug.Log(offerLog.ToString());
            }

            int effectiveOpenCapturerSlots = openCapturerSlots + apcPassengerFollowupDemand;
            UnitData unit = PickUnit(building, snapshot, spendBudget,
                effectiveOpenCapturerSlots, openAssaultSlots,
                openTransportSlots, urgentTransportDemand,
                openLogisticsSlots, openFireSupportSlots,
                openMobileAirSurveillanceSlots,
                preferDefensiveFireSupport,
                eliteAssaultTarget, eliteFireSupportTarget, defensiveBaseThreat,
                allowDefensiveEliteAssault, defensiveTankReserveCost,
                defensiveBaseManpowerShortage, defensiveMassReserveCost, defensiveBaseTankBought,
                defensiveArmorThreat, strategicArmorParity, wantsEliteFireSupport, activeFireSupportCount,
                proactiveDefFireSupport, proactiveAntiAir, activeSAMs, activeAAAs, aaaCap, aaaThreat,
                defensiveInfantryThreat, offensiveAntiInfantryFireSupport,
                matureEconomyEliteAssaultPivot,
                intelReport != null ? Mathf.Max(intelReport.enemyInfantryPressureScore, intelReport.enemyInfantryForce) : 0f,
                intelReport != null ? Mathf.Max(intelReport.enemyArmorThreatScore, intelReport.enemyArmorForce) : 0f);
            if (unit == null && forcedProduction)
            {
                unit = FindCheapestAffordableLandUnit(building, remaining);
                if (unit != null)
                    Debug.Log($"[AI Shopping] {building.ConstructionDisplayName} @ {cell} — producao_forcada: comprando {unit.displayName} ${unit.cost} (fallback emergencia)");
            }
            if (unit == null)
            {
                Debug.Log($"[AI Shopping] {building.ConstructionDisplayName} @ {cell} — nenhuma unidade selecionada (sem fit ou sem budget)");
                continue;
            }

            if (defensiveBaseThreat)
            {
                string heavyStatus = IsDefensiveBaseAssaultTankPurchase(unit)
                    ? $" tanque-ok reserva={defensiveTankReserveCost}"
                    : IsDefensiveBaseBasicMassPurchase(unit) ? " numeros-ok"
                    : allowDefensiveEliteAssault ? " elite-ok" : string.Empty;
                string threatLabel = defensiveArmorThreat ? $" armored<={DefensiveArmorThreatRange}h" : " inimigo<=3h";
                Debug.Log($"[AI Shopping] defesa base {building.ConstructionDisplayName}{threatLabel}{heavyStatus} -> {unit.displayName}");
            }

            int idx = IndexOf(building.OfferedUnits, unit);
            orders.Add(new ShoppingOrder
            {
                Building      = building,
                UnitToBuy     = unit,
                SelectedIndex = idx,
            });

            remaining -= unit.cost;
            occupied.Add(cell);
            bool boughtAggressiveCapturer = IsPrimaryRole(unit, UnitRole.CapturadorAgressivo);
            if ((IsPrimaryRole(unit, UnitRole.Capturador) || boughtAggressiveCapturer) && openCapturerSlots > 0)
            {
                openCapturerSlots--;
                pendingGroundCapturerBuys++;
            }
            else if (IsPrimaryRole(unit, UnitRole.Capturador) && apcPassengerFollowupDemand > 0)
            {
                apcPassengerFollowupDemand--;
                pendingGroundCapturerBuys++;
                Debug.Log($"[AI Shopping] APC followup: passageiro comprado para transporte terrestre -> {unit.displayName}");
            }
            else if ((IsPrimaryRole(unit, UnitRole.Assalto) || boughtAggressiveCapturer) && openAssaultSlots > 0)
                openAssaultSlots--;
            else if (UnitRoleCompatibility.ResolveCompositionRole(unit) == UnitRole.Transportador
                && openTransportSlots > 0)
            {
                openTransportSlots--;
                if (ShouldSeedCapturerForNewAPC(snapshot, openCapturerSlots, pendingGroundCapturerBuys, apcPassengerFollowupDemand))
                {
                    apcPassengerFollowupDemand++;
                    Debug.Log($"[AI Shopping] APC followup: {unit.displayName} comprado, abrindo 1 capturador para embarque no proximo turno");
                }
            }
            else if (IsPrimaryRole(unit, UnitRole.Logistica) && openLogisticsSlots > 0)
                openLogisticsSlots--;
            else if (IsFireSupportPurchase(unit) && openFireSupportSlots > 0)
                openFireSupportSlots--;
            else if (IsDedicatedAirSurveillancePurchase(unit)
                && openMobileAirSurveillanceSlots > 0)
                openMobileAirSurveillanceSlots--;
            if (unit == eliteAssaultTarget)
                eliteAssaultBought = true;
            if (unit == eliteFireSupportTarget)
                eliteFireSupportBought = true;
            if (IsDefensiveBaseAssaultTankPurchase(unit))
                defensiveBaseTankBought = true;
            if (IsAntiAirOnlyUnit(unit) && IsPrimaryRole(unit, UnitRole.FogoIndireto))
                activeSAMs++;
            if (IsAntiAirOnlyUnit(unit) && IsPrimaryRole(unit, UnitRole.Assalto))
                activeAAAs++;

            if (remaining <= 0) break;
        }

        // Air shopping — separate pass over air buildings (transporters, interceptors, ground attack)
        {
            bool wantsAirTransport = openAirTransportSlots > 0;
            bool wantsCacaB        = openCacaBSlots  > 0;
            bool wantsCacaA        = openCacaASlots  > 0;
            bool wantsApache       = openApacheSlots > 0;
            bool wantsBomba        = openBombaSlots  > 0;
            bool wantsAirTanker    = openAirTankerSlots > 0;
            bool wantsAirSurveillance =
                openAirSurveillanceSlots > 0;
            bool anyAirDemand =
                wantsAirTransport || wantsCacaB || wantsCacaA
                || wantsApache || wantsBomba || wantsAirTanker
                || wantsAirSurveillance;
            bool urgentCacaB       = HasUrgentCacaBThreat(snapshot, intelReport);

            if (airBuildings.Count > 0 && anyAirDemand)
            {
                foreach (ConstructionManager building in airBuildings)
                {
                    Vector3Int cell = building.CurrentCellPosition; cell.z = 0;
                    if (!building.CanProduceUnitsForSlot(snapshot.AISlotIndex))
                    {
                        Debug.Log($"[AI Shopping Air] {building.ConstructionDisplayName} @ {cell} — não produz para o time, pulando");
                        continue;
                    }
                    if (occupied.Contains(cell))
                    {
                        Debug.Log($"[AI Shopping Air] {building.ConstructionDisplayName} @ {cell} — célula ocupada, pulando");
                        continue;
                    }

                    UnitData airUnit = PickAirUnit(building, remaining,
                        wantsAirTransport, wantsCacaB, wantsCacaA,
                        wantsApache, wantsBomba, wantsAirTanker,
                        wantsAirSurveillance,
                        urgentCacaB,
                        snapshot.AITeam);
                    if (airUnit == null)
                    {
                        Debug.Log($"[AI Shopping Air] {building.ConstructionDisplayName} @ {cell} — sem unidade aérea disponível ou sem budget");
                        continue;
                    }

                    int idx = IndexOf(building.OfferedUnits, airUnit);
                    orders.Add(new ShoppingOrder { Building = building, UnitToBuy = airUnit, SelectedIndex = idx });
                    Debug.Log($"[AI Shopping Air] {building.ConstructionDisplayName} @ {cell} → compra {airUnit.displayName} ${airUnit.cost}");
                    remaining -= airUnit.cost;
                    occupied.Add(cell);

                    bool isIntercept = IsPrimaryRole(airUnit, UnitRole.Interceptador);
                    bool isAtaque    = IsPrimaryRole(airUnit, UnitRole.AtaqueAereo);
                    bool isElite     = airUnit.eliteLevel >= 1;
                    if (UnitRoleCompatibility.ResolveCompositionRole(airUnit) == UnitRole.Transportador) { if (openAirTransportSlots > 0) openAirTransportSlots--; wantsAirTransport = openAirTransportSlots > 0; }
                    else if (isIntercept && !isElite)                   { if (openCacaBSlots  > 0) openCacaBSlots--;  wantsCacaB  = openCacaBSlots  > 0; }
                    else if (isIntercept &&  isElite)                   { if (openCacaASlots  > 0) openCacaASlots--;  wantsCacaA  = openCacaASlots  > 0; }
                    else if (isAtaque    && !isElite)                   { if (openApacheSlots > 0) openApacheSlots--; wantsApache = openApacheSlots > 0; }
                    else if (isAtaque    &&  isElite)                   { if (openBombaSlots  > 0) openBombaSlots--;  wantsBomba  = openBombaSlots  > 0; }
                    else if (IsPrimaryRole(airUnit, UnitRole.Logistica)) { if (openAirTankerSlots > 0) openAirTankerSlots--; wantsAirTanker = openAirTankerSlots > 0; }
                    else if (IsDedicatedAirSurveillancePurchase(airUnit))
                    {
                        if (openAirSurveillanceSlots > 0)
                            openAirSurveillanceSlots--;
                        wantsAirSurveillance =
                            openAirSurveillanceSlots > 0;
                    }

                    if (!wantsAirTransport && !wantsCacaB
                        && !wantsCacaA && !wantsApache
                        && !wantsBomba && !wantsAirTanker
                        && !wantsAirSurveillance)
                        break;
                    if (remaining <= 0) break;
                }
            }
        }

        return orders;
    }
    private static HashSet<Vector3Int> BuildProductionOccupiedCells()
    {
        var occupied = new HashSet<Vector3Int>();
        List<UnitManager> units = UnitManager.AllActive;
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || unit.IsDead || unit.IsEmbarked)
                continue;
            if (unit.GetHeightLevel() != HeightLevel.Surface)
                continue;

            Vector3Int cell = unit.CurrentCellPosition;
            cell.z = 0;
            occupied.Add(cell);
        }

        return occupied;
    }
    private static int IndexOf(IReadOnlyList<UnitData> list, UnitData target)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == target) return i;
        return 0;
    }
}
