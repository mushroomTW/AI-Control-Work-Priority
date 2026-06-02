using System;
using System.Collections.Generic;
using Verse;

namespace AIControlWorkPriority
{
    public class AIControlStateStore : GameComponent
    {
        public static AIControlStateStore Instance;

        // pawnId -> HashSet<workTypeDefName> (代表已被 AI 託管的格子)
        private Dictionary<string, HashSet<string>> managedStates = new Dictionary<string, HashSet<string>>();

        // 用於線程安全排隊的 action 佇列，供背景執行緒將工作排回主執行緒執行
        private readonly Queue<Action> queuedActions = new Queue<Action>();
        private readonly object queueLock = new object();

        public AIControlStateStore(Game game)
        {
            Instance = this;
        }

        public static AIControlStateStore GetInstance()
        {
            if (Instance == null && Current.Game != null)
            {
                Instance = Current.Game.GetComponent<AIControlStateStore>();
            }
            return Instance;
        }

        public bool IsManaged(string pawnId, string workTypeDefName)
        {
            if (managedStates.TryGetValue(pawnId, out var workTypes))
            {
                return workTypes.Contains(workTypeDefName);
            }
            return false;
        }

        public void SetManaged(string pawnId, string workTypeDefName, bool managed)
        {
            if (!managedStates.ContainsKey(pawnId))
            {
                managedStates[pawnId] = new HashSet<string>();
            }

            if (managed)
            {
                managedStates[pawnId].Add(workTypeDefName);
            }
            else
            {
                managedStates[pawnId].Remove(workTypeDefName);
                if (managedStates[pawnId].Count == 0)
                {
                    managedStates.Remove(pawnId);
                }
            }
        }

        public void SetAllManaged(Pawn pawn, bool managed)
        {
            if (pawn == null) return;
            string pawnId = pawn.ThingID;

            if (managed)
            {
                if (!managedStates.ContainsKey(pawnId))
                {
                    managedStates[pawnId] = new HashSet<string>();
                }
                foreach (var workType in DefDatabase<WorkTypeDef>.AllDefs)
                {
                    managedStates[pawnId].Add(workType.defName);
                }
            }
            else
            {
                managedStates.Remove(pawnId);
            }
        }

        public void SetAllManagedInMap(bool managed)
        {
            var pawns = Find.CurrentMap?.mapPawns.FreeColonists;
            if (pawns == null) return;

            foreach (var pawn in pawns)
            {
                SetAllManaged(pawn, managed);
            }
        }

        // 排程在主執行緒中執行的 action
        public void EnqueueAction(Action action)
        {
            lock (queueLock)
            {
                queuedActions.Enqueue(action);
            }
        }

        public override void GameComponentUpdate()
        {
            base.GameComponentUpdate();
            
            // 在主執行緒中執行排隊的 actions
            Action nextAction = null;
            do
            {
                lock (queueLock)
                {
                    nextAction = queuedActions.Count > 0 ? queuedActions.Dequeue() : null;
                }
                if (nextAction != null)
                {
                    try
                    {
                        nextAction();
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[AIControlWorkPriority] 執行排隊 action 失敗: " + ex);
                    }
                }
            } while (nextAction != null);
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            // 每隔 15000 Ticks (1/4 遊戲天) 進行一次失效 Pawn 清理
            if (Find.TickManager.TicksGame % 15000 == 0)
            {
                CleanupDeadOrMissingPawns();
            }
        }

        private void CleanupDeadOrMissingPawns()
        {
            if (managedStates.Count == 0) return;

            List<string> toRemove = new List<string>();
            foreach (var key in managedStates.Keys)
            {
                Pawn pawn = FindPawnById(key);
                if (pawn == null || pawn.Destroyed || pawn.Dead)
                {
                    toRemove.Add(key);
                }
            }

            if (toRemove.Count > 0)
            {
                foreach (var key in toRemove)
                {
                    managedStates.Remove(key);
                }
                Log.Message($"[AIControlWorkPriority] 已清理 {toRemove.Count} 個失效或死亡殖民者的 AI 託管狀態。");
            }
        }

        private Pawn FindPawnById(string thingId)
        {
            if (Current.Game == null) return null;
            
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawns)
                {
                    if (pawn.ThingID == thingId) return pawn;
                }
            }

            if (Find.World != null)
            {
                foreach (var caravan in Find.WorldObjects.Caravans)
                {
                    foreach (var pawn in caravan.PawnsListForReading)
                    {
                        if (pawn.ThingID == thingId) return pawn;
                    }
                }
            }

            return null;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            List<string> keys = new List<string>();
            List<List<string>> values = new List<List<string>>();

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                foreach (var pair in managedStates)
                {
                    keys.Add(pair.Key);
                    values.Add(new List<string>(pair.Value));
                }
            }

            Scribe_Collections.Look(ref keys, "pawnKeys", LookMode.Value);
            Scribe_Collections.Look(ref values, "workValues", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                managedStates = new Dictionary<string, HashSet<string>>();
                if (keys != null && values != null)
                {
                    for (int i = 0; i < keys.Count; i++)
                    {
                        if (i < values.Count)
                        {
                            managedStates[keys[i]] = new HashSet<string>(values[i]);
                        }
                    }
                }
            }
        }
    }
}
