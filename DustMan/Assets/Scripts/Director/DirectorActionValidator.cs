using System.Collections.Generic;
using UnityEngine;

public class DirectorValidationResult
{
    public DirectorAction Action;
    public bool Accepted = true;
    public bool Changed;
    public readonly List<string> Messages = new List<string>();

    public string MessageText
    {
        get
        {
            return Messages.Count > 0 ? string.Join("; ", Messages) : "";
        }
    }

    public void MarkChanged(string message)
    {
        Changed = true;
        if (!string.IsNullOrEmpty(message))
            Messages.Add(message);
    }
}

public static class DirectorActionValidator
{
    public static DirectorValidationResult Validate(DirectorAction proposed,
        DirectorObservation observation, DirectorActionMask mask, DirectorConfig config)
    {
        var result = new DirectorValidationResult();
        var action = proposed != null ? proposed.Copy() : DirectorAction.Create("missing_policy");
        result.Action = action;

        if (proposed == null)
            result.MarkChanged("пустое действие заменено на hold");

        if (string.IsNullOrEmpty(action.PolicyId))
        {
            action.PolicyId = "unknown";
            result.MarkChanged("добавлен пустой policy id");
        }

        if (observation == null)
        {
            action.MarketGenerosityTarget = 0f;
            action.BuyerMoodTarget = 0f;
            action.PlanLead = false;
            action.ActionId = string.IsNullOrEmpty(action.ActionId) ? "hold" : action.ActionId;
            result.MarkChanged("нет observation, действие сведено к hold");
            AddValidationReason(result, action);
            return result;
        }

        ValidateActionIds(result, action, observation, mask, config);
        ValidateBudget(result, action, observation, mask, config);

        if (!mask.CanUseWeeklyBudget)
        {
            action.MarketGenerosityTarget = 0f;
            action.BuyerMoodTarget = 0f;
            action.AddTag("budget_masked");
            result.MarkChanged("недельный бюджет закрыт для policy");
        }
        else
        {
            ValidateMarketTarget(result, action, observation, mask);
            ValidateBuyerTarget(result, action, observation, mask);
        }

        ValidateLead(result, action, observation, mask, config);

        if (string.IsNullOrEmpty(action.ActionId))
        {
            action.ActionId = "hold";
            result.MarkChanged("добавлен пустой action id");
        }

        AddValidationReason(result, action);
        return result;
    }

