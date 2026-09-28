using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public class DirectorSimulationProfile
{
    public string Id = "balanced";
    public int StartingMoney = -1;
    public float BuyChance = 0.9f;
    public float RepairSkill = 0.85f;
    public float MistakeChance = 0.1f;
    public float SalePriceRatio = 0.95f;
    public bool PreferHum = true;
    public bool PreferCheap = true;

    public static DirectorSimulationProfile Balanced()
    {
        return new DirectorSimulationProfile();
    }

    public static DirectorSimulationProfile Struggling()
    {
        return new DirectorSimulationProfile
        {
            Id = "struggling",
            StartingMoney = 55,
            BuyChance = 0.75f,
            RepairSkill = 0.55f,
            MistakeChance = 0.35f,
            SalePriceRatio = 1.2f,
            PreferHum = false,
            PreferCheap = true,
        };
    }
}

public class DirectorSimulationConfig
{
    public string RunId = "";
    public int Days = 14;
    public int Seed = 1234;
    public DirectorSimulationProfile Profile = DirectorSimulationProfile.Balanced();
    public IDirectorPolicy Policy;
    public bool WriteDataset = true;
    public string DatasetPath = "";
}

public class DirectorSimulationDayResult
{
    public int Day;
    public int MoneyBefore;
    public int MoneyAfter;
    public int Spent;
    public int Earned;
    public int UnusedSlots;
    public bool Bought;
    public bool Repaired;
    public bool Sold;
    public bool SaleRefused;
    public int ItemQuality;
    public string ObservationHash = "";
    public string AllowedActionIds = "";
    public string BlockedActionIds = "";
    public string ChosenActionId = "";
    public bool ValidationChanged;
    public string ValidationNotes = "";
}

public class DirectorSimulationResult
{
    public string RunId = "";
    public string ProfileId = "";
    public string PolicyId = "";
    public string DatasetPath = "";
    public int DaysRun;
    public int DecisionRows;
    public int FinalMoney;
    public readonly List<DirectorSimulationDayResult> Days = new List<DirectorSimulationDayResult>();
}

public static class DirectorSimulation
{
    private class GameStateSnapshot
    {
        public int Money;
        public int Day;
        public int SlotsLeft;
        public int SpentToday;
        public int EarnedToday;
        public ItemInstance CurrentItem;
        public readonly List<MarketOffer> MarketStock = new List<MarketOffer>();
    }

