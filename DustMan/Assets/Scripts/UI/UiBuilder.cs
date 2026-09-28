using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// Программная сборка uGUI (в проекте нет заготовленных канвасов).
public static class UiBuilder
{
    private static Font cachedFont;

    public static Font DefaultFont
    {
        get
        {
            if (cachedFont == null)
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return cachedFont;
        }
    }

    /// EventSystem с InputSystemUIInputModule (в проекте включён только Input System).
    public static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        var module = go.AddComponent<InputSystemUIInputModule>();
        if (InputSystem.actions != null)
            module.actionsAsset = InputSystem.actions;
    }

    public static Canvas CreateCanvas(string name, int sortingOrder)
    {
        EnsureEventSystem();

        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static RectTransform CreateRect(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return rt;
    }

    public static Image CreatePanel(string name, Transform parent, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = SpriteUtils.White;
        img.color = color;
        return img;
    }

    public static Text CreateText(string name, Transform parent, string content, int size,
        Color color, TextAnchor align,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
        var text = rt.gameObject.AddComponent<Text>();
        text.font = DefaultFont;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.text = content;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        // Надписи не должны перехватывать клики (кнопки ловят их своим Image).
        text.raycastTarget = false;
        return text;
    }

    public static Button CreateButton(string name, Transform parent, string label,
        UnityAction onClick, Color bg,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var img = CreatePanel(name, parent, bg, anchorMin, anchorMax, offsetMin, offsetMax);
        var button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        if (onClick != null)
            button.onClick.AddListener(onClick);

        CreateText("Label", img.transform, label, 18, Color.white, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return button;
    }

    /// Горизонтальный слайдер с ручкой (шкала цены).
    public static Slider CreateSlider(string name, Transform parent, float min, float max,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
        var slider = rt.gameObject.AddComponent<Slider>();

        var bg = CreatePanel("Background", rt, new Color(0.15f, 0.15f, 0.18f, 1f),
            new Vector2(0f, 0.35f), new Vector2(1f, 0.65f), Vector2.zero, Vector2.zero);

        var fillArea = CreateRect("Fill Area", rt,
            new Vector2(0f, 0.35f), new Vector2(1f, 0.65f), new Vector2(6, 0), new Vector2(-6, 0));
        var fill = CreatePanel("Fill", fillArea, new Color(0.85f, 0.7f, 0.25f, 1f),
            Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(12, 0));

        var handleArea = CreateRect("Handle Slide Area", rt,
            Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-10, 0));
        var handle = CreatePanel("Handle", handleArea, new Color(0.95f, 0.92f, 0.85f, 1f),
            Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(20, 0));

        slider.targetGraphic = handle;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = true;
        return slider;
    }
}
