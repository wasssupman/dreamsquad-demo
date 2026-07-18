using System.Reflection;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // squad-character-page Unit 5 — the scene-facing builder. Sits on one GameObject
    // under SquadPanel and constructs the whole character page at runtime (detail
    // panel + live-Spine backdrop + header strip + roster browser), then hands the
    // pieces to SquadCharacterPageController. Runtime construction mirrors the
    // project's SquadBuilderView pattern; the only authored state is this component
    // and its asset references, so the scene footprint (and rollback) is minimal.
    //
    // Layout constants are the values verified via the Play overlay preview
    // (spine scale 2.2 / feet 0.42, detail 1/3, header band on top of the 2/3).
    public class SquadCharacterPage : MonoBehaviour
    {
        [SerializeField] private DefenderCatalog catalog;
        [SerializeField] private DreamstoneCatalog stoneCatalog;
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private Material skeletonGraphicMaterial;
        [SerializeField] private TMP_FontAsset font;

        [Header("Layout")]
        [SerializeField] private float detailWidth = 0.34f;
        [SerializeField] private float headerHeight = 0.14f;
        [SerializeField] private float spineScale = 2.2f;
        [SerializeField] private float spineFeet = 0.42f;
        [SerializeField] private float cardHeight = 0.40f;

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

            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                canvas.rootCanvas.additionalShaderChannels |=
                    AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 |
                    AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;

            // ---- Detail panel (left) ----
            var detail = Panel("DetailPanel", self, new Vector2(0f, 0f), new Vector2(detailWidth, 1f), DetailBg);

            var spineGo = new GameObject("SpineView", typeof(RectTransform));
            spineGo.transform.SetParent(detail, false);
            var sg = spineGo.AddComponent<SkeletonGraphic>();
            sg.material = skeletonGraphicMaterial;
            sg.raycastTarget = false;
            var sgRt = (RectTransform)spineGo.transform;
            sgRt.anchorMin = new Vector2(0.5f, spineFeet); sgRt.anchorMax = new Vector2(0.5f, spineFeet);
            sgRt.pivot = new Vector2(0.5f, 0f); sgRt.sizeDelta = new Vector2(400f, 600f);
            spineGo.transform.localScale = Vector3.one * spineScale;

            var pf = Image("PortraitFallback", detail, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white);
            pf.rectTransform.sizeDelta = new Vector2(300f, 300f);
            pf.preserveAspect = true; pf.raycastTarget = false; pf.enabled = false;

            var cardRoot = Rect("CardRoot", detail, new Vector2(0f, 0f), new Vector2(1f, cardHeight));

            var detailView = detail.gameObject.AddComponent<SquadUnitDetailView>();
            SetField(detailView, "spineView", sg);
            SetField(detailView, "portraitFallback", pf);
            SetField(detailView, "cardRoot", cardRoot);
            SetField(detailView, "font", font);

            // ---- Header strip (right, top band) ----
            var header = Panel("HeaderStrip", self, new Vector2(detailWidth, 1f - headerHeight), new Vector2(1f, 1f), HeaderBg);
            var headerStrip = header.gameObject.AddComponent<SquadHeaderStrip>();
            SetField(headerStrip, "catalog", catalog);
            SetField(headerStrip, "stoneCatalog", stoneCatalog);
            SetField(headerStrip, "font", font);

            // ---- Roster browser (right, below header) ----
            var browserRt = Panel("BrowserPanel", self, new Vector2(detailWidth, 0f), new Vector2(1f, 1f - headerHeight), BrowserBg);
            var browser = browserRt.gameObject.AddComponent<SquadRosterBrowser>();
            SetField(browser, "font", font);

            // ---- Controller (built inactive so its fields are set before OnEnable) ----
            var ctrlGo = new GameObject("Controller");
            ctrlGo.transform.SetParent(self, false);
            ctrlGo.SetActive(false);
            var controller = ctrlGo.AddComponent<SquadCharacterPageController>();
            SetField(controller, "catalog", catalog);
            SetField(controller, "stoneCatalog", stoneCatalog);
            SetField(controller, "profileSO", profileSO);
            SetField(controller, "detailView", detailView);
            SetField(controller, "browser", browser);
            SetField(controller, "header", headerStrip);
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

        private Image Image(string name, Transform parent, Vector2 aMin, Vector2 aMax, Color color)
        {
            var rt = Rect(name, parent, aMin, aMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static void SetField(object target, string name, object value)
        {
            var f = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
        }
    }
}
