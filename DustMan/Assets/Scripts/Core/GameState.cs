using System.Collections.Generic;
using UnityEngine;

public class MarketOffer
{
    public ItemInstance Item;
    public int Price;
}

public class DayResult
{
    public int Day;
    public int Spent;
    public int Earned;
    public int RentPaid;
    public int MoneyAfter;
}

/// Состояние игры на день-цикл: деньги, слоты, текущий предмет, рынок.
/// Живёт между сценами; сохранений в срезе нет.
public static class GameState
{
    public static EconomyConfig Economy { get; private set; }
    public static GestureConfig Gestures { get; private set; }
    public static ItemDefinition LanternDefinition { get; private set; }

    public static int Money;
    public static int Day;
    public static int SlotsLeft;
    public static int SpentToday;
    public static int EarnedToday;

    /// Купленный фонарь (максимум один в срезе); null — руки пусты.
    public static ItemInstance CurrentItem;

    public static readonly List<MarketOffer> MarketStock = new List<MarketOffer>();

    private static bool initialized;
    private static readonly System.Random rng = new System.Random();

    public static System.Random Rng => rng;

    public static void EnsureInit()
    {
        if (initialized) return;
        initialized = true;

        Economy = Resources.Load<EconomyConfig>("EconomyConfig");
        if (Economy == null)
        {
            Debug.LogWarning("GameState: EconomyConfig не найден в Resources, использую дефолты из кода.");
            Economy = ScriptableObject.CreateInstance<EconomyConfig>();
        }

        Gestures = Resources.Load<GestureConfig>("GestureConfig");
        if (Gestures == null)
        {
            Debug.LogWarning("GameState: GestureConfig не найден в Resources, использую дефолты из кода.");
            Gestures = ScriptableObject.CreateInstance<GestureConfig>();
        }

        LanternDefinition = Resources.Load<ItemDefinition>("Lantern");
        if (LanternDefinition == null)
        {
            Debug.LogWarning("GameState: Lantern не найден в Resources, собираю фолбэк в коде.");
            LanternDefinition = FallbackData.BuildLantern();
        }

        Money = Economy.startingMoney;
        Day = 1;
        SlotsLeft = Economy.daySlots;
        SpentToday = 0;
        EarnedToday = 0;
        CurrentItem = null;

        DirectorTelemetry.ResetSession();
        DirectorTelemetry.Log(DirectorEventType.DayStarted,
            DirectorTelemetry.Payload(DirectorTelemetry.Num("day", Day)));

        GameDirector.EnsureInit();
        GameDirector.OnDayStart(Day);
        RestockMarket();
    }

    public static bool TrySpendSlot(int cost, string reason = "")
    {
        if (SlotsLeft < cost) return false;
        SlotsLeft -= cost;
        DirectorTelemetry.Log(DirectorEventType.SlotSpent,
            DirectorTelemetry.Payload(
                DirectorTelemetry.Str("reason", reason),
                DirectorTelemetry.Num("cost", cost)));
        return true;
    }

    public static void RestockMarket()
    {
        MarketStock.Clear();
        var plan = GameDirector.BuildMarketPlan(rng);
        DirectorTelemetry.Log(DirectorEventType.DirectorActionApplied,
            DirectorTelemetry.Payload(
                DirectorTelemetry.Num("stock_count", plan.StockCount),
                DirectorTelemetry.Num("defect_bias", plan.DefectBias),
                DirectorTelemetry.Num("price_discount", plan.PriceDiscount),
                DirectorTelemetry.Bool("include_lead", plan.IncludeLead)),
            directorActionId: "market_plan");

        int leadIndex = plan.IncludeLead ? rng.Next(plan.StockCount) : -1;
        for (int i = 0; i < plan.StockCount; i++)
        {
            var item = ItemInstanceGenerator.Generate(LanternDefinition, rng, plan.DefectBias);
            if (i == leadIndex)
                item.Hum = true;
            MarketStock.Add(new MarketOffer
            {
                Item = item,
                Price = GameDirector.AdjustMarketPrice(ItemInstanceGenerator.MarketPrice(item, rng)),
            });
        }
        GameDirector.ReportMarketStock(MarketStock);
        DirectorTelemetry.Log(DirectorEventType.MarketRestocked,
            DirectorTelemetry.Payload(
                DirectorTelemetry.Num("stock_count", MarketStock.Count),
                DirectorTelemetry.Bool("lead_in_stock", leadIndex >= 0)));
    }

    /// Закрывает день: применяет аренду, возвращает сводку. Новый день — StartNewDay().
    public static DayResult EndDay()
    {
        GameDirector.ReportDayEnd(SlotsLeft, SpentToday, EarnedToday);

        int rent = 0;
        if (Economy.rentEnabled && Day == Economy.rentDay)
        {
            rent = Economy.rentAmount;
            Money -= rent;
        }

        DirectorTelemetry.Log(DirectorEventType.DayEnded,
            DirectorTelemetry.Payload(
                DirectorTelemetry.Num("day", Day),
                DirectorTelemetry.Num("spent", SpentToday),
                DirectorTelemetry.Num("earned", EarnedToday),
                DirectorTelemetry.Num("rent", rent),
                DirectorTelemetry.Num("unused_slots", SlotsLeft),
                DirectorTelemetry.Num("money_after", Money)));

        return new DayResult
        {
            Day = Day,
            Spent = SpentToday,
            Earned = EarnedToday,
            RentPaid = rent,
            MoneyAfter = Money,
        };
    }

