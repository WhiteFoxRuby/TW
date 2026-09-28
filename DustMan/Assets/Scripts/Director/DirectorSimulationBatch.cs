using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public class DirectorSimulationBatchConfig
{
    public string BatchId = "";
    public int DaysPerRun = 14;
    public int FirstSeed = 20260710;
    public int SeedCount = 4;
    public bool WriteRunDatasets = true;
    public bool WriteSummary = true;
    public string OutputDirectory = "";
    public List<DirectorSimulationProfile> Profiles = new List<DirectorSimulationProfile>();
    public List<string> PolicyIds = new List<string>();
}

public class DirectorSimulationRunMetrics
{
    public string RunId = "";
    public string ProfileId = "";
    public string PolicyId = "";
    public int Seed;
    public int DaysRun;
    public int DecisionRows;
    public int StartingMoney;
    public int FinalMoney;
    public int TotalSpent;
    public int TotalEarned;
    public int PurchaseDays;
    public int RepairDays;
    public int SoldDays;
    public int SaleRefusalDays;
    public int IdleDays;
    public float AverageUnusedSlots;
    public float AverageItemQuality;
    public int ActionDiversity;
    public string MostUsedActionId = "";
    public int ValidationChanges;
    public int BudgetValidationChanges;
    public int LeadDeliveryDays;
    public bool EconomySurvived;
    public string DatasetPath = "";
}

public class DirectorSimulationAggregateMetrics
{
    public string ProfileId = "";
    public string PolicyId = "";
    public int Runs;
    public int Days;
    public int DecisionRows;
    public float SurvivalRate;
    public float AverageFinalMoney;
    public float AverageSpent;
    public float AverageEarned;
    public float AverageSoldDays;
    public float AverageSaleRefusalDays;
    public float AverageIdleDays;
    public float AverageUnusedSlots;
    public float AverageItemQuality;
    public float AverageActionDiversity;
    public float AverageValidationChanges;
    public float AverageBudgetValidationChanges;
    public float AverageLeadDeliveryDays;
}

public class DirectorSimulationBatchResult
{
    public string BatchId = "";
    public string OutputDirectory = "";
    public string RunsJsonlPath = "";
    public string SummaryJsonlPath = "";
    public string SummaryCsvPath = "";
    public int TotalRuns;
    public int TotalDays;
    public readonly List<DirectorSimulationRunMetrics> Runs = new List<DirectorSimulationRunMetrics>();
    public readonly List<DirectorSimulationAggregateMetrics> Aggregates = new List<DirectorSimulationAggregateMetrics>();
}

public static class DirectorSimulationBatch
{
    public static DirectorSimulationBatchResult Run(DirectorSimulationBatchConfig config)
    {
        bool outerEcho = DirectorTelemetry.EchoToDebug;
        bool outerSave = DirectorTelemetry.SaveJsonl;
        bool outerDecisionEcho = GameDirector.EchoDecisionsToDebug;
        DirectorTelemetry.EchoToDebug = false;
        DirectorTelemetry.SaveJsonl = false;
        GameDirector.EchoDecisionsToDebug = false;

        try
        {
            return RunInternal(config);
        }
        finally
        {
            DirectorTelemetry.EchoToDebug = outerEcho;
            DirectorTelemetry.SaveJsonl = outerSave;
            GameDirector.EchoDecisionsToDebug = outerDecisionEcho;
        }
    }

    private static DirectorSimulationBatchResult RunInternal(DirectorSimulationBatchConfig config)
    {
        config ??= new DirectorSimulationBatchConfig();
        Normalize(config);

        GameState.EnsureInit();

        var result = new DirectorSimulationBatchResult
        {
            BatchId = config.BatchId,
            OutputDirectory = ResolveOutputDirectory(config),
        };
        result.RunsJsonlPath = Path.Combine(result.OutputDirectory, "runs.jsonl");
        result.SummaryJsonlPath = Path.Combine(result.OutputDirectory, "summary.jsonl");
        result.SummaryCsvPath = Path.Combine(result.OutputDirectory, "summary.csv");

        if (config.WriteSummary || config.WriteRunDatasets)
            Directory.CreateDirectory(result.OutputDirectory);

        foreach (var profile in config.Profiles)
        {
            for (int seedOffset = 0; seedOffset < config.SeedCount; seedOffset++)
            {
                int seed = config.FirstSeed + seedOffset;
                foreach (string policyId in config.PolicyIds)
                {
                    var policy = CreatePolicy(policyId, seed);
                    string runId = SanitizeFileName(config.BatchId + "_" + profile.Id + "_" + policy.PolicyId + "_" + seed);
                    string datasetPath = config.WriteRunDatasets
                        ? Path.Combine(result.OutputDirectory, runId + ".jsonl")
                        : "";

                    var run = DirectorSimulation.Run(new DirectorSimulationConfig
                    {
                        RunId = runId,
                        Days = config.DaysPerRun,
                        Seed = seed,
                        Profile = profile,
                        Policy = policy,
                        WriteDataset = config.WriteRunDatasets,
                        DatasetPath = datasetPath,
                    });

                    var metrics = BuildRunMetrics(run, profile, seed);
                    result.Runs.Add(metrics);
                    result.TotalRuns++;
                    result.TotalDays += metrics.DaysRun;
                }
            }
        }

        BuildAggregates(result);

        if (config.WriteSummary)
            WriteSummaryFiles(result);

        return result;
    }

