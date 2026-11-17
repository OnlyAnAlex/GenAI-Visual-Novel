using UnityEngine;

namespace VnNpc
{
    /// <summary>
    /// Configuração do endpoint. Não existe campo de chave de propósito, porque este asset é versionado e entra na build.
    /// Campos públicos: dado de autoria, tratado como somente leitura em runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "LlmEndpointConfig", menuName = "VN NPC/Endpoint LLM")]
    public sealed class LlmEndpointConfig : ScriptableObject
    {
        [Header("Endpoint")]
        [Tooltip("Base compatível com a OpenAI. Na distribuição, aponte para o seu proxy.")]
        public string baseUrl = "https://openrouter.ai/api/v1";

        [Tooltip("Slug do modelo. Escolha um que suporte structured_outputs.")]
        public string model = "deepseek/deepseek-v4.1-flash";

        [Header("Autenticação")]
        [Tooltip("Desligue quando baseUrl for um proxy seu que guarda a chave no servidor.")]
        public bool requireApiKey = true;

        public string apiKeyEnvironmentVariable = "OPENROUTER_API_KEY";

        [Header("Geração")]
        [Tooltip("Alguns modelos ignoram temperature.")]
        public bool sendTemperature = true;

        [Range(0f, 2f)] public float temperature = 0.85f;

        [Tooltip("Inclui os tokens de raciocínio. Se vier pouco, a resposta chega vazia ou cortada.")]
        public int maxTokens = 1000;

        [Header("Rede")]
        public int requestTimeoutSeconds = 40;

        [Tooltip("Novas tentativas apenas para falhas transitórias (rede, 408, 429, 5xx).")]
        public int maxHttpRetries = 1;

        [Header("Extensões da OpenRouter")]
        [Tooltip("Desligue em endpoints que rejeitam campos desconhecidos.")]
        public bool useOpenRouterExtensions = true;

        [Tooltip("low, none, etc. Vazio não envia o campo. Modelos variam nos valores aceitos.")]
        public string reasoningEffort = "low";

        [Tooltip("Atribuição opcional: URL do jogo. Precisa existir para o título aparecer.")]
        public string appReferer = "";

        public string appTitle = "VN NPC";

        public string ChatCompletionsUrl => baseUrl.TrimEnd('/') + "/chat/completions";
    }
}
