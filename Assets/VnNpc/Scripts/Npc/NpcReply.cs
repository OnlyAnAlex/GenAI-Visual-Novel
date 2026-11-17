using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VnNpc
{
    /// <summary>Uma "tela" da visual novel: uma expressão e o texto que aparece com ela.</summary>
    public readonly struct NpcBeat
    {
        public readonly string ExpressionId;
        public readonly string Text;

        public NpcBeat(string expressionId, string text)
        {
            ExpressionId = expressionId;
            Text = text;
        }
    }

    public sealed class NpcReply
    {
        public IReadOnlyList<NpcBeat> Beats { get; }
        public bool RealWorldConcern { get; }

        /// <summary>True quando a fala veio do texto de autoria, e não do modelo.</summary>
        public bool IsFallback { get; }

        /// <summary>Motivo técnico do fallback. None quando o modelo respondeu ou quando só a guarda rejeitou.</summary>
        public LlmFailure Failure { get; }

        public NpcReply(IReadOnlyList<NpcBeat> beats, bool realWorldConcern, bool isFallback = false, LlmFailure failure = LlmFailure.None)
        {
            Beats = beats;
            RealWorldConcern = realWorldConcern;
            IsFallback = isFallback;
            Failure = failure;
        }

        /// <summary>Forma canônica guardada no histórico, para o modelo continuar vendo apenas JSON válido.</summary>
        public string ToHistoryJson()
        {
            var beats = new JArray();
            foreach (NpcBeat beat in Beats)
            {
                beats.Add(new JObject
                {
                    [ChatRequestBuilder.ExpressionField] = beat.ExpressionId,
                    [ChatRequestBuilder.TextField] = beat.Text
                });
            }

            return new JObject
            {
                [ChatRequestBuilder.BeatsField] = beats,
                [ChatRequestBuilder.ConcernField] = RealWorldConcern
            }.ToString(Formatting.None);
        }
    }
}