    private static void ValidateActionIds(DirectorValidationResult result, DirectorAction action,
        DirectorObservation observation, DirectorActionMask mask, DirectorConfig config)
    {
        var ids = DirectorActionLibrary.SplitActionIds(action.ActionId);
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            if (id == "hold")
                continue;

            var block = DirectorActionLibrary.Find(id);
            if (block == null)
            {
                result.MarkChanged("неизвестный блок " + id);
                continue;
            }

            var reasons = new List<string>();
            if (!DirectorActionLibrary.IsBlockAllowed(block, observation, mask, config, reasons))
                result.MarkChanged("блок " + id + " недоступен: " + string.Join("/", reasons));
        }
    }

    private static void ValidateBudget(DirectorValidationResult result, DirectorAction action,
        DirectorObservation observation, DirectorActionMask mask, DirectorConfig config)
    {
        if (action.BudgetCost <= 0f)
            return;

        if (!mask.CanUseWeeklyBudget
            || (config != null && observation.WeeklyBudgetUsed + action.BudgetCost > config.weeklyKnobBudget))
        {
            action.MarketGenerosityTarget = 0f;
            action.BuyerMoodTarget = 0f;
            action.PlanLead = false;
            action.AddTag("budget_blocked");
            result.MarkChanged("стоимость блоков не помещается в недельный бюджет");
        }
    }

    private static void ValidateMarketTarget(DirectorValidationResult result, DirectorAction action,
        DirectorObservation observation, DirectorActionMask mask)
    {
        if (!mask.CanAdjustMarket)
        {
            if (!Mathf.Approximately(action.MarketGenerosityTarget, observation.MarketGenerosity))
            {
                action.MarketGenerosityTarget = observation.MarketGenerosity;
                action.AddTag("market_masked");
                result.MarkChanged("изменение рынка запрещено маской");
            }
            return;
        }

        float clamped = Mathf.Clamp(action.MarketGenerosityTarget, -1f, 1f);
        if (!Mathf.Approximately(clamped, action.MarketGenerosityTarget))
        {
            action.MarketGenerosityTarget = clamped;
            action.AddTag("market_clamped");
            result.MarkChanged("цель рынка зажата в -1..1");
        }
    }

    private static void ValidateBuyerTarget(DirectorValidationResult result, DirectorAction action,
        DirectorObservation observation, DirectorActionMask mask)
    {
        if (!mask.CanAdjustBuyer)
        {
            if (!Mathf.Approximately(action.BuyerMoodTarget, observation.BuyerMood))
            {
                action.BuyerMoodTarget = observation.BuyerMood;
                action.AddTag("buyer_masked");
                result.MarkChanged("изменение покупателя запрещено маской");
            }
            return;
        }

        float clamped = Mathf.Clamp(action.BuyerMoodTarget, -1f, 1f);
        if (!Mathf.Approximately(clamped, action.BuyerMoodTarget))
        {
            action.BuyerMoodTarget = clamped;
            action.AddTag("buyer_clamped");
            result.MarkChanged("цель покупателя зажата в -1..1");
        }
    }

    private static void ValidateLead(DirectorValidationResult result, DirectorAction action,
        DirectorObservation observation, DirectorActionMask mask, DirectorConfig config)
    {
        if (!action.PlanLead)
            return;

        if (!mask.CanPlanLead)
        {
            action.PlanLead = false;
            action.AddTag("lead_masked");
            result.MarkChanged("зацепка запрещена маской");
            return;
        }

        if (observation.LeadPlanned)
        {
            action.PlanLead = false;
            action.AddTag("lead_already_planned");
            result.MarkChanged("зацепка уже запланирована");
            return;
        }

        if (config != null && observation.DaysSinceLastLead < config.leadCooldownDays)
        {
            action.PlanLead = false;
            action.AddTag("lead_cooldown");
            result.MarkChanged("зацепка заблокирована кулдауном");
        }
    }

    private static void AddValidationReason(DirectorValidationResult result, DirectorAction action)
    {
        if (result.Changed && result.Messages.Count > 0)
            action.AddReason("валидатор: " + result.MessageText);
    }
}

public class RandomDirectorPolicy : IDirectorPolicy
{
    private readonly System.Random rng;
    private readonly DirectorConfig config;

    public string PolicyId { get { return "random"; } }

    public RandomDirectorPolicy(int seed, DirectorConfig config = null)
    {
        rng = new System.Random(seed);
        this.config = config;
    }

    public RandomDirectorPolicy(System.Random rng, DirectorConfig config = null)
    {
        this.rng = rng ?? new System.Random();
        this.config = config;
    }

    public DirectorAction Decide(DirectorObservation observation, DirectorActionMask mask)
    {
        if (observation == null)
            return DirectorAction.Create(PolicyId);

        var candidates = DirectorActionLibrary.BuildCandidates(observation, config, mask);
        var available = new List<DirectorActionBlock>();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].Allowed && candidates[i].Block.Id != "hold")
                available.Add(candidates[i].Block);
        }

        if (available.Count == 0)
            return DirectorActionLibrary.CreateBaseAction(PolicyId, observation, mask);

        var block = available[rng.Next(available.Count)];
        var action = DirectorActionLibrary.CreateActionFromBlock(PolicyId, block.Id, observation, mask);
        action.AddReason("random baseline");
        return action;
    }
}

public class ReplayDirectorPolicy : IDirectorPolicy
{
    private readonly Queue<DirectorAction> actions = new Queue<DirectorAction>();

    public string PolicyId { get { return "replay"; } }
    public int Remaining { get { return actions.Count; } }

    public ReplayDirectorPolicy()
    {
    }

    public ReplayDirectorPolicy(IEnumerable<DirectorAction> actions)
    {
        if (actions == null) return;
        foreach (var action in actions)
            Enqueue(action);
    }

    public void Enqueue(DirectorAction action)
    {
        if (action != null)
            actions.Enqueue(action.Copy());
    }

    public void Clear()
    {
        actions.Clear();
    }

    public DirectorAction Decide(DirectorObservation observation, DirectorActionMask mask)
    {
        if (actions.Count == 0)
        {
            var hold = DirectorAction.Create(PolicyId);
            hold.AddReason("replay empty");
            return hold;
        }

        var action = actions.Dequeue().Copy();
        action.PolicyId = PolicyId;
        if (action.Reasons.Count == 0)
            action.AddReason("replay action");
        return action;
    }
}
