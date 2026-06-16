using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // wave-authoring-test-mode unit 4 — 테스트 모드 플랜 피커. TestModeConfig.planCatalog
    // 의 작성 플랜을 버튼으로 나열, 선택 시 TestModeContext.Set 후 BattleScene 로드.
    // UI 는 OnEnable 에서 자체 빌드(SquadBuilderView 패턴).
    public class TestModePanelView : MonoBehaviour
    {
        [SerializeField] private TestModeConfig config;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private OutgameMenuController menu;

        private bool _built;

        private void OnEnable()
        {
            if (_built) return;
            _built = true;
            Build();
        }

        private void Build()
        {
            var rt = transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            var bg = GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.07f, 0.10f, 0.97f);

            AddText("Title", "TEST MODE — SELECT WAVE PLAN", 44,
                new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.95f));

            var listGo = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup));
            listGo.transform.SetParent(transform, false);
            var lrt = (RectTransform)listGo.transform;
            lrt.anchorMin = new Vector2(0.28f, 0.22f);
            lrt.anchorMax = new Vector2(0.72f, 0.8f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var vlg = listGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 16;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            int count = (config != null && config.planCatalog != null) ? config.planCatalog.Length : 0;
            if (count == 0)
            {
                AddText("Empty", "(planCatalog is empty — check TestModeConfig)", 26,
                    new Vector2(0.2f, 0.48f), new Vector2(0.8f, 0.6f));
            }
            for (int i = 0; i < count; i++)
            {
                var plan = config.planCatalog[i];
                if (plan == null) continue;
                int waveCount = plan.waves != null ? plan.waves.Count : 0;
                string label = $"{plan.displayName}  ({waveCount} waves)";
                var captured = plan;
                var btn = CreateButton(label, listGo.transform, new Vector2(0, 72),
                    new Color(0.18f, 0.32f, 0.62f, 1f));
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 72;
                btn.onClick.AddListener(() => StartPlan(captured));
            }

            var close = CreateButton("CLOSE", transform, new Vector2(220, 64),
                new Color(0.42f, 0.16f, 0.18f, 1f));
            var crt = (RectTransform)close.transform;
            crt.anchorMin = new Vector2(0.5f, 0.06f);
            crt.anchorMax = new Vector2(0.5f, 0.06f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(220, 64);
            close.onClick.AddListener(Close);
        }

        private void StartPlan(WavePlanAsset plan)
        {
            TestModeContext.Set(plan, config != null ? config.defenderPreset : null);
            Debug.Log($"[TestModePanelView] 테스트 모드 시작 — plan='{(plan != null ? plan.displayName : "NULL")}'.");
            SceneManager.LoadScene(SceneNames.Battle);
        }

        private void Close()
        {
            if (menu != null) menu.OnClosePanels();
            else gameObject.SetActive(false);
        }

        private Button CreateButton(string text, Transform parent, Vector2 size, Color bg)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;
            go.GetComponent<Image>().color = bg;

            var l = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            l.transform.SetParent(go.transform, false);
            var lrt = (RectTransform)l.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var label = l.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 26;
            label.color = Color.white;
            if (font != null) label.font = font;
            return go.GetComponent<Button>();
        }

        private void AddText(string name, string text, int fontSize, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            if (font != null) tmp.font = font;
        }
    }
}
