using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// Дебаг-сцена жестов (этап 2): все 6 жестов на голых прямоугольниках
/// с индикаторами прогресса. R — сброс, Q — в хаб.
public class GestureDebugController : MonoBehaviour
{
    private readonly List<GestureRecognizer> recognizers = new List<GestureRecognizer>();
    private readonly List<Transform> bars = new List<Transform>();
    private readonly List<TextMesh> statusLabels = new List<TextMesh>();

    private void Awake()
    {
        GameState.EnsureInit();

        var cam = Camera.main;
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
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.16f);

        SpriteUtils.MakeText("Title", null, new Vector3(0, 4.6f, 0),
            "GestureDebug — 6 жестов | R — сброс, Q — в хаб", 36, 0.14f, Color.white, 5);

        var cfg = GameState.Gestures;

        BuildStation(GestureKind.Rotate, new Vector3(-5.8f, 1.9f, 0),
            "Вращение", $"зажми ЛКМ и кружи против часовой ({cfg.screwTurnsToRemove} об.)",
            new Color(0.75f, 0.4f, 0.35f));
        BuildStation(GestureKind.Wipe, new Vector3(0f, 1.9f, 0),
            "Протирка", "зажми ЛКМ и стирай пятна",
            new Color(0.45f, 0.42f, 0.3f));
        BuildStation(GestureKind.Rock, new Vector3(5.8f, 1.9f, 0),
            "Качание", $"зажми ЛКМ, води влево-вправо ({cfg.rocksRequired} кач.)",
            new Color(0.5f, 0.35f, 0.55f));
        BuildDragStation(new Vector3(-5.8f, -2.3f, 0));
        BuildStation(GestureKind.Click, new Vector3(0f, -2.3f, 0),
            "Клик", "кликай по площадке",
            new Color(0.3f, 0.5f, 0.4f));
        BuildStation(GestureKind.Zoom, new Vector3(5.8f, -2.3f, 0),
            "Зум", "крути колесо мыши над площадкой",
            new Color(0.3f, 0.42f, 0.55f));
    }

    private GestureRecognizer BuildStation(GestureKind kind, Vector3 center,
        string title, string hint, Color color)
    {
        var areaSize = new Vector2(4.2f, 2.4f);

        var station = new GameObject($"Station_{kind}");
        station.transform.position = center;

        var area = SpriteUtils.MakeSprite("Area", station.transform, Vector3.zero, areaSize, color, 1);
        var rec = area.AddComponent<GestureRecognizer>();
        rec.kind = kind;
        rec.config = GameState.Gestures;
        rec.areaSize = areaSize;

        FinishStation(station.transform, rec, center, title, hint);
        return rec;
    }

    private void BuildDragStation(Vector3 center)
    {
        var station = new GameObject("Station_Drag");
        station.transform.position = center;

        // Слот-цель справа, полупрозрачный контур.
        var slot = SpriteUtils.MakeSprite("Slot", station.transform,
            new Vector3(1.4f, 0f, 0f), new Vector2(1.1f, 1.1f), new Color(1f, 1f, 1f, 0.35f), 1);

        // Перетаскиваемый квадрат слева.
        var box = SpriteUtils.MakeSprite("Box", station.transform,
            new Vector3(-1.4f, 0f, 0f), new Vector2(0.9f, 0.9f), new Color(0.85f, 0.65f, 0.3f), 2);
        var rec = box.AddComponent<GestureRecognizer>();
        rec.kind = GestureKind.Drag;
        rec.config = GameState.Gestures;
        rec.areaSize = new Vector2(0.9f, 0.9f);
        rec.dragTarget = slot.transform;

        FinishStation(station.transform, rec, center, "Перетаскивание", "тащи квадрат в слот");
    }

    private void FinishStation(Transform station, GestureRecognizer rec, Vector3 center,
        string title, string hint)
    {
        SpriteUtils.MakeText("Title", station, new Vector3(0f, 1.55f, 0f),
            title, 30, 0.12f, Color.white, 5);
        SpriteUtils.MakeText("Hint", station, new Vector3(0f, 1.15f, 0f),
            hint, 22, 0.09f, new Color(1f, 1f, 1f, 0.65f), 5);

        // Индикатор прогресса.
        SpriteUtils.MakeSprite("BarBG", station, new Vector3(0f, -1.55f, 0f),
            new Vector2(4.2f, 0.3f), new Color(0f, 0f, 0f, 0.6f), 4);
        var fill = SpriteUtils.MakeSprite("BarFill", station, new Vector3(-2.1f, -1.55f, 0f),
            new Vector2(0.001f, 0.24f), new Color(0.4f, 0.9f, 0.4f), 5);

        var status = SpriteUtils.MakeText("Status", station, new Vector3(0f, -1.95f, 0f),
            "0%", 22, 0.09f, new Color(1f, 1f, 1f, 0.8f), 5);

        recognizers.Add(rec);
        bars.Add(fill.transform);
        statusLabels.Add(status);
    }

    private void Update()
    {
        for (int i = 0; i < recognizers.Count; i++)
        {
            var rec = recognizers[i];
            float p = Mathf.Clamp01(rec.Progress);
            const float fullWidth = 4.2f;
            bars[i].localScale = new Vector3(Mathf.Max(0.001f, fullWidth * p), 0.24f, 1f);
            bars[i].localPosition = new Vector3(-fullWidth * 0.5f + fullWidth * p * 0.5f, -1.55f, 0f);

            string extra = rec.kind switch
            {
                GestureKind.Click => $"кликов: {rec.ClickCount}",
                GestureKind.Rotate when rec.OvershootCount > 0 => $"{Mathf.RoundToInt(p * 100)}% (перекрут ×{rec.OvershootCount})",
                _ => rec.Completed ? "готово" : $"{Mathf.RoundToInt(p * 100)}%",
            };
            statusLabels[i].text = extra;
            statusLabels[i].color = rec.Completed
                ? new Color(0.5f, 1f, 0.5f)
                : new Color(1f, 1f, 1f, 0.8f);
        }

        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.rKey.wasPressedThisFrame)
        {
            foreach (var rec in recognizers)
                rec.ResetGesture();
        }
        if (keyboard.qKey.wasPressedThisFrame)
            SceneManager.LoadScene("SampleScene");
    }
}
