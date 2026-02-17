using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("References")]
    public Tilemap tilemap;                 // arraste aqui o seu Tilemap_Terrain
    public bool recalcBoundsOnStart = true; // se true, tenta calcular os bounds baseado nos tiles pintados no Start (pode ser custoso em mapas grandes)
    public Collider2D clampCollider;        // se setado, pan/zoom respeitam este collider

    [Header("Zoom")]
    public float zoomSpeed = 8f;            // sensibilidade do scroll
    public float minOrthoSize = 3f; // menor valor de orthographicSize (mais zoom in)
    public float maxOrthoSize = 30f; // maior valor de orthographicSize (mais zoom out)
    public bool limitZoomToBounds = true; // limita o zoom maximo para que a camera nunca mostre area fora dos bounds (definidos por clampCollider ou pelos tiles pintados)

    [Header("Pan (RMB/MMB drag)")]
    public float panSpeed = 1f;             // 1 = "na mao", >1 mais rapido
    public KeyCode panMouseButton = KeyCode.Mouse1; // fallback legado (quando RMB/MMB estao desativados)
    public bool panWithRightMouse = true;
    public bool panWithMiddleMouse = true;

    [Header("Clamp Padding (world units)")]
    public float padding = 0.5f;

    [Header("Focus")]
    public float focusSpeed = 5f;
    [Header("Cursor Follow")]
    [Tooltip("Quantas celulas de margem manter entre cursor e a borda visivel antes de mover camera.")]
    public float cursorEdgeMarginCellsX = 2f;
    public float cursorEdgeMarginCellsY = 2f;

    private Camera _cam;
    private Bounds _paintedWorldBounds;
    private bool _hasBounds;
    private Coroutine _focusRoutine;

    private Vector3 _dragStartWorld;
    private bool _dragging;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        if (!_cam.orthographic)
            Debug.LogWarning("[CameraController] Sua camera nao esta Orthographic.");
    }

    void Start()
    {
        if (recalcBoundsOnStart)
            RecalculatePaintedBounds();
        ClampCamera();
    }

    void Update()
    {
        HandleZoom();
        HandlePan();
        ClampCamera();
    }

    void HandleZoom()
    {
        float scroll = GetMouseScrollY();
        if (Mathf.Abs(scroll) < 0.01f) return;

#if ENABLE_INPUT_SYSTEM
        // Input System retorna valores maiores (linha do wheel), normaliza para sensacao similar ao Input legacy
        scroll /= 120f;
#endif

        Vector3 mouseScreen = GetMousePosition();
        mouseScreen.z = -transform.position.z;
        Vector3 worldBefore = _cam.ScreenToWorldPoint(mouseScreen);

        float target = _cam.orthographicSize - scroll * zoomSpeed * Time.unscaledDeltaTime * 10f;
        _cam.orthographicSize = Mathf.Clamp(target, minOrthoSize, GetEffectiveMaxOrthoSize());

        Vector3 worldAfter = _cam.ScreenToWorldPoint(mouseScreen);
        Vector3 delta = worldBefore - worldAfter;
        transform.position += new Vector3(delta.x, delta.y, 0f);
    }

    void HandlePan()
    {
        if (PanStartPressed())
        {
            _dragging = true;
            _dragStartWorld = MouseWorld();
        }
        else if (PanReleased())
        {
            _dragging = false;
        }

        if (!_dragging) return;

        Vector3 now = MouseWorld();
        Vector3 delta = _dragStartWorld - now; // move o mundo na direcao do arrasto
        transform.position += delta * panSpeed;

        // mantem o "gancho" no ponto inicial do mouse pra arrastar continuo
        _dragStartWorld = MouseWorld();
    }

    bool PanStartPressed()
    {
        bool rmb = panWithRightMouse && GetMouseButtonDown(1);
        bool mmb = panWithMiddleMouse && GetMouseButtonDown(2);
        bool legacy = (!panWithRightMouse && !panWithMiddleMouse) && GetKeyDown(panMouseButton);
        return rmb || mmb || legacy;
    }

    bool PanReleased()
    {
        bool rmb = panWithRightMouse && GetMouseButtonUp(1);
        bool mmb = panWithMiddleMouse && GetMouseButtonUp(2);
        bool legacy = (!panWithRightMouse && !panWithMiddleMouse) && GetKeyUp(panMouseButton);
        return rmb || mmb || legacy;
    }

    Vector3 MouseWorld()
    {
        Vector3 m = GetMousePosition();
        m.z = -transform.position.z; // funciona bem em camera 2D
        return _cam.ScreenToWorldPoint(m);
    }

    float GetMouseScrollY()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#else
        return Input.mouseScrollDelta.y;
#endif
    }

    bool GetMouseButtonDown(int button)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return false;
        if (button == 1) return Mouse.current.rightButton.wasPressedThisFrame;
        if (button == 2) return Mouse.current.middleButton.wasPressedThisFrame;
        if (button == 0) return Mouse.current.leftButton.wasPressedThisFrame;
        return false;
