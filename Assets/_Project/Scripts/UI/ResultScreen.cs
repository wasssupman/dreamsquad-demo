using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wassup.UI
{
    // Manages the battle result overlay (DEFEAT / VICTORY) and a Restart button.
    // Disabled on Awake. Emits RestartRequested when the player taps the Restart button;
    // BattleBridge subscribes to this event to tear down and restart the match.
    public class ResultScreen : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI resultLabel;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button redraftButton;

        public event Action RestartRequested;
        public event Action RedraftRequested;

        private void Awake()
        {
            gameObject.SetActive(false);
            if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
            if (redraftButton != null) redraftButton.onClick.AddListener(OnRedraftClicked);
        }

        private void OnDestroy()
        {
            if (restartButton != null) restartButton.onClick.RemoveListener(OnRestartClicked);
            if (redraftButton != null) redraftButton.onClick.RemoveListener(OnRedraftClicked);
        }

        public void ShowDefeat()
        {
            resultLabel.text = "DEFEAT";
            gameObject.SetActive(true);
        }

        public void ShowVictory()
        {
            resultLabel.text = "VICTORY";
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnRestartClicked() => RestartRequested?.Invoke();
        private void OnRedraftClicked() => RedraftRequested?.Invoke();
    }
}
