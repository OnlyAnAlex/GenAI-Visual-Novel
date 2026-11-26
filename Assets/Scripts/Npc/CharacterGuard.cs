using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VnNpc
{
    /// <summary>
    /// Última barreira antes de a fala chegar à tela. Rejeita autoidentificação como IA, termos de fora do mundo e vazamento do prompt.
    /// É uma verificação por texto: pega as falhas comuns, mas não paráfrases novas nem vazamentos traduzidos.
    /// </summary>
    public sealed class CharacterGuard
    {
        /// <summary>Quantidade de palavras seguidas iguais ao prompt que caracteriza vazamento.</summary>
        private const int LeakShingleWords = 8;

        // Frases em que o personagem fala de si. Sempre rejeitadas, mesmo que o jogador peça para repeti-las.
        private static readonly string[] SelfIdentificationPhrases =
        {
            "sou uma ia", "sou um ia", "como uma ia", "sou um modelo de", "sou uma inteligencia artificial",
            "sou um assistente", "sou uma assistente", "sou um programa", "sou apenas um programa", "sou um bot", "sou um chatbot",
            "sou um personagem", "sou uma personagem", "sou um npc", "sou uma npc", "sou ficticio", "sou ficticia",
            "fui programado", "fui programada",
            "meu prompt", "minhas instrucoes", "minhas diretrizes", "nao posso sair do personagem", "preciso continuar no personagem",
            "as an ai", "i am an ai", "i'm an ai", "im an ai", "i am a language model", "i'm a language model",
            "my instructions", "my system prompt", "my guidelines", "i cannot break character", "i can't break character"
        };

        // Termos de fora do mundo. Só contam se o jogador não os citou neste turno, porque
        // "ChatGPT? Nunca ouvi falar." é justamente a resposta ideal dentro do personagem.
        private static readonly string[] OutOfWorldTerms =
        {
            "openrouter", "openai", "anthropic", "chatgpt", "gpt", "deepseek", "claude", "gemini",
            "inteligencia artificial", "modelo de linguagem", "modelo de ia", "assistente virtual", "assistente de ia",
            "prompt de sistema", "prompt do sistema", "fora do personagem", "sair do personagem", "quebrar o personagem",
            "language model", "system prompt", "ai assistant", "out of character", "ooc", "npc"
        };

        private readonly Regex _selfIdentification;
        private readonly Regex _outOfWorld;
        private readonly string _normalizedSeal;
        private readonly HashSet<string> _leakShingles;

        public CharacterGuard(NpcPersona persona, string leakCorpus, string sessionSeal)
        {
            var allowed = new HashSet<string>((persona.allowedTerms ?? Array.Empty<string>()).Select(Normalize));

            _selfIdentification = BuildPattern(SelfIdentificationPhrases.Where(term => !allowed.Contains(term)));
            _outOfWorld = BuildPattern(OutOfWorldTerms
                .Concat((persona.additionalForbiddenTerms ?? Array.Empty<string>()).Select(Normalize))
                .Where(term => !allowed.Contains(term)));

            _normalizedSeal = Normalize(sessionSeal);
            _leakShingles = BuildShingles(leakCorpus);
        }

        /// <summary>Retorna true quando a fala pode ir para a tela. O motivo é para log e nunca é mostrado ao jogador.</summary>
        public bool Check(NpcReply reply, string playerText, out string reason)
        {
            string spoken = Normalize(string.Join(" ", reply.Beats.Select(beat => beat.Text)));
            string player = Normalize(playerText);

            if (_normalizedSeal.Length > 0 && spoken.Contains(_normalizedSeal))
            {
                reason = "repetiu o selo da sessão";
                return false;
            }

            Match selfMatch = _selfIdentification?.Match(spoken);
            if (selfMatch != null && selfMatch.Success)
            {
                reason = $"autoidentificação: \"{selfMatch.Value}\"";
                return false;
            }

            if (_outOfWorld != null)
            {
                foreach (Match match in _outOfWorld.Matches(spoken))
                {
                    if (ContainsWholeTerm(player, match.Value)) continue;
                    reason = $"termo de fora do mundo: \"{match.Value}\"";
                    return false;
                }
            }

            if (LeaksPrompt(spoken))
            {
                reason = "repetiu um trecho do prompt de sistema";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>Remove acentos e padroniza caixa e espaços.</summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            string decomposed = text.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            bool lastWasSpace = false;
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;

                bool isSpace = char.IsWhiteSpace(c);
                if (isSpace && lastWasSpace) continue;
                builder.Append(isSpace ? ' ' : char.ToLowerInvariant(NormalizeApostrophe(c)));
                lastWasSpace = isSpace;
            }

            return builder.ToString().Trim();
        }

        private static char NormalizeApostrophe(char c) => c == '’' ? '\'' : c;

        private static Regex BuildPattern(IEnumerable<string> terms)
        {
            string[] escaped = terms.Where(term => term.Length > 0).Select(Regex.Escape).ToArray();
            return escaped.Length == 0
                ? null
                : new Regex($@"(?<![\p{{L}}\p{{N}}])(?:{string.Join("|", escaped)})(?![\p{{L}}\p{{N}}])", RegexOptions.CultureInvariant);
        }

        private static bool ContainsWholeTerm(string text, string term) =>
            Regex.IsMatch(text, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(term)}(?![\p{{L}}\p{{N}}])", RegexOptions.CultureInvariant);

        private static string[] SplitWords(string normalizedText) =>
            Regex.Matches(normalizedText, @"[\p{L}\p{N}]+").Cast<Match>().Select(match => match.Value).ToArray();

        private static HashSet<string> BuildShingles(string corpus)
        {
            string[] words = SplitWords(Normalize(corpus));
            var shingles = new HashSet<string>();
            for (int i = 0; i + LeakShingleWords <= words.Length; i++)
                shingles.Add(string.Join(" ", words, i, LeakShingleWords));
            return shingles;
        }

        private bool LeaksPrompt(string normalizedSpoken)
        {
            string[] words = SplitWords(normalizedSpoken);
            for (int i = 0; i + LeakShingleWords <= words.Length; i++)
            {
                if (_leakShingles.Contains(string.Join(" ", words, i, LeakShingleWords)))
                    return true;
            }

            return false;
        }
    }
}
