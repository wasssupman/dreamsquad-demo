using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-deck-page unit 4 — scene-facing runtime builder. One GameObject
    // under DreamcatcherPanel builds the whole page (card-art detail + deck strip +
    // card grid) and injects DreamcatcherDeckPageController. Mirrors SquadCharacterPage
    // but simpler — the image is a static Sprite, so no SkeletonGraphic/material.
    public class DreamcatcherDeckPage : MonoBehaviour
    {
        [SerializeField] private DreamcatcherCardCatalog catalog;
        // dreamcatcher-attach-requirement unit 5 — 런타임 생성되는 detail view 에 넘길
        // "{유닛명} 전용" 접두 해석기 소스. 씬 와이어는 이 한 곳뿐.
        [SerializeField] private DefenderCatalog defenderCatalog;
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private TMP_FontAsset font;

        [Header("Layout")]
        [SerializeField] private float detailWidth = 0.34f;
        [SerializeField] private float headerHeight = 0.16f;
        // ui-polish 2026-07-18 — 큰 폰트 수용 위해 카드 영역 확대 + 하단 여백.
        [SerializeField] private float cardHeight = 0.50f;
        [SerializeField] private float cardBottomMargin = 0.03f;
        [SerializeField] private float artFeet = 0.52f;
        private static readonly Color DetailBg = new Color(0.08f, 0.09f, 0.13f, 1f);
        private static readonly Color HeaderBg = new Color(0.10f, 0.11f, 0.15f, 1f);
        private static readonly Color BrowserBg = new Color(0.04f, 0.05f, 0.07f, 1f);

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

            // ---- Detail (left) ----
            var detail = Panel("DetailPanel", self, new Vector2(0f, 0f), new Vector2(detailWidth, 1f), DetailBg);

            var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
            artGo.transform.SetParent(detail, false);
            var artRt = (RectTransform)artGo.transform;
            artRt.anchorMin = new Vector2(0.5f, artFeet); artRt.anchorMax = new Vector2(0.5f, artFeet);
            artRt.pivot = new Vector2(0.5f, 0f); artRt.sizeDelta = new Vector2(300f, 420f);
            var artImg = artGo.GetComponent<Image>();
            artImg.preserveAspect = true; artImg.raycastTarget = false;

            var cardRoot = Rect("CardRoot", detail, new Vector2(0f, cardBottomMargin), new Vector2(1f, cardHeight));

            var detailView = detail.gameObject.AddComponent<DreamcatcherCardDetailView>();
            SetField(detailView, "artImage", artImg);
            SetField(detailView, "cardRoot", cardRoot);
            SetField(detailView, "font", font);
            SetField(detailView, "defenderCatalog", defenderCatalog);

            // ---- Deck strip (right, top band) ----
            var strip = Panel("DeckStrip", self, new Vector2(detailWidth, 1f - headerHeight), new Vector2(1f, 1f), HeaderBg);
            var deckStrip = strip.gameObject.AddComponent<DreamcatcherDeckStrip>();
            SetField(deckStrip, "catalog", catalog);
            SetField(deckStrip, "font", font);

            // ---- Card browser (right, below) ----
            var browserRt = Panel("BrowserPanel", self, new Vector2(detailWidth, 0f), new Vector2(1f, 1f - headerHeight), BrowserBg);
            var browser = browserRt.gameObject.AddComponent<DreamcatcherCardBrowser>();
            SetField(browser, "font", font);

            // ---- Controller (inactive → inject → activate) ----
            var ctrlGo = new GameObject("Controller");
            ctrlGo.transform.SetParent(self, false);
            ctrlGo.SetActive(false);
            var controller = ctrlGo.AddComponent<DreamcatcherDeckPageController>();
            SetField(controller, "catalog", catalog);
            SetField(controller, "profileSO", profileSO);
            SetField(controller, "detailView", detailView);
            SetField(controller, "browser", browser);
            SetField(controller, "deckStrip", deckStrip);
            ctrlGo.SetActive(true);
        }

        private RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        private RectTransform Panel(string name, Transform parent, Vector2 aMin, Vector2 aMax, Color bg)
        {
            var rt = Rect(name, parent, aMin, aMax);
            rt.gameObject.AddComponent<Image>().color = bg;
            return rt;
        }

        private static void SetField(object target, string name, object value)
        {
            var f = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
        }
    }
}
