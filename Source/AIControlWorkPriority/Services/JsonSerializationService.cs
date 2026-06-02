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
                return JObject.Parse(json);
            }
            catch (Exception ex)
            {
                Log.Error("[AIControlWorkPriority] JObject 解析失敗: " + ex);
                return null;
            }
        }
    }
}
