using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Проверки для батч-режима и меню: валидация данных и приёмка этапа 1
/// (20 генераций фонаря дают разные комбинации дефектов).
public static class DevChecks
{
    [MenuItem("DustMan/Run Dev Checks")]
    public static void RunFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("DevChecks: останови Play mode — проверки сбрасывают состояние режиссёра.");
            return;
        }
        int errors = Run();
        Debug.Log(errors == 0
            ? "DevChecks: все проверки пройдены."
            : $"DevChecks: ошибок — {errors}.");
    }

    /// Точка входа для Unity -batchmode -executeMethod DevChecks.RunAll.
    [MenuItem("DustMan/Run Director Simulation")]
    public static void RunDirectorSimulationFromMenu()
    {
        LogDirectorSimulation(RunDirectorSimulation("editor_rule_based", 14, 20260710));
    }

    public static void RunDirectorSimulationBatch()
    {
        var result = RunDirectorSimulation("editor_rule_based", 14, 20260710);
        LogDirectorSimulation(result);
        if (Application.isBatchMode)
            EditorApplication.Exit(result.DecisionRows == result.DaysRun && result.DaysRun == 14 ? 0 : 1);
    }

    [MenuItem("DustMan/Run Director Batch Simulation")]
    public static void RunDirectorBatchSimulationFromMenu()
    {
        LogDirectorBatchSimulation(RunDirectorBatchSimulation("editor_policy_compare", 14, 20260710, 4, true));
    }

    public static void RunDirectorBatchSimulationBatch()
    {
        var result = RunDirectorBatchSimulation("editor_policy_compare", 14, 20260710, 4, true);
        LogDirectorBatchSimulation(result);
        if (Application.isBatchMode)
            EditorApplication.Exit(result.TotalRuns > 0 && result.Aggregates.Count > 0 ? 0 : 1);
    }

    private static DirectorSimulationResult RunDirectorSimulation(string runId, int days, int seed)
    {
        var result = DirectorSimulation.Run(new DirectorSimulationConfig
        {
            RunId = runId,
            Days = days,
            Seed = seed,
            Profile = DirectorSimulationProfile.Balanced(),
            Policy = new RuleBasedDirectorPolicy(GameDirector.Config),
            WriteDataset = true,
        });
        return result;
    }

    private static void LogDirectorSimulation(DirectorSimulationResult result)
    {
        Debug.Log($"DirectorSimulation: {result.DecisionRows} rows, final money {result.FinalMoney}, dataset {result.DatasetPath}");
    }

    private static DirectorSimulationBatchResult RunDirectorBatchSimulation(string batchId,
        int daysPerRun, int firstSeed, int seedCount, bool writeRunDatasets)
    {
        return DirectorSimulationBatch.Run(new DirectorSimulationBatchConfig
        {
            BatchId = batchId,
            DaysPerRun = daysPerRun,
            FirstSeed = firstSeed,
            SeedCount = seedCount,
            WriteRunDatasets = writeRunDatasets,
            WriteSummary = true,
        });
    }

    private static void LogDirectorBatchSimulation(DirectorSimulationBatchResult result)
    {
        Debug.Log($"DirectorSimulationBatch: {result.TotalRuns} runs, {result.TotalDays} days, summary {result.SummaryCsvPath}");
    }

    public static void RunAll()
    {
        int errors = Run();
        if (Application.isBatchMode)
            EditorApplication.Exit(errors == 0 ? 0 : 1);
    }

    private static int Run()
    {
        int errors = 0;

        // --- данные загружаются из Resources ---
        var economy = Resources.Load<EconomyConfig>("EconomyConfig");
        var gestures = Resources.Load<GestureConfig>("GestureConfig");
        var lantern = Resources.Load<ItemDefinition>("Lantern");
        errors += Check(economy != null, "EconomyConfig загружается из Resources");
        errors += Check(gestures != null, "GestureConfig загружается из Resources");
        errors += Check(lantern != null, "Lantern (ItemDefinition) загружается из Resources");

        foreach (var toolId in new[] { "screwdriver", "pliers", "rag", "magnifier", "sandpaper" })
        {
            var tool = Resources.Load<ToolDefinition>("Tool_" + toolId);
            errors += Check(tool != null, $"Tool_{toolId} загружается");
            if (tool != null)
                errors += Check(tool.id == toolId, $"Tool_{toolId}: id совпадает с именем файла");
        }

        if (lantern == null)
            return errors + 1;

        // --- состав фонаря ---
        errors += Check(lantern.modules.Count == 5, "У фонаря 5 модулей");
        var defectPool = new List<DefectDefinition>();
        foreach (var module in lantern.modules)
        {
            errors += Check(module != null, "Модуль не null");
            if (module == null) continue;
            foreach (var defect in module.possibleDefects)
            {
                errors += Check(defect != null, $"Дефект в {module.id} не null");
                if (defect != null)
                    defectPool.Add(defect);
            }
        }
        errors += Check(defectPool.Count == 5, $"Пул дефектов = 5 (сейчас {defectPool.Count})");
        foreach (var defect in defectPool)
        {
            bool needsTool = defect.id == "dirty_body" || defect.id == "oxidized_contacts" || defect.id == "bent_button";
            if (needsTool)
                errors += Check(defect.tool != null, $"У дефекта {defect.id} назначен инструмент");
            else
                errors += Check(defect.tool == null, $"Дефект {defect.id} чинится заменой (tool пуст)");
        }

        // --- приёмка этапа 1: 20 генераций, разные комбинации ---
        var rng = new System.Random(12345);
        var signatures = new HashSet<string>();
        for (int i = 0; i < 20; i++)
        {
            var item = ItemInstanceGenerator.Generate(lantern, rng);
            int n = item.TotalDefects;
            errors += Check(n >= lantern.minDefects && n <= lantern.maxDefects,
                $"Генерация {i + 1}: дефектов {n} в границах [{lantern.minDefects}..{lantern.maxDefects}]");

            var ids = new List<string>();
            foreach (var d in item.AllDefects())
                ids.Add(d.Definition.id);
            var distinct = new HashSet<string>(ids);
            errors += Check(distinct.Count == ids.Count, $"Генерация {i + 1}: дефекты не повторяются");
            ids.Sort();
            signatures.Add(string.Join("+", ids));

            int price = ItemInstanceGenerator.MarketPrice(item, rng);
            errors += Check(price >= lantern.marketPriceMin && price <= lantern.marketPriceMax,
                $"Генерация {i + 1}: цена {price} в границах рынка");

            Debug.Log($"DevChecks генерация {i + 1}: {string.Join("+", ids)}, цена {price}");
        }
        errors += Check(signatures.Count > 1,
            $"20 генераций дают разные комбинации (уникальных: {signatures.Count})");

        // --- качество: идеальный ремонт = 100, нетронутый = низкое ---
        if (economy != null)
        {
            var perfect = ItemInstanceGenerator.Generate(lantern, rng);
            foreach (var d in perfect.AllDefects())
                d.MarkFixed();
            errors += Check(perfect.ComputeQuality(economy) == 100,
                $"Идеальный ремонт даёт качество 100 (сейчас {perfect.ComputeQuality(economy)})");

            var untouched = ItemInstanceGenerator.Generate(lantern, rng);
            errors += Check(untouched.ComputeQuality(economy) <= 60,
                "Нечиненый фонарь имеет низкое качество");

            // Экономика среза: купить дешевле — починить — продать дороже.
            perfect.Quality = 100;
            int sale = perfect.FairPrice(economy);
            errors += Check(sale > lantern.marketPriceMax,
                $"Продажа идеального ({sale}) даёт положительную маржу против покупки (максимум {lantern.marketPriceMax})");
        }

        errors += CheckDirector();

        return errors;
    }

    /// AI-режиссёр: неделя безденежья должна поднять давление и включить
    /// щедрый рынок, не нарушая клампы и недельный бюджет ручек.
    private static int CheckDirector()
    {
        int errors = 0;

        var cfg = Resources.Load<DirectorConfig>("DirectorConfig");
        errors += Check(cfg != null, "DirectorConfig загружается из Resources");
        if (cfg == null) return errors;

        int savedMoney = GameState.Money;
        GameDirector.EnsureInit();
        GameDirector.ResetState();
        GameState.Money = 10;

        float maxMarketGenerosity = 0f;
        for (int day = 1; day <= 7; day++)
        {
            GameDirector.OnDayStart(day);
            maxMarketGenerosity = Mathf.Max(maxMarketGenerosity, GameDirector.MarketGenerosity);
            GameDirector.ReportDayEnd(0, 30, 0); // тратим, не зарабатываем
        }

        errors += Check(GameDirector.Pressure > 0.5f,
            $"Давление растёт при пустом кошельке (сейчас {GameDirector.Pressure:0.00})");
        errors += Check(maxMarketGenerosity > 0f,
            $"Щедрость рынка включается при высоком давлении (максимум {maxMarketGenerosity:0.00})");
        errors += Check(GameDirector.Pressure <= 1f && GameDirector.Boredom <= 1f && GameDirector.Frustration <= 1f,
            "Индексы в границах 0..1");
        errors += Check(GameDirector.MarketGenerosity <= 1f && GameDirector.MarketGenerosity >= -1f,
            "Ручка рынка в границах -1..1");
        // Бюджет — интеграл |отклонений| по дням; перерасход ограничен одним днём.
        errors += Check(GameDirector.WeeklyBudgetUsed <= cfg.weeklyKnobBudget + 2f,
            $"Недельный бюджет отклонения соблюдён ({GameDirector.WeeklyBudgetUsed:0.00}/{cfg.weeklyKnobBudget})");

        var plan = GameDirector.BuildMarketPlan(new System.Random(7));
        errors += Check(plan.StockCount >= cfg.marketMinStock, "Кламп: рынок никогда не пустой");
        errors += Check(plan.StockCount <= GameDirector.MaxMarketStock,
            $"Кламп: рынок не больше вместимости UI ({plan.StockCount}/{GameDirector.MaxMarketStock})");
        errors += Check(plan.PriceDiscount <= cfg.priceDiscountAtFullGenerosity,
            "Скидка рынка не превышает предел из конфига");
        errors += Check(GameDirector.AdjustMarketPrice(1) >= cfg.marketPriceFloor,
            "Кламп: цена рынка не ниже пола");

        var observation = DirectorObservationBuilder.Build(16);
        errors += Check(!string.IsNullOrEmpty(observation.ObservationHash),
            "DirectorObservationBuilder собирает hash наблюдения");
        errors += Check(observation.RecentEventCount == observation.RecentEvents.Count,
            "DirectorObservationBuilder хранит окно событий без расхождения счётчика");
        errors += Check(observation.RecentEventCount <= 16,
            "DirectorObservationBuilder соблюдает размер окна событий");

        var policyAction = GameDirector.Policy.Decide(observation, DirectorActionMask.Default);
        errors += Check(GameDirector.Policy is RuleBasedDirectorPolicy,
            "GameDirector по умолчанию использует RuleBasedDirectorPolicy");
        errors += Check(policyAction != null && policyAction.PolicyId == "rule_based",
            "IDirectorPolicy возвращает решение с policy id");
        errors += Check(policyAction != null && !string.IsNullOrEmpty(policyAction.ActionId),
            "IDirectorPolicy возвращает решение с action id");

        var blockMask = DirectorActionLibrary.BuildMask(observation, cfg, DirectorActionMask.Default);
        errors += Check(DirectorActionLibrary.Blocks.Count >= 8,
            "DirectorActionLibrary содержит стартовую библиотеку блоков");
        errors += Check(DirectorActionLibrary.Find("market_generous") != null
                        && DirectorActionLibrary.Find("story_lead_humming_item") != null,
            "DirectorActionLibrary содержит применяемые MVP-блоки");
        errors += Check(blockMask.AllowedActionIds.Contains("hold"),
            "DirectorActionLibrary всегда оставляет hold в allowed action mask");
        errors += Check(blockMask.BlockedActionIds.Exists(id => id.StartsWith("familiar_item:")),
            "DirectorActionLibrary держит future-блоки вне allowed mask");

        var blockAction = DirectorActionLibrary.CreateActionFromBlock("test_policy",
            "market_generous", observation, blockMask);
        errors += Check(blockAction.ActionId == "market_generous"
                        && blockAction.BudgetCost > 0f
                        && blockAction.RiskLevel >= 1
                        && blockAction.TargetIndex.Contains("pressure"),
            "DirectorActionLibrary превращает блок в DirectorAction с metadata");

        var validation = DirectorActionValidator.Validate(policyAction, observation, DirectorActionMask.Default, cfg);
        errors += Check(validation.Action != null && validation.Accepted,
            "DirectorActionValidator принимает корректное rule-based решение");

        var blockedAction = DirectorAction.Create("test_policy");
        blockedAction.MarketGenerosityTarget = 8f;
        blockedAction.BuyerMoodTarget = -8f;
        blockedAction.PlanLead = true;
        blockedAction.AddTag("unsafe_test");
        var blockedMask = DirectorActionMask.Default;
        blockedMask.CanAdjustMarket = false;
        blockedMask.CanAdjustBuyer = false;
        blockedMask.CanPlanLead = false;
        var blockedValidation = DirectorActionValidator.Validate(blockedAction, observation, blockedMask, cfg);
        errors += Check(blockedValidation.Changed
                        && !blockedValidation.Action.PlanLead
                        && Mathf.Approximately(blockedValidation.Action.MarketGenerosityTarget, observation.MarketGenerosity)
                        && Mathf.Approximately(blockedValidation.Action.BuyerMoodTarget, observation.BuyerMood),
            "DirectorActionValidator режет запрещённые маской действия");

        var randomPolicy = new RandomDirectorPolicy(11, cfg);
        var randomAction = randomPolicy.Decide(observation, DirectorActionMask.Default);
        var randomValidation = DirectorActionValidator.Validate(randomAction, observation, DirectorActionMask.Default, cfg);
        errors += Check(randomAction != null
                        && randomAction.PolicyId == "random"
                        && randomValidation.Action != null,
            "RandomDirectorPolicy даёт валидируемое baseline-решение");

        var replayPolicy = new ReplayDirectorPolicy(new[] { policyAction });
        var replayAction = replayPolicy.Decide(observation, DirectorActionMask.Default);
        var replayEmpty = replayPolicy.Decide(observation, DirectorActionMask.Default);
        errors += Check(replayAction != null
                        && replayAction.PolicyId == "replay"
                        && replayPolicy.Remaining == 0
                        && replayEmpty.ActionId == "hold",
            "ReplayDirectorPolicy воспроизводит очередь и затем возвращает hold");

        GameDirector.SetPolicy(new RandomDirectorPolicy(5, cfg));
        errors += Check(GameDirector.Policy is RandomDirectorPolicy,
            "GameDirector умеет подменять policy");
        GameDirector.SetPolicy(null);
        errors += Check(GameDirector.Policy is RuleBasedDirectorPolicy,
            "GameDirector возвращается к RuleBasedDirectorPolicy через null");

        // Зацепка: принудительная — появляется в плане один раз.
        GameDirector.ForceLead();
        var leadPlan = GameDirector.BuildMarketPlan(new System.Random(8));
        errors += Check(leadPlan.IncludeLead, "Принудительная зацепка попадает в план завоза");
        var afterPlan = GameDirector.BuildMarketPlan(new System.Random(9));
        errors += Check(!afterPlan.IncludeLead, "Зацепка расходуется после завоза");

        // Кламп «не позже дня X»: без всякой скуки зацепка приходит к leadMaxDays.
        GameDirector.ResetState();
        GameState.Money = 500; // никакого давления и скуки
        bool leadByMinimum = false;
        for (int day = 1; day <= cfg.leadMaxDays && !leadByMinimum; day++)
        {
            GameDirector.OnDayStart(day);
            leadByMinimum = GameDirector.LeadPlanned;
            GameDirector.ReportDayEnd(0, 0, 0);
        }
        errors += Check(leadByMinimum,
            $"Сюжетный минимум: зацепка выдана не позже дня {cfg.leadMaxDays}");

        var simResult = DirectorSimulation.Run(new DirectorSimulationConfig
        {
            RunId = "devchecks_rule_based",
            Days = 4,
            Seed = 404,
            Profile = DirectorSimulationProfile.Balanced(),
            Policy = new RuleBasedDirectorPolicy(cfg),
            WriteDataset = true,
        });
        errors += Check(simResult.DecisionRows == 4 && simResult.DaysRun == 4,
            "DirectorSimulation writes one dataset row per simulated day");
        errors += Check(!string.IsNullOrEmpty(simResult.DatasetPath)
                        && System.IO.File.Exists(simResult.DatasetPath),
            "DirectorSimulation saves JSONL into Telemetry/simulations");
        errors += Check(simResult.Days.Count > 0
                        && !string.IsNullOrEmpty(simResult.Days[0].ObservationHash)
                        && !string.IsNullOrEmpty(simResult.Days[0].AllowedActionIds)
                        && !string.IsNullOrEmpty(simResult.Days[0].ChosenActionId),
            "DirectorSimulation links observation, allowed actions and chosen action");
        errors += Check(simResult.Days.Exists(d => d.Bought || d.Repaired || d.Sold || d.SaleRefused),
            "DirectorSimulation models player activity");

        var batchResult = DirectorSimulationBatch.Run(new DirectorSimulationBatchConfig
        {
            BatchId = "devchecks_policy_compare",
            DaysPerRun = 3,
            FirstSeed = 707,
            SeedCount = 1,
            WriteRunDatasets = false,
            WriteSummary = true,
        });
        errors += Check(batchResult.TotalRuns == 4 && batchResult.TotalDays == 12,
            "DirectorSimulationBatch runs default profile/policy matrix");
        errors += Check(batchResult.Aggregates.Count == 4,
            "DirectorSimulationBatch aggregates metrics by profile and policy");
        errors += Check(!string.IsNullOrEmpty(batchResult.SummaryCsvPath)
                        && System.IO.File.Exists(batchResult.SummaryCsvPath)
                        && System.IO.File.Exists(batchResult.RunsJsonlPath),
            "DirectorSimulationBatch saves run metrics and summary files");
        errors += Check(batchResult.Aggregates.Exists(a => a.ProfileId == "balanced" && a.PolicyId == "rule_based")
                        && batchResult.Aggregates.Exists(a => a.ProfileId == "struggling" && a.PolicyId == "random"),
            "DirectorSimulationBatch compares rule-based and random policies across profiles");

        GameDirector.ResetState();
        GameState.Money = savedMoney;
        return errors;
    }

    private static int Check(bool ok, string what)
    {
        if (!ok)
            Debug.LogError("DevChecks FAIL: " + what);
        return ok ? 0 : 1;
    }
}
