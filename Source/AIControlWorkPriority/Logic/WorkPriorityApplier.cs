using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using RimWorld;
using Verse;

namespace AIControlWorkPriority
{
    public static class WorkPriorityApplier
    {
        public static bool ApplyPlan(JObject plan, LogEntry log)
        {
            if (plan == null)
            {
                log.errors.Add("套用計畫失敗：計畫 JSON 物件為空 (null)");
                return false;
            }

            // 1. 解析 summary
            log.summary = plan["summary"]?.ToString() ?? "AI 無提供決策摘要。";

            // 2. 解析 alerts
            log.alerts = new List<string>();
            var alertsArray = plan["alerts"] as JArray;
            if (alertsArray != null)
            {
                foreach (var alert in alertsArray)
                {
                    if (alert != null)
                    {
                        log.alerts.Add(alert.ToString());
                    }
                }
            }

            // 3. 解析 changes
            var changesArray = plan["changes"] as JArray;
            if (changesArray == null)
            {
                log.errors.Add("計畫中缺少 changes 變更清單或格式不正確。");
                return false;
            }

            int successCount = 0;
            HashSet<Pawn> affectedPawns = new HashSet<Pawn>();

            foreach (var change in changesArray)
            {
                Pawn pawn;
                WorkTypeDef workType;
                int priority;
                string errorReason;

                if (WorkPriorityPlanValidator.ValidateChange(change, out pawn, out workType, out priority, out errorReason))
                {
                    try
                    {
                        if (pawn.workSettings == null)
                        {
                            log.errors.Add($"殖民者 {pawn.LabelShort} 的 workSettings 為空，無法寫入。");
                            continue;
                        }

                        // 確保開啟手動工作優先級 (Manual Priorities)
                        if (Find.PlaySettings != null && !Find.PlaySettings.useWorkPriorities)
                        {
                            Find.PlaySettings.useWorkPriorities = true;
                        }

                        pawn.workSettings.SetPriority(workType, priority);
                        affectedPawns.Add(pawn);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        log.errors.Add($"套用 {pawn?.LabelShort} 的 {workType?.labelShort} 優先級失敗: {ex.Message}");
                    }
                }
                else
                {
                    log.errors.Add($"項目被拒絕: {errorReason}");
                }
            }

            // 4. 通知受影響的 Pawn 重新建置優先級快取
            foreach (var pawn in affectedPawns)
            {
                try
                {
                    pawn.workSettings.Notify_UseWorkPrioritiesChanged();
                }
                catch (Exception ex)
                {
                    Log.Error($"[AIControlWorkPriority] 通知 {pawn.LabelShort} 工作優先級改變出錯: " + ex.Message);
                }
            }

            log.appliedCount = successCount;
            return true;
        }
    }
}