    public static DirectorSimulationBatchResult RunDefaultComparison()
    {
        return Run(new DirectorSimulationBatchConfig
        {
            BatchId = "manual_policy_compare",
            DaysPerRun = 14,
            FirstSeed = 20260710,
            SeedCount = 4,
            WriteRunDatasets = true,
            WriteSummary = true,
        });
    }

    private static void Normalize(DirectorSimulationBatchConfig config)
    {
        if (string.IsNullOrEmpty(config.BatchId))
            config.BatchId = "batch_" + config.FirstSeed.ToString(CultureInfo.InvariantCulture);
        if (config.DaysPerRun < 1)
            config.DaysPerRun = 1;
        if (config.SeedCount < 1)
            config.SeedCount = 1;
        if (config.Profiles == null)
            config.Profiles = new List<DirectorSimulationProfile>();
        if (config.Profiles.Count == 0)
        {
            config.Profiles.Add(DirectorSimulationProfile.Balanced());
            config.Profiles.Add(DirectorSimulationProfile.Struggling());
        }
        if (config.PolicyIds == null)
            config.PolicyIds = new List<string>();
        if (config.PolicyIds.Count == 0)
        {
            config.PolicyIds.Add("rule_based");
            config.PolicyIds.Add("random");
        }
    }

    private static IDirectorPolicy CreatePolicy(string policyId, int seed)
    {
        switch (policyId)
        {
            case "random":
                return new RandomDirectorPolicy(seed, GameDirector.Config);
            case "rule_based":
            default:
                return new RuleBasedDirectorPolicy(GameDirector.Config);
        }
    }

    private static DirectorSimulationRunMetrics BuildRunMetrics(DirectorSimulationResult run,
        DirectorSimulationProfile profile, int seed)
    {
        var metrics = new DirectorSimulationRunMetrics
        {
            RunId = run.RunId,
            ProfileId = run.ProfileId,
            PolicyId = run.PolicyId,
            Seed = seed,
            DaysRun = run.DaysRun,
            DecisionRows = run.DecisionRows,
            StartingMoney = profile.StartingMoney >= 0
                ? profile.StartingMoney
                : (GameState.Economy != null ? GameState.Economy.startingMoney : 100),
            FinalMoney = run.FinalMoney,
            DatasetPath = run.DatasetPath,
        };

        var actionCounts = new Dictionary<string, int>();
        int qualityCount = 0;
        int totalQuality = 0;
        int unusedSlots = 0;

        foreach (var day in run.Days)
        {
            metrics.TotalSpent += day.Spent;
            metrics.TotalEarned += day.Earned;
            unusedSlots += day.UnusedSlots;

            if (day.Bought) metrics.PurchaseDays++;
            if (day.Repaired) metrics.RepairDays++;
            if (day.Sold) metrics.SoldDays++;
            if (day.SaleRefused) metrics.SaleRefusalDays++;
            if (!day.Bought && !day.Repaired && !day.Sold && !day.SaleRefused)
                metrics.IdleDays++;
            if (day.ItemQuality > 0)
            {
                totalQuality += day.ItemQuality;
                qualityCount++;
            }

            string action = string.IsNullOrEmpty(day.ChosenActionId) ? "missing" : day.ChosenActionId;
            if (!actionCounts.ContainsKey(action))
                actionCounts[action] = 0;
            actionCounts[action]++;

            if (day.ValidationChanged)
                metrics.ValidationChanges++;
            if (day.ValidationChanged && (day.ChosenActionId.Contains("budget") || day.ValidationNotes.Contains("budget")))
                metrics.BudgetValidationChanges++;
            if (day.ChosenActionId.Contains("story_lead_humming_item"))
                metrics.LeadDeliveryDays++;
        }

        metrics.ActionDiversity = actionCounts.Count;
        metrics.MostUsedActionId = MostUsedAction(actionCounts);
        metrics.AverageUnusedSlots = run.DaysRun > 0 ? (float)unusedSlots / run.DaysRun : 0f;
        metrics.AverageItemQuality = qualityCount > 0 ? (float)totalQuality / qualityCount : 0f;
        metrics.EconomySurvived = metrics.FinalMoney > 0;
        return metrics;
    }

