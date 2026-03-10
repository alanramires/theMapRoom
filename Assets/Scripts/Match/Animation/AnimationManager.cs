using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class UnitMoveAnimationSpeedOverride
{
    [Tooltip("UnitData alvo do override de velocidade.")]
    public UnitData unitData;
    [Range(0.1f, 4f)] public float speed = 1f;
}

public class AnimationManager : MonoBehaviour
{
    public static AnimationManager Instance { get; private set; }
    [SerializeField] private CursorController cursorController;

    [Header("Selection Visual")]
    [SerializeField] [Range(0.05f, 1f)] private float selectionBlinkActiveDuration = 0.16f;
    [SerializeField] [Range(0.05f, 1f)] private float selectionBlinkInactiveDuration = 0.16f;
    [Header("Movement Animation")]
    [SerializeField] [Range(0.04f, 0.4f)] private float moveStepDuration = 0.12f;
    [SerializeField] [Range(0f, 0.35f)] private float moveArcHeight = 0.05f;
    [SerializeField] private AnimationCurve moveStepCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Overrides de velocidade por UnitData. Se vazio/sem match, usa 1.")]
    [SerializeField] private List<UnitMoveAnimationSpeedOverride> unitMoveSpeedOverrides = new List<UnitMoveAnimationSpeedOverride>();
    [Header("Embark Sequence Timing")]
    [Tooltip("Tempo da animacao de pouso forcado do transportador antes do embarque.")]
    [SerializeField] [Range(0f, 2f)] private float embarkForcedLandingDuration = 0.25f;
    [Tooltip("Pausa entre o fim do pouso forcado e o inicio da animacao de embarque.")]
    [SerializeField] [Range(0f, 2f)] private float embarkAfterForcedLandingDelay = 0.10f;
    [Tooltip("Duracao do embarque padrao (somente deslize ate o transportador).")]
    [FormerlySerializedAs("embarkMoveStepDuration")]
    [SerializeField] [Range(0.04f, 0.8f)] private float embarkDefaultMoveStepDuration = 0.12f;
    [Tooltip("Duracao total do embarque para passageiro em Air High ate Ground.")]
    [FormerlySerializedAs("embarkAirHighToLowDuration")]
    [SerializeField] [Range(0.04f, 2f)] private float embarkAirHighToGroundDuration = 0.10f;
    [Tooltip("Duracao total do embarque para passageiro em Air Low ate Ground.")]
    [SerializeField] [Range(0f, 2f)] private float embarkAirLowToGroundDuration = 0.05f;
    [Tooltip("Pausa apos a animacao de embarque antes de concluir o ciclo.")]
    [SerializeField] [Range(0f, 2f)] private float embarkAfterMoveDelay = 0.15f;
    [Header("Embark Layer Transition Timing")]
    [Tooltip("Tempo normalizado (0..1) para concluir AirHigh->AirLow durante embarque em voo alto.")]
    [SerializeField] [Range(0.01f, 0.99f)] private float embarkHighToLowNormalizedTime = 0.50f;
    [Tooltip("Tempo normalizado (0..1) para concluir AirLow->Ground durante embarque iniciando em AirLow.")]
    [SerializeField] [Range(0.01f, 1f)] private float embarkLowToGroundNormalizedTime = 1.00f;
    [Header("Disembark Sequence Timing")]
    [Tooltip("Tempo da animacao de pouso forcado do transportador antes do desembarque.")]
    [SerializeField] [Range(0f, 2f)] private float disembarkForcedLandingDuration = 0.25f;
    [Tooltip("Pausa apos o pouso forcado do transportador antes do desembarque.")]
    [SerializeField] [Range(0f, 2f)] private float disembarkAfterForcedLandingDelay = 0.10f;
    [Tooltip("Pausa antes de spawnar os passageiros em cima do transportador.")]
    [SerializeField] [Range(0f, 2f)] private float disembarkBeforeSpawnDelay = 0.10f;
    [Tooltip("Pausa apos spawnar os passageiros antes de mover para os hexes destino.")]
    [SerializeField] [Range(0f, 2f)] private float disembarkAfterSpawnDelay = 0.15f;
    [Tooltip("Intervalo entre o spawn de um passageiro e o proximo (quando houver mais de um).")]
    [SerializeField] [Range(0f, 2f)] private float disembarkSpawnStepDelay = 0.08f;
    [Tooltip("Pausa apos o passageiro terminar o movimento antes de confirmar o desembarque dele.")]
    [SerializeField] [Range(0f, 2f)] private float disembarkAfterPassengerMoveDelay = 0.10f;
    [Tooltip("Pausa apos tocar load/encerrar um passageiro antes de iniciar o proximo.")]
    [SerializeField] [Range(0f, 2f)] private float disembarkAfterPassengerLoadDelay = 0.12f;
    [Tooltip("Pausa apos os movimentos de desembarque antes de finalizar a acao.")]
    [SerializeField] [Range(0f, 2f)] private float disembarkAfterMoveDelay = 0.15f;
    [Tooltip("Pausa apos encerrar o transportador antes de liberar o cursor/estado.")]
    [SerializeField] [Range(0f, 2f)] private float disembarkAfterTransporterDoneDelay = 0.10f;
    [Tooltip("Duracao total do pouso para transportador em Air High ate Ground no desembarque.")]
    [SerializeField] [Range(0.04f, 2f)] private float disembarkAirHighToGroundDuration = 0.10f;
    [Tooltip("Duracao total do pouso para transportador em Air Low ate Ground no desembarque.")]
    [SerializeField] [Range(0f, 2f)] private float disembarkAirLowToGroundDuration = 0.05f;
    [Header("Disembark Layer Transition Timing")]
    [Tooltip("Tempo normalizado (0..1) para concluir AirHigh->AirLow durante pouso para desembarque.")]
    [SerializeField] [Range(0.01f, 0.99f)] private float disembarkHighToLowNormalizedTime = 0.50f;
    [Tooltip("Tempo normalizado (0..1) para concluir AirLow->Ground durante pouso para desembarque.")]
    [SerializeField] [Range(0.01f, 1f)] private float disembarkLowToGroundNormalizedTime = 1.00f;
    [Header("Capture Sequence Timing")]
    [Tooltip("Pausa entre confirmar captura e tocar capturing SFX.")]
    [SerializeField] [Range(0f, 2f)] private float capturePreSfxDelay = 0.12f;
    [Tooltip("Pausa apos capturing SFX antes de aplicar o dano de captura.")]
    [SerializeField] [Range(0f, 2f)] private float capturePostCapturingSfxDelay = 0.12f;
    [Tooltip("Pausa apos done SFX no fluxo sem captura total.")]
    [SerializeField] [Range(0f, 2f)] private float capturePostDoneSfxDelay = 0.05f;
    [Tooltip("Pausa apos captured SFX no fluxo de captura total.")]
    [SerializeField] [Range(0f, 2f)] private float capturePostCapturedSfxDelay = 0.10f;
    [Header("Mirando Preview Line")]
    [Tooltip("Material usado na linha de preview de tiro durante o estado Mirando.")]
    [SerializeField] private Material mirandoPreviewMaterial;
    [Tooltip("Cor da linha de preview de tiro.")]
    [SerializeField] private Color mirandoPreviewColor = new Color(1f, 0.65f, 0.2f, 0.95f);
    [Tooltip("Largura da linha de preview de tiro.")]
    [SerializeField] [Range(0.03f, 0.4f)] private float mirandoPreviewWidth = 0.12f;
    [Tooltip("Velocidade do segmento animado (efeito sprinkler).")]
    [SerializeField] [Range(0.2f, 8f)] private float mirandoPreviewSpeed = 3f;
    [Tooltip("Comprimento do segmento animado da linha de preview.")]
    [SerializeField] [Range(0.2f, 5f)] private float mirandoPreviewSegmentLength = 1.1f;
    [Tooltip("Quantidade de segmentos animados simultaneos no preview de Mirando.")]
    [SerializeField] [Range(1, 8)] private int mirandoPreviewSegmentQuantities = 1;
    [Tooltip("Multiplicador do preview de spotter em Mirando (largura e comprimento do traco).")]
    [SerializeField] [Range(0.2f, 2f)] private float mirandoSpotterPreviewMultiplier = 0.55f;
    [Tooltip("Quantidade de segmentos animados simultaneos para cada linha de spotter no preview de Mirando.")]
    [SerializeField] [Range(1, 8)] private int mirandoSpotterSegmentQuantities = 1;
    [Tooltip("Velocidade dos segmentos animados das linhas de spotter no preview de Mirando.")]
    [SerializeField] [Range(0.2f, 8f)] private float mirandoSpotterSegmentSpeed = 3f;
    [Tooltip("Intensidade da curvatura da parabola para armas parabolic.")]
    [SerializeField] [Range(0.2f, 4f)] private float mirandoParabolaBend = 1.2f;
    [Tooltip("Curvatura minima para tiros quase verticais (evita arco exagerado).")]
    [SerializeField] [Range(0.01f, 0.3f)] private float mirandoParabolaMinVerticalBend = 0.05f;
    [Tooltip("Peso angular da parabola: 1 = linear, >1 achata mais em vertical, <1 curva mais em diagonal.")]
    [SerializeField] [Range(0.2f, 3f)] private float mirandoParabolaHorizontalBendWeight = 0.85f;
    [Tooltip("Quantidade de pontos amostrados na curva parabolica.")]
    [SerializeField] [Range(8, 64)] private int mirandoParabolaSamples = 24;
    [Tooltip("Sorting layer da linha de preview de tiro.")]
    [SerializeField] private SortingLayerReference mirandoPreviewSortingLayer;
    [SerializeField, HideInInspector] private bool mirandoPreviewSortingLayerInitialized;
    [Tooltip("Sorting order da linha de preview de tiro.")]
    [SerializeField] private int mirandoPreviewSortingOrder = 120;
    [Header("Merge Queue Preview Line")]
    [Tooltip("Multiplicador da linha de fusao (largura e comprimento do traco).")]
    [SerializeField] [Range(0.2f, 2f)] private float mergeQueuePreviewMultiplier = 0.55f;
    [Tooltip("Quantidade de segmentos animados simultaneos na linha de fusao.")]
    [SerializeField] [Range(1, 8)] private int mergeQueuePreviewSegmentQuantities = 1;
    [Tooltip("Velocidade dos segmentos animados da linha de fusao.")]
    [SerializeField] [Range(0.2f, 8f)] private float mergeQueuePreviewSegmentSpeed = 3f;
    [Tooltip("Multiplicador de intervalo entre segmentos da linha de fusao (1 = padrao).")]
    [SerializeField] [Range(0.2f, 3f)] private float mergeQueuePreviewSegmentSpacingMultiplier = 1f;
    [Header("Merge Sequence Timing")]
    [Tooltip("Duracao base de cada passo de movimento da unidade que vai fundir.")]
    [SerializeField] [Range(0.04f, 2f)] private float mergeMoveStepDuration = 0.20f;
    [Tooltip("Pausa apos o cursor saltar para o proximo doador antes de iniciar o movimento.")]
    [SerializeField] [Range(0f, 2f)] private float mergeCursorHopDelay = 0.06f;
    [Tooltip("Pausa apos o movimento do doador antes de tocar load/desaparecer.")]
    [SerializeField] [Range(0f, 2f)] private float mergeAfterParticipantMoveDelay = 0.10f;
    [Tooltip("Pausa apos load/desaparecer de um doador antes do proximo.")]
    [SerializeField] [Range(0f, 2f)] private float mergeAfterParticipantLoadDelay = 0.12f;
    [Header("VTOL Landing FX")]
    [Tooltip("Frames da animacao de poeira de pouso VTOL (em ordem).")]
    [SerializeField] private Sprite[] vtolLandingFrames;
    [Tooltip("Duracao por frame da animacao de pouso VTOL.")]
    [SerializeField] [Range(0.02f, 0.2f)] private float vtolLandingFrameDuration = 0.06f;
    [Tooltip("Somente unidades com pelo menos uma skill desta lista podem tocar o FX de pouso VTOL.")]
    [SerializeField] private List<SkillData> vtolLandingFxAllowedSkills = new List<SkillData>();
    [SerializeField] private SortingLayerReference vtolLandingSortingLayer;
    [SerializeField, HideInInspector] private bool vtolLandingSortingLayerInitialized;
    [SerializeField] private int vtolLandingSortingOrder = 135;
    [SerializeField] [Range(0.2f, 4f)] private float vtolLandingScale = 1f;
    [Header("Taking Hit FX")]
    [Tooltip("Frames da animacao de hit (em ordem).")]
    [SerializeField] private Sprite[] takingHitFrames;
    [Tooltip("Duracao por frame da animacao de taking hit.")]
    [SerializeField] [Range(0.02f, 0.2f)] private float takingHitFrameDuration = 0.06f;
    [Tooltip("Duracao total do flash (pisca vermelho/branco) ao tomar hit.")]
    [SerializeField] [Range(0.02f, 1f)] private float takingHitFlashDuration = 0.15f;
    [Tooltip("Intervalo do flash entre branco e vermelho.")]
    [SerializeField] [Range(0.01f, 0.2f)] private float takingHitFlashInterval = 0.03f;
    [Tooltip("Cor do hit flash quando alterna para estado de dano.")]
    [SerializeField] private Color takingHitFlashDamageColor = new Color(1f, 0.3f, 0.3f, 1f);
    [Tooltip("Duracao do shake ao tomar hit.")]
    [SerializeField] [Range(0.02f, 1f)] private float takingHitShakeDuration = 0.12f;
    [Tooltip("Magnitude do shake ao tomar hit.")]
    [SerializeField] [Range(0.001f, 0.2f)] private float takingHitShakeMagnitude = 0.03f;
    [SerializeField] private SortingLayerReference takingHitSortingLayer;
    [SerializeField, HideInInspector] private bool takingHitSortingLayerInitialized;
    [SerializeField] private int takingHitSortingOrder = 140;
    [SerializeField] [Range(0.2f, 4f)] private float takingHitScale = 1f;
    [Header("Explosion FX")]
    [Tooltip("Frames da animacao de explosao (em ordem).")]
    [SerializeField] private Sprite[] explosionFrames;
    [Tooltip("Duracao por frame da animacao de explosao.")]
    [SerializeField] [Range(0.02f, 0.2f)] private float explosionFrameDuration = 0.06f;
    [SerializeField] private SortingLayerReference explosionSortingLayer;
    [SerializeField, HideInInspector] private bool explosionSortingLayerInitialized;
    [SerializeField] private int explosionSortingOrder = 145;
    [SerializeField] [Range(0.2f, 4f)] private float explosionScale = 1f;
    [Header("Combat Projectile FX")]
    [Tooltip("Velocidade base do projetil (units/s).")]
    [SerializeField] [Range(0.5f, 20f)] private float combatProjectileSpeed = 9f;
    [Tooltip("Duracao minima do voo do projetil.")]
    [SerializeField] [Range(0.03f, 2f)] private float combatProjectileMinDuration = 0.06f;
    [Tooltip("Escala do sprite do projetil.")]
    [SerializeField] [Range(0.05f, 3f)] private float combatProjectileScale = 0.75f;
    [Tooltip("Curvatura para trajetoria parabolica de projetil.")]
    [SerializeField] [Range(0.2f, 4f)] private float combatProjectileParabolaBend = 1.2f;
    [Tooltip("Delay antes do revide (se houver).")]
    [SerializeField] [Range(0f, 1f)] private float combatCounterShotDelay = 0.10f;
    [SerializeField] private SortingLayerReference combatProjectileSortingLayer;
    [SerializeField, HideInInspector] private bool combatProjectileSortingLayerInitialized;
    [SerializeField] private int combatProjectileSortingOrder = 210;
    [Header("Supply Sequence Timing")]
    [Tooltip("Pausa curta apos mover o cursor para permitir foco visual antes da animacao de suprimento.")]
    [SerializeField] [Range(0f, 0.5f)] private float supplyCursorFocusDelay = 0.10f;
    [Tooltip("Intervalo entre cada item de servico surgindo na animacao de suprimento.")]
    [SerializeField] [Range(0f, 1f)] private float supplySpawnInterval = 0.12f;
    [Tooltip("Padding apos o tempo de voo do item antes de aplicar o servico no alvo.")]
    [SerializeField] [Range(0f, 1f)] private float supplyFlightPadding = 0.05f;
    [Tooltip("Pausa apos concluir um alvo (load + hasActed) antes de ir para o proximo.")]
    [SerializeField] [Range(0f, 1f)] private float supplyPostTargetDelay = 0.18f;
    [Tooltip("Pausa final do supridor antes de tocar done e encerrar a acao.")]
    [SerializeField] [Range(0f, 2f)] private float supplySupplierFinalDelay = 0.25f;
    [Header("Money UI Timing")]
    [Tooltip("Duracao do fade do texto de atualizacao de dinheiro (text_update).")]
    [SerializeField] [Range(0.05f, 5f)] private float moneyUpdateFadeDuration = 1.2f;
    [Header("Inspect")]
    [Tooltip("Tempo de exibicao do panel helper ao inspecionar unidade.")]
    [SerializeField] [Range(0.5f, 20f)] private float inspectUnitDisplayDuration = 4f;
    [Tooltip("Tempo de exibicao do panel helper ao inspecionar construcao.")]
    [SerializeField] [Range(0.5f, 20f)] private float inspectConstructionDisplayDuration = 4f;
    [Header("Supply Projectile FX")]
    [Tooltip("Velocidade do item voando no suprimento (units/s).")]
    [SerializeField] [Range(0.2f, 20f)] private float supplyProjectileSpeed = 5f;
    [Tooltip("Duracao minima do voo do item de suprimento.")]
    [SerializeField] [Range(0.03f, 2f)] private float supplyProjectileMinDuration = 0.12f;
    [Tooltip("Escala do sprite do item de suprimento.")]
    [SerializeField] [Range(0.05f, 3f)] private float supplyProjectileScale = 0.8f;
    [Header("Combat Death Sequence")]
    [Tooltip("Atraso antes de iniciar a sequencia de morte/explosao apos fim dos tiros.")]
    [SerializeField] [Range(0f, 2f)] private float combatDeathStartDelay = 0.20f;
    [Header("Combat Bump FX")]
    [Tooltip("Distancia do bico (ambos se aproximam e voltam).")]
    [SerializeField] [Range(0.02f, 0.5f)] private float combatBumpDistance = 0.12f;
    [Tooltip("Duracao de ida (e de volta) do bico.")]
    [SerializeField] [Range(0.03f, 0.4f)] private float combatBumpDuration = 0.10f;
    [Header("Ranged Attack Hit FX")]
    [Tooltip("Duracao padrao da animacao de impacto ranged no defensor.")]
    [SerializeField] [Range(0.05f, 2f)] private float rangedAttackDefenderAnimDuration = 0.55f;
    [Tooltip("Raio inicial do quadrado que fecha no defensor.")]
    [SerializeField] [Range(0.2f, 4f)] private float rangedAttackSquareExtent = 1.2f;
    [Tooltip("Espessura de cada faixa do quadrado.")]
    [SerializeField] [Range(0.05f, 1.2f)] private float rangedAttackSquareBarThickness = 0.28f;
    [Tooltip("Cor do quadrado de impacto.")]
    [SerializeField] private Color rangedAttackSquareColor = new Color(0f, 0f, 0f, 0.9f);
    [Tooltip("Curva de fechamento do quadrado de impacto.")]
    [SerializeField] private AnimationCurve rangedAttackSquareCloseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private SortingLayerReference rangedAttackSquareSortingLayer;
    [SerializeField, HideInInspector] private bool rangedAttackSquareSortingLayerInitialized;
    [SerializeField] private int rangedAttackSquareSortingOrder = 220;

