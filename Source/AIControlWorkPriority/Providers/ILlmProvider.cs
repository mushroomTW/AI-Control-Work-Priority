using System;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Verse;

namespace AIControlWorkPriority
{
    public interface ILlmProvider
    {
        Task<string> CallLlmAsync(string systemPrompt, string userPrompt, string apiKey, string modelName, string baseUrl, int timeoutSeconds);
    }

    public static class LlmProviderFactory
    {
        public static ILlmProvider GetProvider(string providerName)
        {
            switch (providerName)
            {
                case "Gemini":
                    return new GeminiProvider();
                case "OpenAI":
                    return new OpenAiProvider();
                case "OpenAI-Compatible":
                    return new OpenAiCompatibleProvider();
                default:
                    throw new ArgumentException($"未知的 Provider: {providerName}");
            }
        }
    }

    public class GeminiProvider : ILlmProvider
    {
        private static readonly HttpClient client = new HttpClient();

        public async Task<string> CallLlmAsync(string systemPrompt, string userPrompt, string apiKey, string modelName, string baseUrl, int timeoutSeconds)
        {
            if (string.IsNullOrEmpty(apiKey))
                throw new ArgumentException("API 金鑰未設定。");

            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

            // 建立 Gemini Request Body
            JObject requestBody = new JObject();
            
            // 系統指令 (System Instruction)
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                requestBody["systemInstruction"] = new JObject
                {
                    ["parts"] = new JArray
                    {
                        new JObject { ["text"] = systemPrompt }
                    }
                };
            }

            // 用戶內容
            requestBody["contents"] = new JArray
            {
                new JObject
                {
                    ["role"] = "user",
                    ["parts"] = new JArray
                    {
                        new JObject { ["text"] = userPrompt }
                    }
                }
            };

            // 設定 JSON 輸出模式 (僅適用於較新模型如 gemini-1.5 或 gemini-2.0/2.5)
            requestBody["generationConfig"] = new JObject
            {
                ["responseMimeType"] = "application/json"
            };

            string jsonContent = requestBody.ToString();

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
            using (var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json"))
            {
                HttpResponseMessage response = await client.PostAsync(url, httpContent, cts.Token);
                string responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"HTTP {response.StatusCode}: {responseString}");
                }

                // 解析 Gemini Response
                JObject responseJson = JObject.Parse(responseString);
                var textToken = responseJson["candidates"]?[0]?["content"]?["parts"]?[0]?["text"];
                
                if (textToken == null)
                {
                    throw new Exception("無法從 Gemini 回傳的資料中提取文本。回傳內容: " + responseString);
                }

                return textToken.ToString().Trim();
            }
        }
    }

    public class OpenAiProvider : ILlmProvider
    {
        private static readonly HttpClient client = new HttpClient();

        public async Task<string> CallLlmAsync(string systemPrompt, string userPrompt, string apiKey, string modelName, string baseUrl, int timeoutSeconds)
        {
            if (string.IsNullOrEmpty(apiKey))
                throw new ArgumentException("API 金鑰未設定。");

            string url = "https://api.openai.com/v1/chat/completions";

            // 建立 OpenAI Request Body
            JObject requestBody = new JObject();
            requestBody["model"] = modelName;
            
            JArray messages = new JArray();
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new JObject { ["role"] = "system", ["content"] = systemPrompt });
            }
            messages.Add(new JObject { ["role"] = "user", ["content"] = userPrompt });
            requestBody["messages"] = messages;

            // 要求 JSON 格式輸出
            requestBody["response_format"] = new JObject { ["type"] = "json_object" };

            string jsonContent = requestBody.ToString();

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
            using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, url))
            {
                httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                HttpResponseMessage response = await client.SendAsync(httpRequest, cts.Token);
                string responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"HTTP {response.StatusCode}: {responseString}");
                }

                JObject responseJson = JObject.Parse(responseString);
                var textToken = responseJson["choices"]?[0]?["message"]?["content"];
                
                if (textToken == null)
                {
                    throw new Exception("無法從 OpenAI 回傳的資料中提取文本。回傳內容: " + responseString);
                }

                return textToken.ToString().Trim();
            }
        }
    }

    public class OpenAiCompatibleProvider : ILlmProvider
    {
        private static readonly HttpClient client = new HttpClient();

        public async Task<string> CallLlmAsync(string systemPrompt, string userPrompt, string apiKey, string modelName, string baseUrl, int timeoutSeconds)
        {
            if (string.IsNullOrEmpty(baseUrl))
                throw new ArgumentException("自訂端點 URL (Base URL) 未設定。");

            // 確保路徑以 /v1/chat/completions 結尾（如果玩家只輸入了 Domain 或基礎路徑）
            string url = baseUrl;
            if (!url.EndsWith("/chat/completions"))
            {
                url = url.TrimEnd('/') + "/chat/completions";
            }

            JObject requestBody = new JObject();
            requestBody["model"] = modelName;
            
            JArray messages = new JArray();
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new JObject { ["role"] = "system", ["content"] = systemPrompt });
            }
            messages.Add(new JObject { ["role"] = "user", ["content"] = userPrompt });
            requestBody["messages"] = messages;

            // 部分第三方 API 可能不支援 response_format，故此處寬鬆處理 (可選擇性包含)
            requestBody["response_format"] = new JObject { ["type"] = "json_object" };

            string jsonContent = requestBody.ToString();

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
            using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, url))
            {
                httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                if (!string.IsNullOrEmpty(apiKey))
                {
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                }

                HttpResponseMessage response = await client.SendAsync(httpRequest, cts.Token);
                string responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"HTTP {response.StatusCode}: {responseString}");
                }

                JObject responseJson = JObject.Parse(responseString);
                var textToken = responseJson["choices"]?[0]?["message"]?["content"];
                
                if (textToken == null)
                {
                    throw new Exception("無法從 OpenAI-Compatible 回傳的資料中提取文本。回傳內容: " + responseString);
                }

                return textToken.ToString().Trim();
            }
        }
    }
}
