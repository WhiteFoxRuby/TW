using System.Collections.Generic;
using System.Globalization;
using System.Text;

public class DirectorEventToken
{
    public string EventType;
    public int Day;
    public int Slot;
    public int DayDelta;
    public int SlotDelta;
    public int Money;
    public int SlotsLeft;
    public string CurrentItemId;
    public int ItemQuality;
    public string ActiveDefectIds;
    public string DirectorActionId;
    public string PayloadJson;
}

public class DirectorObservation
{
    public string SessionId;
    public string ObservationHash;
    public int SourceEventId;

    public int Day;
    public int Slot;
    public int DaySlots;
    public int SlotsLeft;
    public int Money;
    public int SpentToday;
    public int EarnedToday;

    public bool CurrentItemHeld;
    public string CurrentItemId;
    public int CurrentItemAgeDays;
    public bool CurrentItemRepairCompleted;
    public bool CurrentItemHum;
    public int CurrentItemQuality;
    public int CurrentItemTotalDefects;
    public int CurrentItemFixedDefects;
    public int CurrentItemRevealedDefects;
    public string CurrentItemActiveDefectIds;
    public float CurrentItemCleanliness;

    public int MarketStockCount;
    public int MarketHumCount;
    public int MarketAveragePrice;

    public int DaysSinceLastLead;
    public bool LeadPlanned;

    public float Pressure;
    public float Boredom;
    public float Frustration;
    public float MarketGenerosity;
    public float BuyerMood;
    public float WeeklyBudgetUsed;

    public int RecentEventCount;
    public int RecentGestureErrors;
    public int RecentCheckFails;
    public int RecentSaleAttempts;
    public int RecentSaleRefusals;
    public int RecentRepairsCompleted;
    public int RecentItemsBought;
    public int RecentLeadsSeen;
    public int RecentLeadsAcquired;
    public int RecentLeadsResolved;
    public int RecentDayEnds;
    public int RecentUnusedSlots;
    public string RecentEventTypes;
    public readonly List<DirectorEventToken> RecentEvents = new List<DirectorEventToken>();

    public string ToDebugString()
    {
        string item = CurrentItemHeld
            ? $"{CurrentItemId} q{CurrentItemQuality} age {CurrentItemAgeDays}d"
            : "none";
        return $"obs {ObservationHash} ev={RecentEventCount} d{Day}/s{Slot} " +
               $"money={Money} slots={SlotsLeft}/{DaySlots} item={item} " +
               $"err={RecentGestureErrors} fail={RecentCheckFails} sales={RecentSaleAttempts}/{RecentSaleRefusals}";
    }
}

public static class DirectorObservationBuilder
{
    public const int DefaultEventWindow = 64;

    public static DirectorObservation LastObservation { get; private set; }

    public static DirectorObservation Build(int eventWindow = DefaultEventWindow)
    {
        return BuildForDay(GameState.Day, eventWindow);
    }

    public static DirectorObservation BuildForDay(int day, int eventWindow = DefaultEventWindow)
    {
        if (eventWindow < 1) eventWindow = 1;

        var observation = new DirectorObservation
        {
            SessionId = DirectorTelemetry.SessionId,
            SourceEventId = LastEventId(),
            Day = day,
            Slot = CurrentSlotIndex(),
            DaySlots = GameState.Economy != null ? GameState.Economy.daySlots : 0,
            SlotsLeft = GameState.SlotsLeft,
            Money = GameState.Money,
            SpentToday = GameState.SpentToday,
            EarnedToday = GameState.EarnedToday,
            DaysSinceLastLead = DaysSinceLastLead(day),
            LeadPlanned = GameDirector.LeadPlanned,
            Pressure = GameDirector.Pressure,
            Boredom = GameDirector.Boredom,
            Frustration = GameDirector.Frustration,
            MarketGenerosity = GameDirector.MarketGenerosity,
            BuyerMood = GameDirector.BuyerMood,
            WeeklyBudgetUsed = GameDirector.WeeklyBudgetUsed,
        };

        FillCurrentItem(observation);
        FillMarket(observation);
        FillRecentEvents(observation, eventWindow);
        observation.ObservationHash = ComputeHash(observation);
        LastObservation = observation;
        return observation;
    }