    private Coroutine movementRoutine;
    private UnitManager selectedBlinkUnit;
    private static Sprite cachedWhiteSprite;

    public bool IsAnimatingMovement => movementRoutine != null;
    public float CombatCounterShotDelay => Mathf.Clamp(combatCounterShotDelay, 0f, 1f);
    public float CombatDeathStartDelay => Mathf.Clamp(combatDeathStartDelay, 0f, 2f);
    public Material MirandoPreviewMaterial => mirandoPreviewMaterial;
    public Color MirandoPreviewColor => mirandoPreviewColor;
    public float MirandoPreviewWidth => Mathf.Clamp(mirandoPreviewWidth, 0.03f, 0.4f);
    public float MirandoPreviewSpeed => Mathf.Clamp(mirandoPreviewSpeed, 0.2f, 8f);
    public float MirandoPreviewSegmentLength => Mathf.Clamp(mirandoPreviewSegmentLength, 0.2f, 5f);
    public int MirandoPreviewSegmentQuantities => Mathf.Clamp(mirandoPreviewSegmentQuantities, 1, 8);
    public float MirandoSpotterPreviewMultiplier => Mathf.Clamp(mirandoSpotterPreviewMultiplier, 0.2f, 2f);
    public int MirandoSpotterSegmentQuantities => Mathf.Clamp(mirandoSpotterSegmentQuantities, 1, 8);
    public float MirandoSpotterSegmentSpeed => Mathf.Clamp(mirandoSpotterSegmentSpeed, 0.2f, 8f);
    public float MergeQueuePreviewMultiplier => Mathf.Clamp(mergeQueuePreviewMultiplier, 0.2f, 2f);
    public int MergeQueuePreviewSegmentQuantities => Mathf.Clamp(mergeQueuePreviewSegmentQuantities, 1, 8);
    public float MergeQueuePreviewSegmentSpeed => Mathf.Clamp(mergeQueuePreviewSegmentSpeed, 0.2f, 8f);
    public float MergeQueuePreviewSegmentSpacingMultiplier => Mathf.Clamp(mergeQueuePreviewSegmentSpacingMultiplier, 0.2f, 3f);
    public float MergeMoveStepDuration => Mathf.Clamp(mergeMoveStepDuration, 0.04f, 2f);
    public float MergeCursorHopDelay => Mathf.Clamp(mergeCursorHopDelay, 0f, 2f);
    public float MergeAfterParticipantMoveDelay => Mathf.Clamp(mergeAfterParticipantMoveDelay, 0f, 2f);
    public float MergeAfterParticipantLoadDelay => Mathf.Clamp(mergeAfterParticipantLoadDelay, 0f, 2f);
    public float MirandoParabolaBend => Mathf.Clamp(mirandoParabolaBend, 0.2f, 4f);
    public float MirandoParabolaMinVerticalBend => Mathf.Clamp(mirandoParabolaMinVerticalBend, 0.01f, 0.3f);
    public float MirandoParabolaHorizontalBendWeight => Mathf.Clamp(mirandoParabolaHorizontalBendWeight, 0.2f, 3f);
    public int MirandoParabolaSamples => Mathf.Clamp(mirandoParabolaSamples, 8, 64);
    public int MirandoPreviewSortingLayerId => mirandoPreviewSortingLayer.Id;
    public int MirandoPreviewSortingOrder => mirandoPreviewSortingOrder;
    public float SupplyCursorFocusDelay => Mathf.Clamp(supplyCursorFocusDelay, 0f, 0.5f);
    public float SupplySpawnInterval => Mathf.Clamp(supplySpawnInterval, 0f, 1f);
    public float SupplyFlightPadding => Mathf.Clamp(supplyFlightPadding, 0f, 1f);
    public float SupplyPostTargetDelay => Mathf.Clamp(supplyPostTargetDelay, 0f, 1f);
    public float SupplySupplierFinalDelay => Mathf.Clamp(supplySupplierFinalDelay, 0f, 2f);
    public float MoneyUpdateFadeDuration => Mathf.Clamp(moneyUpdateFadeDuration, 0.05f, 5f);
    public float InspectUnitDisplayDuration => Mathf.Clamp(inspectUnitDisplayDuration, 0.5f, 20f);
    public float InspectConstructionDisplayDuration => Mathf.Clamp(inspectConstructionDisplayDuration, 0.5f, 20f);
    public float SupplyProjectileSpeed => Mathf.Clamp(supplyProjectileSpeed, 0.2f, 20f);
    public float SupplyProjectileMinDuration => Mathf.Clamp(supplyProjectileMinDuration, 0.03f, 2f);
    public float SupplyProjectileScale => Mathf.Clamp(supplyProjectileScale, 0.05f, 3f);
    public float EmbarkForcedLandingDuration => Mathf.Clamp(embarkForcedLandingDuration, 0f, 2f);
    public float EmbarkAfterForcedLandingDelay => Mathf.Clamp(embarkAfterForcedLandingDelay, 0f, 2f);
    public float EmbarkDefaultMoveStepDuration => Mathf.Clamp(embarkDefaultMoveStepDuration, 0.04f, 0.8f);
    public float EmbarkAirHighToGroundDuration => Mathf.Clamp(embarkAirHighToGroundDuration, 0.04f, 2f);
    public float EmbarkAirLowToGroundDuration => Mathf.Clamp(embarkAirLowToGroundDuration, 0f, 2f);
    public float EmbarkAfterMoveDelay => Mathf.Clamp(embarkAfterMoveDelay, 0f, 2f);
    public float EmbarkHighToLowNormalizedTime => Mathf.Clamp(embarkHighToLowNormalizedTime, 0.01f, 0.99f);
    public float EmbarkLowToGroundNormalizedTime => Mathf.Clamp(embarkLowToGroundNormalizedTime, 0.01f, 1f);
    public float DisembarkForcedLandingDuration => Mathf.Clamp(disembarkForcedLandingDuration, 0f, 2f);
    public float DisembarkAfterForcedLandingDelay => Mathf.Clamp(disembarkAfterForcedLandingDelay, 0f, 2f);
    public float DisembarkBeforeSpawnDelay => Mathf.Clamp(disembarkBeforeSpawnDelay, 0f, 2f);
    public float DisembarkAfterSpawnDelay => Mathf.Clamp(disembarkAfterSpawnDelay, 0f, 2f);
    public float DisembarkSpawnStepDelay => Mathf.Clamp(disembarkSpawnStepDelay, 0f, 2f);
    public float DisembarkAfterPassengerMoveDelay => Mathf.Clamp(disembarkAfterPassengerMoveDelay, 0f, 2f);
    public float DisembarkAfterPassengerLoadDelay => Mathf.Clamp(disembarkAfterPassengerLoadDelay, 0f, 2f);
    public float DisembarkAfterMoveDelay => Mathf.Clamp(disembarkAfterMoveDelay, 0f, 2f);
    public float DisembarkAfterTransporterDoneDelay => Mathf.Clamp(disembarkAfterTransporterDoneDelay, 0f, 2f);
    public float DisembarkAirHighToGroundDuration => Mathf.Clamp(disembarkAirHighToGroundDuration, 0.04f, 2f);
    public float DisembarkAirLowToGroundDuration => Mathf.Clamp(disembarkAirLowToGroundDuration, 0f, 2f);
    public float DisembarkHighToLowNormalizedTime => Mathf.Clamp(disembarkHighToLowNormalizedTime, 0.01f, 0.99f);
    public float DisembarkLowToGroundNormalizedTime => Mathf.Clamp(disembarkLowToGroundNormalizedTime, 0.01f, 1f);
    public float CapturePreSfxDelay => Mathf.Clamp(capturePreSfxDelay, 0f, 2f);
    public float CapturePostCapturingSfxDelay => Mathf.Clamp(capturePostCapturingSfxDelay, 0f, 2f);
    public float CapturePostDoneSfxDelay => Mathf.Clamp(capturePostDoneSfxDelay, 0f, 2f);
    public float CapturePostCapturedSfxDelay => Mathf.Clamp(capturePostCapturedSfxDelay, 0f, 2f);

