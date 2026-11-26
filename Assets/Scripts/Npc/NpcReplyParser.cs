using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VnNpc
{
    /// <summary>
    /// Transforma a saída do modelo em um NpcReply válido. Nem todo provedor garante o schema, por isso tudo é validado aqui.
    /// Expressão desconhecida vira a padrão, e texto longo é dividido em mais telas.
    /// </summary>
    public sealed class NpcReplyParser
    {
        public const int MaxCharsPerBeat = 240;
        public const int MaxBeats = 6;

        private readonly HashSet<string> _validExpressions;
        private readonly string _defaultExpression;

        public NpcReplyParser(IEnumerable<string> validExpressions, string defaultExpression)
        {
            _validExpressions = new HashSet<string>(validExpressions);
            _defaultExpression = defaultExpression;
        }

        public bool TryParse(string raw, out NpcReply reply, out string error)
        {
            reply = null;
            if (!TryExtractObject(raw, out JObject root, out error)) return false;

            if (!(root[ChatRequestBuilder.BeatsField] is JArray rawBeats))
            {
                error = $"O campo {ChatRequestBuilder.BeatsField} não existe ou não é uma lista.";
                return false;
            }

            var beats = new List<NpcBeat>(rawBeats.Count);
            foreach (JToken token in rawBeats)
            {
                if (!(token is JObject rawBeat)) continue;
                string text = CleanText((rawBeat[ChatRequestBuilder.TextField] as JValue)?.Value as string);
                if (text.Length == 0) continue;
                AddPaged(beats, ResolveExpression(rawBeat), text);
            }

            if (beats.Count == 0)
            {
                error = "A resposta não trouxe nenhuma fala.";
                return false;
            }

            bool concern = (root[ChatRequestBuilder.ConcernField] as JValue)?.Value is bool flag && flag;
            reply = new NpcReply(beats, concern);
            return true;
        }

        private static bool TryExtractObject(string raw, out JObject root, out string error)
        {
            root = null;
            error = null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "Resposta vazia.";
                return false;
            }

            // Tolera cercas de markdown ou texto em volta, comuns em modelos que ignoram response_format.
            int start = raw.IndexOf('{');
            int end = raw.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                error = "A resposta não contém um objeto JSON.";
                return false;
            }

            try
            {
                root = JToken.Parse(raw.Substring(start, end - start + 1)) as JObject;
            }
            catch (JsonException exception)
            {
                error = "JSON inválido: " + exception.Message;
                return false;
            }

            if (root != null) return true;
            error = "A resposta não é um objeto JSON.";
            return false;
        }

        private string ResolveExpression(JObject rawBeat)
        {
            string id = ((rawBeat[ChatRequestBuilder.ExpressionField] as JValue)?.Value as string)?.Trim().ToLowerInvariant();
            return id != null && _validExpressions.Contains(id) ? id : _defaultExpression;
        }

        private static string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var builder = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == '\n' || c == '\r' || c == '\t') builder.Append(' ');
                else if (!char.IsControl(c)) builder.Append(c);
            }

            return builder.ToString().Trim();
        }

        private static void AddPaged(List<NpcBeat> beats, string expression, string text)
        {
            string remaining = text;
            while (remaining.Length > 0 && beats.Count < MaxBeats)
            {
                int cut = FindPageBreak(remaining);
                beats.Add(new NpcBeat(expression, remaining.Substring(0, cut).Trim()));
                remaining = remaining.Substring(cut).TrimStart();
            }
        }

        /// <summary>Corta no fim de frase mais próximo do limite. Se não houver, no último espaço.</summary>
        private static int FindPageBreak(string text)
        {
            if (text.Length <= MaxCharsPerBeat) return text.Length;

            int lastSpace = -1;
            for (int i = MaxCharsPerBeat - 1; i > 0; i--)
            {
                char c = text[i];
                if ((c == '.' || c == '!' || c == '?' || c == '…') && char.IsWhiteSpace(text[i + 1]))
                    return i + 1;
                if (lastSpace < 0 && c == ' ')
                    lastSpace = i;
            }

            return lastSpace > 0 ? lastSpace : MaxCharsPerBeat;
        }
    }
}
