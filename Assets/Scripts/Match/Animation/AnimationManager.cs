using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class AnimationManager : MonoBehaviour
{
    [Header("Selection Visual")]
    [SerializeField] [Range(0.05f, 1f)] private float selectionBlinkActiveDuration = 0.16f;
    [SerializeField] [Range(0.05f, 1f)] private float selectionBlinkInactiveDuration = 0.16f;
    [Header("Movement Animation")]
    [SerializeField] [Range(0.04f, 0.4f)] private float moveStepDuration = 0.12f;
    [SerializeField] [Range(0f, 0.35f)] private float moveArcHeight = 0.05f;
    [SerializeField] private AnimationCurve moveStepCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Coroutine movementRoutine;
    private UnitManager selectedBlinkUnit;

    public bool IsAnimatingMovement => movementRoutine != null;

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
        Action<Vector3Int> onCellReached)
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
            onCellReached));
    }

    private IEnumerator AnimateMovementRoutine(
        UnitManager unit,
        Tilemap movementTilemap,
        List<Vector3Int> path,
        bool playStartSfx,
        Action onAnimationStart,
        Action onAnimationFinished,
        Action<Vector3Int> onCellReached)
    {
        if (unit != null)
            unit.SetSelected(false);

        if (playStartSfx)
            onAnimationStart?.Invoke();

        Tilemap effectiveTilemap = movementTilemap != null ? movementTilemap : unit.BoardTilemap;
        float manualSpeed = unit != null ? unit.GetManualMoveAnimationSpeed() : 1f;
        float duration = Mathf.Max(0.04f, moveStepDuration / Mathf.Max(0.1f, manualSpeed));
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
}