    private static int LastEventId()
    {
        var records = DirectorTelemetry.Records;
        return records.Count == 0 ? 0 : records[records.Count - 1].EventId;
    }

    private static int CurrentSlotIndex()
    {
        int daySlots = GameState.Economy != null ? GameState.Economy.daySlots : 0;
        if (daySlots <= 0) return 0;
        int used = daySlots - GameState.SlotsLeft;
        if (used < 0) return 0;
        return used > daySlots ? daySlots : used;
    }

    private static void FillCurrentItem(DirectorObservation observation)
    {
        var item = GameState.CurrentItem;
        observation.CurrentItemHeld = item != null;
        if (item == null) return;

        observation.CurrentItemId = item.Definition != null ? item.Definition.id : "";
        observation.CurrentItemAgeDays = CurrentItemAgeDays(observation.Day, observation.CurrentItemId);
        observation.CurrentItemRepairCompleted = item.RepairCompleted;
        observation.CurrentItemHum = item.Hum;
        observation.CurrentItemQuality = item.Quality;
        observation.CurrentItemTotalDefects = item.TotalDefects;
        observation.CurrentItemFixedDefects = item.FixedDefects;
        observation.CurrentItemCleanliness = item.CleanlinessFraction();

        var active = new List<string>();
        int revealed = 0;
        foreach (var defect in item.AllDefects())
        {
            if (defect.Revealed) revealed++;
            if (!defect.IsFixed && defect.Definition != null)
                active.Add(defect.Definition.id);
        }
        active.Sort();
        observation.CurrentItemRevealedDefects = revealed;
        observation.CurrentItemActiveDefectIds = string.Join("+", active);
    }

    private static void FillMarket(DirectorObservation observation)
    {
        var stock = GameState.MarketStock;
        observation.MarketStockCount = stock.Count;
        if (stock.Count == 0) return;

        int priceSum = 0;
        int hum = 0;
        foreach (var offer in stock)
        {
            priceSum += offer.Price;
            if (offer.Item != null && offer.Item.Hum)
                hum++;
        }
        observation.MarketHumCount = hum;
        observation.MarketAveragePrice = priceSum / stock.Count;
    }

    private static void FillRecentEvents(DirectorObservation observation, int eventWindow)
    {
        var records = DirectorTelemetry.Records;
        int from = records.Count > eventWindow ? records.Count - eventWindow : 0;
        var typeList = new List<string>();

        for (int i = from; i < records.Count; i++)
        {
            var r = records[i];
            observation.RecentEventCount++;
            CountEvent(observation, r);
            typeList.Add(r.EventType);
            observation.RecentEvents.Add(ToToken(observation, r));
        }

        observation.RecentEventTypes = string.Join(",", typeList);
    }

    private static DirectorEventToken ToToken(DirectorObservation observation, DirectorEventRecord record)
    {
        return new DirectorEventToken
        {
            EventType = record.EventType,
            Day = record.Day,
            Slot = record.Slot,
            DayDelta = observation.Day - record.Day,
            SlotDelta = observation.Slot - record.Slot,
            Money = record.Money,
            SlotsLeft = record.SlotsLeft,
            CurrentItemId = record.CurrentItemId,
            ItemQuality = record.ItemQuality,
            ActiveDefectIds = record.ActiveDefectIds,
            DirectorActionId = record.DirectorActionId,
            PayloadJson = record.PayloadJson,
        };
    }

    private static void CountEvent(DirectorObservation observation, DirectorEventRecord record)
    {
        switch (record.EventType)
        {
            case nameof(DirectorEventType.GestureError):
                observation.RecentGestureErrors++;
                break;
            case nameof(DirectorEventType.RepairCheckFailed):
                observation.RecentCheckFails++;
                break;
            case nameof(DirectorEventType.SaleAttempted):
                observation.RecentSaleAttempts++;
                break;
            case nameof(DirectorEventType.SaleRefused):
                observation.RecentSaleRefusals++;
                break;
            case nameof(DirectorEventType.RepairCompleted):
                observation.RecentRepairsCompleted++;
                break;
            case nameof(DirectorEventType.ItemBought):
                observation.RecentItemsBought++;
                break;
            case nameof(DirectorEventType.LeadSeen):
                observation.RecentLeadsSeen++;
                break;
            case nameof(DirectorEventType.LeadAcquired):
                observation.RecentLeadsAcquired++;
                break;
            case nameof(DirectorEventType.LeadResolved):
                observation.RecentLeadsResolved++;
                break;
            case nameof(DirectorEventType.DayEnded):
                observation.RecentDayEnds++;
                observation.RecentUnusedSlots += record.SlotsLeft;
                break;
        }
    }

