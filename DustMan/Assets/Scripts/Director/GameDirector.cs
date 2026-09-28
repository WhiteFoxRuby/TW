using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// План завоза рынка на день — ручка «состав лута рынка».
public class MarketPlan
{
    public int StockCount;
    /// -1..1: >0 склоняет генерацию к минимуму дефектов, <0 — к максимуму.
    public float DefectBias;
    /// Скидка/наценка в монетах (положительная = дешевле для игрока).
    public int PriceDiscount;
    /// Пометить один фонарь как «гудящий» (зацепка-плейсхолдер).
    public bool IncludeLead;
}

/// AI-режиссёр (design/ai-director.md, раздел MVP): следит за сигналами,
/// держит индексы давление/скука/фрустрация в коридоре и раз в день
/// подкручивает ручки — состав рынка, настроение покупателя, момент зацепки.
/// Тик — день (гистерезис); недельный бюджет ограничивает суммарное вмешательство.
public static class GameDirector
{
    private class DayRecord
    {
        public int Spent;
        public int Earned;
        public int UnusedSlots;
        public int GestureErrors;
        public int Repairs;
        public int Walkaways;
        public int SaleAttempts;

        public DayRecord Copy()
        {
            return new DayRecord
            {
                Spent = Spent,
                Earned = Earned,
                UnusedSlots = UnusedSlots,
                GestureErrors = GestureErrors,
                Repairs = Repairs,
                Walkaways = Walkaways,
                SaleAttempts = SaleAttempts,
            };
        }
    }

    private class DirectorRuntimeSnapshot
    {
        public IDirectorPolicy Policy;
        public float Pressure;
        public float Boredom;
        public float Frustration;
        public float MarketGenerosity;
        public float BuyerMood;
        public bool LeadPlanned;
        public int LastLeadDay;
        public bool ManualOverride;
        public bool EchoDecisionsToDebug;
        public float WeeklyBudgetUsed;
        public readonly List<string> DecisionLog = new List<string>();
        public DirectorObservation LastDecisionObservation;
        public DirectorActionMask LastDecisionMask;
        public DirectorAction LastDecisionAction;
        public DirectorValidationResult LastDecisionValidation;
        public readonly List<DayRecord> History = new List<DayRecord>();
        public DayRecord Today;
        public readonly Queue<string> RecentStockSignatures = new Queue<string>();
        public int CheckFailStreak;
        public int ItemPurchasedDay;
    }

    public static DirectorConfig Config { get; private set; }
    private static IDirectorPolicy policy;

    public static IDirectorPolicy Policy
    {
        get { return CurrentPolicy; }
    }

    // Индексы 0..1 (EMA-сглаженные).
    public static float Pressure { get; private set; }
    public static float Boredom { get; private set; }
    public static float Frustration { get; private set; }

    // Ручки -1..1.
    public static float MarketGenerosity { get; private set; }
    public static float BuyerMood { get; private set; }
    public static bool LeadPlanned { get; private set; }
    public static int LastLeadDay { get; private set; }

    /// Дебаг-панель взяла управление: авторешения не применяются.
    public static bool ManualOverride;
    public static bool EchoDecisionsToDebug = true;

    public static float WeeklyBudgetUsed { get; private set; }
    public static readonly List<string> DecisionLog = new List<string>();
    public static DirectorObservation LastDecisionObservation { get; private set; }
    public static DirectorActionMask LastDecisionMask { get; private set; }
    public static DirectorAction LastDecisionAction { get; private set; }
    public static DirectorValidationResult LastDecisionValidation { get; private set; }

    private static readonly List<DayRecord> history = new List<DayRecord>();
    private static DayRecord today = new DayRecord();
    private static readonly Queue<string> recentStockSignatures = new Queue<string>();
    private static int checkFailStreak;
    private static int itemPurchasedDay = -1;
    private static bool initialized;

