using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // loadout-preset-page unit 4 — 씬 facing 런타임 빌더. PresetPanel 아래 한 GameObject 에서
    // 세로 스크롤 목록 골격 + 확인 팝업을 빌드하고 PresetPageController 를 주입한다.
    // DreamcatcherDeckPage/SquadCharacterPage 의 "비활성 생성 → SetField 주입 → 활성" 패턴과 동형.
    public class PresetPage : MonoBehaviour
    {
        [SerializeField] private SquadPresetCollection collection;
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private TMP_FontAsset font;

        private static readonly Color PageBg = new Color(0.04f, 0.05f, 0.07f, 1f);
        private bool _built;

        private void OnEnable()
        {
            if (_built) return;
            _built = true;
            Build();
        }

        private void Build()
        {
            var self = (RectTransform)transform;
            self.anchorMin = Vector2.zero; self.anchorMax = Vector2.one;
            self.offsetMin = Vector2.zero; self.offsetMax = Vector2.zero;

            var bg = gameObject.GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color = PageBg;

            // ---- Scroll (세로) — SquadRosterBrowser.EnsureGridBuilt 골격 재현 ----
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(self, false);
            var srt = (RectTransform)scrollGo.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var vrt = (RectTransform)viewportGo.transform;
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.02f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            // 상단 여백은 좌상단 CloseButton(높이 ~102px) 아래로 첫 아이템을 내리기 위해 크게.
            vlg.padding = new RectOffset(20, 20, 110, 20); vlg.spacing = 16;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = vrt; scroll.content = content;

            // ---- Confirm popup (scroll 이후 생성 → 최상단 렌더) ----
            var popupGo = new GameObject("ConfirmPopup", typeof(RectTransform));
            popupGo.transform.SetParent(self, false);
            var popup = popupGo.AddComponent<PresetConfirmPopup>();

            // ---- Controller (비활성 생성 → 주입 → 활성) ----
            var ctrlGo = new GameObject("Controller");
            ctrlGo.transform.SetParent(self, false);
            ctrlGo.SetActive(false);
            var controller = ctrlGo.AddComponent<PresetPageController>();
            SetField(controller, "collection", collection);
            SetField(controller, "profileSO", profileSO);
            SetField(controller, "font", font);
            SetField(controller, "content", content);
            SetField(controller, "confirmPopup", popup);
            ctrlGo.SetActive(true);

            // 좌상단 CloseButton(스쿼드/드캐와 동일, 씬에 authored)을 스크롤 위로 올려 클릭 가능하게.
            var closeTf = transform.Find("CloseButton");
            if (closeTf != null) closeTf.SetAsLastSibling();
        }

        private static void SetField(object target, string name, object value)
        {
            var f = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
        }
    }
}
