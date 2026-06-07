using UnityEngine;
using Verse;
using RimWorld;
namespace AIControlWorkPriority
{
    public class Alert_AIControlCountdown : Alert
    {
        public override AlertPriority Priority => AlertPriority.Medium;
        public Alert_AIControlCountdown()
        {
            this.defaultLabel = "AI 調度";
            this.defaultExplanation = "距離下一次 AI 自動進行優先級調度評估的剩餘時間。";
        }
        public override AlertReport GetReport()
        {
            if (!AIControlWorkPriorityMod.Settings.enabled || !AIControlWorkPriorityMod.Settings.showCountdown)
            {
                return AlertReport.Inactive;
            }
            if (Find.CurrentMap == null || Find.World.renderer.wantedMode != RimWorld.Planet.WorldRenderMode.None)
            {
                return AlertReport.Inactive;
            }
            return AlertReport.Active;
        }
        public override string GetLabel()
        {
            var director = WorkPriorityAIDirector.GetInstance();
            if (director == null) return "AI 調度";
            if (director.IsQuerying)
            {
                return "AI 調度: 評估中";
            }
            int currentTick = Find.TickManager.TicksGame;
            int intervalTicks = AIControlWorkPriorityMod.Settings.updateIntervalHours * 2500;
            int nextTriggerTick = director.LastTriggerTick + intervalTicks;
            int remainingTicks = nextTriggerTick - currentTick;
            if (remainingTicks < 0) remainingTicks = 0;
            float remainingHours = remainingTicks / 2500f;
            int hours = Mathf.CeilToInt(remainingHours); // 單位到小時就好
            if (hours <= 0)
            {
                return "AI 調度: 即將進行";
            }
            return $"AI 調度: {hours}小時";
        }
        private static Rect lastRect = Rect.zero;
        public override Rect DrawAt(float top, bool selected)
        {
            // 1. 初始化或動態同步當前幀的座標，避免背景溢出或未對齊
            if (lastRect == Rect.zero)
            {
                float alertWidth = 154f;
                lastRect = new Rect((float)UI.screenWidth - alertWidth, top, alertWidth, 28f);
            }
            
            // 即時同步 y 座標，確保 Alert 滾動或移動時底色不會落後
            lastRect.y = top;
            
            // 2. 在底層繪製科技藍背景
            var oldColor = GUI.color;
            GUI.color = new Color(0.15f, 0.55f, 0.95f, 0.50f);
            GUI.DrawTexture(lastRect, BaseContent.WhiteTex);
            GUI.color = oldColor;
            
            // 3. 執行原版繪製 (文字在最上層)
            Rect result = base.DrawAt(top, selected);
            
            // 4. 自動更新為原版返回的精確寬高與 x 座標，用於下一幀底層繪製
            lastRect.x = result.x;
            lastRect.width = result.width;
            lastRect.height = result.height;
            
            return result;
        }
    }
}