    public static void EnsureInit()
    {
        if (initialized) return;
        initialized = true;

        Config = Resources.Load<DirectorConfig>("DirectorConfig");
        if (Config == null)
        {
            Debug.LogWarning("GameDirector: DirectorConfig не найден в Resources, использую дефолты из кода.");
            Config = ScriptableObject.CreateInstance<DirectorConfig>();
        }
        if (policy == null)
            policy = new RuleBasedDirectorPolicy(Config);
        ResetState();
    }

    /// Полный сброс (новая игра, тесты).
    public static void ResetState()
    {
        Pressure = Boredom = Frustration = 0f;
        MarketGenerosity = BuyerMood = 0f;
        LeadPlanned = false;
        LastLeadDay = 0;
        ManualOverride = false;
        WeeklyBudgetUsed = 0f;
        LastDecisionObservation = null;
        LastDecisionMask = default;
        LastDecisionAction = null;
        LastDecisionValidation = null;
        DecisionLog.Clear();
        history.Clear();
        today = new DayRecord();
        recentStockSignatures.Clear();
        checkFailStreak = 0;
        itemPurchasedDay = -1;
    }

    public static void SetPolicy(IDirectorPolicy newPolicy)
    {
        EnsureInit();
        policy = newPolicy ?? new RuleBasedDirectorPolicy(Config);
    }

    internal static object CaptureRuntimeState()
    {
        EnsureInit();
        var snapshot = new DirectorRuntimeSnapshot
        {
            Policy = policy,
            Pressure = Pressure,
            Boredom = Boredom,
            Frustration = Frustration,
            MarketGenerosity = MarketGenerosity,
            BuyerMood = BuyerMood,
            LeadPlanned = LeadPlanned,
            LastLeadDay = LastLeadDay,
            ManualOverride = ManualOverride,
            EchoDecisionsToDebug = EchoDecisionsToDebug,
            WeeklyBudgetUsed = WeeklyBudgetUsed,
            LastDecisionObservation = LastDecisionObservation,
            LastDecisionMask = CopyMask(LastDecisionMask),
            LastDecisionAction = LastDecisionAction != null ? LastDecisionAction.Copy() : null,
            LastDecisionValidation = LastDecisionValidation,
            Today = today.Copy(),
            CheckFailStreak = checkFailStreak,
            ItemPurchasedDay = itemPurchasedDay,
        };
        snapshot.DecisionLog.AddRange(DecisionLog);
        foreach (var record in history)
            snapshot.History.Add(record.Copy());
        foreach (var signature in recentStockSignatures)
            snapshot.RecentStockSignatures.Enqueue(signature);
        return snapshot;
    }

    internal static void RestoreRuntimeState(object snapshotObject)
    {
        EnsureInit();
        if (!(snapshotObject is DirectorRuntimeSnapshot snapshot))
            return;

        policy = snapshot.Policy ?? new RuleBasedDirectorPolicy(Config);
        Pressure = snapshot.Pressure;
        Boredom = snapshot.Boredom;
        Frustration = snapshot.Frustration;
        MarketGenerosity = snapshot.MarketGenerosity;
        BuyerMood = snapshot.BuyerMood;
        LeadPlanned = snapshot.LeadPlanned;
        LastLeadDay = snapshot.LastLeadDay;
        ManualOverride = snapshot.ManualOverride;
        EchoDecisionsToDebug = snapshot.EchoDecisionsToDebug;
        WeeklyBudgetUsed = snapshot.WeeklyBudgetUsed;
        LastDecisionObservation = snapshot.LastDecisionObservation;
        LastDecisionMask = CopyMask(snapshot.LastDecisionMask);
        LastDecisionAction = snapshot.LastDecisionAction != null ? snapshot.LastDecisionAction.Copy() : null;
        LastDecisionValidation = snapshot.LastDecisionValidation;

        DecisionLog.Clear();
        DecisionLog.AddRange(snapshot.DecisionLog);
        history.Clear();
        foreach (var record in snapshot.History)
            history.Add(record.Copy());
        today = snapshot.Today != null ? snapshot.Today.Copy() : new DayRecord();
        recentStockSignatures.Clear();
        foreach (var signature in snapshot.RecentStockSignatures)
            recentStockSignatures.Enqueue(signature);
        checkFailStreak = snapshot.CheckFailStreak;
        itemPurchasedDay = snapshot.ItemPurchasedDay;
    }