    public static void StartNewDay()
    {
        Day++;
        SlotsLeft = Economy.daySlots;
        SpentToday = 0;
        EarnedToday = 0;
        DirectorTelemetry.Log(DirectorEventType.DayStarted,
            DirectorTelemetry.Payload(DirectorTelemetry.Num("day", Day)));
        GameDirector.OnDayStart(Day); // решения режиссёра — утром, до завоза рынка
        RestockMarket();
    }
}

/// Фолбэк-данные на случай, если .asset-файлы не загрузились.
/// Значения зеркалят Assets/Data/Resources; источник истины — ассеты.
public static class FallbackData
{
    public static ItemDefinition BuildLantern()
    {
        var rag = Tool("rag", "Тряпка", GestureKind.Wipe, new Color(0.9f, 0.85f, 0.7f));
        var sandpaper = Tool("sandpaper", "Наждачка", GestureKind.Rock, new Color(0.85f, 0.6f, 0.35f));
        var pliers = Tool("pliers", "Плоскогубцы", GestureKind.Drag, new Color(0.5f, 0.5f, 0.55f));

        var dirty = Defect("dirty_body", "Грязный корпус", DefectVisibility.Immediate,
            rag, GestureKind.Wipe, "", false, 99, "корпус в грязи");
        var oxidized = Defect("oxidized_contacts", "Окисленные контакты", DefectVisibility.AfterDisassembly,
            sandpaper, GestureKind.Rock, "не включается", true, 2, "");
        var bent = Defect("bent_button", "Погнутая кнопка", DefectVisibility.Immediate,
            pliers, GestureKind.Drag, "кнопка не нажимается", true, 1, "кнопка перекошена");
        var cracked = Defect("cracked_lens", "Треснутая линза", DefectVisibility.Magnifier,
            null, GestureKind.Drag, "светит тускло", true, 3, "");
        var dead = Defect("dead_batteries", "Севшие батарейки", DefectVisibility.AfterDisassembly,
            null, GestureKind.Drag, "не включается", true, 2, "");

        var body = Module("body", "Корпус", new Color(0.55f, 0.58f, 0.62f),
            new Vector2(2.4f, 1.2f), new Vector2(-3.2f, 0f), dirty);
        var lens = Module("lens", "Линза", new Color(0.65f, 0.85f, 0.95f),
            new Vector2(0.9f, 0.9f), new Vector2(-0.8f, 0f), cracked);
        var lamp = Module("lamp", "Лампа и контакты", new Color(1f, 0.95f, 0.7f),
            new Vector2(0.5f, 0.7f), new Vector2(0.8f, 0f));
        var bay = Module("battery_bay", "Батарейный отсек", new Color(0.45f, 0.5f, 0.4f),
            new Vector2(1.6f, 1f), new Vector2(2.6f, 0f), oxidized, dead);
        var button = Module("button", "Кнопка", new Color(0.95f, 0.75f, 0.2f),
            new Vector2(0.5f, 0.3f), new Vector2(4.2f, 0f), bent);

        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        item.id = "lantern";
        item.displayName = "Карманный фонарь";
        item.modules = new List<ModuleDefinition> { body, lens, lamp, bay, button };
        item.baseRepairedPrice = 60;
        item.marketPriceMin = 15;
        item.marketPriceMax = 25;
        item.minDefects = 2;
        item.maxDefects = 3;
        return item;
    }

    private static ToolDefinition Tool(string id, string name, GestureKind gesture, Color color)
    {
        var t = ScriptableObject.CreateInstance<ToolDefinition>();
        t.id = id;
        t.displayName = name;
        t.gesture = gesture;
        t.color = color;
        return t;
    }

    private static DefectDefinition Defect(string id, string name, DefectVisibility vis,
        ToolDefinition tool, GestureKind gesture, string symptom, bool blocks, int priority, string note)
    {
        var d = ScriptableObject.CreateInstance<DefectDefinition>();
        d.id = id;
        d.displayName = name;
        d.visibility = vis;
        d.tool = tool;
        d.gesture = gesture;
        d.symptom = symptom;
        d.blocksFunction = blocks;
        d.symptomPriority = priority;
        d.marketNote = note;
        return d;
    }

    private static ModuleDefinition Module(string id, string name, Color color,
        Vector2 size, Vector2 explodedOffset, params DefectDefinition[] defects)
    {
        var m = ScriptableObject.CreateInstance<ModuleDefinition>();
        m.id = id;
        m.displayName = name;
        m.color = color;
        m.size = size;
        m.explodedOffset = explodedOffset;
        m.possibleDefects = new List<DefectDefinition>(defects);
        return m;
    }
}
