using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AIControlWorkPriority
{
    // Patch 點 1: 攔截工作格子的繪製與互動
    [HarmonyPatch(typeof(WidgetsWork), "DrawWorkBoxFor")]
    public static class Patch_WidgetsWork_DrawWorkBoxFor
    {
        [HarmonyPostfix]
        public static void Postfix(float x, float y, Pawn p, WorkTypeDef wType, bool incapableBecauseOfCapacities)
        {
            if (p == null || wType == null || incapableBecauseOfCapacities) return;

            // 格子的 Rect (RimWorld 原版工作格大小通常是 25x25)
            Rect rect = new Rect(x, y, 25f, 25f);
            string pawnId = p.ThingID;

            var stateStore = AIControlStateStore.GetInstance();
            if (stateStore == null) return;

            bool isManaged = stateStore.IsManaged(pawnId, wType.defName);

            // 1. 視覺標示：若是 AI 託管，覆蓋一層精緻的半透明淡藍色與細邊框
            if (isManaged)
            {
                // 淺藍色半透明底色
                Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.6f, 0.9f, 0.15f));
                
                // 藍色細邊框
                Color prevColor = GUI.color;
                GUI.color = new Color(0.2f, 0.6f, 0.9f, 0.5f);
                Widgets.DrawBox(rect, 1);
                GUI.color = prevColor;
            }

            // 2. 滑鼠懸停提示 (Tooltip)
            string tipText = isManaged 
                ? $"【AI 託管中】\n下次 AI 調度時可修改此優先級。\n右鍵點擊：切換為手動鎖定。" 
                : $"【手動鎖定中】\nAI 不會修改此優先級。\n右鍵點擊：切換為 AI 託管。";
            TooltipHandler.TipRegion(rect, new TipSignal(tipText, rect.GetHashCode()));

            // 3. 玩家左鍵點擊修改時，自動切回手動鎖定
            // 只要偵測到玩家在該格按下滑鼠左鍵，即代表玩家正在手動介入
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Mouse.IsOver(rect))
            {
                if (isManaged)
                {
                    stateStore.SetManaged(pawnId, wType.defName, false);
                    Messages.Message($"{p.LabelShort} 的 {wType.labelShort} 已切換為手動鎖定。", MessageTypeDefOf.CautionInput, false);
                }
            }

            // 4. 右鍵點擊彈出 FloatMenu 選單以切換狀態
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && Mouse.IsOver(rect))
            {
                Event.current.Use(); // 攔截右鍵事件防止原版選單彈出
                
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                if (isManaged)
                {
                    options.Add(new FloatMenuOption("切換為手動控制", () =>
                    {
                        stateStore.SetManaged(pawnId, wType.defName, false);
                        Messages.Message($"{p.LabelShort} 的 {wType.labelShort} 已切換為手動控制。", MessageTypeDefOf.CautionInput, false);
                    }));
                }
                else
                {
                    options.Add(new FloatMenuOption("交由 AI 託管", () =>
                    {
                        stateStore.SetManaged(pawnId, wType.defName, true);
                        Messages.Message($"{p.LabelShort} 的 {wType.labelShort} 已交由 AI 託管。", MessageTypeDefOf.PositiveEvent, false);
                    }));
                }
                
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }
    }

    // Patch 點 2: 在工作分頁 (Work Tab) 頂部右上方加入批次控制與立即調度按鈕
    [HarmonyPatch(typeof(MainTabWindow_Work), "DoWindowContents")]
    public static class Patch_MainTabWindow_Work_DoWindowContents
    {
        [HarmonyPostfix]
        public static void Postfix(Rect rect)
        {
            // 在右上方繪製控制按鈕 (原版左側有手動優先級 checkbox)
            float btnWidth = 100f;
            float btnHeight = 25f;
            float startX = rect.width - 330f;
            float startY = 10f;

            Rect btnAllManaged = new Rect(startX, startY, btnWidth, btnHeight);
            Rect btnAllManual = new Rect(startX + btnWidth + 5f, startY, btnWidth, btnHeight);
            Rect btnDispatch = new Rect(startX + 2 * (btnWidth + 5f), startY, btnWidth, btnHeight);

            // 繪製按鈕
            if (Widgets.ButtonText(btnAllManaged, "全部 AI 託管"))
            {
                var stateStore = AIControlStateStore.GetInstance();
                if (stateStore != null)
                {
                    stateStore.SetAllManagedInMap(true);
                    Messages.Message("已將當前地圖所有殖民者的所有工作格子交由 AI 託管。", MessageTypeDefOf.PositiveEvent, false);
                }
            }

            if (Widgets.ButtonText(btnAllManual, "全部手動"))
            {
                var stateStore = AIControlStateStore.GetInstance();
                if (stateStore != null)
                {
                    stateStore.SetAllManagedInMap(false);
                    Messages.Message("已將當前地圖所有殖民者的所有工作格子設為手動控制。", MessageTypeDefOf.CautionInput, false);
                }
            }

            if (Widgets.ButtonText(btnDispatch, "立即調度"))
            {
                var director = WorkPriorityAIDirector.GetInstance();
                if (director != null)
                {
                    director.TriggerManualDispatch();
                }
                else
                {
                    Messages.Message("無法取得 AI 調度協調器，請確認遊戲已正常載入。", MessageTypeDefOf.RejectInput, false);
                }
            }
        }
    }
}
