using System.Collections.Generic;
using UnityEngine;

public class DirectorActionBlock
{
    public string Id;
    public string Title;
    public string Description;
    public string Level = "day";
    public string TargetIndex = "";
    public string[] Tags = new string[0];
    public string Preconditions = "";
    public int CooldownDays;
    public float BudgetCost;
    public int RiskLevel;
    public string Payload = "";
    public bool Implemented = true;

    public bool RequiresMarketControl;
    public bool RequiresBuyerControl;
    public bool RequiresLeadControl;
    public bool RequiresBudget;

    public bool HasMarketGenerosityTarget;
    public float MarketGenerosityTarget;
    public bool HasBuyerMoodTarget;
    public float BuyerMoodTarget;
    public bool PlanLead;

    public float MinPressure = -1f;
    public float MaxPressure = 2f;
    public float MinBoredom = -1f;
    public float MaxBoredom = 2f;
    public float MinFrustration = -1f;
    public float MaxFrustration = 2f;
    public bool RequireNoLeadPlanned;
    public bool UseConfigLeadCooldown;
}

public class DirectorActionCandidate
{
    public DirectorActionBlock Block;
    public bool Allowed;
    public readonly List<string> Reasons = new List<string>();

    public string ReasonText
    {
        get { return Reasons.Count > 0 ? string.Join("; ", Reasons) : ""; }
    }
}

public static class DirectorActionLibrary
{
    private static readonly List<DirectorActionBlock> blocks = new List<DirectorActionBlock>
    {
        new DirectorActionBlock
        {
            Id = "hold",
            Title = "Hold",
            Description = "No directed intervention.",
            Level = "day",
            TargetIndex = "none",
            BudgetCost = 0f,
            RiskLevel = 0,
            Payload = "{}",
        },
        new DirectorActionBlock
        {
            Id = "market_generous",
            Title = "Generous market",
            Description = "Increase stock, reduce price pressure and bias toward easier items.",
            Level = "day",
            TargetIndex = "pressure",
            Tags = new[] { "market", "economy", "support" },
            Preconditions = "pressure is high; weekly budget has room",
            CooldownDays = 1,
            BudgetCost = 1f,
            RiskLevel = 1,
            Payload = "{market_generosity:1}",
            RequiresMarketControl = true,
            RequiresBudget = true,
            HasMarketGenerosityTarget = true,
            MarketGenerosityTarget = 1f,
        },
        new DirectorActionBlock
        {
            Id = "market_strict",
            Title = "Strict market",
            Description = "Make market a little less generous when the player is stable.",
            Level = "day",
            TargetIndex = "economy",
            Tags = new[] { "market", "economy", "restraint" },
            Preconditions = "pressure is low; weekly budget has room",
            CooldownDays = 1,
            BudgetCost = 0.5f,
            RiskLevel = 1,
            Payload = "{market_generosity:-0.5}",
            RequiresMarketControl = true,
            RequiresBudget = true,
            HasMarketGenerosityTarget = true,
            MarketGenerosityTarget = -0.5f,
            MaxPressure = 0.5f,
        },
        new DirectorActionBlock
        {
            Id = "buyer_friendly",
            Title = "Friendly buyer",
            Description = "Make the buyer more tolerant of the player's price.",
            Level = "day",
            TargetIndex = "frustration",
            Tags = new[] { "buyer", "economy", "support" },
            Preconditions = "pressure or frustration suggests the player needs relief",
            CooldownDays = 1,
            BudgetCost = 0.8f,
            RiskLevel = 1,
            Payload = "{buyer_mood:0.6..1}",
            RequiresBuyerControl = true,
            RequiresBudget = true,
            HasBuyerMoodTarget = true,
            BuyerMoodTarget = 0.6f,
        },
        new DirectorActionBlock
        {
            Id = "buyer_strict",
            Title = "Strict buyer",
            Description = "Make the buyer stricter when the player is too comfortable.",
            Level = "day",
            TargetIndex = "economy",
            Tags = new[] { "buyer", "economy", "restraint" },
            Preconditions = "pressure and frustration are low",
            CooldownDays = 1,
            BudgetCost = 0.4f,
            RiskLevel = 1,
            Payload = "{buyer_mood:-0.4}",
            RequiresBuyerControl = true,
            RequiresBudget = true,
            HasBuyerMoodTarget = true,
            BuyerMoodTarget = -0.4f,
            MaxPressure = 0.5f,
            MaxFrustration = 0.75f,
        },
        new DirectorActionBlock
        {
            Id = "story_lead_humming_item",
            Title = "Humming item lead",
            Description = "Put a humming item into the next market restock.",
            Level = "story",
            TargetIndex = "narrative",
            Tags = new[] { "story", "lead", "market" },
            Preconditions = "no lead is already planned; lead cooldown passed",
            CooldownDays = 0,
            BudgetCost = 0.75f,
            RiskLevel = 1,
            Payload = "{lead_type:humming_item}",
            RequiresLeadControl = true,
            RequiresBudget = true,
            PlanLead = true,
            RequireNoLeadPlanned = true,
            UseConfigLeadCooldown = true,
        },
        new DirectorActionBlock
        {
            Id = "familiar_item",
            Title = "Familiar item",
            Description = "Bias content toward an already understood item category.",
            Level = "day",
            TargetIndex = "frustration",
            Tags = new[] { "market", "learning", "comfort" },
            Preconditions = "not implemented in current slice",
            BudgetCost = 0.5f,
            RiskLevel = 1,
            Payload = "{loot_bias:familiar_item}",
            RequiresMarketControl = true,
            RequiresBudget = true,
            Implemented = false,
        },
        new DirectorActionBlock
        {
            Id = "easy_defects",
            Title = "Easy defects",
            Description = "Bias new items toward fewer hidden or blocking defects.",
            Level = "day",
            TargetIndex = "frustration",
            Tags = new[] { "repair", "learning", "comfort" },
            Preconditions = "not implemented in current slice",
            BudgetCost = 0.7f,
            RiskLevel = 1,
            Payload = "{defect_bias:easier}",
            RequiresMarketControl = true,
            RequiresBudget = true,
            Implemented = false,
        },
        new DirectorActionBlock
        {
            Id = "varied_defects",
            Title = "Varied defects",
            Description = "Bias new items away from repeated recent defect patterns.",
            Level = "day",
            TargetIndex = "boredom",
            Tags = new[] { "repair", "variety", "market" },
            Preconditions = "not implemented in current slice",
            BudgetCost = 0.5f,
            RiskLevel = 1,
            Payload = "{defect_bias:varied}",
            RequiresMarketControl = true,
            RequiresBudget = true,
            Implemented = false,
        },
    };

