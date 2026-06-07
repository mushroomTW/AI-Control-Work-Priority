using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Verse;

namespace AIControlWorkPriority
{
    public static class JsonSerializationService
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        public static string Serialize(object obj)
        {
            try
            {
                return JsonConvert.SerializeObject(obj, Settings);
            }
            catch (Exception ex)
            {
                Log.Error("[AIControlWorkPriority] JSON 序列化失敗: " + ex);
                return null;
            }
        }

        public static T Deserialize<T>(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json)) return default(T);
                return JsonConvert.DeserializeObject<T>(json, Settings);
            }
            catch (Exception ex)
            {
                Log.Error("[AIControlWorkPriority] JSON 反序列化失敗: " + ex);
                return default(T);
            }
        }

        public static JObject ParseJObject(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json)) return null;
                string cleaned = CleanJsonString(json);
                return JObject.Parse(cleaned);
            }
            catch (Exception ex)
            {
                Log.Error("[AIControlWorkPriority] JObject 解析失敗: " + ex + "\n原始回傳內容:\n" + json);
                return null;
            }
        }

        public static string CleanJsonString(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            string cleaned = input.Trim();

            // 尋找第一個大括號與最後一個大括號，精準擷取中間的 JSON 物件
            int firstBrace = cleaned.IndexOf('{');
            int lastBrace = cleaned.LastIndexOf('}');

            if (firstBrace != -1 && lastBrace != -1 && lastBrace > firstBrace)
            {
                return cleaned.Substring(firstBrace, lastBrace - firstBrace + 1);
            }

            return cleaned;
        }
    }
}
