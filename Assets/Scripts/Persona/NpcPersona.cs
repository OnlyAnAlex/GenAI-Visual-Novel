using System;
using UnityEngine;

namespace VnNpc
{
    [Serializable]
    public sealed class ExpressionEntry
    {
        [Tooltip("Identificador enviado ao modelo. Use minúsculas e sem espaços.")]
        public string id;

        [TextArea(1, 2), Tooltip("Quando o personagem usa esta expressão.")]
        public string whenToUse;

        public Sprite sprite;
    }

    [Serializable]
    public sealed class PersonaExample
    {
        [TextArea(1, 3)] public string playerLine;
        public string expressionId;
        [TextArea(1, 3)] public string reply;
    }

    [Serializable]
    public sealed class AuthoredLine
    {
        public string expressionId;
        [TextArea(1, 3)] public string text;
    }

    /// <summary>
    /// Ficha do personagem. Aceita as macros {{char}} e {{user}}.
    /// Campos públicos: dado de autoria, tratado como somente leitura em runtime.
    /// O conteúdo deste asset vai na build. Não coloque segredos aqui.
    /// </summary>
    [CreateAssetMenu(fileName = "NpcPersona", menuName = "VN NPC/Persona")]
    public sealed class NpcPersona : ScriptableObject
    {
        [Header("Identidade")]
        public string characterName = "Personagem";
        public string responseLanguage = "português do Brasil";
        [TextArea(2, 6)] public string identity;
        [TextArea(2, 6)] public string personality;
        [TextArea(2, 6)] public string speechStyle;
        [TextArea(2, 6)] public string knowledge;
        [TextArea(2, 6), Tooltip("Conceitos que não existem para o personagem. O modelo reage a eles com estranhamento.")]
        public string unknownConcepts;
        [TextArea(2, 6)] public string scene;

        [Header("Permanecer em cena")]
        [TextArea(2, 6), Tooltip("Como o personagem reage quando alguém tenta tirá-lo da cena.")]
        public string deflectionStyle;
        [Tooltip("Inclua tentativas de quebra de personagem. Costuma ser a parte que mais protege a persona.")]
        public PersonaExample[] examples = Array.Empty<PersonaExample>();

        [Header("Expressões")]
        public string defaultExpressionId = "neutro";
        public ExpressionEntry[] expressions = Array.Empty<ExpressionEntry>();

        [Header("Falas escritas por você")]
        public AuthoredLine openingLine = new AuthoredLine();
        [Tooltip("Usadas quando a API falha ou quando toda resposta é rejeitada.")]
        public AuthoredLine[] fallbackLines = Array.Empty<AuthoredLine>();

        [Header("Guarda de personagem")]
        [Tooltip("Termos extras que não podem aparecer nas respostas, salvo se o jogador tiver acabado de citá-los.")]
        public string[] additionalForbiddenTerms = Array.Empty<string>();
        [Tooltip("Termos da lista padrão que são legítimos neste mundo, como 'inteligencia artificial' em uma ficção científica.")]
        public string[] allowedTerms = Array.Empty<string>();

        [Header("Aviso fora da ficção")]
        [TextArea(2, 5), Tooltip("Exibido pela interface do jogo, nunca pelo personagem.")]
        public string realWorldNotice =
            "Este personagem é fictício e gerado por IA. Se você está passando por um momento difícil, " +
            "converse com alguém de confiança ou procure apoio. No Brasil, o CVV atende pelo telefone 188, 24 horas por dia.";

        public ExpressionEntry FindExpression(string id)
        {
            for (int i = 0; i < expressions.Length; i++)
            {
                if (string.Equals(expressions[i].id, id, StringComparison.Ordinal))
                    return expressions[i];
            }

            return null;
        }
    }
}
