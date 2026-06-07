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

            // 1. 解析 summary (容錯: summary, Summary, decision_summary, description)
            var summaryToken = plan["summary"] ?? plan["Summary"] ?? plan["decision_summary"] ?? plan["description"];
            log.summary = summaryToken?.ToString() ?? "AI 無提供決策摘要。";

            // 2. 解析 alerts (容錯: alerts, Alerts, warnings, Warnings)
            log.alerts = new List<string>();
            var alertsArray = (plan["alerts"] ?? plan["Alerts"] ?? plan["warnings"] ?? plan["Warnings"]) as JArray;
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

            // 3. 解析 changes (容錯: changes, Changes, changes_list, changesList, workPriorityChanges, work_priority_changes, updates, Updates)
            var changesArray = (plan["changes"] ?? plan["Changes"] ?? plan["changes_list"] ?? plan["changesList"] ?? plan["workPriorityChanges"] ?? plan["work_priority_changes"] ?? plan["updates"] ?? plan["Updates"]) as JArray;
            if (changesArray == null)
            {
                log.errors.Add("計畫中缺少 changes 變更清單或格式不正確。");
                Log.Warning("[AIControlWorkPriority] 計畫中缺少 changes 變更清單或格式不正確。完整的 JSON 內容如下:\n" + plan.ToString());
                return false;
            }

            // 3.5 智慧型平鋪化（Flattening）處理：支援平鋪與 Pawn-workSettings 分組兩種主流 JSON 結構
            List<JToken> flattenedChanges = new List<JToken>();
            foreach (var item in changesArray)
            {
                if (item == null) continue;

                // 檢查是否為 Pawn-workSettings 分組結構
                var workSettingsToken = item["workSettings"] ?? item["WorkSettings"] ?? item["work_settings"] ?? item["priorities"] ?? item["Priorities"];
                if (workSettingsToken is JObject workSettingsObj)
                {
                    string pawnId = (item["pawnId"] ?? item["PawnId"] ?? item["pawn_id"])?.ToString();
                    if (string.IsNullOrEmpty(pawnId)) continue;

                    foreach (var property in workSettingsObj.Properties())
                    {
                        JObject flatChange = new JObject();
                        flatChange["pawnId"] = pawnId;
                        flatChange["workTypeDefName"] = property.Name;
                        flatChange["priority"] = property.Value;
                        flatChange["reason"] = (item["reason"] ?? item["Reason"] ?? "Grouped update")?.ToString();
                        
                        flattenedChanges.Add(flatChange);
                    }
                }
                else
                {
                    // 已經是平鋪結構，直接加入
                    flattenedChanges.Add(item);
                }
            }

            Log.Message($"[AIControlWorkPriority] 開始套用 AI 工作優先級計畫，原始項目數: {changesArray.Count}，解析平鋪後項目數: {flattenedChanges.Count}。決策摘要: {log.summary}");

            int successCount = 0;
            HashSet<Pawn> affectedPawns = new HashSet<Pawn>();

            foreach (var change in flattenedChanges)
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

            Log.Message($"[AIControlWorkPriority] 計畫套用完成。成功套用: {successCount} 筆，拒絕/失敗: {flattenedChanges.Count - successCount} 筆。");
            if (log.errors.Count > 0)
            {
                Log.Warning("[AIControlWorkPriority] 套用過程中的拒絕與錯誤日誌:\n" + string.Join("\n", log.errors));
            }

            log.appliedCount = successCount;
            return true;
        }
    }
}