    private static DirectorActionMask CopyMask(DirectorActionMask source)
    {
        return new DirectorActionMask
        {
            CanAdjustMarket = source.CanAdjustMarket,
            CanAdjustBuyer = source.CanAdjustBuyer,
            CanPlanLead = source.CanPlanLead,
            CanUseWeeklyBudget = source.CanUseWeeklyBudget,
            AllowedActionIds = source.AllowedActionIds != null
                ? new List<string>(source.AllowedActionIds)
                : new List<string>(),
            BlockedActionIds = source.BlockedActionIds != null
                ? new List<string>(source.BlockedActionIds)
                : new List<string>(),
        };
    }

    // ------------------------------------------------------------- отчёты игры

    public static void ReportPurchase()
    {
        itemPurchasedDay = GameState.Day;
    }

    public static void ReportGestureError(string reason = "")
    {
        today.GestureErrors++;
        DirectorTelemetry.Log(DirectorEventType.GestureError,
            DirectorTelemetry.Payload(DirectorTelemetry.Str("reason", reason)));
    }

    public static void ReportCheckResult(bool passed)
    {
        checkFailStreak = passed ? 0 : checkFailStreak + 1;
    }

    public static void ReportRepairCompleted(int quality)
    {
        today.Repairs++;
        checkFailStreak = 0;
    }

    public static void ReportSaleAttempt(bool sold)
    {
        today.SaleAttempts++;
        if (!sold) today.Walkaways++;
        else itemPurchasedDay = -1;
    }

    public static void ReportMarketStock(List<MarketOffer> stock)
    {
        foreach (var offer in stock)
        {
            var ids = offer.Item.AllDefects().Select(d => d.Definition.id).OrderBy(s => s).ToList();
            recentStockSignatures.Enqueue(string.Join("+", ids));
        }
        int cap = Mathf.Max(4, Config.repetitionWindowRestocks * 3);
        while (recentStockSignatures.Count > cap)
            recentStockSignatures.Dequeue();
    }

    /// Конец дня: снимок дневных счётчиков в историю.
    public static void ReportDayEnd(int unusedSlots, int spent, int earned)
    {
        today.UnusedSlots = unusedSlots;
        today.Spent = spent;
        today.Earned = earned;
        history.Add(today);
        while (history.Count > Mathf.Max(1, Config.historyDays))
            history.RemoveAt(0);
        today = new DayRecord();
    }

    // ------------------------------------------------------------- тик дня

    /// Утро, до завоза рынка: пересчёт индексов и решения по ручкам.
    public static void OnDayStart(int day)
    {
        EnsureInit();

        // Недельный бюджет отклонения.
        if ((day - 1) % 7 == 0)
            WeeklyBudgetUsed = 0f;

        float rawPressure = ComputePressure();
        float rawBoredom = ComputeBoredom(day);
        float rawFrustration = ComputeFrustration();

        float a = Config.indexSmoothing;
        Pressure = Mathf.Lerp(Pressure, rawPressure, a);
        Boredom = Mathf.Lerp(Boredom, rawBoredom, a);
        Frustration = Mathf.Lerp(Frustration, rawFrustration, a);

        if (ManualOverride)
        {
            LogDecision(day, "ручное управление — авторешения пропущены",
                DirectorObservationBuilder.BuildForDay(day));
            return;
        }

        var observation = DirectorObservationBuilder.BuildForDay(day);
        var mask = DirectorActionLibrary.BuildMask(observation, Config, DirectorActionMask.Default);
        var proposedAction = CurrentPolicy.Decide(observation, mask);
        var validation = DirectorActionValidator.Validate(proposedAction, observation, mask, Config);
        var action = validation.Action;
        LastDecisionObservation = observation;
        LastDecisionMask = mask;
        LastDecisionAction = action != null ? action.Copy() : null;
        LastDecisionValidation = validation;
        ApplyPolicyAction(action);

        LogDecision(day, action != null ? action.ReasonText : "в коридоре, ручки не двигаю",
            observation, action, validation, mask);
    }