    private static string MostUsedAction(Dictionary<string, int> actionCounts)
    {
        string best = "";
        int bestCount = -1;
        foreach (var pair in actionCounts)
        {
            if (pair.Value > bestCount)
            {
                best = pair.Key;
                bestCount = pair.Value;
            }
        }
        return best;
    }

    private static void BuildAggregates(DirectorSimulationBatchResult result)
    {
        var groups = new Dictionary<string, List<DirectorSimulationRunMetrics>>();
        foreach (var run in result.Runs)
        {
            string key = run.ProfileId + "|" + run.PolicyId;
            if (!groups.ContainsKey(key))
                groups[key] = new List<DirectorSimulationRunMetrics>();
            groups[key].Add(run);
        }

        foreach (var pair in groups)
        {
            var runs = pair.Value;
            if (runs.Count == 0)
                continue;

            var aggregate = new DirectorSimulationAggregateMetrics
            {
                ProfileId = runs[0].ProfileId,
                PolicyId = runs[0].PolicyId,
                Runs = runs.Count,
            };

            int survived = 0;
            foreach (var run in runs)
            {
                aggregate.Days += run.DaysRun;
                aggregate.DecisionRows += run.DecisionRows;
                aggregate.AverageFinalMoney += run.FinalMoney;
                aggregate.AverageSpent += run.TotalSpent;
                aggregate.AverageEarned += run.TotalEarned;
                aggregate.AverageSoldDays += run.SoldDays;
                aggregate.AverageSaleRefusalDays += run.SaleRefusalDays;
                aggregate.AverageIdleDays += run.IdleDays;
                aggregate.AverageUnusedSlots += run.AverageUnusedSlots;
                aggregate.AverageItemQuality += run.AverageItemQuality;
                aggregate.AverageActionDiversity += run.ActionDiversity;
                aggregate.AverageValidationChanges += run.ValidationChanges;
                aggregate.AverageBudgetValidationChanges += run.BudgetValidationChanges;
                aggregate.AverageLeadDeliveryDays += run.LeadDeliveryDays;
                if (run.EconomySurvived)
                    survived++;
            }

            float n = runs.Count;
            aggregate.SurvivalRate = survived / n;
            aggregate.AverageFinalMoney /= n;
            aggregate.AverageSpent /= n;
            aggregate.AverageEarned /= n;
            aggregate.AverageSoldDays /= n;
            aggregate.AverageSaleRefusalDays /= n;
            aggregate.AverageIdleDays /= n;
            aggregate.AverageUnusedSlots /= n;
            aggregate.AverageItemQuality /= n;
            aggregate.AverageActionDiversity /= n;
            aggregate.AverageValidationChanges /= n;
            aggregate.AverageBudgetValidationChanges /= n;
            aggregate.AverageLeadDeliveryDays /= n;

            result.Aggregates.Add(aggregate);
        }
    }

    private static void WriteSummaryFiles(DirectorSimulationBatchResult result)
    {
        var runsJsonl = new StringBuilder();
        foreach (var run in result.Runs)
            runsJsonl.AppendLine(RunMetricsJson(run));
        File.WriteAllText(result.RunsJsonlPath, runsJsonl.ToString(), new UTF8Encoding(false));

        var summaryJsonl = new StringBuilder();
        foreach (var aggregate in result.Aggregates)
            summaryJsonl.AppendLine(AggregateJson(aggregate));
        File.WriteAllText(result.SummaryJsonlPath, summaryJsonl.ToString(), new UTF8Encoding(false));

        var csv = new StringBuilder();
        csv.AppendLine("profile_id,policy_id,runs,days,survival_rate,avg_final_money,avg_spent,avg_earned,avg_sold_days,avg_sale_refusal_days,avg_idle_days,avg_unused_slots,avg_item_quality,avg_action_diversity,avg_validation_changes,avg_budget_validation_changes,avg_lead_delivery_days");
        foreach (var aggregate in result.Aggregates)
            csv.AppendLine(AggregateCsv(aggregate));
        File.WriteAllText(result.SummaryCsvPath, csv.ToString(), new UTF8Encoding(false));
    }

