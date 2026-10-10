using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace PaulistaBusinessWorld
{
    // Chamada HTTP direta à Messages API da Anthropic (UnityWebRequest funciona no desktop e no WebGL).
    // A chave vem da variável de ambiente ANTHROPIC_API_KEY ou do painel "IA" do jogo.
    public static class ClaudeClient
    {
        const string Endpoint = "https://api.anthropic.com/v1/messages";
        public const string Model = "claude-opus-5-5";
        const string PrefsKey = "pbw.anthropicKey";

        static string sessionKey;

        public static string ApiKey
        {
            get
            {
                if (!string.IsNullOrEmpty(sessionKey)) return sessionKey;
                var saved = PlayerPrefs.GetString(PrefsKey, "");
                if (saved.Length > 0) return saved;
#if !UNITY_WEBGL || UNITY_EDITOR
                return Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
#else
                return null;
#endif
            }
        }

        public static bool HasKey => !string.IsNullOrEmpty(ApiKey);

        public static void SetKey(string key, bool remember)
        {
            sessionKey = key?.Trim();
            if (remember && !string.IsNullOrEmpty(sessionKey)) PlayerPrefs.SetString(PrefsKey, sessionKey);
            else PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        [Serializable] public class Message { public string role; public string content; }
        [Serializable] class OutputConfig { public string effort; }
        [Serializable]
        class Request
        {
            public string model;
            public int max_tokens;
            public string system;
            public Message[] messages;
            public OutputConfig output_config;
            public string fallbacks;
        }
        [Serializable] class ContentBlock { public string type; public string text; }
        [Serializable] class Response { public ContentBlock[] content; public string stop_reason; }
        [Serializable] class ErrorBody { public string type; public string message; }
        [Serializable] class ErrorResponse { public ErrorBody error; }

        // onDone(texto, erro): exatamente um dos dois é não nulo.
        public static IEnumerator Send(string system, List<Message> history, Action<string, string> onDone)
        {
            var body = JsonUtility.ToJson(new Request
            {
                model = Model,
                max_tokens = 4000,
                system = system,
                messages = history.ToArray(),
                output_config = new OutputConfig { effort = "low" }, // conversa curta: prioriza latência
                fallbacks = "default",
            });
            return Post(body, onDone);
        }

        // Saída estruturada: a resposta é um JSON válido segundo o schema.
        public static IEnumerator SendStructured(string prompt, string schemaJson, Action<string, string> onDone)
        {
            var body = JsonUtility.ToJson(new Request
            {
                model = Model,
                max_tokens = 8000,
                messages = new[] { new Message { role = "user", content = prompt } },
                output_config = new OutputConfig { effort = "medium" },
                fallbacks = "default",
            });
            body = body.Replace("\"output_config\":{\"effort\":\"medium\"}",
                "\"output_config\":{\"effort\":\"medium\",\"format\":{\"type\":\"json_schema\",\"schema\":" + schemaJson + "}}");
            body = body.Replace("\"system\":\"\",", "");
            return Post(body, onDone);
        }

        static IEnumerator Post(string body, Action<string, string> onDone)
        {
            using var req = new UnityWebRequest(Endpoint, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = 90;
            req.SetRequestHeader("content-type", "application/json");
            req.SetRequestHeader("x-api-key", ApiKey);
            req.SetRequestHeader("anthropic-version", "2023-06-01");
            req.SetRequestHeader("anthropic-beta", "server-side-fallback-2026-07-01");
            req.SetRequestHeader("anthropic-dangerous-direct-browser-access", "true");
            yield return req.SendWebRequest();

            var text = req.downloadHandler?.text ?? "";
            if (req.result != UnityWebRequest.Result.Success)
            {
                string detail = req.error;
                try
                {
                    var err = JsonUtility.FromJson<ErrorResponse>(text);
                    if (err?.error != null && !string.IsNullOrEmpty(err.error.message)) detail = $"{err.error.type}: {err.error.message}";
                }
                catch (ArgumentException) { }
                onDone(null, $"Falha na API ({req.responseCode}): {detail}");
                yield break;
            }

            Response res;
            try { res = JsonUtility.FromJson<Response>(text); }
            catch (ArgumentException e) { onDone(null, "Resposta inválida da API: " + e.Message); yield break; }

            if (res.stop_reason == "refusal")
            {
                onDone("Prefiro não entrar nesse assunto. Podemos falar sobre negócios?", null);
                yield break;
            }
            var sb = new StringBuilder();
            if (res.content != null)
                foreach (var block in res.content)
                    if (block.type == "text") sb.Append(block.text);
            var reply = sb.ToString().Trim();
            if (reply.Length == 0) onDone(null, "A API não retornou texto.");
            else onDone(reply, null);
        }
    }
}