    /// Гистерезис: ручка двигается к цели не быстрее шага в день.
    private static float MoveKnob(float current, float target)
    {
        float step = Mathf.Clamp(target - current, -Config.knobMaxStepPerDay, Config.knobMaxStepPerDay);
        return Mathf.Clamp(current + step, -1f, 1f);
    }

    private static IDirectorPolicy CurrentPolicy
    {
        get
        {
            if (policy == null)
                policy = new RuleBasedDirectorPolicy(Config);
            return policy;
        }
    }

    private static void ApplyPolicyAction(DirectorAction action)
    {
        if (action == null)
            return;

        MarketGenerosity = MoveKnob(MarketGenerosity, action.MarketGenerosityTarget);
        BuyerMood = MoveKnob(BuyerMood, action.BuyerMoodTarget);
        if (action.PlanLead)
            LeadPlanned = true;

        // Бюджет отклонения: платим за |отклонение| ручек в день, не за их движение.
        // Исчерпан — ручки распадаются к нейтрали (ни спасательного круга, ни удушения).
        float deviation = Mathf.Abs(MarketGenerosity) + Mathf.Abs(BuyerMood);
        if (deviation > 0f && WeeklyBudgetUsed + deviation > Config.weeklyKnobBudget)
        {
            MarketGenerosity = Mathf.MoveTowards(MarketGenerosity, 0f, Config.knobMaxStepPerDay);
            BuyerMood = Mathf.MoveTowards(BuyerMood, 0f, Config.knobMaxStepPerDay);
            deviation = Mathf.Abs(MarketGenerosity) + Mathf.Abs(BuyerMood);
            action.AddTag("budget_clamp");
            action.AddReason("бюджет недели исчерпан → ручки к нейтрали");
        }
        WeeklyBudgetUsed += deviation;
    }

    // ------------------------------------------------------------- индексы

    private static float ComputePressure()
    {
        var c = Config;

        // Runway: на сколько дней хватит денег при среднем дневном минусе.
        float avgBurn = history.Count == 0 ? 0f : (float)history.Average(d => d.Spent - d.Earned);
        float runway = avgBurn <= 0.01f ? 99f : GameState.Money / avgBurn;
        float runwayFactor = Mathf.Clamp01(1f - runway / Mathf.Max(0.1f, c.runwayComfortDays));

        float lowMoney = GameState.Money < c.lowMoneyThreshold ? 1f : 0f;

        float frozen = 0f;
        if (GameState.CurrentItem != null && itemPurchasedDay > 0)
        {
            int held = GameState.Day - itemPurchasedDay;
            frozen = Mathf.Clamp01((float)(held - c.frozenDaysComfort) / Mathf.Max(1, c.frozenDaysComfort));
        }

        return Mathf.Clamp01(runwayFactor * c.pressureWeightRunway
                           + lowMoney * c.pressureWeightLowMoney
                           + frozen * c.pressureWeightFrozen);
    }

    private static float ComputeBoredom(int day)
    {
        var c = Config;

        float repetition = 0f;
        if (recentStockSignatures.Count >= 4)
        {
            int distinct = recentStockSignatures.Distinct().Count();
            repetition = Mathf.Clamp01(1f - (float)distinct / recentStockSignatures.Count);
        }

        float leadOverdue = Mathf.Clamp01(
            (float)(day - LastLeadDay - c.leadCooldownDays) /
            Mathf.Max(1, c.leadOverdueDays - c.leadCooldownDays));

        float idle = history.Count == 0 ? 0f
            : Mathf.Clamp01((float)history.Average(d => d.UnusedSlots) /
                            Mathf.Max(1, GameState.Economy != null ? GameState.Economy.daySlots : 4));

        return Mathf.Clamp01(repetition * c.boredomWeightRepetition
                           + leadOverdue * c.boredomWeightLeadOverdue
                           + idle * c.boredomWeightIdleSlots);
    }

