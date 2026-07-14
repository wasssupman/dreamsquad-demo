using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;

namespace Wassup.UI
{
    // gift-phase-presentation unit 1 — 카드 출신(내 덱 / Lucid / Rim).
    // 판정은 호출측이 GiftAddedEntryIds + GiftKind 로 수행해 넘긴다(계약 2).
    public enum GiftOrigin { None, Lucid, Rim }

    // gift-phase-presentation unit 1 — 선물 페이즈 카드 1장 위젯.
    // 계층: FrameRoot(포일 홀로 프레임, 선물만) → Plate(카드 몸체) → Art + Name(앞면)
    //       → Back(뒷면 덮개 + 문양, 플립 리빌용).
    // 루트 RectTransform 하나만 안무 대상. 위젯은 face/origin 상태만 소유하고
    // 트윈/타이밍은 전부 GiftPhaseView(unit 2) 소관.
    public class GiftCardWidget
    {
        public GameObject Go;
        public RectTransform Rt;

        private Image _frame;     // 크리스프 틴트 링(기본 UI 머티리얼)
        private Image _foil;      // 포일 시머 오버레이(_MainTex 미샘플 — 링 위 홀로 워시)
        private Image _plate;
        private Image _art;
        private TextMeshProUGUI _name;
        private Image _back;
        private Image _backEmblem;

        // 전 위젯이 공유하는 절차 스프라이트/포일 머티리얼(카드당 인스턴스 금지).
        // GiftPhaseView 가 1회 빌드해 넘기고 파괴 시 Dispose 를 호출한다.
        public class Shared
        {
            public Sprite FrameSprite;   // 투명 fill + 흰 보더 링(틴트용)
            public Sprite PlateSprite;   // 흰 fill(틴트용)
            public Sprite EmblemSprite;  // 흰 링(뒷면 문양, 틴트용)
            public Material LucidFoil;
            public Material RimFoil;

            public static Shared Build(GiftConfig cfg)
            {
                var s = new Shared
                {
                    FrameSprite = UiRoundedSprite.Make(
                        cfg.frameRadius, cfg.frameBorder, Color.clear, Color.white),
                    PlateSprite = UiRoundedSprite.Make(
                        cfg.frameRadius, 0f, Color.white, Color.white),
                    EmblemSprite = UiRoundedSprite.MakeCircle(
                        96, Color.clear, 10f, Color.white),
                };
                var shader = Shader.Find("Wassup/UI/DraftCardFoil");
                if (shader != null)
                {
                    s.LucidFoil = MakeFoil(shader, "GiftFoil_Lucid", cfg);
                    s.RimFoil = MakeFoil(shader, "GiftFoil_Rim", cfg);
                }
                return s;
            }

            private static Material MakeFoil(Shader shader, string name, GiftConfig cfg)
            {
                var m = new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };
                m.SetFloat("_Intensity", cfg.foilIntensity);
                m.SetFloat("_Speed", cfg.foilSpeed);
                m.SetFloat("_HueShift", cfg.foilHueShift);
                return m;
            }

            public void Dispose()
            {
                if (LucidFoil != null) Object.Destroy(LucidFoil);
                if (RimFoil != null) Object.Destroy(RimFoil);
                LucidFoil = null;
                RimFoil = null;
            }
        }

