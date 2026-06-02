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

        // 用於 Fallback JSON 的編輯快取
        private string fallbackJsonEditBuffer = "";
        private bool isFallbackJsonDirty = true;

        public AIControlWorkPriorityMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<AIControlWorkPrioritySettings>();
            
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
            float tabWidth = inRect.width / 4f;

            if (Widgets.ButtonText(new Rect(tabRect.x + 0 * tabWidth, tabRect.y, tabWidth - 5f, tabRect.height), "核心 API 設定", true, true, activeTab == 0))
            {
                activeTab = 0;
            }
            if (Widgets.ButtonText(new Rect(tabRect.x + 1 * tabWidth, tabRect.y, tabWidth - 5f, tabRect.height), "Fallback 模型", true, true, activeTab == 1))
            {
                activeTab = 1;
            }
            if (Widgets.ButtonText(new Rect(tabRect.x + 2 * tabWidth, tabRect.y, tabWidth - 5f, tabRect.height), "資料收集與策略", true, true, activeTab == 2))
            {
                activeTab = 2;
            }
            if (Widgets.ButtonText(new Rect(tabRect.x + 3 * tabWidth, tabRect.y, tabWidth - 5f, tabRect.height), "行動記錄與提醒", true, true, activeTab == 3))
            {
                activeTab = 3;
            }

            // 主內容區域
            Rect contentRect = new Rect(inRect.x, inRect.y + 45f, inRect.width, inRect.height - 45f);
            
            switch (activeTab)
            {
                case 0:
                    DrawCoreSettings(contentRect);
                    break;
                case 1:
                    DrawFallbackSettings(contentRect);
                    break;
                case 2:
                    DrawDataAndStrategySettings(contentRect);
                    break;
                case 3:
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

            // API 參數
            listing.Label($"API 供應商 (Provider): {Settings.llmProvider}");
            if (listing.ButtonText("選擇 API 供應商..."))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("Gemini", () => { Settings.llmProvider = "Gemini"; Settings.modelName = "gemini-1.5-flash"; }),
                    new FloatMenuOption("OpenAI", () => { Settings.llmProvider = "OpenAI"; Settings.modelName = "gpt-4o-mini"; }),
                    new FloatMenuOption("OpenAI-Compatible", () => { Settings.llmProvider = "OpenAI-Compatible"; Settings.modelName = ""; })
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }
            listing.Gap(10f);

            listing.Label("模型名稱 (Model Name):");
            Settings.modelName = listing.TextEntry(Settings.modelName).Trim();

            listing.Label("API 金鑰 (API Key):");
            Settings.apiKey = listing.TextEntry(Settings.apiKey).Trim();

            if (Settings.llmProvider == "OpenAI-Compatible")
            {
                listing.Label("自訂端點 URL (Base URL):");
                Settings.baseUrl = listing.TextEntry(Settings.baseUrl).Trim();
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
            listing.Gap(10f);

            listing.Label($"API 逾時時間: {Settings.timeoutSeconds} 秒");
            Settings.timeoutSeconds = (int)listing.Slider(Settings.timeoutSeconds, 5f, 120f);

            listing.Label($"單模型最多重試次數: {Settings.maxRetryCount} 次");
            Settings.maxRetryCount = (int)listing.Slider(Settings.maxRetryCount, 1f, 10f);

            listing.Label($"重試間隔: {Settings.retryIntervalSeconds} 秒");
            Settings.retryIntervalSeconds = (int)listing.Slider(Settings.retryIntervalSeconds, 1f, 30f);

            // 立即調度測試按鈕 (需要遊戲在運行中才能點擊)
            listing.Gap(20f);
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

        private void DrawFallbackSettings(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            Text.Font = GameFont.Medium;
            listing.Label("多模型備份鏈 (Fallback Chain) 設定：");
            Text.Font = GameFont.Small;
            listing.Label("當主要模型失敗且重試耗盡時，系統會依序嘗試以下備份模型。請以 JSON 陣列格式編輯：");
            listing.Gap(10f);

            if (isFallbackJsonDirty)
            {
                fallbackJsonEditBuffer = Settings.fallbackConfigsJson;
                isFallbackJsonDirty = false;
            }

            // 提供一個大的 TextArea 讓用戶編輯 JSON，並進行錯誤防護
            Rect jsonRect = listing.GetRect(250f);
            fallbackJsonEditBuffer = Widgets.TextArea(jsonRect, fallbackJsonEditBuffer);

            Rect btnRect = listing.GetRect(35f);
            Rect btnLeft = new Rect(btnRect.x, btnRect.y, 150f, btnRect.height);
            Rect btnRight = new Rect(btnRect.x + 160f, btnRect.y, 150f, btnRect.height);

            if (Widgets.ButtonText(btnLeft, "驗證並套用"))
            {
                // 我們將在 JsonSerializationService 實作後提供完整驗證
                // 這裡暫時檢查基本的括號
                if (string.IsNullOrEmpty(fallbackJsonEditBuffer) || (fallbackJsonEditBuffer.StartsWith("[") && fallbackJsonEditBuffer.EndsWith("]")))
                {
                    Settings.fallbackConfigsJson = fallbackJsonEditBuffer;
                    Settings.Write();
                    Find.WindowStack.Add(new Dialog_MessageBox("已成功套用 Fallback 設定！"));
                }
                else
                {
                    Find.WindowStack.Add(new Dialog_MessageBox("JSON 格式不正確，必須以 [ 開頭並以 ] 結尾。"));
                }
            }

            if (Widgets.ButtonText(btnRight, "重置為預設"))
            {
                fallbackJsonEditBuffer = "[\n  {\n    \"provider\": \"Gemini\",\n    \"modelName\": \"gemini-1.5-flash\",\n    \"apiKey\": \"YOUR_BACKUP_KEY_HERE\",\n    \"baseUrl\": \"\",\n    \"enabled\": true\n  }\n]";
                Settings.fallbackConfigsJson = fallbackJsonEditBuffer;
                Settings.Write();
            }

            listing.Gap(15f);
            Text.Font = GameFont.Tiny;
            listing.Label("欄位說明：");
            listing.Label("- provider: Gemini, OpenAI, 或 OpenAI-Compatible\n- modelName: 備份模型名稱\n- apiKey: 對應該模型的金鑰（為空則共用主要金鑰）\n- baseUrl: 自訂端點（僅 OpenAI-Compatible 使用）\n- enabled: 是否啟用本項 (true/false)");
            Text.Font = GameFont.Small;

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
    }
}
