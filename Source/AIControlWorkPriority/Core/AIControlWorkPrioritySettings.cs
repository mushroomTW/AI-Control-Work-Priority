using System;
using System.Collections.Generic;
using Verse;

namespace AIControlWorkPriority
{
    public class AIControlWorkPrioritySettings : ModSettings
    {
        public bool enabled = true;
        public int updateIntervalHours = 12;
        public string strategyProfile = "平衡發展";
        public bool showCountdown = true;
        
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
            Scribe_Values.Look(ref showCountdown, "showCountdown", true);
            
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