        public static GiftCardWidget Create(RectTransform parent, string name, GiftConfig cfg, Shared shared)
        {
            var w = new GiftCardWidget();
            w.Go = new GameObject(name, typeof(RectTransform));
            w.Rt = (RectTransform)w.Go.transform;
            w.Rt.SetParent(parent, false);
            w.Rt.sizeDelta = cfg.cardSize;

            // 프레임(선물만 활성) — 카드보다 사방 frameBorder 만큼 큼.
            // 2층: 크리스프 틴트 링 + 그 위 포일 시머 오버레이(DraftCardFoil 은
            // _MainTex 를 샘플하지 않는 저알파 오버레이 셰이더라 링 형태를 못 만든다).
            w._frame = MakeChild(w.Rt, "Frame", out var frt);
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = -Vector2.one * cfg.frameBorder;
            frt.offsetMax = Vector2.one * cfg.frameBorder;
            w._frame.sprite = shared.FrameSprite;
            w._frame.type = Image.Type.Sliced;
            w._frame.gameObject.SetActive(false);

            w._foil = MakeChild(frt, "Foil", out var lrt2);
            Stretch(lrt2);
            w._foil.sprite = shared.FrameSprite;
            w._foil.type = Image.Type.Sliced;

            // 카드 몸체 플레이트.
            w._plate = MakeChild(w.Rt, "Plate", out var prt);
            Stretch(prt);
            w._plate.sprite = shared.PlateSprite;
            w._plate.type = Image.Type.Sliced;
            w._plate.color = cfg.facePlateColor;

            // 아트(앞면).
            w._art = MakeChild(w.Rt, "Art", out var art);
            Stretch(art, cfg.artInset);
            w._art.preserveAspect = true;

            // 이름(앞면 하단).
            var nameGO = new GameObject("Name", typeof(RectTransform));
            var nrt = (RectTransform)nameGO.transform;
            nrt.SetParent(w.Rt, false);
            nrt.anchorMin = new Vector2(0f, 0f);
            nrt.anchorMax = new Vector2(1f, 0f);
            nrt.pivot = new Vector2(0.5f, 0f);
            nrt.anchoredPosition = new Vector2(0f, 8f);
            nrt.sizeDelta = new Vector2(-10f, cfg.nameFontSize * 2f);
            w._name = nameGO.AddComponent<TextMeshProUGUI>();
            w._name.fontSize = cfg.nameFontSize;
            w._name.alignment = TextAlignmentOptions.Center;
            w._name.color = new Color(0.96f, 0.94f, 0.82f, 1f);
            w._name.raycastTarget = false;

            // 뒷면 덮개(플립 리빌 전) — 프레임 색으로 틴트 + 중앙 문양.
            w._back = MakeChild(w.Rt, "Back", out var brt);
            Stretch(brt);
            w._back.sprite = shared.PlateSprite;
            w._back.type = Image.Type.Sliced;
            w._backEmblem = MakeChild(brt, "Emblem", out var ert);
            ert.anchorMin = ert.anchorMax = new Vector2(0.5f, 0.5f);
            ert.sizeDelta = new Vector2(cfg.cardSize.x * 0.45f, cfg.cardSize.x * 0.45f);
            w._backEmblem.sprite = shared.EmblemSprite;
            w._back.gameObject.SetActive(false);

            return w;
        }

        private static Image MakeChild(RectTransform parent, string name, out RectTransform rt)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        private static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.one * inset;
            rt.offsetMax = -Vector2.one * inset;
        }

        // 출신 설정 — None 은 프레임 없음(내 덱 10장). Lucid/Rim 은 포일 프레임 점화
        // + 뒷면을 같은 계열 색으로 물들인다.
        public void SetOrigin(GiftOrigin origin, GiftConfig cfg, Shared shared)
        {
            bool gifted = origin != GiftOrigin.None;
            _frame.gameObject.SetActive(gifted);
            if (!gifted) return;

            bool lucid = origin == GiftOrigin.Lucid;
            Color frameColor = lucid ? cfg.lucidFrameColor : cfg.rimFrameColor;
            _frame.color = frameColor;                                 // 크리스프 링
            var foilMat = lucid ? shared.LucidFoil : shared.RimFoil;
            if (foilMat != null) _foil.material = foilMat;             // 위에 홀로 시머
            _foil.color = Color.Lerp(Color.white, frameColor, 0.4f);
            _back.color = Color.Lerp(frameColor, Color.black, cfg.backDarken);
            _backEmblem.color = frameColor;
        }

        // 앞/뒷면 전환 — 플립 트윈(scale.x 스왑)은 안무 소관, 위젯은 face 만 바꾼다.
        public void SetFace(bool front)
        {
            _back.gameObject.SetActive(!front);
        }

        public void Bind(DreamcatcherCard card, GiftConfig cfg)
        {
            if (card != null && card.art != null)
            {
                _art.sprite = card.art;
                _art.color = Color.white;
            }
            else
            {
                _art.sprite = null;
                _art.color = (card != null && card.category == CardCategory.Subconscious)
                    ? cfg.fallbackSubconsciousColor : cfg.fallbackNormalColor;
            }
            _name.text = card != null ? card.displayName : "";
        }

        // 프레임 글로우 펄스용 — 안무가 프레임 색 알파를 흔들 때 쓴다.
        public void SetFrameAlpha(float a)
        {
            if (!_frame.gameObject.activeSelf) return;
            var c = _frame.color;
            c.a = a;
            _frame.color = c;
            c = _foil.color;
            c.a = a;
            _foil.color = c;
        }
    }
}
