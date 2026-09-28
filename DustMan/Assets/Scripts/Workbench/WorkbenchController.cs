using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Сцена верстака (этапы 3-4): осмотр (вращение, зум, лупа), работа
/// (разборка по винтам, инструменты со стола, устранение дефектов),
/// проверка кнопкой фонаря с симптомами, расчёт качества.
public class WorkbenchController : MonoBehaviour
{
    private class ToolView
    {
        public ToolDefinition Def;
        public GameObject Go;
        public Vector3 Home;
    }

    private ItemInstance item;
    private LanternView view;
    private GestureConfig cfg;
    private Camera cam;

    private readonly List<ToolView> tools = new List<ToolView>();
    private ToolView heldTool;

    private string heldSpare;          // "lens" | "batteries" | null
    private GameObject heldSpareGo;
    private Vector2 spareLensPos;
    private Vector2 spareBatteriesPos;

    private ScrewView activeScrew;     // вращается отвёрткой
    private ScrewView draggingScrew;   // переносится со стола в отверстие

    private bool draggingButton;
    private bool inspectDragging;
    private Vector2 lastCursor;

    private RockTracker rockEngagement;
    /// Накопленные ходы (не качания): нечётный ход не теряется между захватами.
    private int rockStrokesBase;
    private bool rockOnFixedContacts;

    private Vector2 lastWipeCursor;
    private bool hasLastWipe;

    private GameObject magnifierGlass;
    private float magnifierHover;

    private bool checkPassed;
    private bool finished;

    private Text titleText;
    private Text logText;
    private GameObject finishButtonGo;

    private void Awake()
    {
        GameState.EnsureInit();
        cfg = GameState.Gestures;

        item = GameState.CurrentItem;
        if (item == null || item.RepairCompleted)
        {
            item = ItemInstanceGenerator.Generate(GameState.LanternDefinition, GameState.Rng);
            Debug.Log("Верстак запущен без купленного фонаря — сгенерирован тестовый экземпляр.\n" + item.Describe());
        }
        else
        {
            DirectorTelemetry.Log(DirectorEventType.RepairStarted,
                DirectorTelemetry.Payload(
                    DirectorTelemetry.Str("item_id", item.Definition.id),
                    DirectorTelemetry.Bool("hum", item.Hum)),
                item);
        }

        BuildScene();
        BuildUi();

        // Восстановить частичный прогресс зачистки (возврат в сцену по Q).
        var oxide = item.FindDefect("oxidized_contacts");
        if (oxide != null && !oxide.IsFixed && oxide.Progress > 0f)
            rockStrokesBase = Mathf.RoundToInt(oxide.Progress * Mathf.Max(1, cfg.rocksRequired)) * 2;

        Log("Осмотри фонарь: вращай перетаскиванием, зумь колесом, скрытое ищи лупой.");
    }

