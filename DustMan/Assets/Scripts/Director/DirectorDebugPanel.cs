using UnityEngine;
using UnityEngine.UI;

/// Отладочная панель режиссёра (F1): текущие индексы, ручки, бюджет,
/// история решений, ручной override (design/ai-director.md, «Телеметрия и отладка»).
public class DirectorDebugPanel : MonoBehaviour
{
    private static DirectorDebugPanel instance;

    private GameObject canvasRoot;
    private Text bodyText;
    private float refreshTimer;

    public static void Toggle()
    {
        if (instance != null)
        {
            Destroy(instance.gameObject);
            instance = null;
            return;
        }
        var go = new GameObject("DirectorDebugPanel");
        instance = go.AddComponent<DirectorDebugPanel>();
    }

    private void Awake()
    {
        GameDirector.EnsureInit();
        Build();
    }

    private void OnDestroy()
    {
        if (canvasRoot != null)
            Destroy(canvasRoot);
        if (instance == this)
            instance = null;
    }

    private void Build()
    {
        var canvas = UiBuilder.CreateCanvas("DirectorDebugCanvas", 200);
        canvasRoot = canvas.gameObject;

        var window = UiBuilder.CreatePanel("Window", canvas.transform, new Color(0.05f, 0.08f, 0.1f, 0.92f),
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(8, 80), new Vector2(470, -80));

        UiBuilder.CreateText("Title", window.transform, "AI-режиссёр — отладка (F1 закрыть)", 20,
            new Color(0.7f, 0.9f, 1f), TextAnchor.MiddleCenter,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -34), new Vector2(0, -4));

        bodyText = UiBuilder.CreateText("Body", window.transform, "", 15,
            Color.white, TextAnchor.UpperLeft,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10, 96), new Vector2(-10, -38));

        UiBuilder.CreateButton("OverrideBtn", window.transform, "Override вкл/выкл",
            () => GameDirector.ManualOverride = !GameDirector.ManualOverride,
            new Color(0.35f, 0.3f, 0.2f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10, 52), new Vector2(160, 88));

        UiBuilder.CreateButton("LeadBtn", window.transform, "Зацепка сейчас",
            () => GameDirector.ForceLead(),
            new Color(0.25f, 0.35f, 0.3f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(170, 52), new Vector2(320, 88));

        UiBuilder.CreateButton("GenMinus", window.transform, "Рынок -",
            () => GameDirector.NudgeKnobs(-0.25f, 0f), new Color(0.3f, 0.22f, 0.2f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10, 8), new Vector2(115, 44));
        UiBuilder.CreateButton("GenPlus", window.transform, "Рынок +",
            () => GameDirector.NudgeKnobs(0.25f, 0f), new Color(0.22f, 0.3f, 0.2f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(120, 8), new Vector2(225, 44));
        UiBuilder.CreateButton("MoodMinus", window.transform, "Покуп. -",
            () => GameDirector.NudgeKnobs(0f, -0.25f), new Color(0.3f, 0.22f, 0.2f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(230, 8), new Vector2(335, 44));
        UiBuilder.CreateButton("MoodPlus", window.transform, "Покуп. +",
            () => GameDirector.NudgeKnobs(0f, 0.25f), new Color(0.22f, 0.3f, 0.2f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(340, 8), new Vector2(445, 44));
    }

    private void Update()
    {
        refreshTimer -= Time.deltaTime;
        if (refreshTimer > 0f) return;
        refreshTimer = 0.25f;

        var log = GameDirector.DecisionLog;
        var observation = DirectorObservationBuilder.Build(24);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Индексы:  давление {GameDirector.Pressure:0.00}   скука {GameDirector.Boredom:0.00}   фрустрация {GameDirector.Frustration:0.00}");
        sb.AppendLine($"Ручки:    рынок {GameDirector.MarketGenerosity:+0.00;-0.00;0.00}   покупатель {GameDirector.BuyerMood:+0.00;-0.00;0.00}");
        sb.AppendLine($"Бюджет недели: {GameDirector.WeeklyBudgetUsed:0.00} / {GameDirector.Config.weeklyKnobBudget:0.##}   " +
                      $"зацепка: {(GameDirector.LeadPlanned ? "запланирована" : $"последняя в день {GameDirector.LastLeadDay}")}");
        sb.AppendLine($"Режим: {(GameDirector.ManualOverride ? "РУЧНОЙ (авторешения выключены)" : "автопилот")}");
        sb.AppendLine("--- решения ---");
        int from = Mathf.Max(0, log.Count - 8);
        for (int i = from; i < log.Count; i++)
            sb.AppendLine(log[i]);
        sb.AppendLine("--- observation ---");
        sb.AppendLine(observation.ToDebugString());
        sb.AppendLine($"lead {observation.RecentLeadsSeen}/{observation.RecentLeadsAcquired}/{observation.RecentLeadsResolved}   " +
                      $"market {observation.MarketStockCount} stock/{observation.MarketHumCount} hum");
        bodyText.text = sb.ToString();
    }
}
