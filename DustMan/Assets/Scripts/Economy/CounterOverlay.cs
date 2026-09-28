using UnityEngine;
using UnityEngine.UI;

/// Прилавок: покупатель, шкала цены жестом, реакция лицом, сделка (mvp-scope §3.7).
public class CounterOverlay : MonoBehaviour
{
    private GameObject canvasRoot;
    private Image face;
    private Text reactionText;
    private Text priceText;
    private Slider priceSlider;
    private int fairPrice;

    private static readonly Color HappyColor = new Color(0.35f, 0.75f, 0.35f);
    private static readonly Color ThinkColor = new Color(0.85f, 0.75f, 0.3f);
    private static readonly Color RefuseColor = new Color(0.8f, 0.35f, 0.3f);

    private void Awake()
    {
        HubController.PushOverlay();
        // Гудящий предмет при тщательном ремонте ценится выше (ручка режиссёра).
        fairPrice = Mathf.RoundToInt(
            GameState.CurrentItem.FairPrice(GameState.Economy) *
            GameDirector.SaleFairMultiplier(GameState.CurrentItem));
        DirectorTelemetry.Log(DirectorEventType.CounterOpened,
            DirectorTelemetry.Payload(
                DirectorTelemetry.Num("fair_price", fairPrice),
                DirectorTelemetry.Num("quality", GameState.CurrentItem.Quality),
                DirectorTelemetry.Bool("hum", GameState.CurrentItem.Hum)),
            GameState.CurrentItem);
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
        var canvas = UiBuilder.CreateCanvas("CounterCanvas", 100);
        canvasRoot = canvas.gameObject;

        UiBuilder.CreatePanel("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.6f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var window = UiBuilder.CreatePanel("Window", canvas.transform, new Color(0.12f, 0.14f, 0.18f, 0.97f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-340, -240), new Vector2(340, 240));

        UiBuilder.CreateText("Title", window.transform, "Прилавок — покупатель", 28,
            new Color(0.8f, 0.9f, 1f), TextAnchor.MiddleCenter,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -52), new Vector2(0, -8));

        // «Лицо» покупателя — плейсхолдер: цветной квадрат + подпись эмоции.
        face = UiBuilder.CreatePanel("Face", window.transform, ThinkColor,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-55, -180), new Vector2(55, -70));
        reactionText = UiBuilder.CreateText("Reaction", window.transform, "", 22,
            Color.white, TextAnchor.MiddleCenter,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -220), new Vector2(0, -184));

        var item = GameState.CurrentItem;
        UiBuilder.CreateText("ItemInfo", window.transform,
            $"Карманный фонарь, качество {item.Quality}/100", 18,
            new Color(0.85f, 0.85f, 0.8f), TextAnchor.MiddleCenter,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -252), new Vector2(0, -222));

        priceSlider = UiBuilder.CreateSlider("PriceScale", window.transform,
            0, GameState.Economy.priceScaleMax,
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(40, -60), new Vector2(-40, -20));
        priceSlider.value = GameState.CurrentItem.Definition.baseRepairedPrice;
        priceSlider.onValueChanged.AddListener(_ => UpdateReaction());

        priceText = UiBuilder.CreateText("Price", window.transform, "", 24,
            new Color(0.95f, 0.9f, 0.7f), TextAnchor.MiddleCenter,
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0, -110), new Vector2(0, -70));

        UiBuilder.CreateButton("Offer", window.transform, "Предложить цену",
            Offer, new Color(0.3f, 0.45f, 0.25f, 1f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-260, 14), new Vector2(-20, 58));
        UiBuilder.CreateButton("Leave", window.transform, "Не продавать",
            () => Destroy(this), new Color(0.4f, 0.25f, 0.2f, 1f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(20, 14), new Vector2(260, 58));

        UpdateReaction();
    }

    private void UpdateReaction()
    {
        int price = Mathf.RoundToInt(priceSlider.value);
        priceText.text = $"Твоя цена: {price} монет";

        float ratio = fairPrice <= 0 ? 99f : (float)price / fairPrice;
        if (ratio <= GameDirector.AdjustedHappyRatio())
        {
            face.color = HappyColor;
            reactionText.text = "Доволен — отличная цена!";
        }
        else if (ratio <= GameDirector.AdjustedAcceptRatio())
        {
            face.color = ThinkColor;
            reactionText.text = "Прикидывает... вроде честно.";
        }
        else
        {
            face.color = RefuseColor;
            reactionText.text = "Хмурится — дороговато.";
        }
    }

    private void Offer()
    {
        if (!GameState.TrySpendSlot(GameState.Economy.saleSlotCost, "sale_attempt"))
        {
            reactionText.text = "Слоты дня закончились — заверши день.";
            return;
        }

        var item = GameState.CurrentItem;
        int price = Mathf.RoundToInt(priceSlider.value);
        float ratio = fairPrice <= 0 ? 99f : (float)price / fairPrice;
        bool sold = ratio <= GameDirector.AdjustedAcceptRatio();
        DirectorTelemetry.Log(DirectorEventType.SaleAttempted,
            DirectorTelemetry.Payload(
                DirectorTelemetry.Num("price", price),
                DirectorTelemetry.Num("fair_price", fairPrice),
                DirectorTelemetry.Num("ratio", ratio)),
            item);

        if (sold)
        {
            GameState.Money += price;
            GameState.EarnedToday += price;
            GameState.CurrentItem = null;
            DirectorTelemetry.Log(DirectorEventType.SaleCompleted,
                DirectorTelemetry.Payload(
                    DirectorTelemetry.Num("price", price),
                    DirectorTelemetry.Num("fair_price", fairPrice)),
                item);
            if (item.Hum)
                DirectorTelemetry.Log(DirectorEventType.LeadResolved,
                    DirectorTelemetry.Payload(
                        DirectorTelemetry.Str("lead_type", "humming_item"),
                        DirectorTelemetry.Str("resolution", "sold")),
                    item);
            Debug.Log($"Прилавок: продано за {price} (честная цена {fairPrice}).");
            HubController.Instance.ShowMessage($"Продано за {price} монет!");
        }
        else
        {
            DirectorTelemetry.Log(DirectorEventType.SaleRefused,
                DirectorTelemetry.Payload(
                    DirectorTelemetry.Num("price", price),
                    DirectorTelemetry.Num("fair_price", fairPrice)),
                item);
            Debug.Log($"Прилавок: покупатель ушёл, просили {price}, честная цена {fairPrice}.");
            HubController.Instance.ShowMessage("Покупатель ушёл — цена показалась завышенной.");
        }
        GameDirector.ReportSaleAttempt(sold);
        Destroy(this);
    }
}
