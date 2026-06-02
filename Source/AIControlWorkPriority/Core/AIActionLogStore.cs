using System;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace AIControlWorkPriority
{
    public class LogEntry : IExposable
    {
        public string timestamp;
        public string provider;
        public string modelName;
        public string status; // "Success" 或 "Failed"
        public string summary;
        public int appliedCount;
        public List<string> alerts = new List<string>();
        public List<string> errors = new List<string>();

        public LogEntry() { }

        public void ExposeData()
        {
            Scribe_Values.Look(ref timestamp, "timestamp");
            Scribe_Values.Look(ref provider, "provider");
            Scribe_Values.Look(ref modelName, "modelName");
            Scribe_Values.Look(ref status, "status");
            Scribe_Values.Look(ref summary, "summary");
            Scribe_Values.Look(ref appliedCount, "appliedCount");
            Scribe_Collections.Look(ref alerts, "alerts", LookMode.Value);
            Scribe_Collections.Look(ref errors, "errors", LookMode.Value);
        }
    }

    public class AIActionLogStore : GameComponent
    {
        public static AIActionLogStore Instance;
        private List<LogEntry> logs = new List<LogEntry>();
        private const int MaxLogs = 20;

        public AIActionLogStore(Game game)
        {
            Instance = this;
        }

        public static AIActionLogStore GetInstance()
        {
            if (Instance == null && Current.Game != null)
            {
                Instance = Current.Game.GetComponent<AIActionLogStore>();
            }
            return Instance;
        }

        public List<LogEntry> Logs => logs;

        public void AddLog(LogEntry entry)
        {
            if (entry == null) return;
            
            logs.Add(entry);
            while (logs.Count > MaxLogs)
            {
                logs.RemoveAt(0);
            }
        }

        public string GetFormattedLogs()
        {
            if (logs == null || logs.Count == 0)
            {
                return "無任何行動記錄。";
            }

            StringBuilder sb = new StringBuilder();
            for (int i = logs.Count - 1; i >= 0; i--)
            {
                var log = logs[i];
                sb.AppendLine($"[{log.timestamp}] 狀態: {(log.status == "Success" ? "成功" : "失敗")}");
                sb.AppendLine($"供應商: {log.provider} ({log.modelName})");
                if (log.status == "Success")
                {
                    sb.AppendLine($"變更項目數: {log.appliedCount} 筆");
                    sb.AppendLine($"摘要: {log.summary}");
                    if (log.alerts != null && log.alerts.Count > 0)
                    {
                        sb.AppendLine("提醒:");
                        foreach (var alert in log.alerts)
                        {
                            sb.AppendLine($"  - {alert}");
                        }
                    }
                }
                else
                {
                    if (log.errors != null && log.errors.Count > 0)
                    {
                        sb.AppendLine("錯誤原因:");
                        foreach (var err in log.errors)
                        {
                            sb.AppendLine($"  - {err}");
                        }
                    }
                }
                sb.AppendLine(new string('-', 40));
            }
            return sb.ToString();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref logs, "logs", LookMode.Deep);
            if (logs == null)
            {
                logs = new List<LogEntry>();
            }
        }
    }
}
