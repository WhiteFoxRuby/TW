using UnityEngine;
using UnityEngine.UI;

/// UI-оверлей рынка: 2-3 фонаря с видимым состоянием и ценой (mvp-scope §3.2).
public class MarketOverlay : MonoBehaviour
{
    private GameObject canvasRoot;
    private Text statusText;

    private void Awake()
    {
        HubController.PushOverlay();
        Build();
    }

    private void OnDestroy()
    {
        HubController.PopOverlay();
        if (canvasRoot != null)
            Destroy(canvasRoot);
    }

    private void Build()
    {
        var canvas = UiBuilder.CreateCanvas("MarketCanvas", 100);
        canvasRoot = canvas.gameObject;

        UiBuilder.CreatePanel("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.6f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var window = UiBuilder.CreatePanel("Window", canvas.transform, new Color(0.16f, 0.13f, 0.1f, 0.97f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-380, -260), new Vector2(380, 260));

        UiBuilder.CreateText("Title", window.transform, "Рынок хлама", 30,
            new Color(0.95f, 0.85f, 0.6f), TextAnchor.MiddleCenter,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -56), new Vector2(0, -8));

        if (GameState.MarketStock.Count == 0)
        {
            UiBuilder.CreateText("Empty", window.transform,
                "Сегодня на рынке пусто. Приходи завтра.", 22,
                Color.white, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.4f), new Vector2(1f, 0.7f), Vector2.zero, Vector2.zero);
        }

        int humCount = 0;
        foreach (var offer in GameState.MarketStock)
            if (offer.Item.Hum)
                humCount++;
        DirectorTelemetry.Log(DirectorEventType.MarketOpened,
            DirectorTelemetry.Payload(
                DirectorTelemetry.Num("stock_count", GameState.MarketStock.Count),
                DirectorTelemetry.Num("hum_count", humCount)));
        if (humCount > 0)
            DirectorTelemetry.Log(DirectorEventType.LeadSeen,
                DirectorTelemetry.Payload(
                    DirectorTelemetry.Str("lead_type", "humming_item"),
                    DirectorTelemetry.Num("count", humCount)));

        // Вёрстка рассчитана на 4 ряда: базовые 2-3 + бонус щедрости режиссёра.
        float rowTop = -70f;
        const float rowHeight = 80f;
        for (int i = 0; i < GameState.MarketStock.Count; i++)
        {
            var offer = GameState.MarketStock[i];
            var row = UiBuilder.CreatePanel($"Offer{i}", window.transform, new Color(0.24f, 0.2f, 0.16f, 1f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(16, rowTop - rowHeight), new Vector2(-16, rowTop - 6));

            UiBuilder.CreatePanel("Thumb", row.transform, new Color(0.55f, 0.58f, 0.62f, 1f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12, -24), new Vector2(60, 24));

            string hum = offer.Item.Hum ? " Странно: он едва слышно гудит..." : "";
            UiBuilder.CreateText("Desc", row.transform,
                $"Карманный фонарь — {offer.Price} монет\nСостояние: {offer.Item.VisibleConditionNote()}.{hum}",
                16, Color.white, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(76, 4), new Vector2(-150, -4));

            var captured = offer;
            UiBuilder.CreateButton("Buy", row.transform, "Купить",
                () => Buy(captured), new Color(0.3f, 0.45f, 0.25f, 1f),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-136, -20), new Vector2(-12, 20));

            rowTop -= rowHeight + 6f;
        }

        statusText = UiBuilder.CreateText("Status", window.transform,
            $"Монеты: {GameState.Money}. Покупка занимает {GameState.Economy.marketSlotCost} слот дня.",
            18, new Color(0.9f, 0.9f, 0.85f), TextAnchor.MiddleCenter,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16, 56), new Vector2(-16, 96));

        UiBuilder.CreateButton("Close", window.transform, "Закрыть",
            Close, new Color(0.4f, 0.25f, 0.2f, 1f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-90, 10), new Vector2(90, 48));
    }

    private void Buy(MarketOffer offer)
    {
        if (GameState.CurrentItem != null)
        {
            statusText.text = "В руках уже есть фонарь — сначала почини и продай его.";
            return;
        }
        if (GameState.Money < offer.Price)
        {
            statusText.text = "Не хватает монет.";
            return;
        }
        if (!GameState.TrySpendSlot(GameState.Economy.marketSlotCost, "market_purchase"))
        {
            statusText.text = "Слоты дня закончились — заверши день.";
            return;
        }

        GameState.Money -= offer.Price;
        GameState.SpentToday += offer.Price;
        GameState.CurrentItem = offer.Item;
        GameState.MarketStock.Remove(offer);
        GameDirector.ReportPurchase();
        DirectorTelemetry.Log(DirectorEventType.ItemBought,
            DirectorTelemetry.Payload(
                DirectorTelemetry.Str("item_id", offer.Item.Definition.id),
                DirectorTelemetry.Num("price", offer.Price),
                DirectorTelemetry.Str("visible_condition", offer.Item.VisibleConditionNote()),
                DirectorTelemetry.Bool("hum", offer.Item.Hum)),
            offer.Item);
        if (offer.Item.Hum)
            DirectorTelemetry.Log(DirectorEventType.LeadAcquired,
                DirectorTelemetry.Payload(DirectorTelemetry.Str("lead_type", "humming_item")),
                offer.Item);

        Debug.Log($"Рынок: куплен фонарь за {offer.Price}.\n{offer.Item.Describe()}");
        HubController.Instance.ShowMessage($"Куплен фонарь за {offer.Price} монет. Неси на верстак!");
        Close();
    }

    private void Close()
    {
        Destroy(this);
    }
}
