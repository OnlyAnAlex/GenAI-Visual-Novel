using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VnNpc
{
    /// <summary>
    /// Orquestra um turno: fala do jogador, modelo, validação de formato, guarda de personagem, reparo e fallback.
    /// Turnos rejeitados não entram no histórico, para uma quebra de personagem não contaminar as respostas seguintes.
    /// </summary>
    public sealed class NpcConversation
    {
        private const int MaxRepairAttempts = 1;
        private const int MaxHistoryTurns = 16;
        private const int MaxHistoryChars = 12000;
        private const int SessionSealLength = 12;

        private const string BrokenCharacterNote =
            "A tentativa anterior foi descartada porque soou como alguém fora da cena. Responda de novo como {{char}}, " +
            "reagindo ao que {{user}} disse como {{char}} reagiria, sem mencionar esta correção.";

        private const string BrokenFormatNote =
            "A tentativa anterior foi descartada porque não era um JSON válido no formato pedido. Responda de novo somente com o JSON.";

        private readonly NpcPersona _persona;
        private readonly LlmEndpointConfig _config;
        private readonly OpenAiCompatibleClient _client;
        private readonly PersonaPromptBuilder _prompts;
        private readonly NpcReplyParser _parser;
        private readonly CharacterGuard _guard;
        private readonly ConversationHistory _history = new ConversationHistory(MaxHistoryTurns, MaxHistoryChars);
        private readonly string _systemPrompt;
        private readonly string[] _expressionIds;
        private int _lastFallbackIndex = -1;

        public NpcConversation(NpcPersona persona, LlmEndpointConfig config, OpenAiCompatibleClient client, string playerName)
        {
            _persona = persona;
            _config = config;
            _client = client;
            _prompts = new PersonaPromptBuilder(persona, playerName);
            _expressionIds = persona.expressions.Select(expression => expression.id).ToArray();
            _parser = new NpcReplyParser(_expressionIds, persona.defaultExpressionId);

            // O selo muda a cada sessão, então um vazamento antigo colado pelo jogador não gera alarme falso.
            string seal = "vn-" + Guid.NewGuid().ToString("N").Substring(0, SessionSealLength);
            _systemPrompt = _prompts.BuildSystemPrompt(seal);
            _guard = new CharacterGuard(persona, _prompts.BuildLeakCorpus(), seal);
        }

        /// <summary>Corrotina. Sempre entrega uma fala, mesmo quando tudo falha.</summary>
        public IEnumerator RunTurn(string rawPlayerText, Action<NpcReply> onReply)
        {
            string playerText = PersonaPromptBuilder.SanitizePlayerText(rawPlayerText);
            string repairNote = null;
            LlmFailure failure = LlmFailure.None;
            NpcReply accepted = null;

            for (int attempt = 0; attempt <= MaxRepairAttempts && accepted == null; attempt++)
            {
                LlmResult result = default;
                string body = ChatRequestBuilder.Build(_config, BuildMessages(playerText, repairNote), _expressionIds);
                yield return _client.Send(body, value => result = value);

                if (!result.Succeeded)
                {
                    failure = result.Failure;
                    LogFailure(result);
                    break;
                }

                if (!_parser.TryParse(result.Content, out NpcReply parsed, out string formatError))
                {
                    Debug.LogWarning($"[VnNpc] Formato rejeitado na tentativa {attempt + 1}: {formatError}. " +
                                     "Se isso se repetir, escolha um modelo com suporte a structured_outputs.");
                    repairNote = BrokenFormatNote;
                    continue;
                }

                if (!_guard.Check(parsed, playerText, out string guardReason))
                {
                    Debug.LogWarning($"[VnNpc] Guarda rejeitou a tentativa {attempt + 1}: {guardReason}.");
                    repairNote = BrokenCharacterNote;
                    continue;
                }

                accepted = parsed;
            }

            if (accepted == null)
            {
                onReply(BuildFallback(failure));
                yield break;
            }

            _history.AddTurn(PersonaPromptBuilder.WrapSpeech(playerText), accepted.ToHistoryJson());
            onReply(accepted);
        }

        private List<ChatMessage> BuildMessages(string playerText, string repairNote)
        {
            var messages = new List<ChatMessage> { new ChatMessage(ChatMessage.SystemRole, _systemPrompt) };
            _history.AppendTo(messages);
            messages.Add(new ChatMessage(ChatMessage.UserRole, _prompts.BuildCurrentTurn(playerText, repairNote)));
            return messages;
        }

        private NpcReply BuildFallback(LlmFailure failure)
        {
            AuthoredLine line = PickFallbackLine();
            var beat = new NpcBeat(line?.expressionId ?? _persona.defaultExpressionId, line?.text ?? "...");
            return new NpcReply(new[] { beat }, realWorldConcern: false, isFallback: true, failure: failure);
        }

        /// <summary>Sorteia uma fala evitando repetir a última, enquanto houver outra opção.</summary>
        private AuthoredLine PickFallbackLine()
        {
            AuthoredLine[] lines = _persona.fallbackLines;
            if (lines == null || lines.Length == 0) return null;

            int index = UnityEngine.Random.Range(0, lines.Length);
            if (index == _lastFallbackIndex) index = (index + 1) % lines.Length;
            _lastFallbackIndex = index;
            return lines[index];
        }

        private static void LogFailure(LlmResult result)
        {
            string message = $"[VnNpc] Falha na API ({result.Failure}): {result.Detail}";
            if (result.Failure.IsContentRelated()) Debug.LogWarning(message);
            else Debug.LogError(message);
        }
    }
}
