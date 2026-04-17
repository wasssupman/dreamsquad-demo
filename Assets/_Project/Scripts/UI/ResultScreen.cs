using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;

namespace Wassup.UI
{
    // Manages the battle result overlay (DEFEAT / VICTORY) and a Restart button.
    // Disabled on Awake. Emits RestartRequested when the player taps the Restart button;
    // BattleBridge subscribes to this event to tear down and restart the match.
    public class ResultScreen : MonoBehaviour
    {
        private TextMeshProUGUI resultLabel;
        private TextMeshProUGUI leaderboardLabel;
        private Button restartButton;
        private Button redraftButton;
        private GameObject _panel;
        private bool _built;

        public event Action RestartRequested;
        public event Action RedraftRequested;

        private void Awake()
        {
            BuildCanvas();
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
            ShowDefeat(0);
        }

        public void ShowDefeat(int playerScore)
        {
            ShowResult("DEFEAT", playerScore);
        }

        public void ShowVictory()
        {
            ShowVictory(0);
        }

        public void ShowVictory(int playerScore)
        {
            ShowResult("VICTORY", playerScore);
        }

        private void ShowResult(string resultText, int playerScore)
        {
            if (!_built) BuildCanvas();
            resultLabel.text = resultText;
            leaderboardLabel.text = BuildLeaderboard(playerScore);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnRestartClicked() => RestartRequested?.Invoke();
        private void OnRedraftClicked() => RedraftRequested?.Invoke();

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4;

            if (gameObject.GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(transform, false);
            var backdropRect = (RectTransform)backdrop.transform;
            Stretch(backdropRect);
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.84f);

            _panel = new GameObject("ResultPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            _panel.transform.SetParent(transform, false);
            var panelRect = (RectTransform)_panel.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(720f, 760f);
            _panel.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.16f, 0.96f);

            var layout = _panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 36, 36);
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            resultLabel = CreateLabel(_panel.transform, "ResultLabel", "", 72, TextAlignmentOptions.Center);
            SetPreferredHeight(resultLabel.gameObject, 100f);

            var leaderboardPanel = new GameObject("LeaderboardPanel", typeof(RectTransform), typeof(Image));
            leaderboardPanel.transform.SetParent(_panel.transform, false);
            leaderboardPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.24f);
            SetPreferredHeight(leaderboardPanel, 360f);

            leaderboardLabel = CreateLabel(leaderboardPanel.transform, "LeaderboardLabel", "", 32, TextAlignmentOptions.TopLeft);
            leaderboardLabel.richText = true;
            leaderboardLabel.enableWordWrapping = false;
            var leaderboardRect = (RectTransform)leaderboardLabel.transform;
            leaderboardRect.anchorMin = Vector2.zero;
            leaderboardRect.anchorMax = Vector2.one;
            leaderboardRect.offsetMin = new Vector2(28f, 24f);
            leaderboardRect.offsetMax = new Vector2(-28f, -24f);

            var buttonRow = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            buttonRow.transform.SetParent(_panel.transform, false);
            SetPreferredHeight(buttonRow, 90f);
            var buttonLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 20f;
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonLayout.childControlWidth = false;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = false;
            buttonLayout.childForceExpandHeight = false;

            restartButton = CreateButton(buttonRow.transform, "RestartButton", "RESTART", new Color(0.2f, 0.5f, 0.9f, 1f));
            redraftButton = CreateButton(buttonRow.transform, "RedraftButton", "REDRAFT", new Color(0.85f, 0.55f, 0.18f, 1f));
        }

        private static string BuildLeaderboard(int playerScore)
        {
            var botScores = BotScoreGenerator.GenerateBotScores(5, playerScore, playerScore);
            var rows = new List<(string name, int score, bool isPlayer)>(botScores.Length + 1);

            for (int i = 0; i < botScores.Length; i++)
                rows.Add(($"Bot-{i + 1}", botScores[i], false));

            rows.Add(("YOU", playerScore, true));
            rows.Sort((a, b) => b.score.CompareTo(a.score));

            var builder = new StringBuilder();
            builder.AppendLine("<mspace=0.65em>RANK NAME       SCORE</mspace>");

            for (int i = 0; i < rows.Count; i++)
            {
                string line = $"{(i + 1).ToString().PadRight(4)}{rows[i].name.PadRight(10)}{rows[i].score.ToString().PadLeft(6)}";
                if (rows[i].isPlayer)
                    builder.AppendLine($"<mspace=0.65em><color=#FFD54A>{line}</color></mspace>");
                else
                    builder.AppendLine($"<mspace=0.65em>{line}</mspace>");
            }

            return builder.ToString().TrimEnd();
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text,
            int fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = true;
            return tmp;
        }

        private static Button CreateButton(Transform parent, string name, string text, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            var layout = go.GetComponent<LayoutElement>();
            layout.preferredWidth = 260f;
            layout.preferredHeight = 90f;

            var button = go.GetComponent<Button>();

            var label = CreateLabel(go.transform, "Label", text, 34, TextAlignmentOptions.Center);
            var labelRect = (RectTransform)label.transform;
            Stretch(labelRect);
            return button;
        }

        private static void SetPreferredHeight(GameObject target, float height)
        {
            var layout = target.GetComponent<LayoutElement>();
            if (layout == null) layout = target.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
