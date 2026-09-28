using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Хаб: HUD (день/слоты/деньги), сообщения, открытие рынка/прилавка,
/// переход на верстак, завершение дня.
public class HubController : MonoBehaviour
{
    public static HubController Instance { get; private set; }

    private static int overlayCount;
    public static bool OverlayOpen => overlayCount > 0;
    public static void PushOverlay() { overlayCount++; }
    public static void PopOverlay() { overlayCount = Mathf.Max(0, overlayCount - 1); }

    private Text hudText;
    private Text messageText;
    private float messageTimer;

    private void Awake()
    {
        Instance = this;
        overlayCount = 0; // сцену могли перезагрузить с открытым оверлеем
        GameState.EnsureInit();
        BuildHud();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void BuildHud()
    {
        var canvas = UiBuilder.CreateCanvas("HudCanvas", 10);

        UiBuilder.CreatePanel("HudBG", canvas.transform, new Color(0f, 0f, 0f, 0.55f),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10, -46), new Vector2(420, -10));
        hudText = UiBuilder.CreateText("HudText", canvas.transform, "", 20,
            Color.white, TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22, -46), new Vector2(420, -10));

        UiBuilder.CreateButton("EndDayButton", canvas.transform, "Закончить день",
            EndDayRequested, new Color(0.35f, 0.3f, 0.2f, 0.9f),
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-190, -46), new Vector2(-10, -10));

        messageText = UiBuilder.CreateText("Message", canvas.transform, "", 22,
            new Color(1f, 0.95f, 0.7f), TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-400, 30), new Vector2(400, 70));
    }

    private void Update()
    {
        var e = GameState.Economy;
        string item = GameState.CurrentItem == null
            ? "рук нет ничего"
            : GameState.CurrentItem.RepairCompleted
                ? $"фонарь починен (качество {GameState.CurrentItem.Quality})"
                : "фонарь ждёт ремонта";
        hudText.text = $"День {GameState.Day}   Слоты {GameState.SlotsLeft}/{e.daySlots}   " +
                       $"Монеты {GameState.Money}   В руках: {item}";

        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f)
                messageText.text = "";
        }

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            DirectorDebugPanel.Toggle();
    }

    public void ShowMessage(string msg)
    {
        messageText.text = msg;
        messageTimer = 3.5f;
    }

    public void OpenMarket()
    {
        if (OverlayOpen) return;
        gameObject.AddComponent<MarketOverlay>();
    }

    public void TryEnterWorkbench()
    {
        if (OverlayOpen) return;
        if (GameState.CurrentItem == null)
        {
            ShowMessage("Сначала купи фонарь на рынке.");
            return;
        }
        if (GameState.CurrentItem.RepairCompleted)
        {
            ShowMessage("Фонарь уже починен — продай его на прилавке.");
            return;
        }
        if (GameState.SlotsLeft < GameState.Economy.repairSlotCost)
        {
            ShowMessage("Слоты дня закончились — заверши день.");
            return;
        }

        var player = GameObject.FindWithTag("Player");
        if (player != null)
            SceneCarry.PlayerX = player.transform.position.x;
        SceneManager.LoadScene("Workbench");
    }

    public void OpenCounter()
    {
        if (OverlayOpen) return;
        if (GameState.CurrentItem == null)
        {
            ShowMessage("Продавать нечего — купи и почини фонарь.");
            return;
        }
        if (!GameState.CurrentItem.RepairCompleted)
        {
            ShowMessage("Фонарь ещё не починен — сначала верстак.");
            return;
        }
        if (GameState.SlotsLeft < GameState.Economy.saleSlotCost)
        {
            ShowMessage("Слоты дня закончились — заверши день.");
            return;
        }
        gameObject.AddComponent<CounterOverlay>();
    }

    public void EndDayRequested()
    {
        if (OverlayOpen) return;
        gameObject.AddComponent<DaySummaryOverlay>();
    }
}
