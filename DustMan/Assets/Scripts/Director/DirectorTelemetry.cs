using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public enum DirectorEventType
{
    SessionStarted,
    DayStarted,
    SlotSpent,
    MarketOpened,
    MarketRestocked,
    ItemBought,
    RepairStarted,
    DefectRevealed,
    GestureError,
    RepairCheckFailed,
    RepairCompleted,
    CounterOpened,
    SaleAttempted,
    SaleCompleted,
    SaleRefused,
    DayEnded,
    DirectorDecision,
    DirectorActionApplied,
    LeadSeen,
    LeadAcquired,
    LeadResolved,
}

/// Runtime-событие для будущего обучения/симуляции режиссёра.
/// Хранится в памяти, дублируется в Debug.Log и пишется в JSONL для будущего датасета.
public class DirectorEventRecord
{
    public string SessionId;
    public int EventId;
    public string EventType;
    public string TimestampUtc;
    public int Day;
    public int Slot;
    public int Money;
    public int SlotsLeft;
    public string CurrentItemId;
    public int ItemQuality;
    public string ActiveDefectIds;
    public string DirectorActionId;
    public string PayloadJson;
}

public static class DirectorTelemetry
{
    public const int MaxRecords = 512;

    public static readonly List<DirectorEventRecord> Records = new List<DirectorEventRecord>();
    public static bool EchoToDebug = true;
    public static bool SaveJsonl = true;

    private static string sessionId = Guid.NewGuid().ToString("N");
    private static int nextEventId;
    private static readonly Encoding JsonEncoding = new UTF8Encoding(false);
    private static string jsonlPath;
    private static bool jsonlBroken;

    private class TelemetryRuntimeSnapshot
    {
        public string SessionId;
        public int NextEventId;
        public string JsonlPath;
        public bool JsonlBroken;
        public bool EchoToDebug;
        public bool SaveJsonl;
        public readonly List<DirectorEventRecord> Records = new List<DirectorEventRecord>();
    }

    public static string SessionId => sessionId;
    public static string CurrentJsonlPath => GetJsonlPath();

    public static void ResetSession()
    {
        sessionId = Guid.NewGuid().ToString("N");
        nextEventId = 0;
        jsonlPath = null;
        jsonlBroken = false;
        Records.Clear();
        Log(DirectorEventType.SessionStarted);
    }

    internal static object CaptureRuntimeState()
    {
        var snapshot = new TelemetryRuntimeSnapshot
        {
            SessionId = sessionId,
            NextEventId = nextEventId,
            JsonlPath = jsonlPath,
            JsonlBroken = jsonlBroken,
            EchoToDebug = EchoToDebug,
            SaveJsonl = SaveJsonl,
        };
        snapshot.Records.AddRange(Records);
        return snapshot;
    }

    internal static void RestoreRuntimeState(object snapshotObject)
    {
        if (!(snapshotObject is TelemetryRuntimeSnapshot snapshot))
            return;

        sessionId = snapshot.SessionId;
        nextEventId = snapshot.NextEventId;
        jsonlPath = snapshot.JsonlPath;
        jsonlBroken = snapshot.JsonlBroken;
        EchoToDebug = snapshot.EchoToDebug;
        SaveJsonl = snapshot.SaveJsonl;
        Records.Clear();
        Records.AddRange(snapshot.Records);
    }

    public static DirectorEventRecord Log(DirectorEventType type, string payloadJson = "",
        ItemInstance item = null, string directorActionId = "")
    {
        item ??= GameState.CurrentItem;

        var record = new DirectorEventRecord
        {
            SessionId = sessionId,
            EventId = ++nextEventId,
            EventType = type.ToString(),
            TimestampUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            Day = GameState.Day,
            Slot = CurrentSlotIndex(),
            Money = GameState.Money,
            SlotsLeft = GameState.SlotsLeft,
            CurrentItemId = item != null && item.Definition != null ? item.Definition.id : "",
            ItemQuality = item != null ? item.Quality : 0,
            ActiveDefectIds = ActiveDefects(item),
            DirectorActionId = directorActionId ?? "",
            PayloadJson = string.IsNullOrEmpty(payloadJson) ? "{}" : payloadJson,
        };

        Records.Add(record);
        while (Records.Count > MaxRecords)
            Records.RemoveAt(0);

        if (EchoToDebug)
            Debug.Log("[Telemetry] " + ToCompactString(record));

        AppendJsonl(record);

        return record;
    }