    private static float ComputeFrustration()
    {
        var c = Config;

        float errors = 0f;
        if (history.Count > 0)
        {
            float perRepair = (float)history.Sum(d => d.GestureErrors) /
                              Mathf.Max(1, history.Sum(d => d.Repairs));
            errors = Mathf.Clamp01(perRepair / Mathf.Max(0.5f, c.errorsPerRepairComfort) - 1f);
        }

        float fails = Mathf.Clamp01((float)(checkFailStreak - c.checkFailStreakComfort) /
                                    Mathf.Max(1, c.checkFailStreakComfort));

        float walkaways = 0f;
        int attempts = history.Sum(d => d.SaleAttempts);
        if (attempts > 0)
            walkaways = Mathf.Clamp01((float)history.Sum(d => d.Walkaways) / attempts);

        return Mathf.Clamp01(errors * c.frustrationWeightErrors
                           + fails * c.frustrationWeightCheckFails
                           + walkaways * c.frustrationWeightWalkaways);
    }

    // ------------------------------------------------------------- выходы

    /// План завоза рынка; вызывается GameState.RestockMarket.
    public static MarketPlan BuildMarketPlan(System.Random rng)
    {
        EnsureInit();
        var e = GameState.Economy;
        int baseCount = e != null ? rng.Next(e.marketStockMin, e.marketStockMax + 1) : 2;
        int bonus = Mathf.RoundToInt(MarketGenerosity * Config.stockBonusAtFullGenerosity);

        var plan = new MarketPlan
        {
            StockCount = Mathf.Clamp(baseCount + bonus, Config.marketMinStock, MaxMarketStock),
            DefectBias = MarketGenerosity * Config.defectBiasAtFullGenerosity,
            PriceDiscount = Mathf.RoundToInt(MarketGenerosity * Config.priceDiscountAtFullGenerosity),
            IncludeLead = LeadPlanned,
        };

        if (LeadPlanned)
        {
            LeadPlanned = false;
            LastLeadDay = GameState.Day;
        }
        return plan;
    }

    /// Цена фонаря с учётом щедрости рынка и пола цены (кламп).
    public static int AdjustMarketPrice(int basePrice)
    {
        EnsureInit();
        return Mathf.Max(Config.marketPriceFloor, basePrice - Mathf.RoundToInt(
            MarketGenerosity * Config.priceDiscountAtFullGenerosity));
    }

    public static float AdjustedHappyRatio()
    {
        EnsureInit();
        float baseRatio = GameState.Economy != null ? GameState.Economy.happyPriceRatio : 0.9f;
        return baseRatio + BuyerMood * Config.buyerMoodRatioDelta;
    }

    public static float AdjustedAcceptRatio()
    {
        EnsureInit();
        float baseRatio = GameState.Economy != null ? GameState.Economy.acceptPriceRatio : 1.15f;
        return baseRatio + BuyerMood * Config.buyerMoodRatioDelta;
    }

    /// Верхний предел стока: щедрость не раздувает рынок сверх вместимости UI.
    public static int MaxMarketStock =>
        (GameState.Economy != null ? GameState.Economy.marketStockMax : 3) +
        Mathf.Max(0, Config != null ? Config.stockBonusAtFullGenerosity : 1);

    /// Гудящий предмет, отремонтированный тщательно, продаётся дороже.
    public static float SaleFairMultiplier(ItemInstance item)
    {
        EnsureInit();
        if (item != null && item.Hum && item.Quality >= Config.humQualityThreshold)
            return 1f + Config.humPriceBonus;
        return 1f;
    }

