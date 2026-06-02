using System;
using System.IO;
using Verse;

namespace AIControlWorkPriority
{
    public static class PromptTemplateLoader
    {
        private static string ModRootDir => LoadedModManager.GetMod<AIControlWorkPriorityMod>().Content.RootDir;
        private static string PromptsDir => Path.Combine(ModRootDir, "Prompts");
        
        public static string SystemPromptPath => Path.Combine(PromptsDir, "work-priority-system.md");
        public static string UserPromptPath => Path.Combine(PromptsDir, "work-priority-user.md");
        public static string OutputSchemaPath => Path.Combine(PromptsDir, "output-schema.json");

        public static void EnsurePromptFilesExist()
        {
            try
            {
                if (!Directory.Exists(PromptsDir))
                {
                    Directory.CreateDirectory(PromptsDir);
                }

                if (!File.Exists(SystemPromptPath))
                {
                    File.WriteAllText(SystemPromptPath, GetDefaultSystemPrompt());
                }

                if (!File.Exists(UserPromptPath))
                {
                    File.WriteAllText(UserPromptPath, GetDefaultUserPrompt());
                }

                if (!File.Exists(OutputSchemaPath))
                {
                    File.WriteAllText(OutputSchemaPath, GetDefaultOutputSchema());
                }
            }
            catch (Exception ex)
            {
                Log.Error("[AIControlWorkPriority] 初始化 Prompt 檔案失敗: " + ex);
            }
        }

        public static string LoadSystemPrompt()
        {
            EnsurePromptFilesExist();
            try
            {
                return File.ReadAllText(SystemPromptPath);
            }
            catch (Exception ex)
            {
                Log.Error("[AIControlWorkPriority] 讀取 System Prompt 失敗: " + ex);
                return GetDefaultSystemPrompt();
            }
        }

        public static string LoadUserPrompt(string strategy, string colonyStateJson)
        {
            EnsurePromptFilesExist();
            try
            {
                string userTemplate = File.ReadAllText(UserPromptPath);
                string schema = File.ReadAllText(OutputSchemaPath);

                string result = userTemplate
                    .Replace("{{STRATEGY_PROFILE}}", strategy)
                    .Replace("{{COLONY_STATE_JSON}}", colonyStateJson)
                    .Replace("{{OUTPUT_SCHEMA}}", schema);

                return result;
            }
            catch (Exception ex)
            {
                Log.Error("[AIControlWorkPriority] 讀取/處理 User Prompt 失敗: " + ex);
                return "";
            }
        }

        private static string GetDefaultSystemPrompt()
        {
            return @"# 角色與任務
你是一個 RimWorld (環世界) 的 AI 工作優先級調度專家。你的任務是定時評估殖民地的狀態，並為殖民者調整工作優先級（Work Priorities），以引導殖民地順利生存與發展。

# 規則與限制
1. **工作優先級值**：必須在 `1` 到 `4` 之間。
   - `1`：最高優先級 (會最先執行)。
   - `2`：高優先級。
   - `3`：普通優先級。
   - `4`：低優先級。
   - 請勿回傳其他數值，例如 `0`、`5..9`。
2. **託管約束**：你只能針對已開啟「AI 託管」的工作格提出建議。未開啟 AI 託管（手動鎖定）的工作格不包含在變更清單中，否則會被本地驗證器拒絕。
3. **客觀決策**：請根據殖民者技能、熱情、健康狀態、短期狀態（如飢餓、心情、睡眠、危險）以及殖民地當前的策略方針進行最佳分配。
4. **輸出格式**：必須完全按照規定的 JSON Schema 輸出，不得包含額外的 Markdown 標記（例如 ```json）或任何前言與結語。

# 優先級分配準則
- **生存基礎**：消防 (Firefighter)、病床休養 (Patient)、緊急手術/照護 (Doctor)、休養 (BedRest) 等極其重要的工作，在必要時應給予 `1`。
- **技能相符**：一般工作應優先指派給技能等級高、有熱情（興趣）的殖民者，並將其優先級設為較高（如 `1` 或 `2`）。
- **專職化**：避免讓一個殖民者同時擔當太多 `1` 級工作，應讓他們專注於核心工作，以提高工作效率。
- **避免飢餓與危險**：若殖民地食物短缺或面臨威脅，應大幅調高農業、烹飪或防禦相關工作的優先級。";
        }

        private static string GetDefaultUserPrompt()
        {
            return @"# 當前策略方針
{{STRATEGY_PROFILE}}

# 殖民地狀態數據 (JSON)
{{COLONY_STATE_JSON}}

# 輸出指南
請根據上述數據分析目前殖民地缺口，決定合適的工作優先級變更計畫。
請回傳符合 JSON Schema 的結果。";
        }

        private static string GetDefaultOutputSchema()
        {
            return @"{
  ""$schema"": ""http://json-schema.org/draft-07/schema#"",
  ""title"": ""WorkPriorityPlan"",
  ""type"": ""object"",
  ""properties"": {
    ""summary"": {
      ""type"": ""string"",
      ""description"": ""調度決策的簡短摘要說明。""
    },
    ""alerts"": {
      ""type"": ""array"",
      ""items"": {
        ""type"": ""string""
      },
      ""description"": ""系統提醒或缺口警告，例如：'缺少合格的廚師'、'研究速度過慢'。""
    },
    ""changes"": {
      ""type"": ""array"",
      ""items"": {
        ""type"": ""object"",
        ""properties"": {
          ""pawnId"": {
            ""type"": ""string"",
            ""description"": ""殖民者的 ThingIDNumber (例如：'Pawn_Colonist1')""
          },
          ""workTypeDefName"": {
            ""type"": ""string"",
            ""description"": ""工作類型的 defName (例如：'Doctor', 'Cooking')""
          },
          ""priority"": {
            ""type"": ""integer"",
            ""description"": ""優先級值 (1=最高, 4=最低)""
          },
          ""reason"": {
            ""type"": ""string"",
            ""description"": ""調整此項優先級的簡短理由。""
          }
        },
        ""required"": [""pawnId"", ""workTypeDefName"", ""priority""]
      },
      ""description"": ""需要調整的優先級變更清單。""
    }
  },
  ""required"": [""summary"", ""alerts"", ""changes""]
}";
        }
    }
}
