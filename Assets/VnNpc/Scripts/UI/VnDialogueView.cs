using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace VnNpc
{
    /// <summary>
    /// Só a apresentação da visual novel: retrato, nome, caixa de diálogo, efeito de digitação e campo de texto.
    /// Não conhece a conversa nem a rede. Avisa o controlador pelo evento Submitted.
    /// </summary>
    public sealed class VnDialogueView : MonoBehaviour
    {
        private const float PlaceholderSaturation = 0.45f;
        private const float PlaceholderValue = 0.75f;
        private const int PlaceholderHueSteps = 360;
        private const int PlaceholderHashFactor = 37;
        private const int FullyVisible = 99999; // Valor padrão do TextMeshPro para maxVisibleCharacters.

        [Header("Personagem")]
        [SerializeField] private UnityEngine.UI.Image portrait;
        [SerializeField] private TMP_Text missingSpriteLabel;
        [SerializeField] private TMP_Text speakerNameText;

        [Header("Diálogo")]
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private GameObject continueIndicator;
        [SerializeField] private GameObject thinkingIndicator;
        [SerializeField] private UnityEngine.UI.Button advanceButton;
        [SerializeField, Min(1f)] private float charactersPerSecond = 45f;

        [Header("Entrada")]
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private UnityEngine.UI.Button sendButton;

        [Header("Fora da ficção")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject noticePanel;
        [SerializeField] private TMP_Text noticeText;
        [SerializeField] private UnityEngine.UI.Button noticeCloseButton;

        private bool _isTyping;
        private bool _skipRequested;
        private bool _advanceRequested;

        public event Action<string> Submitted;

        private void Awake()
        {
            // O texto do modelo e o do jogador não são confiáveis. Com rich text ligado, uma tag <size> ou <color> alteraria a interface.
            dialogueText.richText = false;
            inputField.richText = false;
            inputField.characterLimit = PersonaPromptBuilder.MaxPlayerChars;

            advanceButton.onClick.AddListener(HandleAdvanceClicked);
            sendButton.onClick.AddListener(HandleSendClicked);
            noticeCloseButton.onClick.AddListener(HideNotice);
            inputField.onSubmit.AddListener(HandleInputSubmitted);

            continueIndicator.SetActive(false);
            thinkingIndicator.SetActive(false);
            noticePanel.SetActive(false);
            statusText.text = string.Empty;
        }

        private void OnDestroy()
        {
            advanceButton.onClick.RemoveListener(HandleAdvanceClicked);
            sendButton.onClick.RemoveListener(HandleSendClicked);
            noticeCloseButton.onClick.RemoveListener(HideNotice);
            inputField.onSubmit.RemoveListener(HandleInputSubmitted);
        }

        public void SetSpeakerName(string speakerName) => speakerNameText.text = speakerName;

        public void SetExpression(string expressionId, Sprite sprite)
        {
            bool hasSprite = sprite != null;
            portrait.sprite = hasSprite ? sprite : null;
            portrait.color = hasSprite ? Color.white : PlaceholderColor(expressionId);
            missingSpriteLabel.gameObject.SetActive(!hasSprite);
            missingSpriteLabel.text = expressionId;
        }

        public void SetThinking(bool isThinking)
        {
            thinkingIndicator.SetActive(isThinking);
            if (isThinking) dialogueText.text = string.Empty;
        }

        public void SetInputEnabled(bool isEnabled)
        {
            inputField.interactable = isEnabled;
            sendButton.interactable = isEnabled;
            if (isEnabled) inputField.ActivateInputField();
        }

        public void SetStatus(string message) => statusText.text = message ?? string.Empty;

        public void ShowNotice(string message)
        {
            noticeText.text = message;
            noticePanel.SetActive(true);
        }

        /// <summary>Corrotina. Termina com o texto todo visível. Um clique durante a digitação completa a linha.</summary>
        public IEnumerator TypeLine(string line)
        {
            continueIndicator.SetActive(false);
            SelectAdvanceButton();
            dialogueText.text = line;
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.ForceMeshUpdate();

            int totalCharacters = dialogueText.textInfo.characterCount;
            float visibleCharacters = 0f;
            _isTyping = true;
            _skipRequested = false;

            while (visibleCharacters < totalCharacters && !_skipRequested)
            {
                visibleCharacters += charactersPerSecond * Time.unscaledDeltaTime;
                dialogueText.maxVisibleCharacters = Mathf.Min(Mathf.FloorToInt(visibleCharacters), totalCharacters);
                yield return null;
            }

            ShowFullLine();
        }

        /// <summary>Deixa a caixa em estado estável, inclusive se uma corrotina foi interrompida no meio da digitação.</summary>
        public void ShowFullLine()
        {
            dialogueText.maxVisibleCharacters = FullyVisible;
            continueIndicator.SetActive(false);
            _isTyping = false;
        }

        /// <summary>Corrotina. Espera o clique, ou Enter e Espaço, para passar à próxima tela.</summary>
        public IEnumerator WaitForAdvance()
        {
            continueIndicator.SetActive(true);
            _advanceRequested = false;

            while (!_advanceRequested)
                yield return null;

            continueIndicator.SetActive(false);
        }

        private void SelectAdvanceButton()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
                eventSystem.SetSelectedGameObject(advanceButton.gameObject);
        }

        private void HandleAdvanceClicked()
        {
            if (_isTyping) _skipRequested = true;
            else _advanceRequested = true;
        }

        private void HandleSendClicked() => SubmitInput(inputField.text);

        private void HandleInputSubmitted(string text) => SubmitInput(text);

        private void SubmitInput(string text)
        {
            if (!inputField.interactable || string.IsNullOrWhiteSpace(text)) return;

            inputField.text = string.Empty;
            Submitted?.Invoke(text);
        }

        private void HideNotice() => noticePanel.SetActive(false);

        /// <summary>Permite testar sem arte: cada expressão sem sprite ganha uma cor própria, sempre a mesma.</summary>
        private static Color PlaceholderColor(string expressionId)
        {
            int hash = 0;
            foreach (char c in expressionId)
                hash = (hash * PlaceholderHashFactor + c) % PlaceholderHueSteps;

            return Color.HSVToRGB(hash / (float)PlaceholderHueSteps, PlaceholderSaturation, PlaceholderValue);
        }
    }
}
