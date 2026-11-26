using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace VnNpc
{
    /// <summary>
    /// Envia requisições a /chat/completions com UnityWebRequest e repete falhas transitórias.
    /// Parar uma corrotina não executa blocos finally, por isso a requisição ativa fica em um campo,
    /// para o dono poder liberá-la em AbortActive.
    /// </summary>
    public sealed class OpenAiCompatibleClient
    {
        private const float BaseBackoffSeconds = 1f;
        private const float MaxRetryAfterSeconds = 20f;
        private const string HttpsScheme = "https://";
        private const string LocalhostPrefix = "http://localhost";
        private const string LoopbackPrefix = "http://127.0.0.1";

        private readonly LlmEndpointConfig _config;
        private UnityWebRequest _active;

        public OpenAiCompatibleClient(LlmEndpointConfig config)
        {
            _config = config;
        }

        /// <summary>Corrotina. Chama onDone exatamente uma vez, a menos que o dono a interrompa.</summary>
        public IEnumerator Send(string jsonBody, Action<LlmResult> onDone)
        {
            if (!TryResolveKey(out string key, out string configurationError))
            {
                onDone(LlmResult.Fail(LlmFailure.Configuration, configurationError));
                yield break;
            }

            LlmResult result;
            for (int attempt = 0; ; attempt++)
            {
                UnityWebRequest request = CreateRequest(jsonBody, key);
                _active = request;
                yield return request.SendWebRequest();

                result = ReadResult(request);
                ReleaseActive();

                if (!result.Failure.IsTransient() || attempt >= _config.maxHttpRetries)
                    break;

                float backoff = BaseBackoffSeconds * (1 << attempt);
                yield return new WaitForSecondsRealtime(Mathf.Max(backoff, result.RetryAfterSeconds));
            }

            onDone(result);
        }

        /// <summary>Pare a corrotina de Send antes de chamar, senão ela acessaria uma requisição já liberada.</summary>
        public void AbortActive()
        {
            if (_active == null) return;
            _active.Abort();
            ReleaseActive();
        }

        private bool TryResolveKey(out string key, out string error)
        {
            key = null;
            error = null;
            if (!_config.requireApiKey) return true;

            string url = _config.baseUrl;
            bool isSecure = url.StartsWith(HttpsScheme, StringComparison.OrdinalIgnoreCase)
                            || url.StartsWith(LocalhostPrefix, StringComparison.OrdinalIgnoreCase)
                            || url.StartsWith(LoopbackPrefix, StringComparison.OrdinalIgnoreCase);
            if (!isSecure)
            {
                error = "A chave só é enviada por HTTPS. Corrija baseUrl.";
                return false;
            }

            return LocalApiKeyStore.TryGetKey(_config.apiKeyEnvironmentVariable, out key, out error);
        }

        private UnityWebRequest CreateRequest(string jsonBody, string key)
        {
            var request = new UnityWebRequest(_config.ChatCompletionsUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = _config.requestTimeoutSeconds
            };
            request.SetRequestHeader("Content-Type", "application/json");

            if (!string.IsNullOrEmpty(key))
                request.SetRequestHeader("Authorization", "Bearer " + key);

            if (_config.useOpenRouterExtensions && !string.IsNullOrWhiteSpace(_config.appReferer))
            {
                request.SetRequestHeader("HTTP-Referer", _config.appReferer);
                request.SetRequestHeader("X-OpenRouter-Title", _config.appTitle);
            }

            return request;
        }

        private static LlmResult ReadResult(UnityWebRequest request)
        {
            switch (request.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    return LlmResult.Fail(LlmFailure.Network, request.error);
                case UnityWebRequest.Result.DataProcessingError:
                    return LlmResult.Fail(LlmFailure.BadResponse, request.error);
            }

            byte[] data = request.downloadHandler.data;
            string body = data == null ? string.Empty : Encoding.UTF8.GetString(data);
            LlmResult result = ChatResponseParser.Parse(request.responseCode, body);
            return result.Failure == LlmFailure.RateLimited
                ? result.WithRetryAfter(ParseRetryAfter(request.GetResponseHeader("Retry-After")))
                : result;
        }

        private static float ParseRetryAfter(string header)
        {
            if (string.IsNullOrEmpty(header)) return 0f;
            return float.TryParse(header, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds)
                ? Mathf.Clamp(seconds, 0f, MaxRetryAfterSeconds)
                : 0f; // Retry-After também pode ser uma data. Nesse caso vale o backoff padrão.
        }

        private void ReleaseActive()
        {
            if (_active == null) return;
            _active.Dispose();
            _active = null;
        }
    }
}
