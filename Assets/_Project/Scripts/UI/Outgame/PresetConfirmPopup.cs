using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wassup.UI
{
    // loadout-preset-page unit 4 — 소형 확인 팝업(적용 전 교체 확인). dim + 메시지 + [취소]/[적용].
    // 아웃게임 UI 전용, 전투(TimeManager) 의존 없음. 단일 역할 뷰 — Manager 싱글톤 아님.
    // 페이지 빌더가 페이지 루트 아래 생성해 컨트롤러에 주입한다.
    public class PresetConfirmPopup : MonoBehaviour
    {
        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color PanelBg = new Color(0.12f, 0.13f, 0.17f, 1f);
        private static readonly Color ConfirmBg = new Color(0.16f, 0.5f, 0.28f, 1f);
        private static readonly Color CancelBg = new Color(0.30f, 0.32f, 0.38f, 1f);

        private TMP_FontAsset _font;
        private GameObject _root;
        private TMP_Text _message;
        private Action _onConfirm;
        private bool _built;

        public void Init(TMP_FontAsset font) => _font = font;

        public void Show(string message, Action onConfirm)
        {
            EnsureBuilt();
            _onConfirm = onConfirm;
            if (_message != null) _message.text = message;
            _root.SetActive(true);
            transform.SetAsLastSibling(); // 페이지 위 최상단
        }

        private void Hide()
        {
            _onConfirm = null;
            if (_root != null) _root.SetActive(false);
        }

        private void OnConfirm()
        {
            var cb = _onConfirm;
            Hide();
            cb?.Invoke();
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            var selfRt = (RectTransform)transform;
            selfRt.anchorMin = Vector2.zero; selfRt.anchorMax = Vector2.one;
            selfRt.offsetMin = Vector2.zero; selfRt.offsetMax = Vector2.zero;

            // 전체 화면 dim (클릭 차단)
            _root = new GameObject("ConfirmRoot", typeof(RectTransform), typeof(Image));
            _root.transform.SetParent(transform, false);
            var rrt = (RectTransform)_root.transform;
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = Dim;

            // 중앙 패널
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_root.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = new Vector2(0.5f, 0.5f); prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f); prt.sizeDelta = new Vector2(760, 360);
            panel.GetComponent<Image>().color = PanelBg;

            var msgGo = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
            msgGo.transform.SetParent(panel.transform, false);
            var mrt = (RectTransform)msgGo.transform;
            mrt.anchorMin = new Vector2(0f, 0.35f); mrt.anchorMax = new Vector2(1f, 1f);
            mrt.offsetMin = new Vector2(40, 0); mrt.offsetMax = new Vector2(-40, -20);
            _message = msgGo.GetComponent<TextMeshProUGUI>();
            _message.fontSize = 34; _message.color = Color.white;
            _message.alignment = TextAlignmentOptions.Center; _message.enableWordWrapping = true;
            if (_font != null) _message.font = _font;

            MakeButton(panel.transform, "취소", CancelBg, new Vector2(-170, 60), Hide);
            MakeButton(panel.transform, "적용", ConfirmBg, new Vector2(170, 60), OnConfirm);

            _root.SetActive(false);
        }

        private void MakeButton(Transform parent, string label, Color color, Vector2 bottomPos, Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = bottomPos; rt.sizeDelta = new Vector2(260, 92);
            go.GetComponent<Image>().color = color;
            go.GetComponent<Button>().onClick.AddListener(() => onClick());

            var lg = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lg.transform.SetParent(go.transform, false);
            var lrt = (RectTransform)lg.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var t = lg.GetComponent<TextMeshProUGUI>();
            t.text = label; t.fontSize = 34; t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            if (_font != null) t.font = _font;
        }
    }
}
