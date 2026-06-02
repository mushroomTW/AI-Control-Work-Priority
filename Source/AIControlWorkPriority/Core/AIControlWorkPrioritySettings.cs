using System;
using System.Collections.Generic;
using Verse;

namespace AIControlWorkPriority
{
    public class FallbackConfig
    {
        public string provider = "Gemini";
        public string modelName = "";
        public string apiKey = "";
        public string baseUrl = "";
        public bool enabled = true;
    }

    public class AIControlWorkPrioritySettings : ModSettings
    {
        public bool enabled = true;
        public int updateIntervalHours = 12;
        public string strategyProfile = "平衡發展";
        public string llmProvider = "Gemini";
        public string apiKey = "";
        public string modelName = "gemini-1.5-flash";
        public string baseUrl = "";
        
        public int timeoutSeconds = 30;
        public int maxRetryCount = 3;
        public int retryIntervalSeconds = 3;
        
        // 儲存為 JSON 的 Fallback 清單，將在後續由 JsonSerializationService 解析與保存
        public string fallbackConfigsJson = "[]";
        
        // 短期狀態勾選
        public bool includeHunger = true;
        public bool includeMood = true;
        public bool includeSleep = true;
        public bool includeEquipment = true;
        public bool includeCurrentTask = true;
        public bool includeDanger = true;
        public bool includeDisease = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref updateIntervalHours, "updateIntervalHours", 12);
            Scribe_Values.Look(ref strategyProfile, "strategyProfile", "平衡發展");
            Scribe_Values.Look(ref llmProvider, "llmProvider", "Gemini");
            Scribe_Values.Look(ref apiKey, "apiKey", "");
            Scribe_Values.Look(ref modelName, "modelName", "gemini-1.5-flash");
            Scribe_Values.Look(ref baseUrl, "baseUrl", "");
            Scribe_Values.Look(ref timeoutSeconds, "timeoutSeconds", 30);
            Scribe_Values.Look(ref maxRetryCount, "maxRetryCount", 3);
            Scribe_Values.Look(ref retryIntervalSeconds, "retryIntervalSeconds", 3);
            Scribe_Values.Look(ref fallbackConfigsJson, "fallbackConfigsJson", "[]");
            
            Scribe_Values.Look(ref includeHunger, "includeHunger", true);
            Scribe_Values.Look(ref includeMood, "includeMood", true);
            Scribe_Values.Look(ref includeSleep, "includeSleep", true);
            Scribe_Values.Look(ref includeEquipment, "includeEquipment", true);
            Scribe_Values.Look(ref includeCurrentTask, "includeCurrentTask", true);
            Scribe_Values.Look(ref includeDanger, "includeDanger", true);
            Scribe_Values.Look(ref includeDisease, "includeDisease", true);
        }
    }
}
