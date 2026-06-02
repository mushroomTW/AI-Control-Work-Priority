using System;
using Newtonsoft.Json.Linq;
using RimWorld;
using Verse;

namespace AIControlWorkPriority
{
    public static class WorkPriorityPlanValidator
    {
        public static bool ValidateChange(JToken change, out Pawn pawn, out WorkTypeDef workType, out int priority, out string errorReason)
        {
            pawn = null;
            workType = null;
            priority = 0;
            errorReason = null;

            if (change == null)
            {
                errorReason = "變更變量為空 (null)";
                return false;
            }

            // 1. 解析 pawnId
            string pawnId = change["pawnId"]?.ToString();
            if (string.IsNullOrEmpty(pawnId))
            {
                errorReason = "變更變量缺少 pawnId";
                return false;
            }

            pawn = FindPawnById(pawnId);
            if (pawn == null)
            {
                errorReason = $"找不到 ThingID 為 {pawnId} 的殖民者";
                return false;
            }

            if (pawn.Destroyed || pawn.Dead)
            {
                errorReason = $"殖民者 {pawn.LabelShort} 已死亡或被銷毀";
                return false;
            }

            // 2. 解析 workTypeDefName
            string workTypeName = change["workTypeDefName"]?.ToString();
            if (string.IsNullOrEmpty(workTypeName))
            {
                errorReason = "變更變量缺少 workTypeDefName";
                return false;
            }

            workType = DefDatabase<WorkTypeDef>.GetNamed(workTypeName, false);
            if (workType == null)
            {
                errorReason = $"找不到名為 {workTypeName} 的工作類型";
                return false;
            }

            // 3. 檢查殖民者是否可執行該工作
            if (pawn.WorkTypeIsDisabled(workType))
            {
                errorReason = $"殖民者 {pawn.LabelShort} 無法執行工作 {workType.labelShort}";
                return false;
            }

            // 4. 解析 priority
            var priorityToken = change["priority"];
            if (priorityToken == null || !int.TryParse(priorityToken.ToString(), out priority))
            {
                errorReason = "priority 欄位缺失或並非整數";
                return false;
            }

            if (priority < 1 || priority > 4)
            {
                errorReason = $"優先級 {priority} 不在原版範圍 1..4";
                return false;
            }

            // 5. 檢查該格子是否為 AI 託管狀態
            var stateStore = AIControlStateStore.GetInstance();
            if (stateStore == null || !stateStore.IsManaged(pawnId, workTypeName))
            {
                errorReason = $"殖民者 {pawn.LabelShort} 的 {workType.labelShort} 工作格非 AI 託管（目前為手動鎖定）";
                return false;
            }

            return true;
        }

        private static Pawn FindPawnById(string thingId)
        {
            if (Current.Game == null) return null;
            
            foreach (var map in Find.Maps)
            {
                foreach (var p in map.mapPawns.AllPawns)
                {
                    if (p.ThingID == thingId) return p;
                }
            }

            if (Find.World != null)
            {
                foreach (var caravan in Find.WorldObjects.Caravans)
                {
                    foreach (var p in caravan.PawnsListForReading)
                    {
                        if (p.ThingID == thingId) return p;
                    }
                }
            }

            return null;
        }
    }
}
