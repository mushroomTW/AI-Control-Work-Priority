using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using HarmonyLib;
using RimWorld;

namespace AIControlWorkPriority
{
    public class AIControlWorkPriorityMod : Mod
    {
        public static AIControlWorkPrioritySettings Settings;
        private static int activeTab = 0;
        private Vector2 scrollPosition = Vector2.zero;

        public AIControlWorkPriorityMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<AIControlWorkPrioritySettings>();
            
            // 註冊客戶端 Mod 到 RimLLM Framework
            try
            {
                RegisterClientSafe();
            }
            catch (Exception ex)
            {
                Log.Warning("[AIControlWorkPriority] 註冊至 RimLLM Framework 失敗 (可能該 Mod 未啟用): " + ex.Message);
            }

            // 初始化 Harmony Patches
            try
            {
                var harmony = new Harmony("antigravity.aicontrolworkpriority");
                harmony.PatchAll();
                Log.Message("[AIControlWorkPriority] Harmony Patches 成功套用。");
            }
            catch (Exception ex)
            {
                Log.Error("[AIControlWorkPriority] Harmony Patches 套用失敗: " + ex);
            }
        }

        public override string SettingsCategory()
        {
            return "AI 控制工作優先級";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // 繪製 Tab 按鈕
            Rect tabRect = new Rect(inRect.x, inRect.y, inRect.width, 35f);
            float tabWidth = inRect.width / 3f;

            if (Widgets.ButtonText(new Rect(tabRect.x + 0 * tabWidth, tabRect.y, tabWidth - 5f, tabRect.height), "核心調度設定", true, true, activeTab != 0))
            {
                activeTab = 0;
            }
            if (Widgets.ButtonText(new Rect(tabRect.x + 1 * tabWidth, tabRect.y, tabWidth - 5f, tabRect.height), "資料收集與策略", true, true, activeTab != 1))
            {
                activeTab = 1;
            }
            if (Widgets.ButtonText(new Rect(tabRect.x + 2 * tabWidth, tabRect.y, tabWidth - 5f, tabRect.height), "行動記錄與提醒", true, true, activeTab != 2))
            {
                activeTab = 2;
            }

            // 主內容區域
            Rect contentRect = new Rect(inRect.x, inRect.y + 45f, inRect.width, inRect.height - 45f);
            
            switch (activeTab)
            {
                case 0:
                    DrawCoreSettings(contentRect);
                    break;
                case 1:
                    DrawDataAndStrategySettings(contentRect);
                    break;
                case 2:
                    DrawActionLogs(contentRect);
                    break;
            }

            base.DoSettingsWindowContents(inRect);
        }

        private void DrawCoreSettings(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            listing.CheckboxLabeled("啟用 AI 自動調度", ref Settings.enabled, "開啟後，AI 會按設定的時間間隔自動執行調度。");
            listing.Gap(10f);
            listing.CheckboxLabeled("顯示 AI 調度倒計時提示", ref Settings.showCountdown, "在遊戲畫面右側提示欄中顯示距離下次自動調度的剩餘遊戲時間。");
            listing.Gap(10f);

            // 策略方針選擇
            listing.Label($"當前策略方針: {Settings.strategyProfile}");
            if (listing.ButtonText("選擇策略方針..."))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("平衡發展", () => Settings.strategyProfile = "平衡發展"),
                    new FloatMenuOption("保糧優先", () => Settings.strategyProfile = "保糧優先"),
                    new FloatMenuOption("醫療與照護優先", () => Settings.strategyProfile = "醫療與照護優先"),
                    new FloatMenuOption("建設優先", () => Settings.strategyProfile = "建設優先"),
                    new FloatMenuOption("研究優先", () => Settings.strategyProfile = "研究優先"),
                    new FloatMenuOption("戰後恢復", () => Settings.strategyProfile = "戰後恢復"),
                    new FloatMenuOption("防禦整備", () => Settings.strategyProfile = "防禦整備")
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }
            listing.Gap(15f);

