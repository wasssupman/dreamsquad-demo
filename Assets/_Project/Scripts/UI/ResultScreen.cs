using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Core.Api;

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
        private GameObject _panel;
        private bool _built;

        public event Action RestartRequested;

        private void Awake()
        {
            BuildCanvas();
            gameObject.SetActive(false);
            if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
        }

        private void OnDestroy()
        {
            if (restartButton != null) restartButton.onClick.RemoveListener(OnRestartClicked);
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

        // tournament-play-report Unit 4 — swap the bot leaderboard for the real
        // tournament ranking once it arrives (popup shows the bot list first;
        // guests / fetch failures simply never get here). A response landing
        // after the popup closed (e.g. instant RESTART) is dropped.
        //
        // Tournament slots are pre-assigned (maxEntryCount, currently 10): every
        // slot is rendered, and slots no opponent has taken yet read WAITING...
        // (English — the TMP font ships no Korean glyphs).
        public void UpdateLeaderboard(TournamentApi.ResultData data, string ownUserId)
        {
            if (!gameObject.activeSelf) return;
            if (data == null || leaderboardLabel == null) return;

            // dev server omits the schema's `rank` field (probe 2026-07-08) —
            // order by score and derive the display rank from position; a
            // server-provided rank (>0) wins when it ever appears.
            var entries = data.entries != null
                ? new List<TournamentApi.ResultEntry>(data.entries)
                : new List<TournamentApi.ResultEntry>();
            entries.Sort((a, b) => b.score.CompareTo(a.score));

            int totalSlots = Mathf.Max(data.maxEntryCount, entries.Count);
            if (totalSlots == 0) return; // nothing meaningful to draw — keep the bot list

            var builder = new StringBuilder();
            builder.AppendLine("<mspace=0.65em>RANK NAME       SCORE</mspace>");
            for (int i = 0; i < totalSlots; i++)
            {
                if (i < entries.Count)
                {
                    var e = entries[i];
                    int rank = e.rank > 0 ? e.rank : i + 1;
                    string line = $"{rank.ToString().PadRight(4)}{DisplayName(e.userName).PadRight(10)}{e.score.ToString().PadLeft(6)}";
                    if (!string.IsNullOrEmpty(ownUserId) && e.userId == ownUserId)
                        builder.AppendLine($"<mspace=0.65em><color=#FFD54A>{line}</color></mspace>");
                    else
                        builder.AppendLine($"<mspace=0.65em>{line}</mspace>");
                }
                else
                {
                    string line = $"{(i + 1).ToString().PadRight(4)}{"WAITING...".PadRight(10)}{"-".PadLeft(6)}";
                    builder.AppendLine($"<mspace=0.65em><color=#9AA0A6>{line}</color></mspace>");
                }
            }
            leaderboardLabel.text = builder.ToString().TrimEnd();
        }

        // empty names would collapse the mspace column; long ones would break it.
        private static string DisplayName(string userName)
        {
            if (string.IsNullOrEmpty(userName)) return "?";
            return userName.Length <= 10 ? userName : userName.Substring(0, 10);
        }

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
            backdrop.GetComponent<Image>().color = UiOverlay.Dim;

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
            // tournament-play-report Unit 4 — sized for header + 10 tournament
            // slots (was 360/32 for the 6-row bot list).
            SetPreferredHeight(leaderboardPanel, 440f);

            leaderboardLabel = CreateLabel(leaderboardPanel.transform, "LeaderboardLabel", "", 28, TextAlignmentOptions.TopLeft);
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

            UiLayer.Apply(gameObject);
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
