using System.Collections;
using UnityEngine;

namespace VnNpc
{
    /// <summary>
    /// Liga a conversa à interface e mantém o ciclo: esperando o jogador, pensando, apresentando as telas.
    /// Dependências em uma direção só: Controller → NpcConversation → Client, e Controller → View.
    /// </summary>
    public sealed class VnNpcController : MonoBehaviour
    {
        private const string ConnectionStatus = "Sem resposta do modelo. Veja o Console.";

        [SerializeField] private NpcPersona persona;
        [SerializeField] private LlmEndpointConfig endpoint;
        [SerializeField] private VnDialogueView view;
        [SerializeField, Tooltip("Valor da macro {{user}}.")] private string playerName = "Viajante";

        private OpenAiCompatibleClient _client;
        private NpcConversation _conversation;
        private bool _isBusy;
        private bool _isReady;

        private void Start()
        {
            if (persona == null || endpoint == null || view == null)
            {
                Debug.LogError("[VnNpc] Preencha persona, endpoint e view no VnNpcController.", this);
                enabled = false;
                return;
            }

            if (persona.expressions == null || persona.expressions.Length == 0)
            {
                Debug.LogError("[VnNpc] A persona precisa de ao menos uma expressão.", persona);
                enabled = false;
                return;
            }

            _client = new OpenAiCompatibleClient(endpoint);
            _conversation = new NpcConversation(persona, endpoint, _client, playerName);
            view.Submitted += HandleSubmitted;
            view.SetSpeakerName(persona.characterName);
            _isReady = true;

            _isBusy = true;
            view.SetInputEnabled(false);
            StartCoroutine(PresentOpening());
        }

        private void OnEnable()
        {
            if (_isReady) FinishTurn();
        }

        private void OnDisable()
        {
            // Desativar o objeto mata as corrotinas sem executar finally. Nesta ordem, nada acessa a requisição já liberada.
            StopAllCoroutines();
            if (_client != null) _client.AbortActive();
            _isBusy = false;
        }

        private void OnDestroy()
        {
            if (view != null) view.Submitted -= HandleSubmitted;
        }

        private void HandleSubmitted(string text)
        {
            if (_isBusy) return;

            _isBusy = true;
            view.SetInputEnabled(false);
            view.SetStatus(null);
            StartCoroutine(RunTurn(text));
        }

        private IEnumerator RunTurn(string text)
        {
            view.SetThinking(true);

            NpcReply reply = null;
            yield return _conversation.RunTurn(text, value => reply = value);

            view.SetThinking(false);

            if (reply.IsFallback && reply.Failure != LlmFailure.None && !reply.Failure.IsContentRelated())
                view.SetStatus(ConnectionStatus);

            yield return PresentBeats(reply);

            if (reply.RealWorldConcern)
                view.ShowNotice(persona.realWorldNotice);

            FinishTurn();
        }

        private IEnumerator PresentOpening()
        {
            AuthoredLine opening = persona.openingLine;
            string expressionId = opening != null && !string.IsNullOrEmpty(opening.expressionId)
                ? opening.expressionId
                : persona.defaultExpressionId;

            ShowExpression(expressionId);
            if (opening != null && !string.IsNullOrEmpty(opening.text))
                yield return view.TypeLine(opening.text);

            FinishTurn();
        }

        /// <summary>A última tela não espera clique: o campo de texto abre com a fala ainda visível.</summary>
        private IEnumerator PresentBeats(NpcReply reply)
        {
            for (int i = 0; i < reply.Beats.Count; i++)
            {
                NpcBeat beat = reply.Beats[i];
                ShowExpression(beat.ExpressionId);
                yield return view.TypeLine(beat.Text);

                if (i < reply.Beats.Count - 1)
                    yield return view.WaitForAdvance();
            }
        }

        private void ShowExpression(string expressionId)
        {
            ExpressionEntry entry = persona.FindExpression(expressionId);
            view.SetExpression(expressionId, entry?.sprite);
        }

        private void FinishTurn()
        {
            _isBusy = false;
            view.SetThinking(false);
            view.ShowFullLine();
            view.SetInputEnabled(true);
        }
    }
}
