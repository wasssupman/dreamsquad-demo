using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wassup.UI
{
    // lobby-neon-restyle unit 4 — 로비 START CTA 네온 리본 배너.
    //
    // 시안 실측 형상: 좌우 끝이 뾰족한 **180° 회전대칭** 리본이다. 왼쪽 꼭짓점은 아래쪽
    // (pointFrac), 오른쪽은 그 대칭이라 위쪽에 온다 — 가만히 있어도 기울어 보이는 게 이 형태의
    // 핵심이라 좌우를 상하대칭으로 만들면 시안 느낌이 사라진다. 본체 바깥 rimGap 만큼 떨어진
    // 자리에 흰-시안 링이 돌고 링 양쪽으로 파란 글로우가 번진다.
    //
    // 글로우가 rect 경계에서 잘리지 않도록 본체를 (gap + 링 절반 + 글로우 도달거리)만큼 **안쪽으로
    // 들여서** 굽는다. 결과적으로 터치 rect ≥ 보이는 배너 — 히트박스가 살짝 넉넉해지는 건 CTA 에
    // 유리한 방향이라 그대로 둔다.
    //
    // 칩(LobbyNeonChip)과 달리 9-slice 가 아니라 rect 크기 그대로 굽는 full-rect 스프라이트다.
    // 사선 모서리·대각 그라디언트·셰브론은 9-slice 로 늘리면 그대로 깨진다.
    [DisallowMultipleComponent]
    public class LobbyNeonCta : MonoBehaviour
    {
        [Tooltip("배너를 받을 Image. 비우면 같은 GO 의 Image(=Button targetGraphic).")]
        [SerializeField] private Image background;
        [Tooltip("아웃라인을 입힐 라벨. 비우면 라벨은 건드리지 않는다.")]
        [SerializeField] private TMP_Text label;

        [Header("형상")]
        [Tooltip("옆 꼭짓점의 세로 위치. 0=위, 1=아래. 왼쪽 기준이고 오른쪽은 180° 대칭으로 뒤집힌다.")]
        [SerializeField, Range(0.5f, 1f)] private float pointFrac = 0.75f;
        [Tooltip("꼭짓점에서 먼 쪽 모서리가 안으로 들어간 정도(가로 반폭 대비).")]
        [SerializeField, Range(0f, 0.4f)] private float farCornerInset = 0.17f;
        [Tooltip("꼭짓점에서 가까운 쪽 모서리가 안으로 들어간 정도.")]
        [SerializeField, Range(0f, 0.4f)] private float nearCornerInset = 0.07f;
        [Tooltip("모서리 둥글기. 키우면 좌우 꼭짓점이 뭉툭해져 시안의 뾰족한 맛이 사라진다.")]
        [SerializeField] private float cornerRadius = 9f;

        [Header("림 / 글로우")]
        [Tooltip("본체 바깥에서 링까지의 빈 거리(px). 여기로 배경이 비쳐 링이 떠 보인다.")]
        [SerializeField] private float rimGap = 9f;
        [SerializeField] private float rimWidth = 4.5f;
        [Tooltip("링에서 글로우가 번지는 거리(px). 안팎 양쪽으로 퍼진다.")]
        [SerializeField] private float glowReach = 18f;
        [SerializeField] private Color rimColor = new Color32(247, 252, 255, 255);
        [SerializeField] private Color glowColor = new Color32(48, 129, 211, 255);

        [Header("본체")]
        [SerializeField] private Color bodyLeft = new Color32(252, 86, 176, 255);
        [SerializeField] private Color bodyRight = new Color32(126, 72, 223, 255);
        [Tooltip("그라디언트 축 각도(도). 0=오른쪽, 음수=오른쪽 아래로 기운다.")]
        [SerializeField] private float gradientAngle = -45f;

        [Header("셰브론 (양쪽 끝 안쪽, 둘 다 가운데를 향한다)")]
        [Tooltip("본체 좌우 끝에서 바깥쪽 셰브론까지의 거리(px).")]
        [SerializeField] private float chevronInset = 26f;
        [SerializeField] private float chevronWidth = 18f;
        [SerializeField] private float chevronHalfHeight = 17f;
        [SerializeField] private float chevronThickness = 9f;
        [SerializeField] private float chevronSpacing = 6f;
        [SerializeField] private int chevronCount = 2;
        [SerializeField] private Color chevronColor = new Color(1f, 1f, 1f, 0.38f);

        [Header("라벨 아웃라인")]
        [SerializeField, Range(0f, 1f)] private float labelOutlineWidth = 0.3f;
        [SerializeField] private Color labelOutlineColor = new Color32(105, 5, 146, 255);

        private Sprite _baked;

        private void Awake()
        {
            if (background == null) background = GetComponent<Image>();
            if (background == null)
            {
                Debug.LogWarning($"{nameof(LobbyNeonCta)}: background Image 없음 — 스킨 생략.", this);
                return;
            }

            Rect rect = ((RectTransform)background.transform).rect;
            _baked = Bake(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height));
            background.sprite = _baked;
            background.type = Image.Type.Simple;
            background.preserveAspect = false;
            background.color = Color.white;

            if (label != null && labelOutlineWidth > 0f) ApplyLabelOutline();
        }

        // 시안의 굵은 보라 아웃라인. 여기 세 줄은 전부 필요하고 하나라도 빠지면 조용히 안 보인다:
        //   1) fontMaterial 로 인스턴스를 떠서 공유 머티리얼(다른 Anton 라벨 전부)을 오염시키지 않는다.
        //   2) TMP 모바일 SDF 셰이더는 아웃라인을 OUTLINE_ON 키워드로 가른다. 인스펙터의 슬라이더는
        //      이 키워드를 같이 켜주지만 스크립트로 _OutlineWidth 만 올리면 키워드가 꺼진 채라
        //      화면에 아무 변화가 없다.
        //   3) 글리프 쿼드 여백은 아웃라인 0 기준으로 잡혀 있어, 다시 계산하지 않으면 넓힌 아웃라인이
        //      쿼드 밖으로 나가 잘린다.
        private void ApplyLabelOutline()
        {
            Material mat = label.fontMaterial;
            if (mat == null) return;
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            label.outlineColor = labelOutlineColor;
            label.outlineWidth = labelOutlineWidth;
            label.UpdateMeshPadding();
        }

        private void OnDestroy()
        {
            if (_baked != null)
            {
                Destroy(_baked.texture);
                Destroy(_baked);
                _baked = null;
            }
        }

        private Sprite Bake(int width, int height)
        {
            int w = Mathf.Max(16, width), h = Mathf.Max(16, height);

            // 본체는 글로우 도달거리까지 감안해 안쪽으로 들인다 — 바깥 여백이 곧 글로우 자리.
            float allowance = rimGap + rimWidth * 0.5f + glowReach;
            float halfW = Mathf.Max(4f, w * 0.5f - allowance);
            float halfH = Mathf.Max(4f, h * 0.5f - allowance);

            // 코너 라운딩은 sd 에서 radius 를 빼서 만든다 — 폴리곤은 그만큼 미리 줄여 잡는다.
            float r = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(halfW, halfH) - 1f);
            float pw = halfW - r, ph = halfH - r;
            float pointY = ph * (1f - 2f * pointFrac);   // 왼쪽 꼭짓점 y (아래쪽이 음수)

            var poly = new[]
            {
                new Vector2(-pw * (1f - farCornerInset), ph),    // top-left
                new Vector2(-pw, pointY),                        // left point
                new Vector2(-pw * (1f - nearCornerInset), -ph),  // bottom-left
                new Vector2(pw * (1f - farCornerInset), -ph),    // bottom-right (180° 대칭)
                new Vector2(pw, -pointY),                        // right point
                new Vector2(pw * (1f - nearCornerInset), ph),    // top-right
            };

            float gRad = gradientAngle * Mathf.Deg2Rad;
            var gDir = new Vector2(Mathf.Cos(gRad), Mathf.Sin(gRad));
            // **가로 폭**만으로 정규화한다 — 대각선 길이로 나누면 좌우 끝(세로 중앙)에서 t 가
            // 0/1 에 도달하지 못해 시안의 진한 핑크·보라가 안 나오고 전체가 중간색으로 뜬다.
            // 위아래 모서리는 t 가 범위를 넘지만 clamp 로 흡수된다.
            float gSpan = Mathf.Max(1f, 2f * Mathf.Abs(halfW * gDir.x));

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[w * h];
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(x - cx, y - cy);
                    float sd = SdPolygon(p, poly) - r;       // <=0 이면 본체 안

                    // 뒤에서 앞으로: 글로우 → 링 → 본체(+셰브론).
                    Color c = Color.clear;

                    float rimOffset = sd - rimGap;            // 링 중심선 기준 거리
                    if (sd > -1f)
                    {
                        float t = Mathf.Clamp01(1f - Mathf.Abs(rimOffset) / Mathf.Max(1f, glowReach));
                        float a = t * t * glowColor.a;        // 부드러운 감쇠
                        if (a > 0f) c = Blend(c, new Color(glowColor.r, glowColor.g, glowColor.b, a));
                    }

                    float rimA = Mathf.Clamp01(0.5f - (Mathf.Abs(rimOffset) - rimWidth * 0.5f));
                    if (rimA > 0f) c = Blend(c, new Color(rimColor.r, rimColor.g, rimColor.b, rimColor.a * rimA));

                    float bodyA = Mathf.Clamp01(0.5f - sd);
                    if (bodyA > 0f)
                    {
                        float t = Mathf.Clamp01(0.5f + Vector2.Dot(p, gDir) / gSpan);
                        Color body = Color.Lerp(bodyLeft, bodyRight, t);
                        float chev = ChevronCoverage(p, halfW);
                        if (chev > 0f)
                            body = Blend(body, new Color(chevronColor.r, chevronColor.g, chevronColor.b,
                                                         chevronColor.a * chev));
                        body.a *= bodyA;
                        c = Blend(c, body);
                    }

                    px[y * w + x] = c;
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        // 좌우 셰브론 그룹. 둘 다 가운데를 향한다(왼쪽은 오른쪽 화살표, 오른쪽은 왼쪽 화살표).
        // 본체 밖은 호출부가 이미 잘라내므로 여기서는 형태만 그린다.
        private float ChevronCoverage(Vector2 p, float halfW)
        {
            if (chevronCount <= 0 || chevronThickness <= 0f) return 0f;

            float ax = Mathf.Abs(p.x);
            float stride = chevronWidth + chevronSpacing;
            // 바깥 끝에서 안쪽으로 세는 좌표 — 좌우 대칭이라 |x| 하나로 양쪽을 함께 처리한다.
            float d = halfW - chevronInset - ax;
            if (d < -chevronWidth || d > chevronCount * stride) return 0f;

            float best = 0f;
            for (int i = 0; i < chevronCount; i++)
            {
                // i 번째 셰브론의 꼭짓점(안쪽)까지의 거리. tip 은 안쪽, 날개는 바깥으로 벌어진다.
                float tipD = i * stride;
                float local = d - tipD;                       // 0=tip, 음수=바깥쪽 날개 방향
                if (local > 0f || local < -chevronWidth) continue;
                // 날개: |y| 가 tip 에서 멀어질수록 선형으로 커진다.
                float armY = (-local) * (chevronHalfHeight / Mathf.Max(1f, chevronWidth));
                float dist = Mathf.Abs(Mathf.Abs(p.y) - armY);
                // 대각선 두께 보정 — 수직 거리 기준이라 기울기만큼 나눠준다.
                float slope = chevronHalfHeight / Mathf.Max(1f, chevronWidth);
                float perp = dist / Mathf.Sqrt(1f + slope * slope);
                best = Mathf.Max(best, Mathf.Clamp01(0.5f - (perp - chevronThickness * 0.5f)));
            }
            return best;
        }

        private static Color Blend(Color dst, Color src)
        {
            float a = src.a + dst.a * (1f - src.a);
            if (a <= 0f) return Color.clear;
            Color rgb = (src * src.a + dst * dst.a * (1f - src.a)) / a;
            rgb.a = a;
            return rgb;
        }

        // 볼록/오목 무관 폴리곤 부호거리(iq). 안쪽이 음수.
        private static float SdPolygon(Vector2 p, Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i];
                Vector2 wv = p - v[i];
                Vector2 b = wv - e * Mathf.Clamp01(Vector2.Dot(wv, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * wv.y > e.y * wv.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }
    }
}
