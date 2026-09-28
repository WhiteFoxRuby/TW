using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// Распознаватель одного жеста над прямоугольной зоной (mvp-scope §5).
/// Активируется при захвате зоны мышью, отдаёт события прогресса.
/// Верстак использует трекеры из GestureCore напрямую; этот компонент —
/// самостоятельная обёртка для дебаг-сцены и простых зон.
public class GestureRecognizer : MonoBehaviour
{
    public GestureKind kind = GestureKind.Click;
    public GestureConfig config;
    /// Зона взаимодействия вокруг transform.position.
    public Vector2 areaSize = new Vector2(3f, 2f);
    /// Для Drag: слот-цель.
    public Transform dragTarget;
    /// Для Wipe: рисовать ли ячейки грязи как дочерние спрайты.
    public bool buildWipeVisuals = true;

    public float Progress { get; private set; }
    public bool Completed { get; private set; }
    /// Сколько раз жест ушёл за допустимый предел (перекрут).
    public int OvershootCount { get; private set; }
    public int ClickCount { get; private set; }

    public event Action<GestureRecognizer> ProgressChanged;
    public event Action<GestureRecognizer> GestureCompleted;

    private RotationTracker rotation;
    private RockTracker rock;
    private WipeGrid wipe;
    private GameObject wipeCellsRoot;

    private bool engaged;
    private bool overshootReported;
    private Vector3 dragStartPos;
    private Vector2 dragGrabOffset;
    private float zoomValue = 0.5f;
    private float clickFlash;
    private bool wipeEngaged;
    private Vector2 lastWipeCursor;
    private bool hasLastWipe;

    private void Start()
    {
        if (config == null)
        {
            GameState.EnsureInit();
            config = GameState.Gestures;
        }
        rotation = new RotationTracker();
        rock = new RockTracker(config.rockMinAmplitude);
        dragStartPos = transform.position;
        if (kind == GestureKind.Wipe)
            BuildWipeGrid();
        if (kind == GestureKind.Zoom)
            SetProgress(zoomValue, false);
    }

    private void OnDestroy()
    {
        if (wipeCellsRoot != null)
            Destroy(wipeCellsRoot);
    }

    public void ResetGesture()
    {
        Progress = 0f;
        Completed = false;
        OvershootCount = 0;
        ClickCount = 0;
        overshootReported = false;
        engaged = false;
        wipeEngaged = false;
        hasLastWipe = false;
        zoomValue = 0.5f;
        rotation.Reset();
        rotation.Release();
        rock.Reset();
        if (kind == GestureKind.Drag)
            transform.position = dragStartPos;
        if (kind == GestureKind.Wipe)
            BuildWipeGrid();
        if (kind == GestureKind.Zoom)
            Progress = zoomValue;
        ProgressChanged?.Invoke(this);
    }

    private void Update()
    {
        var mouse = Mouse.current;
        var cam = Camera.main;
        if (mouse == null || cam == null) return;

        Vector2 cursor = cam.ScreenToWorldPoint(mouse.position.ReadValue());
        bool inside = ContainsPoint(cursor);

        switch (kind)
        {
            case GestureKind.Rotate: UpdateRotate(mouse, cursor, inside); break;
            case GestureKind.Wipe: UpdateWipe(mouse, cursor, inside); break;
            case GestureKind.Rock: UpdateRock(mouse, cursor, inside); break;
            case GestureKind.Drag: UpdateDrag(mouse, cursor, inside); break;
            case GestureKind.Click: UpdateClick(mouse, inside); break;
            case GestureKind.Zoom: UpdateZoom(mouse, inside); break;
        }
    }

    public bool ContainsPoint(Vector2 point)
    {
        Vector2 c = transform.position;
        return Mathf.Abs(point.x - c.x) <= areaSize.x * 0.5f
            && Mathf.Abs(point.y - c.y) <= areaSize.y * 0.5f;
    }

    private void SetProgress(float value, bool allowComplete = true)
    {
        value = Mathf.Clamp01(value);
        bool changed = !Mathf.Approximately(value, Progress);
        Progress = value;
        if (allowComplete && !Completed && Progress >= 1f)
        {
            Completed = true;
            GestureCompleted?.Invoke(this);
        }
        if (changed)
            ProgressChanged?.Invoke(this);
    }

    private void UpdateRotate(Mouse mouse, Vector2 cursor, bool inside)
    {
        if (mouse.leftButton.wasPressedThisFrame && inside)
        {
            rotation.Engage(transform.position, cursor);
            overshootReported = false;
        }
        if (mouse.leftButton.wasReleasedThisFrame)
            rotation.Release();

        if (rotation.Engaged && mouse.leftButton.isPressed)
        {
            rotation.Step(cursor, config.rotationDeadzoneDegPerFrame, config.rotationMinRadius);
            float required = Mathf.Max(1, config.screwTurnsToRemove) * 360f;
            float ccw = Mathf.Max(0f, rotation.AccumulatedDeg);
            SetProgress(ccw / required);

            float limit = required + config.overtightenExtraTurns * 360f;
            if (ccw > limit && !overshootReported)
            {
                overshootReported = true;
                OvershootCount++;
                ProgressChanged?.Invoke(this);
            }
        }
    }

