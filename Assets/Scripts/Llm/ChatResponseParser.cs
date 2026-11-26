using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VnNpc
{
    /// <summary>Converte a resposta HTTP de /chat/completions em um LlmResult. A OpenRouter pode devolver erro mesmo com HTTP 200.</summary>
    public static class ChatResponseParser
    {
        private const int HttpOkMin = 200;
        private const int HttpOkMax = 299;

        public static LlmResult Parse(long httpStatus, string body)
        {
            JObject root = TryParseObject(body);

            if (root != null && root["error"] is JObject topLevelError)
                return FromError(topLevelError, httpStatus);

            if (httpStatus < HttpOkMin || httpStatus > HttpOkMax)
                return LlmResult.Fail(Classify(httpStatus), $"HTTP {httpStatus}");

            if (root == null)
                return LlmResult.Fail(LlmFailure.BadResponse, "O corpo da resposta não é um objeto JSON.");

            if (!(root["choices"] is JArray choices) || choices.Count == 0 || !(choices[0] is JObject choice))
                return LlmResult.Fail(LlmFailure.BadResponse, "A resposta não trouxe choices.");

            return FromChoice(choice, httpStatus);
        }

        private static LlmResult FromChoice(JObject choice, long httpStatus)
        {
            if (choice["error"] is JObject choiceError)
                return FromError(choiceError, httpStatus);

            string finishReason = AsString(choice["finish_reason"]);
            if (finishReason == "content_filter")
                return LlmResult.Fail(LlmFailure.Moderation, "Bloqueado pelo filtro de conteúdo do provedor.");

            if (!(choice["message"] is JObject message))
                return LlmResult.Fail(LlmFailure.BadResponse, "A resposta não trouxe message.");

            string refusal = AsString(message["refusal"]);
            if (!string.IsNullOrWhiteSpace(refusal))
                return LlmResult.Fail(LlmFailure.Refused, refusal);

            if (finishReason == "length")
                return LlmResult.Fail(LlmFailure.Truncated, "Limite de tokens atingido. Aumente maxTokens ou reduza o raciocínio.");

            string content = AsString(message["content"]);
            return string.IsNullOrWhiteSpace(content)
                ? LlmResult.Fail(LlmFailure.BadResponse, "O modelo devolveu conteúdo vazio.")
                : LlmResult.Success(content);
        }

        private static LlmResult FromError(JObject error, long httpStatus)
        {
            long code = httpStatus;
            if (error["code"] is JValue codeValue && codeValue.Type == JTokenType.Integer)
                code = codeValue.ToObject<long>();

            string message = AsString(error["message"]) ?? "Erro sem mensagem.";
            return LlmResult.Fail(Classify(code), $"HTTP {code}: {message}");
        }

        private static LlmFailure Classify(long status)
        {
            switch (status)
            {
                case 401: return LlmFailure.Auth;
                case 402: return LlmFailure.Credits;
                case 403: return LlmFailure.Moderation;
                case 408: return LlmFailure.Network;
                case 429: return LlmFailure.RateLimited;
            }

            if (status >= 400 && status < 500) return LlmFailure.BadRequest;
            return LlmFailure.Server; // 5xx, e também erro no corpo de um HTTP 200 sem código numérico.
        }

        private static JObject TryParseObject(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                return JToken.Parse(body) as JObject;
            }
            catch (JsonException)
            {
                return null; // Gateways podem devolver HTML. O chamador decide pelo status HTTP.
            }
        }

        private static string AsString(JToken token) => (token as JValue)?.Value as string;
    }
}
