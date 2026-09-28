using System.Collections.Generic;
using UnityEngine;

public struct DirectorActionMask
{
    public bool CanAdjustMarket;
    public bool CanAdjustBuyer;
    public bool CanPlanLead;
    public bool CanUseWeeklyBudget;
    public List<string> AllowedActionIds;
    public List<string> BlockedActionIds;

    public static DirectorActionMask Default
    {
        get
        {
            return new DirectorActionMask
            {
                CanAdjustMarket = true,
                CanAdjustBuyer = true,
                CanPlanLead = true,
                CanUseWeeklyBudget = true,
                AllowedActionIds = new List<string>(),
                BlockedActionIds = new List<string>(),
            };
        }
    }

    public string AllowedActionIdsText
    {
        get { return AllowedActionIds != null ? string.Join(",", AllowedActionIds) : ""; }
    }

    public string BlockedActionIdsText
    {
        get { return BlockedActionIds != null ? string.Join(",", BlockedActionIds) : ""; }
    }

    public bool AllowsActionId(string actionId)
    {
        if (AllowedActionIds == null || AllowedActionIds.Count == 0)
            return true;
        return AllowedActionIds.Contains(actionId);
    }
}

public class DirectorAction
{
    private readonly List<string> tags = new List<string>();

    public string PolicyId;
    public string ActionId = "hold";
    public float BudgetCost;
    public int RiskLevel;
    public string Level = "";
    public string TargetIndex = "";
    public string Payload = "";
    public float MarketGenerosityTarget;
    public float BuyerMoodTarget;
    public bool PlanLead;
    public readonly List<string> Reasons = new List<string>();

    public static DirectorAction Create(string policyId)
    {
        return new DirectorAction
        {
            PolicyId = string.IsNullOrEmpty(policyId) ? "unknown" : policyId,
            ActionId = "hold",
        };
    }

    public void AddReason(string reason)
    {
        if (!string.IsNullOrEmpty(reason))
            Reasons.Add(reason);
    }

    public void AddTag(string tag)
    {
        if (string.IsNullOrEmpty(tag) || tags.Contains(tag))
            return;

        tags.Add(tag);
        ActionId = string.Join("+", tags);
    }

    public DirectorAction Copy()
    {
        var copy = new DirectorAction
        {
            PolicyId = PolicyId,
            ActionId = ActionId,
            BudgetCost = BudgetCost,
            RiskLevel = RiskLevel,
            Level = Level,
            TargetIndex = TargetIndex,
            Payload = Payload,
            MarketGenerosityTarget = MarketGenerosityTarget,
            BuyerMoodTarget = BuyerMoodTarget,
            PlanLead = PlanLead,
        };
        copy.tags.AddRange(tags);
        copy.Reasons.AddRange(Reasons);
        return copy;
    }

    public string ReasonText
    {
        get
        {
            return Reasons.Count > 0
                ? string.Join("; ", Reasons)
                : "в коридоре, ручки не двигаю";
        }
    }
}

public interface IDirectorPolicy
{
    string PolicyId { get; }
    DirectorAction Decide(DirectorObservation observation, DirectorActionMask mask);
}

public class RuleBasedDirectorPolicy : IDirectorPolicy
{
    private readonly DirectorConfig config;

    public string PolicyId { get { return "rule_based"; } }

    public RuleBasedDirectorPolicy(DirectorConfig config)
    {
        this.config = config;
    }

    public DirectorAction Decide(DirectorObservation observation, DirectorActionMask mask)
    {
        var c = config != null ? config : GameDirector.Config;
        var action = DirectorAction.Create(PolicyId);

        if (c == null || observation == null)
            return action;

        action = DirectorActionLibrary.CreateBaseAction(PolicyId, observation, mask);

        if (mask.CanAdjustMarket)
        {
            if (observation.Pressure > c.pressureHigh)
            {
                DirectorActionLibrary.TryApplyBlock(action, "market_generous", observation);
                action.AddReason($"давление {observation.Pressure:0.00} высокое → щедрый рынок");
            }
            else if (observation.Pressure < c.pressureLow)
            {
                DirectorActionLibrary.TryApplyBlock(action, "market_strict", observation);
                action.AddReason($"давление {observation.Pressure:0.00} низкое → рынок скупее");
            }
        }

        if (mask.CanAdjustBuyer)
        {
            float moodTarget = 0f;
            if (observation.Pressure > c.pressureHigh) moodTarget = 1f;
            if (observation.Frustration > c.frustrationHigh) moodTarget = Mathf.Max(moodTarget, 0.6f);
            if (observation.Pressure < c.pressureLow && observation.Frustration < c.frustrationHigh) moodTarget = -0.4f;

            action.BuyerMoodTarget = moodTarget;
            if (!Mathf.Approximately(moodTarget, 0f))
            {
                DirectorActionLibrary.TryApplyBlock(action,
                    moodTarget > 0 ? "buyer_friendly" : "buyer_strict", observation);
                action.BuyerMoodTarget = moodTarget;
                action.AddReason($"покупатель → {(moodTarget > 0 ? "сговорчивее" : "прижимистее")} (фрустрация {observation.Frustration:0.00})");
            }
        }

        if (mask.CanPlanLead
            && !observation.LeadPlanned
            && observation.Boredom > c.boredomHigh
            && observation.DaysSinceLastLead >= c.leadCooldownDays)
        {
            DirectorActionLibrary.TryApplyBlock(action, "story_lead_humming_item", observation);
            action.AddReason($"скука {observation.Boredom:0.00} высокая → на рынке появится гудящий фонарь");
        }

        if (mask.CanPlanLead
            && !observation.LeadPlanned
            && !action.PlanLead
            && observation.DaysSinceLastLead >= c.leadMaxDays)
        {
            DirectorActionLibrary.TryApplyBlock(action, "story_lead_humming_item", observation);
            action.AddReason($"сюжетный минимум: {observation.DaysSinceLastLead} дн. без зацепки → выдаю принудительно");
        }

        return action;
    }
}