            // 調度設定
            listing.Label($"自動調度評估間隔: {Settings.updateIntervalHours} 遊戲小時");
            if (listing.ButtonText("選擇評估間隔..."))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("6 遊戲小時", () => Settings.updateIntervalHours = 6),
                    new FloatMenuOption("12 遊戲小時", () => Settings.updateIntervalHours = 12),
                    new FloatMenuOption("24 遊戲小時", () => Settings.updateIntervalHours = 24),
                    new FloatMenuOption("48 遊戲小時", () => Settings.updateIntervalHours = 48)
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }
            listing.Gap(20f);

            // 立即調度測試按鈕 (需要遊戲在運行中才能點擊)
            if (Current.ProgramState == ProgramState.Playing)
            {
                if (listing.ButtonText("立即觸發 AI 調度"))
                {
                    try
                    {
                        var director = WorkPriorityAIDirector.GetInstance();
                        if (director != null)
                        {
                            director.TriggerManualDispatch();
                            Messages.Message("已送出 AI 調度請求，請查看「行動記錄」分頁。", MessageTypeDefOf.TaskCompletion, false);
                        }
                        else
                        {
                            Messages.Message("無法取得 AI 調度協調器，請確認遊戲已正常載入。", MessageTypeDefOf.RejectInput, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[AIControlWorkPriority] 立即調度失敗: " + ex.Message);
                    }
                }
            }
            else
            {
                Text.Font = GameFont.Tiny;
                listing.Label("提示：載入存檔並進入遊戲後，才能點擊「立即觸發 AI 調度」。");
                Text.Font = GameFont.Small;
            }

            listing.End();
        }

        private void DrawDataAndStrategySettings(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            Text.Font = GameFont.Medium;
            listing.Label("收集殖民者短期狀態（可勾選是否納入 AI 評估的考量）：");
            Text.Font = GameFont.Small;
            listing.Gap(10f);

            listing.CheckboxLabeled("飢餓狀態 (Hunger)", ref Settings.includeHunger, "是否將殖民者的飢餓度與營養攝取狀態傳送給 AI。");
            listing.CheckboxLabeled("心情狀態 (Mood)", ref Settings.includeMood, "是否將殖民者的心情值與精神崩潰邊緣狀態傳送給 AI。");
            listing.CheckboxLabeled("睡眠狀態 (Sleep)", ref Settings.includeSleep, "是否將殖民者的睡眠與疲勞度傳送給 AI。");
            listing.CheckboxLabeled("武裝裝備 (Equipment)", ref Settings.includeEquipment, "是否將殖民者裝備的武器與服裝資訊傳送給 AI。");
            listing.CheckboxLabeled("當前工作任務 (Current Task)", ref Settings.includeCurrentTask, "是否將殖民者當前正在執行的工作與任務傳送給 AI。");
            listing.CheckboxLabeled("危險與威脅狀態 (Danger & Threat)", ref Settings.includeDanger, "是否將地圖威脅、襲擊、高溫寒流等危險狀態傳送給 AI。");
            listing.CheckboxLabeled("疾病與近期受傷 (Disease & Injury)", ref Settings.includeDisease, "是否將殖民者的受傷、感染與生病狀態傳送給 AI。");
            
            listing.Gap(20f);
            Text.Font = GameFont.Medium;
            listing.Label("Prompt 模板設定：");
            Text.Font = GameFont.Small;
            listing.Gap(5f);
            
            if (listing.ButtonText("重新載入 Prompt 檔案"))
            {
                // 我們將在 PromptTemplateLoader 實作後提供此功能
                Find.WindowStack.Add(new Dialog_MessageBox("已觸發 Prompt 檔案重新載入。"));
            }

            listing.End();
        }

        private void DrawActionLogs(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            Text.Font = GameFont.Medium;
            listing.Label("最近的 AI 調度行動記錄與提醒：");
            Text.Font = GameFont.Small;
            listing.Gap(10f);

            Rect scrollOutRect = listing.GetRect(rect.height - 80f);
            
            string displayLogs = "行動記錄未載入。\n請先進入遊戲並執行調度。";
            
            if (Current.ProgramState == ProgramState.Playing)
            {
                var store = AIActionLogStore.GetInstance();
                if (store != null)
                {
                    displayLogs = store.GetFormattedLogs();
                }
            }

            float textHeight = Math.Max(400f, Text.CalcHeight(displayLogs, scrollOutRect.width - 25f));
            Rect scrollViewRect = new Rect(0f, 0f, scrollOutRect.width - 20f, textHeight);

            Widgets.BeginScrollView(scrollOutRect, ref scrollPosition, scrollViewRect, true);
            Widgets.Label(scrollViewRect, displayLogs);
            Widgets.EndScrollView();

            listing.End();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void RegisterClientSafe()
        {
            RimLLM_Framework.Core.ClientRegistry.RegisterClient("ai.control.work.priority", typeof(AIControlWorkPriorityMod).Assembly);
            Log.Message("[AIControlWorkPriority] 顯式安全註冊至 RimLLM Framework 成功。");
        }
    }
}