    private void Awake()
    {
        Instance = this;
        EnsureDefaults();
    }

    private void OnEnable()
    {
        Instance = this;
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureDefaults();
    }
#endif

    private void Update()
    {
        if (selectedBlinkUnit == null)
            return;

        selectedBlinkUnit.SetSelectionBlinkDurations(selectionBlinkActiveDuration, selectionBlinkInactiveDuration);
    }

    public void ApplySelectionVisual(UnitManager unit)
    {
        if (unit == null)
            return;

        selectedBlinkUnit = unit;
        unit.SetSelectionBlinkDurations(selectionBlinkActiveDuration, selectionBlinkInactiveDuration);
        unit.SetSelected(true);
    }

    public void ClearSelectionVisual(UnitManager unit)
    {
        if (unit == null)
            return;

        if (selectedBlinkUnit == unit)
            selectedBlinkUnit = null;
        unit.SetSelected(false);
    }

    public void StopCurrentMovement()
    {
        if (movementRoutine == null)
            return;

        StopCoroutine(movementRoutine);
        movementRoutine = null;
    }

    public void PlayMovement(
        UnitManager unit,
        Tilemap movementTilemap,
        List<Vector3Int> path,
        bool playStartSfx,
        Action onAnimationStart,
        Action onAnimationFinished,
        Action<Vector3Int> onCellReached,
        float stepDurationOverride = -1f)
    {
        if (unit == null || path == null || path.Count < 2)
            return;

        StopCurrentMovement();
        movementRoutine = StartCoroutine(AnimateMovementRoutine(
            unit,
            movementTilemap,
            path,
            playStartSfx,
            onAnimationStart,
            onAnimationFinished,
            onCellReached,
            stepDurationOverride));
    }

