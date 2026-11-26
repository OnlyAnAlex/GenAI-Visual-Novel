using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VnNpc
{
    /// <summary>Monta o corpo JSON de /chat/completions, incluindo o schema com as expressões válidas.</summary>
    public static class ChatRequestBuilder
    {
        public const string BeatsField = "beats";
        public const string ExpressionField = "expression";
        public const string TextField = "text";
        public const string ConcernField = "real_world_concern";

        private const string SchemaName = "npc_reply";

        public static string Build(LlmEndpointConfig config, IReadOnlyList<ChatMessage> messages, IReadOnlyList<string> expressionIds)
        {
            var body = new JObject
            {
                ["model"] = config.model,
                ["messages"] = BuildMessages(messages),
                ["max_tokens"] = config.maxTokens,
                ["response_format"] = new JObject
                {
                    ["type"] = "json_schema",
                    ["json_schema"] = new JObject
                    {
                        ["name"] = SchemaName,
                        ["strict"] = true,
                        ["schema"] = BuildReplySchema(expressionIds)
                    }
                }
            };

            if (config.sendTemperature)
                body["temperature"] = config.temperature;

            if (config.useOpenRouterExtensions && !string.IsNullOrWhiteSpace(config.reasoningEffort))
            {
                body["reasoning"] = new JObject
                {
                    ["effort"] = config.reasoningEffort.Trim(),
                    ["exclude"] = true
                };
            }

            return body.ToString(Formatting.None);
        }

        private static JArray BuildMessages(IReadOnlyList<ChatMessage> messages)
        {
            var array = new JArray();
            for (int i = 0; i < messages.Count; i++)
                array.Add(new JObject { ["role"] = messages[i].Role, ["content"] = messages[i].Content });
            return array;
        }

        private static JObject BuildReplySchema(IReadOnlyList<string> expressionIds)
        {
            var expressionEnum = new JArray();
            for (int i = 0; i < expressionIds.Count; i++)
                expressionEnum.Add(expressionIds[i]);

            var beat = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    [ExpressionField] = new JObject { ["type"] = "string", ["enum"] = expressionEnum },
                    [TextField] = new JObject { ["type"] = "string", ["description"] = "Fala do personagem neste trecho." }
                },
                ["required"] = new JArray(ExpressionField, TextField),
                ["additionalProperties"] = false
            };

            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    [BeatsField] = new JObject
                    {
                        ["type"] = "array",
                        ["description"] = "De 1 a 3 trechos de fala, cada um com sua expressão.",
                        ["items"] = beat
                    },
                    [ConcernField] = new JObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "True apenas se o jogador demonstrar sofrimento ou risco reais, fora da ficção."
                    }
                },
                ["required"] = new JArray(BeatsField, ConcernField),
                ["additionalProperties"] = false
            };
        }
    }
}
