using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Serialization;

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
    [Tooltip("Intensidade da curvatura da parabola para armas parabolic.")]
    [SerializeField] [Range(0.2f, 4f)] private float mirandoParabolaBend = 1.2f;
    [Tooltip("Quantidade de pontos amostrados na curva parabolica.")]
    [SerializeField] [Range(8, 64)] private int mirandoParabolaSamples = 24;
    [Tooltip("Sorting layer da linha de preview de tiro.")]
    [SerializeField] private SortingLayerReference mirandoPreviewSortingLayer;
    [SerializeField, HideInInspector] private bool mirandoPreviewSortingLayerInitialized;
    [Tooltip("Sorting order da linha de preview de tiro.")]
    [SerializeField] private int mirandoPreviewSortingOrder = 120;

    private Coroutine movementRoutine;
    private UnitManager selectedBlinkUnit;

    public bool IsAnimatingMovement => movementRoutine != null;
    public Material MirandoPreviewMaterial => mirandoPreviewMaterial;
    public Color MirandoPreviewColor => mirandoPreviewColor;
    public float MirandoPreviewWidth => Mathf.Clamp(mirandoPreviewWidth, 0.03f, 0.4f);
    public float MirandoPreviewSpeed => Mathf.Clamp(mirandoPreviewSpeed, 0.2f, 8f);
    public float MirandoPreviewSegmentLength => Mathf.Clamp(mirandoPreviewSegmentLength, 0.2f, 5f);
    public float MirandoParabolaBend => Mathf.Clamp(mirandoParabolaBend, 0.2f, 4f);
    public int MirandoParabolaSamples => Mathf.Clamp(mirandoParabolaSamples, 8, 64);
    public int MirandoPreviewSortingLayerId => mirandoPreviewSortingLayer.Id;
    public int MirandoPreviewSortingOrder => mirandoPreviewSortingOrder;
    public float EmbarkForcedLandingDuration => Mathf.Clamp(embarkForcedLandingDuration, 0f, 2f);
    public float EmbarkAfterForcedLandingDelay => Mathf.Clamp(embarkAfterForcedLandingDelay, 0f, 2f);
    public float EmbarkDefaultMoveStepDuration => Mathf.Clamp(embarkDefaultMoveStepDuration, 0.04f, 0.8f);
    public float EmbarkAirHighToGroundDuration => Mathf.Clamp(embarkAirHighToGroundDuration, 0.04f, 2f);
    public float EmbarkAirLowToGroundDuration => Mathf.Clamp(embarkAirLowToGroundDuration, 0f, 2f);
    public float EmbarkAfterMoveDelay => Mathf.Clamp(embarkAfterMoveDelay, 0f, 2f);
    public float EmbarkHighToLowNormalizedTime => Mathf.Clamp(embarkHighToLowNormalizedTime, 0.01f, 0.99f);
    public float EmbarkLowToGroundNormalizedTime => Mathf.Clamp(embarkLowToGroundNormalizedTime, 0.01f, 1f);

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
        if (!mirandoPreviewSortingLayerInitialized)
        {
            mirandoPreviewSortingLayer = SortingLayerReference.FromName("SFX");
            mirandoPreviewSortingLayerInitialized = true;
        }
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
}
