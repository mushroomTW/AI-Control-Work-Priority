using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace AIControlWorkPriority
{
    public interface IColonyDataTool
    {
        string ToolName { get; }
        object CollectData(Map map);
    }

    public class ColonyStateCollector
    {
        public static string Collect(Map map)
        {
            if (map == null) return "{}";

            var settings = AIControlWorkPriorityMod.Settings;
            var stateStore = AIControlStateStore.GetInstance();

            var root = new Dictionary<string, object>();
            root["strategy"] = settings.strategyProfile;
            root["priorityRange"] = new Dictionary<string, object>
            {
                { "min", 1 },
                { "max", 4 },
                { "oneMeansHighest", true }
            };

            // 收集殖民地全域信號 (Signals)
            var signals = new Dictionary<string, object>();
            
            // 計算食物儲量
            int foodCount = 0;
            try
            {
                foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSource))
                {
                    if (t.def.IsNutritionGivingIngestible)
                    {
                        foodCount += t.stackCount;
                    }
                }
            }
            catch {}
            signals["foodCount"] = foodCount;

            // 計算藥物儲量
            int medCount = 0;
            try
            {
                foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Medicine))
                {
                    medCount += t.stackCount;
                }
            }
            catch {}
            signals["medicineCount"] = medCount;

            // 地圖全域威脅
            signals["largeFireDanger"] = map.fireWatcher?.LargeFireDangerPresent ?? false;
            signals["dangerRating"] = map.dangerWatcher?.DangerRating.ToString() ?? "None";

            root["colonySignals"] = signals;

            // 工作類型定義對照表
            var workTypes = new List<object>();
            foreach (var wt in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                var wtData = new Dictionary<string, object>
                {
                    { "defName", wt.defName },
                    { "label", wt.labelShort },
                    { "description", wt.description }
                };
                if (wt.relevantSkills != null)
                {
                    var skills = new List<string>();
                    foreach (var sk in wt.relevantSkills)
                    {
                        skills.Add(sk.defName);
                    }
                    wtData["relevantSkills"] = skills;
                }
                workTypes.Add(wtData);
            }
            root["workTypes"] = workTypes;

            // 殖民者資訊
            var pawns = new List<object>();
            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                if (pawn == null) continue;

                var pawnData = new Dictionary<string, object>();
                pawnData["id"] = pawn.ThingID;
                pawnData["name"] = pawn.Name?.ToStringFull ?? pawn.LabelShort;
                pawnData["age"] = pawn.ageTracker?.AgeBiologicalYears ?? 0;

                // 技能與熱情
                var skills = new List<object>();
                if (pawn.skills != null)
                {
                    foreach (var sk in pawn.skills.skills)
                    {
                        skills.Add(new Dictionary<string, object>
                        {
                            { "defName", sk.def.defName },
                            { "level", sk.Level },
                            { "passion", sk.passion.ToString() }
                        });
                    }
                }
                pawnData["skills"] = skills;

                // 禁用工作
                var incapable = new List<string>();
                foreach (var wt in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                {
                    if (pawn.WorkTypeIsDisabled(wt))
                    {
                        incapable.Add(wt.defName);
                    }
                }
                pawnData["incapableOf"] = incapable;

                // 目前優先級與託管狀態
                var workPriorities = new Dictionary<string, object>();
                foreach (var wt in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                {
                    if (pawn.WorkTypeIsDisabled(wt)) continue;
                    
                    int currentPriority = pawn.workSettings?.GetPriority(wt) ?? 0;
                    bool isManaged = stateStore?.IsManaged(pawn.ThingID, wt.defName) ?? false;

                    workPriorities[wt.defName] = new Dictionary<string, object>
                    {
                        { "priority", currentPriority },
                        { "isManaged", isManaged }
                    };
                }
                pawnData["workSettings"] = workPriorities;

                // 健康狀況
                var healthData = new Dictionary<string, object>();
                if (pawn.health != null)
                {
                    healthData["summary"] = pawn.health.summaryHealth?.SummaryHealthPercent.ToString("P0") ?? "100%";
                    healthData["pain"] = pawn.health.hediffSet?.PainTotal.ToString("P0") ?? "0%";
                    healthData["isDowned"] = pawn.Downed;

                    var hediffs = new List<string>();
                    foreach (var hediff in pawn.health.hediffSet.hediffs)
                    {
                        if (hediff.Visible)
                        {
                            hediffs.Add(hediff.LabelCap);
                        }
                    }
                    healthData["hediffs"] = hediffs;
                }
                pawnData["health"] = healthData;

                // 短期狀態勾選納入
                var shortTerm = new Dictionary<string, object>();
                if (settings.includeHunger && pawn.needs?.food != null)
                {
                    shortTerm["hungerPercent"] = pawn.needs.food.CurLevelPercentage.ToString("P0");
                }
                if (settings.includeMood && pawn.needs?.mood != null)
                {
                    shortTerm["moodPercent"] = pawn.needs.mood.CurLevelPercentage.ToString("P0");
                    shortTerm["mentalState"] = pawn.InMentalState ? pawn.MentalState.def.defName : "None";
                }
                if (settings.includeSleep && pawn.needs?.rest != null)
                {
                    shortTerm["sleepPercent"] = pawn.needs.rest.CurLevelPercentage.ToString("P0");
                }
                if (settings.includeEquipment)
                {
                    shortTerm["weapon"] = pawn.equipment?.Primary?.Label ?? "None";
                }
                if (settings.includeCurrentTask)
                {
                    shortTerm["currentJob"] = pawn.CurJob?.def.defName ?? "None";
                }

                pawnData["shortTermState"] = shortTerm;
                pawns.Add(pawnData);
            }
            root["pawns"] = pawns;

            return JsonSerializationService.Serialize(root);
        }
    }
}