    // ------------------------------------------------------------- отладка

    /// Ручной сдвиг ручек с дебаг-панели (бюджет недели не тратит).
    public static void NudgeKnobs(float generosityDelta, float moodDelta)
    {
        MarketGenerosity = Mathf.Clamp(MarketGenerosity + generosityDelta, -1f, 1f);
        BuyerMood = Mathf.Clamp(BuyerMood + moodDelta, -1f, 1f);
    }

    /// Принудительная зацепка с дебаг-панели: сработает на ближайшем завозе.
    public static void ForceLead()
    {
        LeadPlanned = true;
    }

    // ------------------------------------------------------------- телеметрия

    private static void LogDecision(int day, string text, DirectorObservation observation = null,
        DirectorAction action = null, DirectorValidationResult validation = null,
        DirectorActionMask mask = default)
    {
        if (observation == null)
            observation = DirectorObservationBuilder.BuildForDay(day);

        string policyId = action != null ? action.PolicyId : "manual_override";
        string actionId = action != null ? action.ActionId : "manual_override";
        bool validationChanged = validation != null && validation.Changed;
        string validationNotes = validation != null ? validation.MessageText : "";
        string line = $"день {day}: Д {Pressure:0.00} / С {Boredom:0.00} / Ф {Frustration:0.00} | " +
                      $"рынок {MarketGenerosity:+0.00;-0.00;0} покупатель {BuyerMood:+0.00;-0.00;0} " +
                      $"бюджет {WeeklyBudgetUsed:0.00}/{Config.weeklyKnobBudget:0.##} — {text}";
        DecisionLog.Add(line);
        while (DecisionLog.Count > Mathf.Max(4, Config.decisionLogSize))
            DecisionLog.RemoveAt(0);
        DirectorTelemetry.Log(DirectorEventType.DirectorDecision,
            DirectorTelemetry.Payload(
                DirectorTelemetry.Num("day", day),
                DirectorTelemetry.Num("pressure", Pressure),
                DirectorTelemetry.Num("boredom", Boredom),
                DirectorTelemetry.Num("frustration", Frustration),
                DirectorTelemetry.Num("market_generosity", MarketGenerosity),
                DirectorTelemetry.Num("buyer_mood", BuyerMood),
                DirectorTelemetry.Num("weekly_budget_used", WeeklyBudgetUsed),
                DirectorTelemetry.Str("policy_id", policyId),
                DirectorTelemetry.Str("action_id", actionId),
                DirectorTelemetry.Num("action_budget_cost", action != null ? action.BudgetCost : 0f),
                DirectorTelemetry.Num("action_risk_level", action != null ? action.RiskLevel : 0),
                DirectorTelemetry.Str("action_level", action != null ? action.Level : ""),
                DirectorTelemetry.Str("target_index", action != null ? action.TargetIndex : ""),
                DirectorTelemetry.Str("action_payload", action != null ? action.Payload : ""),
                DirectorTelemetry.Str("allowed_action_ids", mask.AllowedActionIdsText),
                DirectorTelemetry.Str("blocked_action_ids", mask.BlockedActionIdsText),
                DirectorTelemetry.Bool("validation_changed", validationChanged),
                DirectorTelemetry.Str("validation_notes", validationNotes),
                DirectorTelemetry.Str("observation_hash", observation.ObservationHash),
                DirectorTelemetry.Num("recent_events", observation.RecentEventCount),
                DirectorTelemetry.Num("recent_gesture_errors", observation.RecentGestureErrors),
                DirectorTelemetry.Num("recent_check_fails", observation.RecentCheckFails),
                DirectorTelemetry.Num("days_since_last_lead", observation.DaysSinceLastLead),
                DirectorTelemetry.Str("reason", text)),
            directorActionId: actionId);
        if (EchoDecisionsToDebug)
            Debug.Log("[Режиссёр] " + line);
    }
}