    private void BuildWipeGrid()
    {
        if (wipeCellsRoot != null)
            Destroy(wipeCellsRoot);

        Vector2 c = transform.position;
        var rect = new Rect(c.x - areaSize.x * 0.5f, c.y - areaSize.y * 0.5f, areaSize.x, areaSize.y);
        wipe = new WipeGrid(config.wipeGridSize, rect);

        if (!buildWipeVisuals) return;

        // Родитель — контейнер станции (без масштаба): сам спрайт зоны
        // отмасштабирован, и дочерние ячейки унаследовали бы его scale.
        wipeCellsRoot = new GameObject("WipeCells");
        if (transform.parent != null)
            wipeCellsRoot.transform.SetParent(transform.parent, false);
        var cells = new GameObject[wipe.GridSize, wipe.GridSize];
        var cellSize = wipe.CellSize;
        for (int x = 0; x < wipe.GridSize; x++)
        {
            for (int y = 0; y < wipe.GridSize; y++)
            {
                var center = wipe.CellCenter(x, y);
                var cell = SpriteUtils.MakeSprite($"Cell_{x}_{y}", wipeCellsRoot.transform,
                    Vector3.zero, cellSize * 0.92f, new Color(0.25f, 0.18f, 0.1f, 0.85f), 6);
                cell.transform.position = new Vector3(center.x, center.y, 0f);
                cells[x, y] = cell;
            }
        }
        wipe.CellCleared = (x, y) =>
        {
            if (cells[x, y] != null) Destroy(cells[x, y]);
        };
    }

    private void UpdateWipe(Mouse mouse, Vector2 cursor, bool inside)
    {
        if (wipe == null) return;

        // Жест начинается только нажатием внутри зоны: чужой зажатый ЛКМ,
        // заехавший в зону, ничего не стирает.
        if (mouse.leftButton.wasPressedThisFrame && inside)
        {
            wipeEngaged = true;
            hasLastWipe = false;
        }
        if (!mouse.leftButton.isPressed)
        {
            wipeEngaged = false;
            hasLastWipe = false;
            return;
        }

        if (wipeEngaged && inside)
        {
            if (hasLastWipe)
                wipe.WipeSegment(lastWipeCursor, cursor, config.wipeBrushRadius);
            else
                wipe.WipeAt(cursor, config.wipeBrushRadius);
            lastWipeCursor = cursor;
            hasLastWipe = true;
            SetProgress(wipe.CleanFraction / config.wipeCompleteFraction);
        }
        else
        {
            hasLastWipe = false;
        }
    }

    private void UpdateRock(Mouse mouse, Vector2 cursor, bool inside)
    {
        if (mouse.leftButton.wasPressedThisFrame && inside)
            engaged = true;
        if (mouse.leftButton.wasReleasedThisFrame && engaged)
        {
            engaged = false;
            rock.Finish();
            SetProgress((float)rock.Rocks / Mathf.Max(1, config.rocksRequired));
        }
        if (engaged && mouse.leftButton.isPressed)
        {
            rock.Step(cursor.x);
            SetProgress((float)rock.Rocks / Mathf.Max(1, config.rocksRequired));
        }
    }

    private void UpdateDrag(Mouse mouse, Vector2 cursor, bool inside)
    {
        if (Completed) return;

        if (mouse.leftButton.wasPressedThisFrame && inside)
        {
            engaged = true;
            dragGrabOffset = (Vector2)transform.position - cursor;
        }

        if (engaged)
        {
            transform.position = cursor + dragGrabOffset;

            bool nearTarget = dragTarget != null &&
                Vector2.Distance(transform.position, dragTarget.position) <= config.dragSnapRadius;
            HighlightTarget(nearTarget);

            // !isPressed вместо wasReleasedThisFrame: пропущенное событие
            // отпускания не оставит квадрат приклеенным к курсору.
            if (!mouse.leftButton.isPressed)
            {
                engaged = false;
                if (nearTarget)
                {
                    transform.position = dragTarget.position;
                    SetProgress(1f);
                }
                else
                {
                    transform.position = dragStartPos;
                    SetProgress(0f);
                }
                HighlightTarget(false);
            }
        }
    }

    private void HighlightTarget(bool on)
    {
        if (dragTarget == null) return;
        var sr = dragTarget.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = on ? new Color(0.4f, 0.9f, 0.4f, 0.9f) : new Color(1f, 1f, 1f, 0.35f);
    }

    private void UpdateClick(Mouse mouse, bool inside)
    {
        if (mouse.leftButton.wasPressedThisFrame && inside)
        {
            ClickCount++;
            clickFlash = 0.15f;
            SetProgress(1f);
        }
        if (clickFlash > 0f)
        {
            clickFlash -= Time.deltaTime;
            if (clickFlash <= 0f && Completed)
            {
                // Клик — мгновенный жест: индикатор гаснет, счётчик остаётся.
                Progress = 0f;
                Completed = false;
                ProgressChanged?.Invoke(this);
            }
        }
    }

    private void UpdateZoom(Mouse mouse, bool inside)
    {
        if (!inside) return;
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f)) return;

        float span = Mathf.Max(0.01f, config.zoomMax - config.zoomMin);
        zoomValue = Mathf.Clamp01(zoomValue + Mathf.Sign(scroll) * (config.zoomStep / span));
        // Зум — не «завершаемый» жест: индикатор показывает уровень, без «готово».
        SetProgress(zoomValue, false);
    }
}
