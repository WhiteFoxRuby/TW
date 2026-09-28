using UnityEngine;

/// Сводка дня: потрачено/заработано, аренда (если включена), новый день.
public class DaySummaryOverlay : MonoBehaviour
{
    private GameObject canvasRoot;

    private void Awake()
    {
        HubController.PushOverlay();
        var result = GameState.EndDay();
        Build(result);
    }

    private void OnDestroy()
    {
        HubController.PopOverlay();
        if (canvasRoot != null)
            Destroy(canvasRoot);
    }

    private void Build(DayResult result)
    {
        var canvas = UiBuilder.CreateCanvas("SummaryCanvas", 110);
        canvasRoot = canvas.gameObject;

        UiBuilder.CreatePanel("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.75f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var window = UiBuilder.CreatePanel("Window", canvas.transform, new Color(0.1f, 0.1f, 0.12f, 0.98f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-280, -200), new Vector2(280, 200));

        UiBuilder.CreateText("Title", window.transform, $"День {result.Day} завершён", 30,
            new Color(0.95f, 0.85f, 0.6f), TextAnchor.MiddleCenter,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -60), new Vector2(0, -12));

        string rent = result.RentPaid > 0 ? $"\nАренда: -{result.RentPaid}" : "";
        int delta = result.Earned - result.Spent - result.RentPaid;
        string deltaLine = delta >= 0 ? $"Итог дня: +{delta}" : $"Итог дня: {delta}";
        UiBuilder.CreateText("Body", window.transform,
            $"Потрачено: {result.Spent}\nЗаработано: {result.Earned}{rent}\n{deltaLine}\n\nМонет теперь: {result.MoneyAfter}",
            22, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0f, 0.25f), new Vector2(1f, 0.8f), Vector2.zero, Vector2.zero);

        UiBuilder.CreateButton("NewDay", window.transform, "Новый день",
            () =>
            {
                GameState.StartNewDay();
                Destroy(this);
            },
            new Color(0.3f, 0.45f, 0.25f, 1f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-110, 16), new Vector2(110, 62));
    }
}