    private void BuildScene()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var camObj = new GameObject("Camera");
            cam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            camObj.tag = "MainCamera";
        }
        cam.orthographic = true;
        cam.orthographicSize = 5;
        cam.transform.position = new Vector3(0, 0, -10);
        cam.backgroundColor = new Color(0.13f, 0.1f, 0.08f);

        // Стол и зоны.
        SpriteUtils.MakeSprite("Table", null, new Vector3(0f, -0.7f, 0f),
            new Vector2(15.5f, 5.6f), new Color(0.36f, 0.26f, 0.18f), 0);
        SpriteUtils.MakeSprite("ToolTray", null, new Vector3(-6.4f, 0f, 0f),
            new Vector2(2.1f, 5.2f), new Color(0.28f, 0.2f, 0.14f), 1);
        SpriteUtils.MakeText("TrayLabel", null, new Vector3(-6.4f, 2.35f, 0f),
            "Инструменты", 24, 0.09f, new Color(1f, 1f, 1f, 0.75f), 10);

        SpriteUtils.MakeSprite("SpareBox", null, new Vector3(5.9f, -1.7f, 0f),
            new Vector2(2.6f, 2.1f), new Color(0.3f, 0.24f, 0.16f), 1);
        SpriteUtils.MakeText("SpareLabel", null, new Vector3(5.9f, -0.5f, 0f),
            "Запас (бесплатно)", 22, 0.085f, new Color(1f, 1f, 1f, 0.75f), 10);

        spareLensPos = new Vector2(5.35f, -1.75f);
        spareBatteriesPos = new Vector2(6.45f, -1.75f);
        SpriteUtils.MakeSprite("SpareLens", null, spareLensPos,
            new Vector2(0.65f, 0.65f), new Color(0.65f, 0.85f, 0.95f), 2);
        SpriteUtils.MakeText("SpareLensLabel", null, new Vector3(spareLensPos.x, -2.35f, 0f),
            "линза", 20, 0.08f, new Color(1f, 1f, 1f, 0.7f), 10);
        SpriteUtils.MakeSprite("SpareBatteries", null, spareBatteriesPos,
            new Vector2(0.5f, 0.7f), new Color(0.65f, 0.85f, 0.45f), 2);
        SpriteUtils.MakeText("SpareBatteriesLabel", null, new Vector3(spareBatteriesPos.x, -2.35f, 0f),
            "батарейки", 20, 0.08f, new Color(1f, 1f, 1f, 0.7f), 10);

        // Фонарь.
        var viewGo = new GameObject("Lantern");
        view = viewGo.AddComponent<LanternView>();
        view.Build(item, cfg);

        BuildTools();

        // Лупа-стекло (видна, пока лупа в руке).
        magnifierGlass = SpriteUtils.MakeSprite("MagnifierGlass", null, Vector3.zero,
            new Vector2(1.1f, 1.1f), new Color(0.7f, 0.9f, 1f, 0.25f), 11);
        magnifierGlass.SetActive(false);
    }

    private void BuildTools()
    {
        var defs = new List<ToolDefinition>
        {
            LoadTool("screwdriver", "Отвёртка", GestureKind.Rotate, new Color(0.8f, 0.35f, 0.3f)),
            LoadTool("pliers", "Плоскогубцы", GestureKind.Drag, new Color(0.5f, 0.5f, 0.55f)),
            LoadTool("rag", "Тряпка", GestureKind.Wipe, new Color(0.9f, 0.85f, 0.7f)),
            LoadTool("magnifier", "Лупа", GestureKind.Zoom, new Color(0.4f, 0.7f, 0.9f)),
            LoadTool("sandpaper", "Наждачка", GestureKind.Rock, new Color(0.85f, 0.6f, 0.35f)),
        };

        for (int i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            var home = new Vector3(-6.4f, 1.7f - i * 0.95f, 0f);
            var container = new GameObject($"Tool_{def.id}");
            container.transform.position = home;
            SpriteUtils.MakeSprite("Sprite", container.transform, Vector3.zero,
                new Vector2(0.7f, 0.7f), def.color, 9);
            SpriteUtils.MakeText("Label", container.transform, new Vector3(0f, -0.52f, 0f),
                def.displayName, 18, 0.075f, new Color(1f, 1f, 1f, 0.8f), 10);

            tools.Add(new ToolView { Def = def, Go = container, Home = home });
        }
    }

    private ToolDefinition LoadTool(string id, string name, GestureKind gesture, Color color)
    {
        var t = Resources.Load<ToolDefinition>("Tool_" + id);
        if (t != null) return t;
        t = ScriptableObject.CreateInstance<ToolDefinition>();
        t.id = id;
        t.displayName = name;
        t.gesture = gesture;
        t.color = color;
        return t;
    }

    private void BuildUi()
    {
        var canvas = UiBuilder.CreateCanvas("WorkbenchCanvas", 10);

        UiBuilder.CreatePanel("TitleBG", canvas.transform, new Color(0f, 0f, 0f, 0.55f),
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -40), new Vector2(0, 0));
        titleText = UiBuilder.CreateText("Title", canvas.transform, "", 20,
            Color.white, TextAnchor.MiddleCenter,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10, -40), new Vector2(-10, 0));

        UiBuilder.CreatePanel("LogBG", canvas.transform, new Color(0f, 0f, 0f, 0.55f),
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0, 0), new Vector2(0, 66));
        logText = UiBuilder.CreateText("Log", canvas.transform, "", 19,
            new Color(1f, 0.95f, 0.8f), TextAnchor.MiddleCenter,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10, 28), new Vector2(-10, 64));
        UiBuilder.CreateText("Hint", canvas.transform,
            "ЛКМ — взять/работать, ПКМ — вернуть инструмент, колесо — зум, Q — в хаб", 15,
            new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10, 2), new Vector2(-10, 28));

        var finishButton = UiBuilder.CreateButton("Finish", canvas.transform,
            "Завершить ремонт (E)", FinishRepair, new Color(0.3f, 0.5f, 0.25f, 0.95f),
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-250, -92), new Vector2(-10, -48));
        finishButtonGo = finishButton.gameObject;
        finishButtonGo.SetActive(false);
    }

    private void Log(string message)
    {
        if (logText != null)
            logText.text = message;
        Debug.Log("Верстак: " + message);
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null || finished) return;

        if (keyboard.qKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene("SampleScene");
            return;
        }
        if (keyboard.eKey.wasPressedThisFrame && checkPassed)
        {
            FinishRepair();
            return;
        }
        if (keyboard.f1Key.wasPressedThisFrame)
            DirectorDebugPanel.Toggle();

        HandleZoom(mouse);

        Vector2 cursor = cam.ScreenToWorldPoint(mouse.position.ReadValue());

        if (heldTool != null)
            heldTool.Go.transform.position = new Vector3(cursor.x + 0.3f, cursor.y - 0.3f, 0f);
        if (heldSpareGo != null)
            heldSpareGo.transform.position = cursor;

        UpdateMagnifier(cursor);

        if (mouse.rightButton.wasPressedThisFrame)
            ReturnHeld();

        // Клик по uGUI (кнопка «Завершить») не должен проваливаться в мир.
        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (mouse.leftButton.wasPressedThisFrame && !overUi)
            OnPress(cursor);
        if (mouse.leftButton.isPressed)
            OnHold(cursor);
        if (mouse.leftButton.wasReleasedThisFrame)
            OnRelease(cursor);

        UpdateTitle();
    }

    private void HandleZoom(Mouse mouse)
    {
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f)) return;
        cam.orthographicSize = Mathf.Clamp(
            cam.orthographicSize - Mathf.Sign(scroll) * cfg.zoomStep,
            cfg.zoomMin, cfg.zoomMax);
    }

    private void UpdateTitle()
    {
        string phase = view.Exploded ? "Работа (разобран)"
            : checkPassed ? "Проверка пройдена"
            : "Осмотр / Работа";
        int forecast = item.ComputeQuality(GameState.Economy);
        titleText.text = $"Верстак — {item.Definition.displayName}   |   Фаза: {phase}   |   Прогноз качества: {forecast}/100";
    }

    // ------------------------------------------------------------------ ввод

    private void OnPress(Vector2 cursor)
    {
        if (heldSpare != null)
            return; // бросаем на release

        if (heldTool != null)
        {
            // Вернуть инструмент кликом по лотку.
            if (cursor.x < -5.3f)
            {
                ReturnHeld();
                return;
            }
            EngageToolGesture(cursor);
            return;
        }

        // Пустая рука: взять инструмент?
        foreach (var tool in tools)
        {
            if (Vector2.Distance(cursor, tool.Go.transform.position) < 0.55f)
            {
                heldTool = tool;
                Log($"{tool.Def.displayName} в руке.");
                return;
            }
        }

        // Взять запасную деталь или выкрученный винт.
        if (view.Exploded)
        {
            if (Vector2.Distance(cursor, spareLensPos) < 0.5f)
            {
                PickSpare("lens", new Vector2(0.65f, 0.65f), new Color(0.65f, 0.85f, 0.95f));
                return;
            }
            if (Vector2.Distance(cursor, spareBatteriesPos) < 0.5f)
            {
                PickSpare("batteries", new Vector2(0.5f, 0.7f), new Color(0.65f, 0.85f, 0.45f));
                return;
            }
        }
        foreach (var screw in view.Screws)
        {
            if (screw.State == ScrewState.Removed &&
                Vector2.Distance(cursor, screw.Go.transform.position) < 0.45f)
            {
                draggingScrew = screw;
                return;
            }
        }

        // Проверка: клик по кнопке собранного фонаря.
        if (!view.Exploded && AllScrewsTightened() &&
            Vector2.Distance(cursor, view.ModuleCenter("button")) < 0.5f)
        {
            DoCheck();
            return;
        }

        // Осмотр: вращение собранного фонаря за корпус.
        if (!view.Exploded && Vector2.Distance(cursor, view.ModuleCenter("body")) < 1.6f)
        {
            inspectDragging = true;
            lastCursor = cursor;
        }
    }

    private void PickSpare(string id, Vector2 size, Color color)
    {
        heldSpare = id;
        heldSpareGo = SpriteUtils.MakeSprite("HeldSpare", null, Vector3.zero, size, color, 12);
    }

    private void EngageToolGesture(Vector2 cursor)
    {
        switch (heldTool.Def.id)
        {
            case "screwdriver":
                foreach (var screw in view.Screws)
                {
                    if (screw.State == ScrewState.Removed) continue;
                    Vector2 screwPos = screw.Go.transform.position;
                    if (Vector2.Distance(cursor, screwPos) < cfg.screwEngageRadius)
                    {
                        activeScrew = screw;
                        screw.Tracker.Engage(screwPos, cursor);
                        break;
                    }
                }
                break;

            case "sandpaper":
                if (view.Exploded &&
                    Vector2.Distance(cursor, view.ContactsCenter) < cfg.rockEngageRadius)
                {
                    var oxide = item.FindDefect("oxidized_contacts");
                    rockEngagement = new RockTracker(cfg.rockMinAmplitude);
                    rockOnFixedContacts = oxide == null || oxide.IsFixed;
                }
                break;

            case "pliers":
                // Кнопка — внешний модуль, чинится и на собранном фонаре.
                if (item.HasActiveDefect("bent_button") &&
                    Vector2.Distance(cursor, view.ModuleCenter("button")) < 0.7f)
                {
                    view.ResetInspectAngle();
                    inspectDragging = false;
                    draggingButton = true;
                    view.ShowButtonTarget(true);
                }
                break;
        }
    }

    private void OnHold(Vector2 cursor)
    {
        if (inspectDragging)
        {
            float delta = cursor.x - lastCursor.x;
            view.AddInspectAngle(-delta * cfg.inspectRotateDegPerUnit);
            lastCursor = cursor;
        }

        if (activeScrew != null)
            HandleScrewRotation(cursor);

        if (heldTool != null && heldTool.Def.id == "rag")
            HandleWipe(cursor);

        if (rockEngagement != null)
            HandleRock(cursor);

        if (draggingButton)
            view.SetButtonDragPos(cursor);

        if (draggingScrew != null)
            draggingScrew.Go.transform.position = cursor;
    }

    private void OnRelease(Vector2 cursor)
    {
        inspectDragging = false;

        if (activeScrew != null)
        {
            activeScrew.Tracker.Release();
            activeScrew = null;
        }

        if (rockEngagement != null)
        {
            rockEngagement.Finish();
            if (rockOnFixedContacts)
            {
                if (rockEngagement.Rocks >= 1)
                {
                    item.ScratchCount++;
                    GameDirector.ReportGestureError("scratch_clean_contacts");
                    Log("Контакты уже чистые — остались лишние царапины (штраф качества).");
                }
            }
            else
            {
                rockStrokesBase += rockEngagement.Strokes;
                ApplyRockProgress();
            }
            rockEngagement = null;
        }
        hasLastWipe = false;

        if (draggingButton)
        {
            draggingButton = false;
            view.ShowButtonTarget(false);
            if (Vector2.Distance(cursor, view.ButtonTargetPos) <= cfg.dragSnapRadius)
            {
                item.FindDefect("bent_button").MarkFixed();
                Log("Кнопка выпрямлена плоскогубцами.");
            }
            view.ResetButtonPos();
            view.RefreshDefectVisuals();
        }

        if (draggingScrew != null)
        {
            if (Vector2.Distance(cursor, view.HoleWorldPos(draggingScrew.Index)) <= cfg.dragSnapRadius)
            {
                draggingScrew.State = ScrewState.Seated;
                draggingScrew.Tracker.Reset();
                draggingScrew.PenaltyGiven = false;
                view.SetScrewParentBody(draggingScrew);
                Log("Винт в отверстии — затяни отвёрткой по часовой.");
            }
            else
            {
                view.SetScrewOnTable(draggingScrew);
            }
            draggingScrew = null;
        }

        if (heldSpare != null)
            DropSpare(cursor);
    }

    private void ReturnHeld()
    {
        if (heldTool != null)
        {
            heldTool.Go.transform.position = heldTool.Home;
            heldTool = null;
            magnifierHover = 0f;
        }
        if (heldSpareGo != null)
        {
            Destroy(heldSpareGo);
            heldSpareGo = null;
            heldSpare = null;
        }

        // Активные жесты не должны продолжаться без инструмента в руке.
        if (activeScrew != null)
        {
            activeScrew.Tracker.Release();
            activeScrew = null;
        }
        rockEngagement = null;
        if (draggingButton)
        {
            draggingButton = false;
            view.ShowButtonTarget(false);
            view.ResetButtonPos();
            view.RefreshDefectVisuals();
        }
        hasLastWipe = false;
    }

    // ------------------------------------------------------------- инструменты

    private void HandleScrewRotation(Vector2 cursor)
    {
        var screw = activeScrew;
        screw.Tracker.Step(cursor, cfg.rotationDeadzoneDegPerFrame, cfg.rotationMinRadius);

        float acc = screw.Tracker.AccumulatedDeg; // CCW > 0
        float required = Mathf.Max(1, cfg.screwTurnsToRemove) * 360f;
        float overLimit = cfg.overtightenExtraTurns * 360f;

        if (screw.State == ScrewState.Tightened)
        {
            if (acc >= required)
            {
                screw.State = ScrewState.Removed;
                screw.Tracker.Release();
                screw.Tracker.Reset();
                screw.PenaltyGiven = false;
                view.SetScrewOnTable(screw);
                activeScrew = null;
                ResetCheckState();
                Log($"Винт выкручен ({CountScrews(ScrewState.Removed)}/{view.Screws.Count}).");
                if (AllScrews(ScrewState.Removed))
                    OnDisassembled();
            }
            else if (acc <= -overLimit && !screw.PenaltyGiven)
            {
                screw.PenaltyGiven = true;
                item.OvertightenCount++;
                GameDirector.ReportGestureError("overtighten_screw");
                Log("Перекрут! Резьба страдает — штраф к качеству.");
            }
        }
        else if (screw.State == ScrewState.Seated)
        {
            float cw = -acc;
            if (cw >= required)
            {
                screw.State = ScrewState.Tightened;
                // Захват не отпускаем: продолжение вращения по часовой сразу
                // копит перекрут (дизайн: «больше N+2 по часовой = перекрут»).
                screw.Tracker.Reset();
                screw.PenaltyGiven = false;
                Log($"Винт затянут ({CountScrews(ScrewState.Tightened)}/{view.Screws.Count}).");
                if (view.Exploded && AllScrews(ScrewState.Tightened))
                {
                    screw.Tracker.Release();
                    activeScrew = null;
                    OnAssembled();
                }
            }
            else if (acc >= 360f)
            {
                // Выкрутили обратно, не затянув.
                screw.State = ScrewState.Removed;
                screw.Tracker.Release();
                screw.Tracker.Reset();
                view.SetScrewOnTable(screw);
                activeScrew = null;
            }
        }
    }

    private void HandleWipe(Vector2 cursor)
    {
        var dirt = item.FindDefect("dirty_body");
        if (dirt == null || dirt.IsFixed || view.DirtGrid == null) return;

        int cleared = hasLastWipe
            ? view.WipeSegmentWorld(lastWipeCursor, cursor, cfg.wipeBrushRadius)
            : view.WipeAtWorld(cursor, cfg.wipeBrushRadius);
        lastWipeCursor = cursor;
        hasLastWipe = true;
        if (cleared <= 0) return;

        dirt.Progress = Mathf.Max(dirt.Progress,
            Mathf.Clamp01(view.DirtGrid.CleanFraction / cfg.wipeCompleteFraction));
        if (dirt.IsFixed)
            Log("Корпус отчищен до блеска.");
    }

    private void HandleRock(Vector2 cursor)
    {
        if (Vector2.Distance(cursor, view.ContactsCenter) > cfg.rockEngageRadius * 1.5f)
            return;
        rockEngagement.Step(cursor.x);
        if (!rockOnFixedContacts)
            ApplyRockProgress(rockEngagement.Strokes);
    }

    private void ApplyRockProgress(int engagementStrokes = 0)
    {
        var oxide = item.FindDefect("oxidized_contacts");
        if (oxide == null || oxide.IsFixed) return;

        int rocks = (rockStrokesBase + engagementStrokes) / 2;
        oxide.Progress = Mathf.Max(oxide.Progress,
            Mathf.Clamp01((float)rocks / Mathf.Max(1, cfg.rocksRequired)));
        if (oxide.IsFixed)
        {
            Log("Контакты зачищены наждачкой.");
            view.RefreshDefectVisuals();
        }
    }

    private void UpdateMagnifier(Vector2 cursor)
    {
        bool holding = heldTool != null && heldTool.Def.id == "magnifier";
        magnifierGlass.SetActive(holding);
        if (!holding)
        {
            magnifierHover = 0f;
            return;
        }
        magnifierGlass.transform.position = new Vector3(cursor.x, cursor.y, 0f);

        var crack = item.FindDefect("cracked_lens");
        if (crack == null || crack.IsFixed || crack.Revealed)
            return;

        if (Vector2.Distance(cursor, view.ModuleCenter("lens")) < cfg.magnifierRadius)
        {
            magnifierHover += Time.deltaTime;
            if (magnifierHover >= cfg.magnifierRevealSeconds)
            {
                crack.Revealed = true;
                view.RefreshDefectVisuals();
                DirectorTelemetry.Log(DirectorEventType.DefectRevealed,
                    DirectorTelemetry.Payload(
                        DirectorTelemetry.Str("defect_id", crack.Definition.id),
                        DirectorTelemetry.Str("source", "magnifier")),
                    item);
                Log("Лупа: на линзе видна трещина!");
            }
        }
        else
        {
            magnifierHover = 0f;
        }
    }

    private void DropSpare(Vector2 cursor)
    {
        if (heldSpare == "lens" && view.Exploded && view.ModuleAabb("lens").Contains(cursor))
        {
            var crack = item.FindDefect("cracked_lens");
            if (crack != null && !crack.IsFixed)
            {
                crack.Revealed = true;
                crack.MarkFixed();
                Log("Линза заменена на новую из запаса.");
            }
            else
            {
                Log("Линза и так в порядке — запасная не понадобилась.");
            }
        }
        else if (heldSpare == "batteries" && view.Exploded && view.ModuleAabb("battery_bay").Contains(cursor))
        {
            var dead = item.FindDefect("dead_batteries");
            if (dead != null && !dead.IsFixed)
            {
                dead.Revealed = true;
                dead.MarkFixed();
                Log("Свежие батарейки установлены.");
            }
            else
            {
                Log("Батарейки и так свежие.");
            }
        }

        Destroy(heldSpareGo);
        heldSpareGo = null;
        heldSpare = null;
        view.RefreshDefectVisuals();
    }

    // ------------------------------------------------------------ сборка/проверка

    private bool AllScrews(ScrewState state)
    {
        foreach (var s in view.Screws)
            if (s.State != state) return false;
        return true;
    }

    private bool AllScrewsTightened() => AllScrews(ScrewState.Tightened);

    private int CountScrews(ScrewState state)
    {
        int n = 0;
        foreach (var s in view.Screws)
            if (s.State == state) n++;
        return n;
    }

    /// Проверка аннулируется, как только целостность фонаря нарушена.
    private void ResetCheckState()
    {
        checkPassed = false;
        finishButtonGo.SetActive(false);
        view.ShowBeam(0);
    }

    private void OnDisassembled()
    {
        view.SetExploded(true);
        ResetCheckState();

        var revealed = new List<string>();
        foreach (var d in item.AllDefects())
        {
            if (d.Definition.visibility == DefectVisibility.AfterDisassembly && !d.Revealed)
            {
                d.Revealed = true;
                revealed.Add(d.Definition.displayName);
                DirectorTelemetry.Log(DirectorEventType.DefectRevealed,
                    DirectorTelemetry.Payload(
                        DirectorTelemetry.Str("defect_id", d.Definition.id),
                        DirectorTelemetry.Str("source", "disassembly")),
                    item);
            }
        }
        Log(revealed.Count > 0
            ? "Фонарь разобран. Внутри видно: " + string.Join(", ", revealed).ToLower() + "."
            : "Фонарь разобран. Внутри всё выглядит целым.");
    }

    private void OnAssembled()
    {
        view.SetExploded(false);
        Log("Фонарь собран. Нажми его кнопку, чтобы проверить.");
    }

    private void DoCheck()
    {
        var symptom = item.FirstSymptom();
        GameDirector.ReportCheckResult(symptom == null);
        if (symptom == null)
        {
            checkPassed = true;
            view.ShowBeam(2);
            finishButtonGo.SetActive(true);
            Log("Проверка: луч ровный, фонарь работает! Заверши ремонт (E).");
            return;
        }

        checkPassed = false;
        finishButtonGo.SetActive(false);
        DirectorTelemetry.Log(DirectorEventType.RepairCheckFailed,
            DirectorTelemetry.Payload(
                DirectorTelemetry.Str("defect_id", symptom.Definition.id),
                DirectorTelemetry.Str("symptom", symptom.Definition.symptom)),
            item);
        if (symptom.Definition.id == "cracked_lens")
        {
            view.ShowBeam(1);
            Log($"Проверка: {symptom.Definition.symptom}. Что-то с оптикой...");
        }
        else
        {
            view.ShowBeam(0);
            Log($"Проверка: {symptom.Definition.symptom}. Ищи причину дальше — штрафа нет.");
        }
    }

    private void FinishRepair()
    {
        if (!checkPassed || finished) return;

        bool storyItem = GameState.CurrentItem == item;
        if (storyItem && !GameState.TrySpendSlot(GameState.Economy.repairSlotCost, "repair_complete"))
        {
            Log("Слоты дня закончились — вернись в хаб и заверши день.");
            return;
        }

        item.RepairCompleted = true;
        item.Quality = item.ComputeQuality(GameState.Economy);
        finished = true;
        DirectorTelemetry.Log(DirectorEventType.RepairCompleted,
            DirectorTelemetry.Payload(
                DirectorTelemetry.Num("quality", item.Quality),
                DirectorTelemetry.Num("fixed_defects", item.FixedDefects),
                DirectorTelemetry.Num("total_defects", item.TotalDefects),
                DirectorTelemetry.Num("cleanliness", item.CleanlinessFraction()),
                DirectorTelemetry.Num("overtighten_count", item.OvertightenCount),
                DirectorTelemetry.Num("scratch_count", item.ScratchCount),
                DirectorTelemetry.Bool("hum", item.Hum)),
            item);
        GameDirector.ReportRepairCompleted(item.Quality);
        Debug.Log($"Ремонт завершён: качество {item.Quality}/100 " +
                  $"(дефекты {item.FixedDefects}/{item.TotalDefects}, чистота {item.CleanlinessFraction():P0}, " +
                  $"перекруты {item.OvertightenCount}, царапины {item.ScratchCount}).");
        SceneManager.LoadScene("SampleScene");
    }
}