#else
        return Input.GetMouseButtonDown(button);
#endif
    }

    bool GetMouseButtonUp(int button)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return false;
        if (button == 1) return Mouse.current.rightButton.wasReleasedThisFrame;
        if (button == 2) return Mouse.current.middleButton.wasReleasedThisFrame;
        if (button == 0) return Mouse.current.leftButton.wasReleasedThisFrame;
        return false;
#else
        return Input.GetMouseButtonUp(button);
#endif
    }

    bool GetKeyDown(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        return false;
#else
        return Input.GetKeyDown(key);
#endif
    }

    bool GetKeyUp(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        return false;
#else
        return Input.GetKeyUp(key);
#endif
    }

    Vector3 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    [ContextMenu("Recalculate Painted Bounds")]
    public void RecalculatePaintedBounds()
    {
        if (clampCollider == null && tilemap != null)
            clampCollider = tilemap.GetComponent<Collider2D>();

        // Prioridade: collider (ex.: BoxCollider2D no Tilemap)
        if (clampCollider != null)
        {
            _paintedWorldBounds = clampCollider.bounds;
            _hasBounds = _paintedWorldBounds.size.x > 0f && _paintedWorldBounds.size.y > 0f;
            if (!_hasBounds)
            {
                Debug.LogWarning("[CameraController] Collider de clamp sem tamanho valido.");
                return;
            }

            _paintedWorldBounds.Expand(new Vector3(padding * 2f, padding * 2f, 0f));
            return;
        }

        if (tilemap == null)
        {
            Debug.LogError("[CameraController] Tilemap nao setado.");
            _hasBounds = false;
            return;
        }

        // Varre o cellBounds, mas so conta celulas que realmente tem tile pintado.
        var cb = tilemap.cellBounds;

        bool found = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        foreach (var cell in cb.allPositionsWithin)
        {
            if (!tilemap.HasTile(cell)) continue;

            // Pega os cantos do "quadrado" da celula em mundo e expande.
            // (Funciona bem como bounds de clamp, mesmo em hex)
            Vector3 world = tilemap.CellToWorld(cell);
            Vector3 cellSize = tilemap.layoutGrid.cellSize;

            Vector3 cMin = world;
            Vector3 cMax = world + cellSize;

            if (!found)
            {
                found = true;
                min = Vector3.Min(cMin, cMax);
                max = Vector3.Max(cMin, cMax);
            }
            else
            {
                min = Vector3.Min(min, Vector3.Min(cMin, cMax));
                max = Vector3.Max(max, Vector3.Max(cMin, cMax));
            }
        }

        if (!found)
        {
            Debug.LogWarning("[CameraController] Nao achei nenhum tile pintado no Tilemap.");
            _hasBounds = false;
            return;
        }

        _paintedWorldBounds = new Bounds();
        _paintedWorldBounds.SetMinMax(min, max);
        _hasBounds = true;

        // Padding
        _paintedWorldBounds.Expand(new Vector3(padding * 2f, padding * 2f, 0f));
    }

    void ClampCamera()
    {
        if (clampCollider != null)
        {
            Bounds liveBounds = clampCollider.bounds;
            if (liveBounds.size.x > 0f && liveBounds.size.y > 0f)
            {
                _paintedWorldBounds = liveBounds;
                _paintedWorldBounds.Expand(new Vector3(padding * 2f, padding * 2f, 0f));
                _hasBounds = true;
            }
        }

        if (!_hasBounds) return;

        float halfH = _cam.orthographicSize;
        float halfW = halfH * _cam.aspect;

        Vector3 p = transform.position;

        float minX = _paintedWorldBounds.min.x + halfW;
        float maxX = _paintedWorldBounds.max.x - halfW;
        float minY = _paintedWorldBounds.min.y + halfH;
        float maxY = _paintedWorldBounds.max.y - halfH;

        // Se o mapa for menor que a visao, centraliza ao inves de clamping estranho
        if (minX > maxX) p.x = (_paintedWorldBounds.min.x + _paintedWorldBounds.max.x) * 0.5f;
        else p.x = Mathf.Clamp(p.x, minX, maxX);

        if (minY > maxY) p.y = (_paintedWorldBounds.min.y + _paintedWorldBounds.max.y) * 0.5f;
        else p.y = Mathf.Clamp(p.y, minY, maxY);

        transform.position = new Vector3(p.x, p.y, transform.position.z);
    }

    float GetEffectiveMaxOrthoSize()
    {
        float effectiveMax = maxOrthoSize;
        if (!limitZoomToBounds || !_hasBounds) return effectiveMax;

        // Mantem viewport inteiro dentro do bounds: halfH <= H/2 e halfW <= W/2
        float byHeight = _paintedWorldBounds.extents.y;
        float byWidth = _paintedWorldBounds.extents.x / Mathf.Max(0.0001f, _cam.aspect);
        float boundMax = Mathf.Min(byHeight, byWidth);

        if (boundMax <= 0f) return minOrthoSize;
        return Mathf.Max(minOrthoSize, Mathf.Min(effectiveMax, boundMax));
    }

    public void FocusOn(Vector3 worldPosition, bool instant = false)
    {
        Vector3 target = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
        target = GetClampedPosition(target);

        if (instant)
        {
            transform.position = target;
            return;
        }

        if (_focusRoutine != null)
            StopCoroutine(_focusRoutine);

        _focusRoutine = StartCoroutine(SmoothFocus(target));
    }

    public void FocusOnWithOffset(Vector3 worldPosition)
    {
        Vector3 screenPoint = _cam.WorldToScreenPoint(worldPosition);
        float targetX = screenPoint.x < Screen.width * 0.5f ? 0.75f : 0.25f;
        float targetY = screenPoint.y < Screen.height * 0.5f ? 0.75f : 0.25f;

        Vector3 desiredScreenPoint = new Vector3(
            Screen.width * targetX,
            Screen.height * targetY,
            screenPoint.z
        );

        Vector3 desiredWorldPoint = _cam.ScreenToWorldPoint(desiredScreenPoint);
        Vector3 delta = worldPosition - desiredWorldPoint;
        Vector3 targetCamPos = transform.position + delta;
        targetCamPos.z = transform.position.z;
        targetCamPos = GetClampedPosition(targetCamPos);

        if (_focusRoutine != null)
            StopCoroutine(_focusRoutine);

        _focusRoutine = StartCoroutine(SmoothFocus(targetCamPos));
    }

    public void AdjustCameraForCursor(Vector3 cursorWorldPos)
    {
        float halfH = _cam.orthographicSize;
        float halfW = halfH * _cam.aspect;

        float cellW = 1f;
        float cellH = 1f;
        if (tilemap != null && tilemap.layoutGrid != null)
        {
            Vector3 cell = tilemap.layoutGrid.cellSize;
            cellW = Mathf.Max(0.0001f, Mathf.Abs(cell.x));
            cellH = Mathf.Max(0.0001f, Mathf.Abs(cell.y));
        }

        float marginX = Mathf.Min(halfW * 0.95f, Mathf.Max(0f, cursorEdgeMarginCellsX) * cellW);
        float marginY = Mathf.Min(halfH * 0.95f, Mathf.Max(0f, cursorEdgeMarginCellsY) * cellH);

        Vector3 camPos = transform.position;
        float left = camPos.x - halfW + marginX;
        float right = camPos.x + halfW - marginX;
        float bottom = camPos.y - halfH + marginY;
        float top = camPos.y + halfH - marginY;

        float deltaX = 0f;
        float deltaY = 0f;

        if (cursorWorldPos.x < left) deltaX = cursorWorldPos.x - left;
        else if (cursorWorldPos.x > right) deltaX = cursorWorldPos.x - right;

        if (cursorWorldPos.y < bottom) deltaY = cursorWorldPos.y - bottom;
        else if (cursorWorldPos.y > top) deltaY = cursorWorldPos.y - top;

        if (Mathf.Abs(deltaX) < 0.0001f && Mathf.Abs(deltaY) < 0.0001f)
            return;

        Vector3 targetCamPos = new Vector3(camPos.x + deltaX, camPos.y + deltaY, transform.position.z);
        targetCamPos = GetClampedPosition(targetCamPos);

        if (_focusRoutine != null)
            StopCoroutine(_focusRoutine);

        _focusRoutine = StartCoroutine(SmoothFocus(targetCamPos));
    }

    IEnumerator SmoothFocus(Vector3 target)
    {
        Vector3 start = transform.position;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * Mathf.Max(0.01f, focusSpeed);
            Vector3 p = Vector3.Lerp(start, target, t);
            transform.position = GetClampedPosition(new Vector3(p.x, p.y, transform.position.z));
            yield return null;
        }

        transform.position = GetClampedPosition(new Vector3(target.x, target.y, transform.position.z));
        _focusRoutine = null;
    }

    Vector3 GetClampedPosition(Vector3 position)
    {
        if (!_hasBounds)
            return new Vector3(position.x, position.y, transform.position.z);

        float halfH = _cam.orthographicSize;
        float halfW = halfH * _cam.aspect;

        float minX = _paintedWorldBounds.min.x + halfW;
        float maxX = _paintedWorldBounds.max.x - halfW;
        float minY = _paintedWorldBounds.min.y + halfH;
        float maxY = _paintedWorldBounds.max.y - halfH;

        float x = minX > maxX ? (_paintedWorldBounds.min.x + _paintedWorldBounds.max.x) * 0.5f : Mathf.Clamp(position.x, minX, maxX);
        float y = minY > maxY ? (_paintedWorldBounds.min.y + _paintedWorldBounds.max.y) * 0.5f : Mathf.Clamp(position.y, minY, maxY);

        return new Vector3(x, y, transform.position.z);
    }
}
