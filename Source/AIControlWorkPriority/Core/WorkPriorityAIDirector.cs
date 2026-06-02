using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using RimWorld;

namespace AIControlWorkPriority
{
    public class WorkPriorityAIDirector : GameComponent
    {
        public static WorkPriorityAIDirector Instance;

        private int lastTriggerTick = 0;
        private bool isQuerying = false;
        private float lastManualTriggerRealTime = -999f;
        
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

        public void TriggerAIDispatchAsync(bool isManual)
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

            // 複製 settings 參數防止異步執行期間被玩家在 UI 修改
            string mainProvider = AIControlWorkPriorityMod.Settings.llmProvider;
            string mainModel = AIControlWorkPriorityMod.Settings.modelName;
            string mainApiKey = AIControlWorkPriorityMod.Settings.apiKey;
            string mainBaseUrl = AIControlWorkPriorityMod.Settings.baseUrl;
            int timeout = AIControlWorkPriorityMod.Settings.timeoutSeconds;
            int maxRetries = AIControlWorkPriorityMod.Settings.maxRetryCount;
            int retryDelay = AIControlWorkPriorityMod.Settings.retryIntervalSeconds;
            string fallbackJson = AIControlWorkPriorityMod.Settings.fallbackConfigsJson;

            // 2. 異步背景執行緒呼叫
            Task.Run(async () =>
            {
                LogEntry log = new LogEntry
                {
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    provider = mainProvider,
                    modelName = mainModel,
                    status = "Failed"
                };

                string response = null;
                bool success = false;

                try
                {
                    // 嘗試主要模型
                    success = await TryCallLlmWithRetry(
                        mainProvider, systemPrompt, userPrompt, mainApiKey, mainModel, mainBaseUrl, 
                        timeout, maxRetries, retryDelay, log, (res) => response = res
                    );

                    // 失敗時，嘗試 Fallback Chain
                    if (!success && !string.IsNullOrEmpty(fallbackJson))
                    {
                        List<FallbackConfig> fallbacks = JsonSerializationService.Deserialize<List<FallbackConfig>>(fallbackJson);
                        if (fallbacks != null && fallbacks.Count > 0)
                        {
                            foreach (var fallback in fallbacks)
                            {
                                if (!fallback.enabled) continue;
                                
                                log.errors.Add($"主要模型嘗試失敗。切換至備用模型: {fallback.provider} ({fallback.modelName})");
                                string fKey = string.IsNullOrEmpty(fallback.apiKey) ? mainApiKey : fallback.apiKey;
                                
                                success = await TryCallLlmWithRetry(
                                    fallback.provider, systemPrompt, userPrompt, fKey, fallback.modelName, fallback.baseUrl, 
                                    timeout, maxRetries, retryDelay, log, (res) => response = res
                                );

                                if (success)
                                {
                                    log.provider = fallback.provider;
                                    log.modelName = fallback.modelName;
                                    break;
                                }
                            }
                        }
                    }

                    // 3. 處理呼叫結果，利用佇列送回主執行緒安全套用
                    if (success && !string.IsNullOrEmpty(response))
                    {
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
                        log.errors.Add("所有模型與 Fallback Chain 均嘗試失敗。");
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
            });
        }

        private async Task<bool> TryCallLlmWithRetry(
            string providerName, string systemPrompt, string userPrompt, string apiKey, string modelName, string baseUrl, 
            int timeoutSeconds, int maxRetries, int retryDelaySeconds, LogEntry log, Action<string> onResponse)
        {
            ILlmProvider provider;
            try
            {
                provider = LlmProviderFactory.GetProvider(providerName);
            }
            catch (Exception ex)
            {
                log.errors.Add($"建立 Provider {providerName} 失敗: {ex.Message}");
                return false;
            }

            int attempt = 0;
            while (attempt < maxRetries)
            {
                attempt++;
                try
                {
                    string result = await provider.CallLlmAsync(systemPrompt, userPrompt, apiKey, modelName, baseUrl, timeoutSeconds);
                    if (!string.IsNullOrEmpty(result))
                    {
                        onResponse(result);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    log.errors.Add($"{providerName} 第 {attempt} 次嘗試失敗: {ex.Message}");
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(retryDelaySeconds * 1000);
                    }
                }
            }

            return false;
        }
    }
}
