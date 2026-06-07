using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using RimWorld;
using RimLLM_Framework.SDK;

namespace AIControlWorkPriority
{
    public class WorkPriorityAIDirector : GameComponent
    {
        public static WorkPriorityAIDirector Instance;

        private int lastTriggerTick = 0;
        private bool isQuerying = false;
        private float lastManualTriggerRealTime = -999f;
        
        public bool IsQuerying => isQuerying;
        public int LastTriggerTick => lastTriggerTick;
        
        // 第一次載入遊戲後的延遲評估 ticks (約 2 遊戲小時)
        private const int InitDelayTicks = 5000;
        private bool initialized = false;

        public WorkPriorityAIDirector(Game game)
        {
            Instance = this;
        }

        public static WorkPriorityAIDirector GetInstance()
        {
            if (Instance == null && Current.Game != null)
            {
                Instance = Current.Game.GetComponent<WorkPriorityAIDirector>();
            }
            return Instance;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastTriggerTick, "lastTriggerTick", 0);
            Scribe_Values.Look(ref initialized, "initialized", false);
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            if (!AIControlWorkPriorityMod.Settings.enabled) return;

            int currentTick = Find.TickManager.TicksGame;

            if (!initialized)
            {
                lastTriggerTick = currentTick + InitDelayTicks - (AIControlWorkPriorityMod.Settings.updateIntervalHours * 2500);
                initialized = true;
            }

            int intervalTicks = AIControlWorkPriorityMod.Settings.updateIntervalHours * 2500;
            if (currentTick - lastTriggerTick >= intervalTicks)
            {
                lastTriggerTick = currentTick;
                TriggerAIDispatchAsync(false);
            }
        }

        public void TriggerManualDispatch()
        {
            float curTime = RealTime.LastRealTime;
            if (curTime - lastManualTriggerRealTime < 30f)
            {
                float remain = 30f - (curTime - lastManualTriggerRealTime);
                Messages.Message($"AI 調度冷卻中，請等待 {remain:F0} 秒。", MessageTypeDefOf.RejectInput, false);
                return;
            }

            lastManualTriggerRealTime = curTime;
            TriggerAIDispatchAsync(true);
        }

        public async void TriggerAIDispatchAsync(bool isManual)
        {
            if (isQuerying)
            {
                if (isManual)
                {
                    Messages.Message("AI 正在調度中，請勿重複觸發。", MessageTypeDefOf.RejectInput, false);
                }
                return;
            }

            isQuerying = true;
            
            Map currentMap = Find.CurrentMap;
            if (currentMap == null)
            {
                isQuerying = false;
                if (isManual) Messages.Message("找不到當前地圖，無法調度。", MessageTypeDefOf.RejectInput, false);
                return;
            }

            // 1. 在主執行緒收集狀態與 Prompt，保證線程安全
            string strategy = AIControlWorkPriorityMod.Settings.strategyProfile;
            string colonyStateJson = ColonyStateCollector.Collect(currentMap);
            string systemPrompt = PromptTemplateLoader.LoadSystemPrompt();
            string userPrompt = PromptTemplateLoader.LoadUserPrompt(strategy, colonyStateJson);

            LogEntry log = new LogEntry
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                provider = "RimLLM Framework",
                modelName = "Auto-managed",
                status = "Failed"
            };

            string response = null;
            bool success = false;

            try
            {
                var request = new LLMRequest
                {
                    ModId = "ai.control.work.priority",
                    Prompt = userPrompt,
                    SystemPrompt = systemPrompt,
                    Temperature = 0.7f,
                    MaxTokens = 4000
                };

                // 在主執行緒上發起非同步呼叫，這能保證 Assembly.GetCallingAssembly() 正確判定為我們的組件
                response = await RimLLMProvider.Instance.GenerateAsync(request);
                if (!string.IsNullOrEmpty(response))
                {
                    Log.Message("[AIControlWorkPriority] 收到原始 LLM 回覆內容:\n" + response);
                    success = true;
                }
            }
            catch (Exception ex)
            {
                log.errors.Add("LLM 呼叫異常: " + ex.Message);
                if (ex.InnerException != null)
                {
                    log.errors.Add("底層錯誤: " + ex.InnerException.Message);
                }
            }

            try
            {
                // 3. 處理呼叫結果，利用佇列送回主執行緒安全套用
                if (success && !string.IsNullOrEmpty(response))
                {
                    Log.Message($"[AIControlWorkPriority] 背景調度呼叫成功，收到回覆，長度: {response.Length}。準備解析...");
                    var plan = JsonSerializationService.ParseJObject(response);
                    if (plan != null)
                    {
                        AIControlStateStore.GetInstance().EnqueueAction(() =>
                        {
                            try
                            {
                                // 主執行緒中的計畫驗證與優先級寫入
                                bool applySuccess = WorkPriorityApplier.ApplyPlan(plan, log);
                                if (applySuccess)
                                {
                                    log.status = "Success";
                                    AIActionLogStore.GetInstance().AddLog(log);
                                    Messages.Message($"AI 工作調度成功！套用了 {log.appliedCount} 筆變更。", MessageTypeDefOf.PositiveEvent, false);
                                }
                                else
                                {
                                    log.status = "Failed";
                                    AIActionLogStore.GetInstance().AddLog(log);
                                }
                            }
                            catch (Exception ex)
                            {
                                log.errors.Add("套用計畫時出錯: " + ex.Message);
                                AIActionLogStore.GetInstance().AddLog(log);
                            }
                        });
                    }
                    else
                    {
                        log.errors.Add("JSON 解析失敗，回傳資料並非合法的 JSON 物件。");
                        AIActionLogStore.GetInstance().AddLog(log);
                    }
                }
                else
                {
                    log.errors.Add("RimLLM Framework 未能回傳有效字串。");
                    string errStr = string.Join("\n", log.errors);
                    Log.Warning("[AIControlWorkPriority] RimLLM Framework 呼叫失敗。詳細錯誤如下:\n" + errStr);
                    AIActionLogStore.GetInstance().AddLog(log);
                }
            }
            catch (Exception ex)
            {
                log.errors.Add("背景執行緒發生未預期異常: " + ex.Message);
                AIActionLogStore.GetInstance().AddLog(log);
            }
            finally
            {
                isQuerying = false;
            }
        }
    }
}
