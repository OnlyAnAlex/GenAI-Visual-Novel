using System.Collections.Generic;

namespace VnNpc
{
    /// <summary>
    /// Janela de turnos enviada ao modelo. Descarta sempre turnos inteiros, do mais antigo para o mais novo.
    /// A persona não mora aqui: o prompt de sistema é reenviado em toda requisição, então nunca sai da janela.
    /// </summary>
    public sealed class ConversationHistory
    {
        private readonly int _maxTurns;
        private readonly int _maxChars;
        private readonly Queue<(string Player, string Reply)> _turns = new Queue<(string, string)>();
        private int _totalChars;

        public ConversationHistory(int maxTurns, int maxChars)
        {
            _maxTurns = maxTurns;
            _maxChars = maxChars;
        }

        public void AddTurn(string wrappedPlayerSpeech, string replyJson)
        {
            _turns.Enqueue((wrappedPlayerSpeech, replyJson));
            _totalChars += wrappedPlayerSpeech.Length + replyJson.Length;

            while (_turns.Count > 1 && (_turns.Count > _maxTurns || _totalChars > _maxChars))
            {
                (string player, string reply) = _turns.Dequeue();
                _totalChars -= player.Length + reply.Length;
            }
        }

        public void AppendTo(List<ChatMessage> messages)
        {
            foreach ((string player, string reply) in _turns)
            {
                messages.Add(new ChatMessage(ChatMessage.UserRole, player));
                messages.Add(new ChatMessage(ChatMessage.AssistantRole, reply));
            }
        }
    }
}