    private IEnumerator AnimateMovementRoutine(
        UnitManager unit,
        Tilemap movementTilemap,
        List<Vector3Int> path,
        bool playStartSfx,
        Action onAnimationStart,
        Action onAnimationFinished,
        Action<Vector3Int> onCellReached,
        float stepDurationOverride)
    {
        if (unit != null)
            unit.SetSelected(false);

        if (playStartSfx)
            onAnimationStart?.Invoke();

        Tilemap effectiveTilemap = movementTilemap != null ? movementTilemap : unit.BoardTilemap;
        float manualSpeed = ResolveUnitMoveSpeed(unit);
        float baseStepDuration = stepDurationOverride > 0f ? stepDurationOverride : moveStepDuration;
        float duration = Mathf.Max(0.04f, baseStepDuration / Mathf.Max(0.1f, manualSpeed));
        float arc = Mathf.Max(0f, moveArcHeight);
        float preservedZ = unit.transform.position.z;

        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3Int fromCell = path[i];
            Vector3Int toCell = path[i + 1];
            Vector3 from = effectiveTilemap != null ? effectiveTilemap.GetCellCenterWorld(fromCell) : unit.transform.position;
            Vector3 to = effectiveTilemap != null ? effectiveTilemap.GetCellCenterWorld(toCell) : unit.transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = EvaluateMoveCurve(t);
                Vector3 p = Vector3.LerpUnclamped(from, to, eased);
                p.y += Mathf.Sin(eased * Mathf.PI) * arc;
                p.z = preservedZ;
                unit.transform.position = p;
                yield return null;
            }