    public static IReadOnlyList<DirectorActionBlock> Blocks
    {
        get { return blocks; }
    }

    public static DirectorActionBlock Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < blocks.Count; i++)
            if (blocks[i].Id == id)
                return blocks[i];
        return null;
    }

    public static DirectorAction CreateBaseAction(string policyId, DirectorObservation observation, DirectorActionMask mask)
    {
        var action = DirectorAction.Create(policyId);
        if (observation == null)
            return action;

        action.MarketGenerosityTarget = mask.CanAdjustMarket ? 0f : observation.MarketGenerosity;
        action.BuyerMoodTarget = mask.CanAdjustBuyer ? 0f : observation.BuyerMood;
        return action;
    }

    public static DirectorAction CreateActionFromBlock(string policyId, string blockId,
        DirectorObservation observation, DirectorActionMask mask)
    {
        var action = CreateBaseAction(policyId, observation, mask);
        TryApplyBlock(action, blockId, observation);
        return action;
    }

    public static bool TryApplyBlock(DirectorAction action, string blockId, DirectorObservation observation)
    {
        var block = Find(blockId);
        if (action == null || block == null)
            return false;

        action.AddTag(block.Id);
        action.BudgetCost += block.BudgetCost;
        action.RiskLevel = Mathf.Max(action.RiskLevel, block.RiskLevel);
        action.Level = AppendToken(action.Level, block.Level);
        action.TargetIndex = AppendToken(action.TargetIndex, block.TargetIndex);
        action.Payload = AppendToken(action.Payload, block.Payload);

        if (block.HasMarketGenerosityTarget)
            action.MarketGenerosityTarget = block.MarketGenerosityTarget;
        if (block.HasBuyerMoodTarget)
            action.BuyerMoodTarget = block.BuyerMoodTarget;
        if (block.PlanLead)
            action.PlanLead = true;

        return true;
    }

    public static DirectorActionMask BuildMask(DirectorObservation observation,
        DirectorConfig config, DirectorActionMask baseMask)
    {
        var mask = baseMask;
        mask.AllowedActionIds = new List<string>();
        mask.BlockedActionIds = new List<string>();

        foreach (var block in blocks)
        {
            var reasons = new List<string>();
            bool allowed = IsBlockAllowed(block, observation, mask, config, reasons);
            if (allowed)
                mask.AllowedActionIds.Add(block.Id);
            else
                mask.BlockedActionIds.Add(block.Id + ":" + (reasons.Count > 0 ? reasons[0] : "blocked"));
        }

        return mask;
    }

    public static List<DirectorActionCandidate> BuildCandidates(DirectorObservation observation,
        DirectorConfig config, DirectorActionMask mask)
    {
        var candidates = new List<DirectorActionCandidate>();
        foreach (var block in blocks)
        {
            var candidate = new DirectorActionCandidate { Block = block };
            candidate.Allowed = IsBlockAllowed(block, observation, mask, config, candidate.Reasons);
            candidates.Add(candidate);
        }
        return candidates;
    }

    public static List<string> SplitActionIds(string actionId)
    {
        var ids = new List<string>();
        if (string.IsNullOrEmpty(actionId))
            return ids;

        var parts = actionId.Split('+');
        for (int i = 0; i < parts.Length; i++)
        {
            string id = parts[i].Trim();
            if (!string.IsNullOrEmpty(id))
                ids.Add(id);
        }
        return ids;
    }

    public static bool IsBlockAllowed(DirectorActionBlock block, DirectorObservation observation,
        DirectorActionMask mask, DirectorConfig config, List<string> reasons)
    {
        if (block == null)
        {
            reasons.Add("missing block");
            return false;
        }

        if (block.Id == "hold")
            return true;

        if (!block.Implemented)
            reasons.Add("not implemented");

        if (observation == null)
            reasons.Add("no observation");

        if (block.RequiresMarketControl && !mask.CanAdjustMarket)
            reasons.Add("market masked");
        if (block.RequiresBuyerControl && !mask.CanAdjustBuyer)
            reasons.Add("buyer masked");
        if (block.RequiresLeadControl && !mask.CanPlanLead)
            reasons.Add("lead masked");
        if (block.RequiresBudget && !mask.CanUseWeeklyBudget)
            reasons.Add("budget masked");

        if (observation != null)
        {
            if (observation.Pressure < block.MinPressure)
                reasons.Add("pressure too low");
            if (observation.Pressure > block.MaxPressure)
                reasons.Add("pressure too high");
            if (observation.Boredom < block.MinBoredom)
                reasons.Add("boredom too low");
            if (observation.Boredom > block.MaxBoredom)
                reasons.Add("boredom too high");
            if (observation.Frustration < block.MinFrustration)
                reasons.Add("frustration too low");
            if (observation.Frustration > block.MaxFrustration)
                reasons.Add("frustration too high");
            if (block.RequireNoLeadPlanned && observation.LeadPlanned)
                reasons.Add("lead already planned");
            if (block.UseConfigLeadCooldown && config != null
                && observation.DaysSinceLastLead < config.leadCooldownDays)
                reasons.Add("lead cooldown");
            if (block.RequiresBudget && config != null
                && observation.WeeklyBudgetUsed + block.BudgetCost > config.weeklyKnobBudget)
                reasons.Add("weekly budget");
            if (IsOnCooldown(block, observation.Day))
                reasons.Add("cooldown");
        }

        return reasons.Count == 0;
    }

    private static bool IsOnCooldown(DirectorActionBlock block, int day)
    {
        if (block.CooldownDays <= 0)
            return false;

        var records = DirectorTelemetry.Records;
        for (int i = records.Count - 1; i >= 0; i--)
        {
            var record = records[i];
            if (record.Day > day)
                continue;
            if (record.EventType != nameof(DirectorEventType.DirectorDecision))
                continue;
            if (!ActionContains(record.DirectorActionId, block.Id))
                continue;
            return day - record.Day < block.CooldownDays;
        }

        return false;
    }

    private static bool ActionContains(string actionId, string blockId)
    {
        var ids = SplitActionIds(actionId);
        for (int i = 0; i < ids.Count; i++)
            if (ids[i] == blockId)
                return true;
        return false;
    }

    private static string AppendToken(string existing, string token)
    {
        if (string.IsNullOrEmpty(token))
            return existing ?? "";
        if (string.IsNullOrEmpty(existing))
            return token;

        var parts = existing.Split('+');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i] == token)
                return existing;
        return existing + "+" + token;
    }
}