    public static DirectorSimulationResult Run(DirectorSimulationConfig config)
    {
        config ??= new DirectorSimulationConfig();
        config.Profile ??= DirectorSimulationProfile.Balanced();
        if (config.Days < 1) config.Days = 1;
        if (string.IsNullOrEmpty(config.RunId))
            config.RunId = BuildRunId(config);

        bool outerEcho = DirectorTelemetry.EchoToDebug;
        bool outerSave = DirectorTelemetry.SaveJsonl;
        bool outerDecisionEcho = GameDirector.EchoDecisionsToDebug;
        DirectorTelemetry.EchoToDebug = false;
        DirectorTelemetry.SaveJsonl = false;
        GameDirector.EchoDecisionsToDebug = false;

        GameState.EnsureInit();

        var gameSnapshot = CaptureGameState();
        var directorSnapshot = GameDirector.CaptureRuntimeState();
        var telemetrySnapshot = DirectorTelemetry.CaptureRuntimeState();
        IDirectorPolicy oldPolicy = GameDirector.Policy;

        var rng = new System.Random(config.Seed);
        var result = new DirectorSimulationResult
        {
            RunId = config.RunId,
            ProfileId = config.Profile.Id,
            PolicyId = (config.Policy ?? oldPolicy).PolicyId,
            DatasetPath = config.WriteDataset ? ResolveDatasetPath(config) : "",
        };
        var dataset = new StringBuilder();

        try
        {
            ResetWorld(config.Profile);
            GameDirector.SetPolicy(config.Policy ?? new RuleBasedDirectorPolicy(GameDirector.Config));

            DirectorTelemetry.ResetSession();

            for (int day = 1; day <= config.Days; day++)
            {
                BeginDay(day);
                int moneyBefore = GameState.Money;

                GameDirector.OnDayStart(day);
                RestockMarket(rng);

                var dayResult = SimulatePlayerDay(config.Profile, rng);
                dayResult.Day = day;
                dayResult.MoneyBefore = moneyBefore;
                FillDecisionFields(dayResult);

                var end = GameState.EndDay();
                dayResult.MoneyAfter = end.MoneyAfter;
                dayResult.Spent = end.Spent;
                dayResult.Earned = end.Earned;
                dayResult.UnusedSlots = GameState.SlotsLeft;

                result.Days.Add(dayResult);
                result.DecisionRows++;
                if (config.WriteDataset)
                    dataset.AppendLine(ToDatasetJson(config, result, dayResult));
            }

            result.DaysRun = result.Days.Count;
            result.FinalMoney = GameState.Money;

            if (config.WriteDataset)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(result.DatasetPath));
                File.WriteAllText(result.DatasetPath, dataset.ToString(), new UTF8Encoding(false));
            }
        }
        finally
        {
            RestoreGameState(gameSnapshot);
            GameDirector.RestoreRuntimeState(directorSnapshot);
            DirectorTelemetry.RestoreRuntimeState(telemetrySnapshot);
            DirectorTelemetry.EchoToDebug = outerEcho;
            DirectorTelemetry.SaveJsonl = outerSave;
            GameDirector.EchoDecisionsToDebug = outerDecisionEcho;
        }

        return result;
    }

    public static DirectorSimulationResult RunDefaultDataset()
    {
        return Run(new DirectorSimulationConfig
        {
            RunId = "manual_rule_based",
            Days = 14,
            Seed = 20260710,
            Profile = DirectorSimulationProfile.Balanced(),
            Policy = new RuleBasedDirectorPolicy(GameDirector.Config),
            WriteDataset = true,
        });
    }

    private static GameStateSnapshot CaptureGameState()
    {
        var snapshot = new GameStateSnapshot
        {
            Money = GameState.Money,
            Day = GameState.Day,
            SlotsLeft = GameState.SlotsLeft,
            SpentToday = GameState.SpentToday,
            EarnedToday = GameState.EarnedToday,
            CurrentItem = GameState.CurrentItem,
        };
        snapshot.MarketStock.AddRange(GameState.MarketStock);
        return snapshot;
    }

    private static void RestoreGameState(GameStateSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        GameState.Money = snapshot.Money;
        GameState.Day = snapshot.Day;
        GameState.SlotsLeft = snapshot.SlotsLeft;
        GameState.SpentToday = snapshot.SpentToday;
        GameState.EarnedToday = snapshot.EarnedToday;
        GameState.CurrentItem = snapshot.CurrentItem;
        GameState.MarketStock.Clear();
        GameState.MarketStock.AddRange(snapshot.MarketStock);
    }

    private static void ResetWorld(DirectorSimulationProfile profile)
    {
        GameDirector.ResetState();
        GameState.Day = 1;
        GameState.SlotsLeft = GameState.Economy != null ? GameState.Economy.daySlots : 4;
        GameState.SpentToday = 0;
        GameState.EarnedToday = 0;
        GameState.Money = profile.StartingMoney >= 0
            ? profile.StartingMoney
            : (GameState.Economy != null ? GameState.Economy.startingMoney : 100);
        GameState.CurrentItem = null;
        GameState.MarketStock.Clear();
    }

    private static void BeginDay(int day)
    {
        GameState.Day = day;
        GameState.SlotsLeft = GameState.Economy != null ? GameState.Economy.daySlots : 4;
        GameState.SpentToday = 0;
        GameState.EarnedToday = 0;
        DirectorTelemetry.Log(DirectorEventType.DayStarted,
            DirectorTelemetry.Payload(DirectorTelemetry.Num("day", day)));
    }

    private static void RestockMarket(System.Random rng)
    {
        GameState.MarketStock.Clear();
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
            var item = ItemInstanceGenerator.Generate(GameState.LanternDefinition, rng, plan.DefectBias);
            if (i == leadIndex)
                item.Hum = true;

            GameState.MarketStock.Add(new MarketOffer
            {
                Item = item,
                Price = GameDirector.AdjustMarketPrice(ItemInstanceGenerator.MarketPrice(item, rng)),
            });
        }

        GameDirector.ReportMarketStock(GameState.MarketStock);
        DirectorTelemetry.Log(DirectorEventType.MarketRestocked,
            DirectorTelemetry.Payload(
                DirectorTelemetry.Num("stock_count", GameState.MarketStock.Count),
                DirectorTelemetry.Bool("lead_in_stock", leadIndex >= 0)));
    }

    private static DirectorSimulationDayResult SimulatePlayerDay(DirectorSimulationProfile profile, System.Random rng)
    {
        var result = new DirectorSimulationDayResult();
        OpenMarket();

        if (GameState.CurrentItem == null && rng.NextDouble() <= profile.BuyChance)
            result.Bought = TryBuy(profile);

        if (GameState.CurrentItem != null)
            result.Repaired = TryRepair(profile, rng, out result.ItemQuality);

        if (GameState.CurrentItem != null && GameState.CurrentItem.RepairCompleted)
        {
            bool refused;
            result.Sold = TrySell(profile, out refused);
            result.SaleRefused = refused;
        }

        return result;
    }

    private static void OpenMarket()
    {
        int humCount = 0;
        foreach (var offer in GameState.MarketStock)
            if (offer.Item != null && offer.Item.Hum)
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
    }

    private static bool TryBuy(DirectorSimulationProfile profile)
    {
        var offer = ChooseOffer(profile);
        if (offer == null || GameState.Money < offer.Price)
            return false;
        if (!GameState.TrySpendSlot(GameState.Economy.marketSlotCost, "simulation_market_purchase"))
            return false;

        GameState.Money -= offer.Price;
        GameState.SpentToday += offer.Price;
        GameState.CurrentItem = offer.Item;
        GameState.MarketStock.Remove(offer);
        GameDirector.ReportPurchase();

        DirectorTelemetry.Log(DirectorEventType.ItemBought,
            DirectorTelemetry.Payload(
                DirectorTelemetry.Str("item_id", offer.Item.Definition.id),
                DirectorTelemetry.Num("price", offer.Price),
                DirectorTelemetry.Bool("hum", offer.Item.Hum)),
            offer.Item);

        if (offer.Item.Hum)
            DirectorTelemetry.Log(DirectorEventType.LeadAcquired,
                DirectorTelemetry.Payload(DirectorTelemetry.Str("lead_type", "humming_item")),
                offer.Item);

        return true;
    }

    private static MarketOffer ChooseOffer(DirectorSimulationProfile profile)
    {
        MarketOffer best = null;
        foreach (var offer in GameState.MarketStock)
        {
            if (offer == null || offer.Item == null)
                continue;
            if (GameState.Money < offer.Price)
                continue;

            if (best == null)
            {
                best = offer;
                continue;
            }

            if (profile.PreferHum && offer.Item.Hum && !best.Item.Hum)
                best = offer;
            else if ((!profile.PreferHum || offer.Item.Hum == best.Item.Hum)
                     && profile.PreferCheap && offer.Price < best.Price)
                best = offer;
        }
        return best;
    }

    private static bool TryRepair(DirectorSimulationProfile profile, System.Random rng, out int quality)
    {
        quality = 0;
        var item = GameState.CurrentItem;
        if (item == null)
            return false;
        if (!GameState.TrySpendSlot(GameState.Economy.repairSlotCost, "simulation_repair"))
            return false;

        DirectorTelemetry.Log(DirectorEventType.RepairStarted,
            DirectorTelemetry.Payload(DirectorTelemetry.Str("source", "simulation")),
            item);

        foreach (var defect in item.AllDefects())
        {
            if (!defect.Revealed)
            {
                defect.Revealed = true;
                DirectorTelemetry.Log(DirectorEventType.DefectRevealed,
                    DirectorTelemetry.Payload(
                        DirectorTelemetry.Str("defect_id", defect.Definition.id),
                        DirectorTelemetry.Str("source", "simulation")),
                    item);
            }

            if (rng.NextDouble() <= profile.RepairSkill)
                defect.MarkFixed();
        }

        if (rng.NextDouble() < profile.MistakeChance)
        {
            item.ScratchCount++;
            GameDirector.ReportGestureError("simulation_mistake");
        }

        if (item.BlockingDefectsRemaining().Count > 0)
        {
            GameDirector.ReportCheckResult(false);
            DirectorTelemetry.Log(DirectorEventType.RepairCheckFailed,
                DirectorTelemetry.Payload(
                    DirectorTelemetry.Str("source", "simulation"),
                    DirectorTelemetry.Num("blocking_left", item.BlockingDefectsRemaining().Count)),
                item);
        }
        else
        {
            GameDirector.ReportCheckResult(true);
        }

        item.Quality = item.ComputeQuality(GameState.Economy);
        item.RepairCompleted = true;
        quality = item.Quality;
        GameDirector.ReportRepairCompleted(item.Quality);
        DirectorTelemetry.Log(DirectorEventType.RepairCompleted,
            DirectorTelemetry.Payload(
                DirectorTelemetry.Num("quality", item.Quality),
                DirectorTelemetry.Num("fixed_defects", item.FixedDefects),
                DirectorTelemetry.Num("total_defects", item.TotalDefects),
                DirectorTelemetry.Str("source", "simulation")),
            item);
        return true;
    }

    private static bool TrySell(DirectorSimulationProfile profile, out bool refused)
    {
        refused = false;
        var item = GameState.CurrentItem;
        if (item == null)
            return false;
        if (!GameState.TrySpendSlot(GameState.Economy.saleSlotCost, "simulation_sale_attempt"))
            return false;

        int fairPrice = Mathf.RoundToInt(item.FairPrice(GameState.Economy) * GameDirector.SaleFairMultiplier(item));
        int price = Mathf.Max(1, Mathf.RoundToInt(fairPrice * profile.SalePriceRatio));
        float ratio = fairPrice <= 0 ? 99f : (float)price / fairPrice;
        bool sold = ratio <= GameDirector.AdjustedAcceptRatio();

        DirectorTelemetry.Log(DirectorEventType.CounterOpened,
            DirectorTelemetry.Payload(
                DirectorTelemetry.Num("fair_price", fairPrice),
                DirectorTelemetry.Num("quality", item.Quality),
                DirectorTelemetry.Bool("hum", item.Hum)),
            item);
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
        }
        else
        {
            refused = true;
            DirectorTelemetry.Log(DirectorEventType.SaleRefused,
                DirectorTelemetry.Payload(
                    DirectorTelemetry.Num("price", price),
                    DirectorTelemetry.Num("fair_price", fairPrice)),
                item);
        }

        GameDirector.ReportSaleAttempt(sold);
        return sold;
    }

    private static void FillDecisionFields(DirectorSimulationDayResult day)
    {
        var observation = GameDirector.LastDecisionObservation;
        var action = GameDirector.LastDecisionAction;
        var validation = GameDirector.LastDecisionValidation;
        var mask = GameDirector.LastDecisionMask;

        day.ObservationHash = observation != null ? observation.ObservationHash : "";
        day.AllowedActionIds = mask.AllowedActionIdsText;
        day.BlockedActionIds = mask.BlockedActionIdsText;
        day.ChosenActionId = action != null ? action.ActionId : "";
        day.ValidationChanged = validation != null && validation.Changed;
        day.ValidationNotes = validation != null ? validation.MessageText : "";
    }

    private static string ToDatasetJson(DirectorSimulationConfig config,
        DirectorSimulationResult result, DirectorSimulationDayResult day)
    {
        var observation = GameDirector.LastDecisionObservation;
        var action = GameDirector.LastDecisionAction;

        return "{"
            + Str("run_id", result.RunId) + ","
            + Str("profile_id", result.ProfileId) + ","
            + Str("policy_id", result.PolicyId) + ","
            + Num("seed", config.Seed) + ","
            + Num("day", day.Day) + ","
            + Str("observation_hash", day.ObservationHash) + ","
            + Str("allowed_action_ids", day.AllowedActionIds) + ","
            + Str("blocked_action_ids", day.BlockedActionIds) + ","
            + Str("chosen_action_id", day.ChosenActionId) + ","
            + Num("pressure", observation != null ? observation.Pressure : 0f) + ","
            + Num("boredom", observation != null ? observation.Boredom : 0f) + ","
            + Num("frustration", observation != null ? observation.Frustration : 0f) + ","
            + Num("budget_cost", action != null ? action.BudgetCost : 0f) + ","
            + Num("risk_level", action != null ? action.RiskLevel : 0) + ","
            + Bool("validation_changed", day.ValidationChanged) + ","
            + Str("validation_notes", day.ValidationNotes) + ","
            + Num("money_before", day.MoneyBefore) + ","
            + Num("money_after", day.MoneyAfter) + ","
            + Num("spent", day.Spent) + ","
            + Num("earned", day.Earned) + ","
            + Num("unused_slots", day.UnusedSlots) + ","
            + Bool("bought", day.Bought) + ","
            + Bool("repaired", day.Repaired) + ","
            + Bool("sold", day.Sold) + ","
            + Bool("sale_refused", day.SaleRefused) + ","
            + Num("item_quality", day.ItemQuality)
            + "}";
    }

    private static string BuildRunId(DirectorSimulationConfig config)
    {
        string profile = config.Profile != null ? config.Profile.Id : "profile";
        string policy = config.Policy != null ? config.Policy.PolicyId : "policy";
        return policy + "_" + profile + "_" + config.Seed.ToString(CultureInfo.InvariantCulture);
    }

    private static string ResolveDatasetPath(DirectorSimulationConfig config)
    {
        if (!string.IsNullOrEmpty(config.DatasetPath))
            return config.DatasetPath;

        string root = "";
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(Application.dataPath))
        {
            var assetsDir = Directory.GetParent(Application.dataPath);
            if (assetsDir != null)
                root = Path.Combine(assetsDir.FullName, "Telemetry", "simulations");
        }
#endif
        if (string.IsNullOrEmpty(root))
            root = Path.Combine(Application.persistentDataPath, "Telemetry", "simulations");

        return Path.Combine(root, config.RunId + ".jsonl");
    }

    private static string Str(string key, string value)
    {
        return $"\"{Escape(key)}\":\"{Escape(value)}\"";
    }

    private static string Num(string key, int value)
    {
        return $"\"{Escape(key)}\":{value}";
    }

    private static string Num(string key, float value)
    {
        return $"\"{Escape(key)}\":{value.ToString("0.###", CultureInfo.InvariantCulture)}";
    }

    private static string Bool(string key, bool value)
    {
        return $"\"{Escape(key)}\":{(value ? "true" : "false")}";
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