    private static int CurrentItemAgeDays(int day, string currentItemId)
    {
        if (string.IsNullOrEmpty(currentItemId)) return 0;

        var records = DirectorTelemetry.Records;
        for (int i = records.Count - 1; i >= 0; i--)
        {
            var r = records[i];
            if (r.EventType == nameof(DirectorEventType.ItemBought) && r.CurrentItemId == currentItemId)
                return System.Math.Max(0, day - r.Day);
        }
        return 0;
    }

    private static int DaysSinceLastLead(int day)
    {
        var records = DirectorTelemetry.Records;
        for (int i = records.Count - 1; i >= 0; i--)
        {
            var type = records[i].EventType;
            if (type == nameof(DirectorEventType.LeadSeen)
                || type == nameof(DirectorEventType.LeadAcquired)
                || type == nameof(DirectorEventType.LeadResolved))
                return System.Math.Max(0, day - records[i].Day);
        }

        return GameDirector.LastLeadDay > 0
            ? System.Math.Max(0, day - GameDirector.LastLeadDay)
            : day;
    }

    private static string ComputeHash(DirectorObservation o)
    {
        unchecked
        {
            uint hash = 2166136261u;
            Mix(ref hash, o.Day);
            Mix(ref hash, o.Slot);
            Mix(ref hash, o.Money);
            Mix(ref hash, o.SlotsLeft);
            Mix(ref hash, o.SpentToday);
            Mix(ref hash, o.EarnedToday);
            Mix(ref hash, o.CurrentItemId);
            Mix(ref hash, o.CurrentItemAgeDays);
            Mix(ref hash, o.CurrentItemQuality);
            Mix(ref hash, o.CurrentItemActiveDefectIds);
            Mix(ref hash, o.MarketStockCount);
            Mix(ref hash, o.MarketHumCount);
            Mix(ref hash, o.DaysSinceLastLead);
            Mix(ref hash, o.LeadPlanned ? 1 : 0);
            Mix(ref hash, o.Pressure);
            Mix(ref hash, o.Boredom);
            Mix(ref hash, o.Frustration);
            Mix(ref hash, o.MarketGenerosity);
            Mix(ref hash, o.BuyerMood);
            Mix(ref hash, o.WeeklyBudgetUsed);
            Mix(ref hash, o.RecentEventTypes);
            return hash.ToString("x8", CultureInfo.InvariantCulture);
        }
    }

    private static void Mix(ref uint hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= 16777619u;
        }
    }

    private static void Mix(ref uint hash, float value)
    {
        Mix(ref hash, (int)(value * 1000f));
    }

    private static void Mix(ref uint hash, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            Mix(ref hash, 0);
            return;
        }

        for (int i = 0; i < value.Length; i++)
        {
            hash ^= value[i];
            hash *= 16777619u;
        }
    }

    public static string ToFeatureLine(DirectorObservation o)
    {
        var sb = new StringBuilder();
        sb.Append("hash=").Append(o.ObservationHash);
        sb.Append(" day=").Append(o.Day);
        sb.Append(" slot=").Append(o.Slot);
        sb.Append(" money=").Append(o.Money);
        sb.Append(" slots_left=").Append(o.SlotsLeft);
        sb.Append(" pressure=").Append(o.Pressure.ToString("0.###", CultureInfo.InvariantCulture));
        sb.Append(" boredom=").Append(o.Boredom.ToString("0.###", CultureInfo.InvariantCulture));
        sb.Append(" frustration=").Append(o.Frustration.ToString("0.###", CultureInfo.InvariantCulture));
        sb.Append(" recent_events=").Append(o.RecentEventCount);
        return sb.ToString();
    }
}
