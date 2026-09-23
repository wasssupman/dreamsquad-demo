using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wassup.BattleCoreUnity.Hud
{
    // battle-core-rebuild unit 5b — HUD 조각을 **코드로 세우는** 공용 손.
    //
    // 왜 프리팹이 아니라 코드인가: 이 층의 HUD 는 옛 씬의 캔버스 계층을 통째로 복사해 오는
    // 대신 **읽기 모델만 보고 다시 짜는 것**이 목적이다(5b). 프리팹을 먼저 만들면 그 안의
    // 배선이 정본이 되어 「값이 어디서 오나」가 다시 씬으로 흩어진다 — 브리지가 비대해진
    // 것과 같은 경로다. 여기서는 **컴포넌트가 자기 화면을 소유**하고, 씬에는 그 컴포넌트와
    // 드라이버 참조만 놓는다.
    //
    // ⚠ 이 클래스는 규칙을 하나도 모른다. 색·폰트 크기·정렬만 안다.
    public static class CoreHudUi
    {
        /// <summary>HUD 가 공유하는 기준 해상도. 가로 전용 게임이라 가로에 맞춘다.</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        // 색은 한 자리에 모은다 — 조각마다 자기 색을 들면 화면이 한 벌로 안 읽힌다.
        public static readonly Color Ink = new Color(1f, 0.97f, 0.9f, 1f);
        public static readonly Color InkDim = new Color(1f, 0.97f, 0.9f, 0.55f);
        public static readonly Color Panel = new Color(0.07f, 0.08f, 0.12f, 0.72f);
        public static readonly Color Accent = new Color(1f, 0.72f, 0.25f, 1f);
        public static readonly Color Good = new Color(0.45f, 0.92f, 0.5f, 1f);
        public static readonly Color Bad = new Color(0.95f, 0.32f, 0.3f, 1f);

        /// <summary>
        /// 이 오브젝트(또는 부모)가 쓰는 캔버스를 찾거나 세운다.
        ///
        /// 여럿이 같은 루트에 붙으므로 **먼저 온 쪽이 세우고 나머지는 붙는다** — 조각마다
        /// 캔버스를 만들면 정렬이 캔버스 순서에 걸려 화면이 겹친다.
        /// </summary>
        public static Canvas EnsureCanvas(GameObject host, int sortingOrder = 0)
        {
            var canvas = host.GetComponentInParent<Canvas>();
            if (canvas != null) return canvas.rootCanvas;

            canvas = host.GetComponent<Canvas>();
            if (canvas == null) canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = host.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // 0.5 = 가로·세로 절충. 가로 전용이지만 기기 화면비가 넓게 갈리므로(제약:
            // 절대 거리 저작이 화면비에 무너진 선례) 한쪽에만 맞추지 않는다.
            scaler.matchWidthOrHeight = 0.5f;

            if (host.GetComponent<GraphicRaycaster>() == null) host.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>앵커·피벗이 같은 빈 RectTransform 하나.</summary>
        public static RectTransform Rect(string name, Transform parent, Vector2 anchor,
                                         Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>단색 판. 텍스처가 없으므로 `Image` 의 기본 흰 사각을 색으로 칠한다.</summary>
        public static Image Fill(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 글자. 폰트는 지정하지 않는다 — TMP 가 프로젝트 기본 폰트를 잡는다.
        /// 여기서 폰트 자산을 고르면 그 선택이 코드에 박혀 저작에서 못 바꾼다.
        /// </summary>
        public static TextMeshProUGUI Label(string name, Transform parent, string text,
                                            float size, Color color,
                                            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            // 잘림보다 **줄어드는 쪽**이 낫다 — 기기마다 폭이 달라 긴 이름이 사라지면
            // 「그 유닛이 왜 안 보이지」가 된다.
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>
        /// 누를 수 있는 판. 반환은 `Button` 이고 라벨은 호출자가 붙인다.
        /// `Image` 가 레이캐스트 대상이라 여기만 `raycastTarget` 이 참이다.
        /// </summary>
        public static Button Button(string name, Transform parent, Vector2 anchor, Vector2 pivot,
                                    Vector2 anchoredPos, Vector2 size, Color color)
        {
            var rt = Rect(name, parent, anchor, pivot, anchoredPos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            return btn;
        }

        /// <summary>왼쪽에서 차오르는 바. 반환은 채움 `Image`(`fillAmount` 로 민다).</summary>
        public static Image Bar(RectTransform host, Color back, Color fill)
        {
            Fill("Back", host, back);
            var f = Fill("Fill", host, fill);
            f.type = Image.Type.Filled;
            f.fillMethod = Image.FillMethod.Horizontal;
            f.fillOrigin = (int)Image.OriginHorizontal.Left;
            f.fillAmount = 1f;
            return f;
        }

        /// <summary>남은 초 → `m:ss`. 음수는 0 으로 접는다(만료 뒤 한 프레임의 음수 표시 방지).</summary>
        public static string Clock(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{total / 60:0}:{total % 60:00}";
        }
    }
}