    private static string RunMetricsJson(DirectorSimulationRunMetrics run)
    {
        return "{"
            + Str("run_id", run.RunId) + ","
            + Str("profile_id", run.ProfileId) + ","
            + Str("policy_id", run.PolicyId) + ","
            + Num("seed", run.Seed) + ","
            + Num("days_run", run.DaysRun) + ","
            + Num("decision_rows", run.DecisionRows) + ","
            + Num("starting_money", run.StartingMoney) + ","
            + Num("final_money", run.FinalMoney) + ","
            + Num("total_spent", run.TotalSpent) + ","
            + Num("total_earned", run.TotalEarned) + ","
            + Num("purchase_days", run.PurchaseDays) + ","
            + Num("repair_days", run.RepairDays) + ","
            + Num("sold_days", run.SoldDays) + ","
            + Num("sale_refusal_days", run.SaleRefusalDays) + ","
            + Num("idle_days", run.IdleDays) + ","
            + Num("average_unused_slots", run.AverageUnusedSlots) + ","
            + Num("average_item_quality", run.AverageItemQuality) + ","
            + Num("action_diversity", run.ActionDiversity) + ","
            + Str("most_used_action_id", run.MostUsedActionId) + ","
            + Num("validation_changes", run.ValidationChanges) + ","
            + Num("budget_validation_changes", run.BudgetValidationChanges) + ","
            + Num("lead_delivery_days", run.LeadDeliveryDays) + ","
            + Bool("economy_survived", run.EconomySurvived) + ","
            + Str("dataset_path", run.DatasetPath)
            + "}";
    }

    private static string AggregateJson(DirectorSimulationAggregateMetrics aggregate)
    {
        return "{"
            + Str("profile_id", aggregate.ProfileId) + ","
            + Str("policy_id", aggregate.PolicyId) + ","
            + Num("runs", aggregate.Runs) + ","
            + Num("days", aggregate.Days) + ","
            + Num("decision_rows", aggregate.DecisionRows) + ","
            + Num("survival_rate", aggregate.SurvivalRate) + ","
            + Num("average_final_money", aggregate.AverageFinalMoney) + ","
            + Num("average_spent", aggregate.AverageSpent) + ","
            + Num("average_earned", aggregate.AverageEarned) + ","
            + Num("average_sold_days", aggregate.AverageSoldDays) + ","
            + Num("average_sale_refusal_days", aggregate.AverageSaleRefusalDays) + ","
            + Num("average_idle_days", aggregate.AverageIdleDays) + ","
            + Num("average_unused_slots", aggregate.AverageUnusedSlots) + ","
            + Num("average_item_quality", aggregate.AverageItemQuality) + ","
            + Num("average_action_diversity", aggregate.AverageActionDiversity) + ","
            + Num("average_validation_changes", aggregate.AverageValidationChanges) + ","
            + Num("average_budget_validation_changes", aggregate.AverageBudgetValidationChanges) + ","
            + Num("average_lead_delivery_days", aggregate.AverageLeadDeliveryDays)
            + "}";
    }

    private static string AggregateCsv(DirectorSimulationAggregateMetrics aggregate)
    {
        return Csv(aggregate.ProfileId) + ","
            + Csv(aggregate.PolicyId) + ","
            + aggregate.Runs.ToString(CultureInfo.InvariantCulture) + ","
            + aggregate.Days.ToString(CultureInfo.InvariantCulture) + ","
            + F(aggregate.SurvivalRate) + ","
            + F(aggregate.AverageFinalMoney) + ","
            + F(aggregate.AverageSpent) + ","
            + F(aggregate.AverageEarned) + ","
            + F(aggregate.AverageSoldDays) + ","
            + F(aggregate.AverageSaleRefusalDays) + ","
            + F(aggregate.AverageIdleDays) + ","
            + F(aggregate.AverageUnusedSlots) + ","
            + F(aggregate.AverageItemQuality) + ","
            + F(aggregate.AverageActionDiversity) + ","
            + F(aggregate.AverageValidationChanges) + ","
            + F(aggregate.AverageBudgetValidationChanges) + ","
            + F(aggregate.AverageLeadDeliveryDays);
    }

    private static string ResolveOutputDirectory(DirectorSimulationBatchConfig config)
    {
        if (!string.IsNullOrEmpty(config.OutputDirectory))
            return config.OutputDirectory;

        string root = "";
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(Application.dataPath))
        {
            var assetsDir = Directory.GetParent(Application.dataPath);
            if (assetsDir != null)
                root = Path.Combine(assetsDir.FullName, "Telemetry", "simulations", "batches");
        }
#endif
        if (string.IsNullOrEmpty(root))
            root = Path.Combine(Application.persistentDataPath, "Telemetry", "simulations", "batches");

        return Path.Combine(root, SanitizeFileName(config.BatchId));
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "batch";

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            bool bad = false;
            for (int i = 0; i < invalid.Length && !bad; i++)
                bad = c == invalid[i];
            sb.Append(bad ? '_' : c);
        }
        return sb.ToString();
    }

    private static string Str(string key, string value)
    {
        return $"\"{Escape(key)}\":\"{Escape(value)}\"";
    }

    private static string Num(string key, int value)
    {
        return $"\"{Escape(key)}\":{value.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string Num(string key, float value)
    {
        return $"\"{Escape(key)}\":{F(value)}";
    }

    private static string Bool(string key, bool value)
    {
        return $"\"{Escape(key)}\":{(value ? "true" : "false")}";
    }

    private static string F(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";
        return "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
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