    private static int CurrentSlotIndex()
    {
        int daySlots = GameState.Economy != null ? GameState.Economy.daySlots : 0;
        if (daySlots <= 0) return 0;
        return Mathf.Clamp(daySlots - GameState.SlotsLeft, 0, daySlots);
    }

    private static string ActiveDefects(ItemInstance item)
    {
        if (item == null) return "";

        var ids = new List<string>();
        foreach (var defect in item.AllDefects())
            if (!defect.IsFixed && defect.Definition != null)
                ids.Add(defect.Definition.id);
        ids.Sort();
        return string.Join("+", ids);
    }

    private static string ToCompactString(DirectorEventRecord r)
    {
        return $"{r.EventId:0000} {r.EventType} d{r.Day}/s{r.Slot} " +
               $"money={r.Money} slots={r.SlotsLeft} item={r.CurrentItemId} " +
               $"action={r.DirectorActionId} payload={r.PayloadJson}";
    }

    private static void AppendJsonl(DirectorEventRecord record)
    {
        if (!SaveJsonl || jsonlBroken) return;

        try
        {
            string path = GetJsonlPath();
            File.AppendAllText(path, ToJsonLine(record) + Environment.NewLine, JsonEncoding);
            if (EchoToDebug && record.EventId == 1)
                Debug.Log("[Telemetry] JSONL " + path);
        }
        catch (Exception e)
        {
            jsonlBroken = true;
            Debug.LogWarning("DirectorTelemetry: JSONL disabled: " + e.Message);
        }
    }

    private static string GetJsonlPath()
    {
        if (!string.IsNullOrEmpty(jsonlPath))
            return jsonlPath;

        string root = "";
#if UNITY_EDITOR
        root = ProjectTelemetryRoot();
#endif
        if (string.IsNullOrEmpty(root))
            root = Path.Combine(Application.persistentDataPath, "Telemetry", "local");

        Directory.CreateDirectory(root);
        jsonlPath = Path.Combine(root, sessionId + ".jsonl");
        return jsonlPath;
    }

#if UNITY_EDITOR
    private static string ProjectTelemetryRoot()
    {
        if (string.IsNullOrEmpty(Application.dataPath)) return "";
        var assetsDir = Directory.GetParent(Application.dataPath);
        return assetsDir == null ? "" : Path.Combine(assetsDir.FullName, "Telemetry", "local");
    }
#endif

    private static string ToJsonLine(DirectorEventRecord r)
    {
        return "{"
            + JsonStr("session_id", r.SessionId) + ","
            + JsonNum("event_id", r.EventId) + ","
            + JsonStr("event_type", r.EventType) + ","
            + JsonStr("timestamp_utc", r.TimestampUtc) + ","
            + JsonNum("day", r.Day) + ","
            + JsonNum("slot", r.Slot) + ","
            + JsonNum("money", r.Money) + ","
            + JsonNum("slots_left", r.SlotsLeft) + ","
            + JsonStr("current_item_id", r.CurrentItemId) + ","
            + JsonNum("item_quality", r.ItemQuality) + ","
            + JsonStr("active_defect_ids", r.ActiveDefectIds) + ","
            + JsonStr("director_action_id", r.DirectorActionId) + ","
            + "\"payload\":" + PayloadObject(r.PayloadJson)
            + "}";
    }

    private static string JsonStr(string key, string value)
    {
        return $"\"{Escape(key)}\":\"{Escape(value)}\"";
    }

    private static string JsonNum(string key, int value)
    {
        return $"\"{Escape(key)}\":{value}";
    }

    private static string PayloadObject(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return "{}";
        string trimmed = payloadJson.Trim();
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            return trimmed;
        return "{" + Str("raw", trimmed) + "}";
    }

    public static string Payload(params string[] fields)
    {
        return "{" + string.Join(",", fields) + "}";
    }

    public static string Str(string key, string value)
    {
        return $"\"{Escape(key)}\":\"{Escape(value)}\"";
    }

    public static string Num(string key, int value)
    {
        return $"\"{Escape(key)}\":{value}";
    }

    public static string Num(string key, float value)
    {
        return $"\"{Escape(key)}\":{value.ToString("0.###", CultureInfo.InvariantCulture)}";
    }

    public static string Bool(string key, bool value)
    {
        return $"\"{Escape(key)}\":{(value ? "true" : "false")}";
    }

    public static string Array(string key, IEnumerable<string> values)
    {
        var sb = new StringBuilder();
        sb.Append('"').Append(Escape(key)).Append("\":[");
        bool first = true;
        foreach (var value in values)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('"').Append(Escape(value)).Append('"');
        }
        sb.Append(']');
        return sb.ToString();
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