            unit.SetCurrentCellPosition(toCell, enforceFinalOccupancyRule: false);
            onCellReached?.Invoke(toCell);
        }

        movementRoutine = null;
        onAnimationFinished?.Invoke();
    }

    private float EvaluateMoveCurve(float t)
    {
        if (moveStepCurve == null || moveStepCurve.length == 0)
            return t;

        return moveStepCurve.Evaluate(t);
    }

    private void EnsureDefaults()
    {
        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();

        if (!mirandoPreviewSortingLayerInitialized)
        {
            mirandoPreviewSortingLayer = SortingLayerReference.FromName("SFX");
            mirandoPreviewSortingLayerInitialized = true;
        }

        if (!vtolLandingSortingLayerInitialized)
        {
            vtolLandingSortingLayer = SortingLayerReference.FromName("SFX");
            vtolLandingSortingLayerInitialized = true;
        }

        if (!takingHitSortingLayerInitialized)
        {
            takingHitSortingLayer = SortingLayerReference.FromName("SFX");
            takingHitSortingLayerInitialized = true;
        }

        if (!explosionSortingLayerInitialized)
        {
            explosionSortingLayer = SortingLayerReference.FromName("SFX");
            explosionSortingLayerInitialized = true;
        }

        if (!combatProjectileSortingLayerInitialized)
        {
            combatProjectileSortingLayer = SortingLayerReference.FromName("SFX");
            combatProjectileSortingLayerInitialized = true;
        }

        if (!rangedAttackSquareSortingLayerInitialized)
        {
            rangedAttackSquareSortingLayer = SortingLayerReference.FromName("SFX");
            rangedAttackSquareSortingLayerInitialized = true;
        }

#if UNITY_EDITOR
        TryAutoAssignVtolLandingFramesInEditor();
        TryAutoAssignTakingHitFramesInEditor();
        TryAutoAssignExplosionFramesInEditor();
#endif
    }

    private float ResolveUnitMoveSpeed(UnitManager unit)
    {
        if (unit == null)
            return 1f;

        UnitData unitData = null;
        unit.TryGetUnitData(out unitData);
        if (unitData == null || unitMoveSpeedOverrides == null || unitMoveSpeedOverrides.Count == 0)
            return 1f;

        for (int i = 0; i < unitMoveSpeedOverrides.Count; i++)
        {
            UnitMoveAnimationSpeedOverride entry = unitMoveSpeedOverrides[i];
            if (entry == null || entry.unitData == null)
                continue;

            if (entry.unitData == unitData)
                return Mathf.Clamp(entry.speed, 0.1f, 4f);
        }

        return 1f;
    }

    public float GetEffectiveMoveStepDuration(UnitManager unit, float stepDurationOverride = -1f)
    {
        float baseStepDuration = stepDurationOverride > 0f ? stepDurationOverride : moveStepDuration;
        float manualSpeed = ResolveUnitMoveSpeed(unit);
        return Mathf.Max(0.04f, baseStepDuration / Mathf.Max(0.1f, manualSpeed));
    }

    public float PlayVtolLandingEffect(UnitManager unit)
    {
        if (unit == null || vtolLandingFrames == null || vtolLandingFrames.Length == 0)
            return 0f;
        if (unit.IsEmbarked)
            return 0f;
        if (!UnitMatchesVtolLandingFxSkillList(unit))
            return 0f;

        GameObject fx = new GameObject("VTOL Landing FX");
        fx.transform.position = unit.transform.position;
        fx.transform.localScale = Vector3.one * Mathf.Max(0.2f, vtolLandingScale);

        SpriteRenderer sr = fx.AddComponent<SpriteRenderer>();
        sr.sortingLayerID = vtolLandingSortingLayer.Id;
        sr.sortingOrder = vtolLandingSortingOrder;

        SpriteSequenceFx sequence = fx.AddComponent<SpriteSequenceFx>();
        sequence.Configure(vtolLandingFrames, vtolLandingFrameDuration, autoDestroy: true);
        return sequence.TotalDuration;
    }

    public float PlayTakingHitEffect(UnitManager unit)
    {
        if (unit == null)
            return 0f;
        if (unit.IsEmbarked)
            return 0f;

        float sequenceDuration = 0f;
        if (takingHitFrames != null && takingHitFrames.Length > 0)
        {
            GameObject fx = new GameObject("Taking Hit FX");
            fx.transform.position = unit.transform.position;
            fx.transform.localScale = Vector3.one * Mathf.Max(0.2f, takingHitScale);

            SpriteRenderer sr = fx.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = takingHitSortingLayer.Id;
            sr.sortingOrder = takingHitSortingOrder;

            SpriteSequenceFx sequence = fx.AddComponent<SpriteSequenceFx>();
            sequence.Configure(takingHitFrames, takingHitFrameDuration, autoDestroy: true);
            sequenceDuration = sequence.TotalDuration;
        }

        SpriteRenderer[] renderers = unit.GetComponentsInChildren<SpriteRenderer>(true);
        float flashDuration = Mathf.Clamp(takingHitFlashDuration, 0.02f, 1f);
        float flashInterval = Mathf.Clamp(takingHitFlashInterval, 0.01f, 0.2f);
        if (renderers != null && renderers.Length > 0 && flashDuration > 0f)
            StartCoroutine(PlayTakingHitFlashRoutine(renderers, flashDuration, flashInterval));

        float shakeDuration = Mathf.Clamp(takingHitShakeDuration, 0.02f, 1f);
        float shakeMagnitude = Mathf.Clamp(takingHitShakeMagnitude, 0.001f, 0.2f);
        if (shakeDuration > 0f && shakeMagnitude > 0f)
            StartCoroutine(PlayTakingHitShakeRoutine(unit.transform, shakeDuration, shakeMagnitude));

        return Mathf.Max(sequenceDuration, Mathf.Max(flashDuration, shakeDuration));
    }

    public float PlayExplosionEffectAt(Vector3 worldPosition)
    {
        float sfxDuration = cursorController != null ? cursorController.PlayExplosionSfx(1f) : 0f;

        if (explosionFrames == null || explosionFrames.Length == 0)
            return sfxDuration;

        GameObject fx = new GameObject("Explosion FX");
        fx.transform.position = worldPosition;
        fx.transform.localScale = Vector3.one * Mathf.Max(0.2f, explosionScale);

        SpriteRenderer sr = fx.AddComponent<SpriteRenderer>();
        sr.sortingLayerID = explosionSortingLayer.Id;
        sr.sortingOrder = explosionSortingOrder;

        SpriteSequenceFx sequence = fx.AddComponent<SpriteSequenceFx>();
        sequence.Configure(explosionFrames, explosionFrameDuration, autoDestroy: true);
        return Mathf.Max(sequence.TotalDuration, sfxDuration);
    }

    private IEnumerator PlayTakingHitFlashRoutine(SpriteRenderer[] renderers, float duration, float interval)
    {
        if (renderers == null || renderers.Length == 0)
            yield break;

        Color[] originals = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                originals[i] = renderers[i].color;
        }

        float elapsed = 0f;
        bool on = false;
        while (elapsed < duration)
        {
            on = !on;
            Color target = on ? Color.white : takingHitFlashDamageColor;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;
                renderers[i].color = target;
            }

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            renderers[i].color = originals[i];
        }
    }

    private static IEnumerator PlayTakingHitShakeRoutine(Transform target, float duration, float magnitude)
    {
        if (target == null)
            yield break;

        Vector3 start = target.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float x = (UnityEngine.Random.value * 2f - 1f) * magnitude;
            float y = (UnityEngine.Random.value * 2f - 1f) * magnitude;
            target.position = start + new Vector3(x, y, 0f);
            yield return null;
        }

        if (target != null)
            target.position = start;
    }

    private bool UnitMatchesVtolLandingFxSkillList(UnitManager unit)
    {
        if (unit == null)
            return false;
        if (vtolLandingFxAllowedSkills == null || vtolLandingFxAllowedSkills.Count == 0)
            return false;

        for (int i = 0; i < vtolLandingFxAllowedSkills.Count; i++)
        {
            SkillData skill = vtolLandingFxAllowedSkills[i];
            if (skill == null)
                continue;
            if (unit.HasSkill(skill))
                return true;
        }

        return false;
    }

    public float PlayWeaponProjectile(UnitManager shooter, UnitManager target, WeaponData weapon, WeaponTrajectoryType trajectory)
    {
        if (shooter == null || target == null)
            return 0f;

        Vector3 from = shooter.transform.position;
        Vector3 to = target.transform.position;
        from.z = to.z;

        float distance = Vector2.Distance(new Vector2(from.x, from.y), new Vector2(to.x, to.y));
        float speed = Mathf.Max(0.5f, combatProjectileSpeed);
        float duration = Mathf.Max(Mathf.Max(0.03f, combatProjectileMinDuration), distance / speed);

        Sprite sprite = weapon != null && weapon.ammunitionSprite != null
            ? weapon.ammunitionSprite
            : (weapon != null ? weapon.sprite : null);
        if (sprite == null)
            sprite = GetWhiteSprite();

        float projectileScale = Mathf.Max(0.05f, combatProjectileScale);
        if (weapon != null && weapon.useExplicitProjectileScale)
            projectileScale = Mathf.Clamp(weapon.projectileScale, 0.05f, 3f);

        StartCoroutine(AnimateWeaponProjectileRoutine(from, to, sprite, trajectory, duration, projectileScale));
        return duration;
    }

    public float PlayServiceProjectileStraight(Vector3 from, Vector3 to, Sprite sprite)
    {
        float distance = Vector2.Distance(new Vector2(from.x, from.y), new Vector2(to.x, to.y));
        float speed = SupplyProjectileSpeed;
        float duration = Mathf.Max(SupplyProjectileMinDuration, distance / speed);
        Sprite resolved = sprite != null ? sprite : GetWhiteSprite();
        float projectileScale = SupplyProjectileScale;
        StartCoroutine(AnimateWeaponProjectileRoutine(from, to, resolved, WeaponTrajectoryType.Straight, duration, projectileScale));
        return duration;
    }

    public float PlayCombatBumpTogether(UnitManager a, UnitManager b)
    {
        if (a == null || b == null)
            return 0f;

        float distance = Mathf.Clamp(combatBumpDistance, 0.02f, 0.5f);
        float duration = Mathf.Clamp(combatBumpDuration, 0.03f, 0.4f);
        StartCoroutine(PlayCombatBumpTogetherRoutine(a.transform, b.transform, distance, duration));
        return duration * 2f;
    }

    public float PlayCombatBumpTowards(UnitManager mover, UnitManager target)
    {
        if (mover == null || target == null)
            return 0f;

        float distance = Mathf.Clamp(combatBumpDistance, 0.02f, 0.5f);
        float duration = Mathf.Clamp(combatBumpDuration, 0.03f, 0.4f);
        StartCoroutine(PlayCombatBumpTowardsRoutine(mover.transform, target.transform.position, distance, duration));
        return duration * 2f;
    }

    private static IEnumerator PlayCombatBumpTogetherRoutine(Transform a, Transform b, float distance, float duration)
    {
        if (a == null || b == null)
            yield break;

        Vector3 a0 = a.position;
        Vector3 b0 = b.position;
        Vector3 dirAB = (b0 - a0).normalized;
        if (dirAB.sqrMagnitude < 0.0001f)
            dirAB = Vector3.right;

        Vector3 a1 = a0 + dirAB * distance;
        Vector3 b1 = b0 - dirAB * distance;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            if (a != null) a.position = Vector3.Lerp(a0, a1, p);
            if (b != null) b.position = Vector3.Lerp(b0, b1, p);
            yield return null;
        }

        t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            if (a != null) a.position = Vector3.Lerp(a1, a0, p);
            if (b != null) b.position = Vector3.Lerp(b1, b0, p);
            yield return null;
        }

        if (a != null) a.position = a0;
        if (b != null) b.position = b0;
    }

    private static IEnumerator PlayCombatBumpTowardsRoutine(Transform mover, Vector3 targetWorldPos, float distance, float duration)
    {
        if (mover == null)
            yield break;

        Vector3 start = mover.position;
        Vector3 dir = (targetWorldPos - start).normalized;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.right;

        Vector3 bump = start + dir * distance;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            if (mover != null) mover.position = Vector3.Lerp(start, bump, p);
            yield return null;
        }

        t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            if (mover != null) mover.position = Vector3.Lerp(bump, start, p);
            yield return null;
        }

        if (mover != null) mover.position = start;
    }

    private IEnumerator AnimateWeaponProjectileRoutine(
        Vector3 from,
        Vector3 to,
        Sprite sprite,
        WeaponTrajectoryType trajectory,
        float duration,
        float projectileScale)
    {
        GameObject go = new GameObject("Combat Projectile FX");
        Transform t = go.transform;
        t.position = from;
        t.localScale = Vector3.one * Mathf.Max(0.05f, projectileScale);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerID = combatProjectileSortingLayer.Id;
        sr.sortingOrder = combatProjectileSortingOrder;

        Vector2 flat = new Vector2(to.x - from.x, to.y - from.y);
        Vector2 dir = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector2.right;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        t.rotation = Quaternion.Euler(0f, 0f, angle);

        Vector3 control = Vector3.zero;
        if (trajectory == WeaponTrajectoryType.Parabolic)
            control = BuildParabolicControlPoint(from, to);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(duration <= 0f ? 1f : elapsed / duration);
            Vector3 pos = trajectory == WeaponTrajectoryType.Parabolic
                ? QuadraticBezier(from, control, to, p)
                : Vector3.Lerp(from, to, p);
            t.position = pos;

            float nextP = Mathf.Clamp01(p + 0.02f);
            Vector3 nextPos = trajectory == WeaponTrajectoryType.Parabolic
                ? QuadraticBezier(from, control, to, nextP)
                : Vector3.Lerp(from, to, nextP);
            Vector2 look = new Vector2(nextPos.x - pos.x, nextPos.y - pos.y);
            if (look.sqrMagnitude > 0.0001f)
            {
                float lookAngle = Mathf.Atan2(look.y, look.x) * Mathf.Rad2Deg;
                t.rotation = Quaternion.Euler(0f, 0f, lookAngle);
            }

            yield return null;
        }

        if (go != null)
            Destroy(go);
    }

    private Vector3 BuildParabolicControlPoint(Vector3 from, Vector3 to)
    {
        Vector2 flat = new Vector2(to.x - from.x, to.y - from.y);
        if (flat.sqrMagnitude <= 0.0001f)
            return (from + to) * 0.5f;

        Vector2 dir = flat.normalized;
        Vector2 clockwiseNormal = new Vector2(dir.y, -dir.x);
        Vector2 antiClockwiseNormal = new Vector2(-dir.y, dir.x);
        // Desempate estavel para alvo quase vertical: sempre anti-horario.
        const float verticalTieEpsilon = 0.01f;
        float dx = to.x - from.x;
        bool isVerticalTie = Mathf.Abs(dx) <= verticalTieEpsilon;
        Vector2 normal = isVerticalTie
            ? antiClockwiseNormal
            : (dx > 0f ? antiClockwiseNormal : clockwiseNormal);
        float distance = flat.magnitude;
        float maxBend = Mathf.Clamp(combatProjectileParabolaBend, 0.2f, Mathf.Max(0.2f, distance));
        float horizontalFactor = Mathf.Clamp01(Mathf.Abs(dir.x)); // 1=horizontal, 0=vertical
        float horizontalWeight = Mathf.Pow(horizontalFactor, MirandoParabolaHorizontalBendWeight);
        float minBend = Mathf.Clamp(MirandoParabolaMinVerticalBend, 0.01f, 0.3f);
        float bend = Mathf.Lerp(minBend, maxBend, horizontalWeight);
        return (from + to) * 0.5f + new Vector3(normal.x, normal.y, 0f) * bend;
    }

    private static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return (u * u * a) + (2f * u * t * b) + (t * t * c);
    }

    public float PlayRangedAttackDefenderEffect(UnitManager defender, float syncDuration = -1f)
    {
        if (defender == null)
            return 0f;

        float duration = syncDuration > 0f
            ? syncDuration
            : Mathf.Clamp(rangedAttackDefenderAnimDuration, 0.05f, 2f);
        StartCoroutine(RangedAttackDefenderEffectRoutine(defender, duration));
        return duration;
    }

    private IEnumerator RangedAttackDefenderEffectRoutine(UnitManager defender, float duration)
    {
        if (defender == null)
            yield break;

        GameObject root = new GameObject("Ranged Attack Hit FX");
        Transform rootTransform = root.transform;
        rootTransform.position = defender.transform.position;

        float extent = Mathf.Max(0.2f, rangedAttackSquareExtent);
        float thickness = Mathf.Clamp(rangedAttackSquareBarThickness, 0.05f, 1.2f);
        Color color = rangedAttackSquareColor;
        int sortingLayerId = rangedAttackSquareSortingLayer.Id;
        int sortingOrder = rangedAttackSquareSortingOrder;

        Transform top = CreateRangedAttackFxBar(rootTransform, "Top", color, sortingLayerId, sortingOrder, new Vector2((extent * 2f) + thickness, thickness));
        Transform bottom = CreateRangedAttackFxBar(rootTransform, "Bottom", color, sortingLayerId, sortingOrder, new Vector2((extent * 2f) + thickness, thickness));
        Transform left = CreateRangedAttackFxBar(rootTransform, "Left", color, sortingLayerId, sortingOrder, new Vector2(thickness, (extent * 2f) + thickness));
        Transform right = CreateRangedAttackFxBar(rootTransform, "Right", color, sortingLayerId, sortingOrder, new Vector2(thickness, (extent * 2f) + thickness));

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(duration <= 0f ? 1f : elapsed / duration);
            float close = EvaluateRangedAttackCloseCurve(t);
            float gap = Mathf.Lerp(extent, 0f, close);

            if (defender != null)
                rootTransform.position = defender.transform.position;

            top.localPosition = new Vector3(0f, gap + (thickness * 0.5f), 0f);
            bottom.localPosition = new Vector3(0f, -gap - (thickness * 0.5f), 0f);
            left.localPosition = new Vector3(-gap - (thickness * 0.5f), 0f, 0f);
            right.localPosition = new Vector3(gap + (thickness * 0.5f), 0f, 0f);
            yield return null;
        }

        if (root != null)
            Destroy(root);
    }

    private float EvaluateRangedAttackCloseCurve(float t)
    {
        if (rangedAttackSquareCloseCurve == null || rangedAttackSquareCloseCurve.length == 0)
            return t;
        return rangedAttackSquareCloseCurve.Evaluate(t);
    }

    private Transform CreateRangedAttackFxBar(
        Transform parent,
        string name,
        Color color,
        int sortingLayerId,
        int sortingOrder,
        Vector2 scale)
    {
        GameObject go = new GameObject(name);
        Transform t = go.transform;
        t.SetParent(parent, false);
        t.localScale = new Vector3(scale.x, scale.y, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite();
        sr.color = color;
        sr.sortingLayerID = sortingLayerId;
        sr.sortingOrder = sortingOrder;
        return t;
    }

    private static Sprite GetWhiteSprite()
    {
        if (cachedWhiteSprite != null)
            return cachedWhiteSprite;

        Texture2D texture = Texture2D.whiteTexture;
        cachedWhiteSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        return cachedWhiteSprite;
    }

#if UNITY_EDITOR
    private void TryAutoAssignVtolLandingFramesInEditor()
    {
        if (vtolLandingFrames != null && vtolLandingFrames.Length > 0)
            return;

        string[] paths = new[]
        {
            "Assets/img/animations/VTOL Landing/vtol_01.png",
            "Assets/img/animations/VTOL Landing/vtol_02.png",
            "Assets/img/animations/VTOL Landing/vtol_03.png",
            "Assets/img/animations/VTOL Landing/vtol_04.png",
            "Assets/img/animations/VTOL Landing/vtol_05.png",
            "Assets/img/animations/VTOL Landing/vtol_06.png"
        };

        List<Sprite> loaded = new List<Sprite>(paths.Length);
        for (int i = 0; i < paths.Length; i++)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
            if (sprite != null)
                loaded.Add(sprite);
        }

        if (loaded.Count > 0)
            vtolLandingFrames = loaded.ToArray();
    }

    private void TryAutoAssignTakingHitFramesInEditor()
    {
        if (takingHitFrames != null && takingHitFrames.Length > 0)
            return;

        string[] paths = new[]
        {
            "Assets/img/animations/hit/hit1.png",
            "Assets/img/animations/hit/hit2.png",
            "Assets/img/animations/hit/hit3.png",
            "Assets/img/animations/hit/hit4.png",
            "Assets/img/animations/hit/hit5.png"
        };

        List<Sprite> loaded = new List<Sprite>(paths.Length);
        for (int i = 0; i < paths.Length; i++)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
            if (sprite != null)
                loaded.Add(sprite);
        }

        if (loaded.Count > 0)
            takingHitFrames = loaded.ToArray();
    }

    private void TryAutoAssignExplosionFramesInEditor()
    {
        if (explosionFrames != null && explosionFrames.Length > 0)
            return;

        string[] paths = new[]
        {
            "Assets/img/animations/explosion/explosion1.png",
            "Assets/img/animations/explosion/explosion2.png",
            "Assets/img/animations/explosion/explosion3.png",
            "Assets/img/animations/explosion/explosion4.png",
            "Assets/img/animations/explosion/explosion5.png",
            "Assets/img/animations/explosion/explosion6.png",
            "Assets/img/animations/explosions/explosion1.png",
            "Assets/img/animations/explosions/explosion2.png",
            "Assets/img/animations/explosions/explosion3.png",
            "Assets/img/animations/explosions/explosion4.png",
            "Assets/img/animations/explosions/explosion5.png",
            "Assets/img/animations/explosions/explosion6.png"
        };

        List<Sprite> loaded = new List<Sprite>(paths.Length);
        for (int i = 0; i < paths.Length; i++)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
            if (sprite != null)
                loaded.Add(sprite);
        }

        if (loaded.Count > 0)
            explosionFrames = loaded.ToArray();

        if ((explosionFrames == null || explosionFrames.Length == 0))
        {
            string[] spriteGuids = AssetDatabase.FindAssets("explosion t:Sprite", new[] { "Assets/img/animations" });
            List<Sprite> discovered = new List<Sprite>();
            for (int i = 0; i < spriteGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                    continue;
                discovered.Add(sprite);
            }

            discovered.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            if (discovered.Count > 0)
                explosionFrames = discovered.ToArray();
        }
    }
#endif
}
