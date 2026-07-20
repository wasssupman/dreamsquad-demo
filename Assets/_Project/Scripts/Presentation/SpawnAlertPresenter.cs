using System.Collections.Generic;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Core;

namespace Wassup.Presentation
{
    // spawn-point-alert unit 1(rev 3) — 다음 웨이브의 각 스폰 지점에서 골까지, 적 이동 경로를 따라
    // 그어지는 에너지 라인 예고(명일방주식). 레퍼런스: 적 스폰 직전 스폰→목적지로 빨간 선이
    // 표시된다(arknights.wiki.gg/wiki/Pathing).
    //
    // 4개 레이어의 합으로 "그냥 빨간 선"이 아닌 VFX 로 만든다:
    //   glow(가산 광휘) + core(흰 중심 코어) + streak(선을 타고 흐르는 에너지) + ring(스폰점 맥동)
    //
    // BattleBridge 예보(read-only) 폴링. 표시 창·트레이싱·흐름 위상 모두 battle 클럭 기준이라
    // 정지/슬로우 시 자연 동결. Wave 1(0초 트리거)·Next Wave 강제 호출은 표시 창이 성립하지
    // 않아 자연 스킵(spec 계약).
    public class SpawnAlertPresenter : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;
        [Tooltip("첫 적 등장 몇 초 전부터 예고선을 띄울지 (2~3초 권장).")]
        [SerializeField] private float leadSec = 2.5f;
        [Tooltip("스폰→골로 선이 그어지는 데 걸리는 시간.")]
        [SerializeField] private float drawSec = 0.55f;
        [Tooltip("첫 적 등장 후 꼬리가 골로 수렴하며 사라지는 시간(0=즉시).")]
        [SerializeField] private float retractSec = 1f;

        [Header("Line")]
        [SerializeField] private Color lineColor = new Color(1f, 0.16f, 0.12f, 1f);
        [Tooltip("코어 실선 굵기. 광휘/스트릭은 이 값의 배수.")]
        [SerializeField] private float lineWidth = 0.14f;
        [Tooltip("골 쪽 끝단 알파(스폰 쪽이 진하다).")]
        [SerializeField, Range(0f, 1f)] private float tailAlpha = 0.55f;
        [Tooltip("바깥 광휘 폭 배수와 세기.")]
        [SerializeField] private float glowWidthScale = 5f;
        [SerializeField, Range(0f, 1f)] private float glowStrength = 0.5f;

        [Header("Flow")]
        [Tooltip("선을 타고 흐르는 에너지 세기(0=끔).")]
        [SerializeField, Range(0f, 2f)] private float streakStrength = 1f;
        [Tooltip("에너지가 스폰→골을 훑는 속도(초당 횟수).")]
        [SerializeField] private float streakSpeed = 0.7f;
        [SerializeField] private float streakWidthScale = 2.6f;

        [Header("Spawn Ring")]
        [Tooltip("스폰 지점 맥동 링 크기(월드). 0 = 끔.")]
        [SerializeField] private float ringSize = 1.1f;
        [Tooltip("링이 퍼져나가는 주기(초).")]
        [SerializeField] private float ringPeriod = 0.9f;

        // BoardSpace 는 평면 뷰라 view +Y 는 "높이"가 아니라 화면 위쪽이다. 그 축으로 띄우면
        // 가로 구간에서 선이 길 중앙을 벗어난다. 대신 보드 평면의 법선(카메라 쪽)으로 띄워
        // 화면상 위치는 유지하면서 깊이만 분리한다 — 타일과의 z-fighting 해소.
        //
        // 크게 주면 유닛을 덮는다. 바닥 타일과 유닛은 둘 다 깊이를 쓰고(타일=불투명,
        // 유닛=AlphaTest) 라인은 투명 큐라, 라인의 가시성은 ZTest 로 갈린다. 즉 이 값은
        // "깊이 정밀도는 이기되 유닛 두께는 넘지 않을 만큼"만 줘야 한다. 틸트 빌보드라
        // 유닛의 깊이 두께가 눌려 있어 여유가 작다.
        [Tooltip("보드 평면 법선(카메라 쪽) 띄움. z-fight 회피 최소치만 — 크면 유닛을 덮는다.")]
        [SerializeField] private float surfaceOffset = 0.012f;

        private class Lane
        {
            public LineRenderer glow;
            public LineRenderer core;
            public LineRenderer streak;
            public SpriteRenderer ring;
            public readonly List<Vector3> points = new(); // view 공간 경로(법선 오프셋 포함)
            public readonly List<float> cumLen = new();
            public float totalLen;
            public float showStartClock;
            public bool shown;
            public bool retracting;        // 표시 창 종료 후 꼬리가 골로 수렴하는 중
            public float retractStartClock;
        }

        private readonly List<Lane> _lanes = new();
        private readonly List<Vector3> _pathBuffer = new();
        private readonly List<Vector3> _drawBuffer = new();
        private Material _glowMat, _coreMat, _streakMat, _ringMat;
        private Texture2D _glowTex, _coreTex, _streakTex, _ringTex;
        private Color _coreBakedColor;
        private Sprite _ringSprite;
        private Camera _camera;

        private void Update()
        {
            if (bridge == null) return;
            bool has = bridge.TryGetSpawnAlertForecast(out float clock, out float[] first);
            int laneCount = has ? first.Length : 0;
            EnsureLanes(laneCount);

            // 스트릭 위상은 전 lane 공유(머티리얼 1개) — battle 클럭 기준이라 정지 시 동결.
            if (_streakMat != null)
                _streakMat.mainTextureOffset = new Vector2(-clock * streakSpeed, 0f);
            if (_coreMat != null && _coreBakedColor != lineColor)
                _coreMat.mainTexture = GetCoreTexture(); // 인스펙터 색 변경 즉시 반영

            for (int i = 0; i < _lanes.Count; i++)
            {
                var lane = _lanes[i];

                // 예보 자체가 없으면(전투 종료·재시작) 잔상 없이 즉시 정리한다.
                if (!has) { if (lane.shown) HideLane(lane); continue; }

                bool inWindow = i < laneCount && first[i] >= 0f
                                && clock >= first[i] - leadSec && clock < first[i];

                if (inWindow)
                {
                    if (!lane.shown)
                    {
                        // 표시 시작 시마다 경로 재조회 — 블로킹 해저드 등 flow 변화를 반영한다.
                        if (!TryCapturePath(lane, i)) continue;
                        lane.showStartClock = clock;
                        lane.shown = true;
                        SetLaneVisible(lane, true);
                    }
                    lane.retracting = false;

                    float drawT = drawSec > 0f ? Mathf.Clamp01((clock - lane.showStartClock) / drawSec) : 1f;
                    BuildSubPolyline(lane, 0f, lane.totalLen * drawT);
                    PushPositions(lane);
                    ApplyLaneColors(lane, drawT, clock, 1f);
                    ApplyRing(lane, drawT, clock, 1f);
                    continue;
                }

                if (!lane.shown) continue;

                // 표시 창 종료(= 첫 적 등장) → 꼬리가 골로 수렴하며 사라진다. 머리는 골에 고정.
                if (!lane.retracting)
                {
                    lane.retracting = true;
                    lane.retractStartClock = clock;
                }
                float rt = retractSec > 0f
                    ? Mathf.Clamp01((clock - lane.retractStartClock) / retractSec)
                    : 1f;
                if (rt >= 1f) { HideLane(lane); continue; }

                float tail = lane.totalLen * Mathf.SmoothStep(0f, 1f, rt); // 가속하며 따라붙는 꼬리
                if (lane.totalLen - tail < 1e-3f) { HideLane(lane); continue; }
                BuildSubPolyline(lane, tail, lane.totalLen);
                PushPositions(lane);
                // 마지막 15% 에서만 살짝 페이드 — 수렴이 주 연출이고 페이드는 팝 방지용.
                // 주의: Unity 의 Mathf.SmoothStep(a,b,t) 는 a~b 를 t 로 보간하는 함수이지
                // GLSL 의 edge 함수 smoothstep(edge0,edge1,x) 가 아니다. 구간을 edge 로 넘기면
                // rt=0 에서도 곧바로 0.85 를 돌려줘 알파가 시작하자마자 0.15 로 붕괴한다
                // (수렴이 안 보이고 "한 번에 사라짐"으로 읽힘). 구간을 먼저 0~1 로 정규화한다.
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, rt));
                ApplyLaneColors(lane, 1f, clock, fade);
                ApplyRing(lane, 1f, clock, (1f - rt) * (1f - rt)); // 꼬리가 떠난 스폰점 링은 빠르게 소멸
            }
        }

        // 경로의 [fromLen, toLen] 구간만 뽑는다(arc-length 기준, 양 끝은 세그먼트 보간).
        // 그리기 = [0, len·drawT], 수렴 = [len·retractT, len].
        private void BuildSubPolyline(Lane lane, float fromLen, float toLen)
        {
            _drawBuffer.Clear();
            fromLen = Mathf.Clamp(fromLen, 0f, lane.totalLen);
            toLen = Mathf.Clamp(toLen, fromLen, lane.totalLen);

            _drawBuffer.Add(PointAtDistance(lane, fromLen));
            for (int i = 0; i < lane.points.Count; i++)
            {
                float c = lane.cumLen[i];
                if (c <= fromLen) continue;
                if (c >= toLen) break;
                _drawBuffer.Add(lane.points[i]);
            }
            _drawBuffer.Add(PointAtDistance(lane, toLen));
        }

        private static Vector3 PointAtDistance(Lane lane, float dist)
        {
            if (dist <= 0f) return lane.points[0];
            int last = lane.points.Count - 1;
            if (dist >= lane.totalLen) return lane.points[last];
            for (int i = 1; i <= last; i++)
            {
                if (lane.cumLen[i] < dist) continue;
                float seg = lane.cumLen[i] - lane.cumLen[i - 1];
                float f = seg > 1e-5f ? (dist - lane.cumLen[i - 1]) / seg : 0f;
                return Vector3.Lerp(lane.points[i - 1], lane.points[i], f);
            }
            return lane.points[last];
        }

        private void PushPositions(Lane lane)
        {
            SetPositions(lane.glow, _drawBuffer);
            SetPositions(lane.core, _drawBuffer);
            if (lane.streak.enabled) SetPositions(lane.streak, _drawBuffer);
        }

        private void ApplyLaneColors(Lane lane, float drawT, float clock, float fade)
        {
            // 그어지는 동안 선단이 밝게 타오르고, 완성 후엔 은은히 숨쉰다.
            float breathe = 1f + 0.12f * Mathf.Sin(clock * 3.4f);
            float ignite = 1f + 1.2f * (1f - drawT) * (1f - drawT); // 초반일수록 강한 발광

            var glow = lineColor * (glowStrength * ignite * breathe);
            glow.a = lineColor.a * glowStrength * fade;
            lane.glow.startColor = glow;
            lane.glow.endColor = new Color(glow.r, glow.g, glow.b, glow.a * tailAlpha);

            // 코어는 RGB 가 텍스처에 구워져 있으므로 tint 는 흰색(밝기 변조만).
            float coreBoost = Mathf.Min(1.6f, ignite * breathe);
            var core = new Color(coreBoost, coreBoost, coreBoost, lineColor.a * fade);
            lane.core.startColor = core;
            lane.core.endColor = new Color(core.r, core.g, core.b, core.a * tailAlpha);

            if (lane.streak.enabled)
            {
                var s = Color.Lerp(lineColor, Color.white, 0.5f) * (streakStrength * breathe);
                s.a = drawT * fade; // 그어지는 동안 함께 등장
                lane.streak.startColor = s;
                lane.streak.endColor = s;
            }
        }

        private void ApplyRing(Lane lane, float drawT, float clock, float fade)
        {
            if (lane.ring == null) return;
            // 스폰 지점에서 반복 확산하는 맥동 링 — "여기서 나온다"를 못 놓치게.
            float phase = ringPeriod > 0f ? Mathf.Repeat(clock / ringPeriod, 1f) : 0f;
            float scale = ringSize * Mathf.Lerp(0.35f, 1f, phase);
            lane.ring.transform.localScale = new Vector3(scale, scale, scale);
            var c = Color.Lerp(lineColor, Color.white, 0.25f);
            c.a = lineColor.a * (1f - phase) * (1f - phase) * drawT * fade;
            lane.ring.color = c;
        }

        private static void SetPositions(LineRenderer line, List<Vector3> pts)
        {
            line.positionCount = pts.Count;
            for (int i = 0; i < pts.Count; i++) line.SetPosition(i, pts[i]);
        }

        private bool TryCapturePath(Lane lane, int laneIndex)
        {
            if (!bridge.TryGetSpawnPathSim(laneIndex, _pathBuffer)) return false;
            SimplifyCollinear(_pathBuffer); // 직선 구간 병합 — 코너 정점만 유지(격자 경로라 정확)

            Vector3 lift = SurfaceLift();
            lane.points.Clear();
            lane.cumLen.Clear();
            for (int i = 0; i < _pathBuffer.Count; i++)
            {
                Vector3 p = (Vector3)BoardSpace.ToView(_pathBuffer[i]) + lift;
                lane.points.Add(p);
                lane.cumLen.Add(i == 0 ? 0f : lane.cumLen[i - 1] + Vector3.Distance(lane.points[i - 1], p));
            }
            lane.totalLen = lane.cumLen[lane.cumLen.Count - 1];
            if (lane.points.Count < 2 || lane.totalLen <= 1e-4f) return false;

            if (lane.ring != null)
            {
                lane.ring.transform.position = lane.points[0];
                var n = BoardSpace.RaycastPlane().normal;
                lane.ring.transform.rotation = Quaternion.LookRotation(n); // 스프라이트 평면을 보드에 눕힘
            }
            return true;
        }

        // 보드 평면 법선을 카메라 쪽으로 정렬해 그만큼 띄운다(화면상 위치 불변, 깊이만 분리).
        private Vector3 SurfaceLift()
        {
            if (surfaceOffset <= 0f) return Vector3.zero;
            Vector3 n = BoardSpace.RaycastPlane().normal;
            // 카메라 시선(forward)과 같은 방향이면 카메라 뒤쪽이므로 뒤집는다.
            if (EnsureCamera() && Vector3.Dot(n, _camera.transform.forward) > 0f) n = -n;
            return n * surfaceOffset;
        }

        private bool EnsureCamera()
        {
            if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
            return _camera != null;
        }

        private static void SetLaneVisible(Lane lane, bool on)
        {
            lane.glow.enabled = on;
            lane.core.enabled = on;
            lane.streak.enabled = on;
            if (lane.ring != null) lane.ring.enabled = on;
        }

        private void HideLane(Lane lane)
        {
            SetLaneVisible(lane, false);
            lane.shown = false;
            lane.retracting = false;
        }

        private void EnsureLanes(int laneCount)
        {
            while (_lanes.Count < laneCount)
            {
                int idx = _lanes.Count;
                var lane = new Lane
                {
                    glow = CreateLine($"SpawnAlertGlow_{idx}", GetGlowMaterial(),
                        lineWidth * glowWidthScale, BoardSortOrder.SpawnAlertOrder),
                    streak = CreateLine($"SpawnAlertStreak_{idx}", GetStreakMaterial(),
                        lineWidth * streakWidthScale, BoardSortOrder.SpawnAlertOrder + 1),
                    core = CreateLine($"SpawnAlertLine_{idx}", GetCoreMaterial(),
                        lineWidth, BoardSortOrder.SpawnAlertOrder + 2),
                    ring = ringSize > 0f ? CreateRing($"SpawnAlertRing_{idx}") : null,
                };
                if (streakStrength <= 0f) lane.streak.enabled = false;
                _lanes.Add(lane);
            }
            // laneCount 축소는 맵 재빌드(배틀 재시작) 케이스 — 초과분은 위 표시 루프가 비활성 유지.
        }

        private LineRenderer CreateLine(string name, Material mat, float width, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.enabled = false;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch; // U = 선 전체, V = 폭 방향
            line.numCornerVertices = 6;
            line.numCapVertices = 4;
            line.widthMultiplier = width;
            line.sortingOrder = order;
            line.sharedMaterial = mat;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            return line;
        }

        private SpriteRenderer CreateRing(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.enabled = false;
            sr.sprite = GetRingSprite();
            sr.sharedMaterial = GetRingMaterial();
            sr.sortingOrder = BoardSortOrder.SpawnAlertOrder + 3;
            return sr;
        }

        // ── 머티리얼 ─────────────────────────────────────────────────────────
        // 광휘/스트릭/링은 가산 합성(에너지 느낌). 레거시 파티클 셰이더는 URP 에서도 렌더된다.
        private static Shader AdditiveShader()
        {
            var s = Shader.Find("Mobile/Particles/Additive")
                    ?? Shader.Find("Particles/Additive")
                    ?? Shader.Find("Legacy Shaders/Particles/Additive");
            return s != null ? s : Shader.Find("Sprites/Default");
        }

        private Material GetGlowMaterial()
        {
            if (_glowMat == null)
                _glowMat = new Material(AdditiveShader()) { mainTexture = GetGlowTexture() };
            return _glowMat;
        }

        // 코어만 알파 합성이다. 가산으로 하면 이 게임의 밝은 배경(청록 잔디·회색 길) 위에서
        // 색이 바래 흐릿한 분홍이 된다 — 채도를 지키려면 알파여야 한다. 광휘/스트릭만 가산.
        private Material GetCoreMaterial()
        {
            if (_coreMat == null)
                _coreMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = GetCoreTexture() };
            return _coreMat;
        }

        private Material GetStreakMaterial()
        {
            if (_streakMat == null)
                _streakMat = new Material(AdditiveShader()) { mainTexture = GetStreakTexture() };
            return _streakMat;
        }

        private Material GetRingMaterial()
        {
            if (_ringMat == null) _ringMat = new Material(AdditiveShader());
            return _ringMat;
        }

        // ── 절차적 텍스처 (외부 에셋 불요) ────────────────────────────────────
        // LineRenderer UV 규약: U = 선 방향, V = 폭 방향.

        // 폭 방향으로 부드럽게 감쇠하는 광휘.
        private Texture2D GetGlowTexture()
        {
            if (_glowTex != null) return _glowTex;
            _glowTex = BuildTex(4, 64, (u, v) =>
            {
                float d = Mathf.Abs(v - 0.5f) * 2f;
                float a = Mathf.Exp(-d * d * 5.5f);
                return a * a;
            });
            return _glowTex;
        }

        // 코어: 중심은 흰색으로 타고 가장자리로 갈수록 lineColor 로 물드는 폭 방향 램프.
        // RGB 를 구워 넣으므로(가장자리 채도 유지) 렌더러 tint 는 흰색으로 쓴다.
        // lineColor 를 인스펙터에서 바꾸면 다시 굽는다(Play 중 실시간 튜닝 지원).
        private Texture2D GetCoreTexture()
        {
            if (_coreTex != null && _coreBakedColor == lineColor) return _coreTex;
            if (_coreTex != null) Destroy(_coreTex);
            _coreBakedColor = lineColor;
            var edge = new Color(lineColor.r, lineColor.g, lineColor.b);
            _coreTex = BuildTexRGB(4, 64, (u, v) =>
            {
                float d = Mathf.Abs(v - 0.5f) * 2f;
                float a = Mathf.Clamp01((1f - d) / 0.45f);          // 중앙 평탄 + 가장자리 감쇠
                var rgb = Color.Lerp(Color.white, edge, Mathf.SmoothStep(0f, 1f, d / 0.65f));
                return new Color(rgb.r, rgb.g, rgb.b, Mathf.Clamp01(a));
            });
            return _coreTex;
        }

        // 선을 타고 흐르는 에너지 — 머리 날카롭고 꼬리 길게 감쇠, 폭 방향은 부드럽게.
        private Texture2D GetStreakTexture()
        {
            if (_streakTex != null) return _streakTex;
            const float head = 0.9f, tail = 0.3f;
            _streakTex = BuildTex(256, 32, (u, v) =>
            {
                float behind = head - u;
                float a = behind < 0f ? 0f : Mathf.Clamp01(1f - behind / tail);
                a = a * a * a;
                float d = Mathf.Abs(v - 0.5f) * 2f;
                return a * Mathf.Exp(-d * d * 3f);
            });
            return _streakTex;
        }

        // 스폰점 맥동 링(방사형).
        private Sprite GetRingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            if (_ringTex == null)
            {
                _ringTex = BuildTex(64, 64, (u, v) =>
                {
                    float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.13f, 2f)); // 테두리 링
                    float core = Mathf.Exp(-Mathf.Pow(r / 0.3f, 2f)) * 0.55f;    // 중심 발광
                    return Mathf.Clamp01(ring + core);
                });
            }
            _ringSprite = Sprite.Create(_ringTex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
            return _ringSprite;
        }

        private static Texture2D BuildTexRGB(int w, int h, System.Func<float, float, Color> sample)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = h > 1 ? y / (float)(h - 1) : 0.5f;
                for (int x = 0; x < w; x++)
                {
                    float u = w > 1 ? x / (float)(w - 1) : 0.5f;
                    px[y * w + x] = sample(u, v);
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D BuildTex(int w, int h, System.Func<float, float, float> alpha)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = h > 1 ? y / (float)(h - 1) : 0.5f;
                for (int x = 0; x < w; x++)
                {
                    float u = w > 1 ? x / (float)(w - 1) : 0.5f;
                    float a = Mathf.Clamp01(alpha(u, v));
                    // 가산 합성이라 RGB 자체가 밝기다(알파는 곱해 넣는다).
                    px[y * w + x] = new Color(a, a, a, a);
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        // 연속 3점이 일직선이면 가운데 점 제거(in-place). 격자 셀 경로 전제라 오차 여유 불요.
        private static void SimplifyCollinear(List<Vector3> pts)
        {
            if (pts.Count < 3) return;
            int write = 1;
            for (int i = 1; i < pts.Count - 1; i++)
            {
                Vector3 prev = pts[write - 1];
                if (Vector3.Cross(pts[i] - prev, pts[i + 1] - pts[i]).sqrMagnitude > 1e-6f)
                    pts[write++] = pts[i];
            }
            pts[write++] = pts[pts.Count - 1];
            pts.RemoveRange(write, pts.Count - write);
        }

        private void OnDestroy()
        {
            if (_glowMat != null) Destroy(_glowMat);
            if (_coreMat != null) Destroy(_coreMat);
            if (_streakMat != null) Destroy(_streakMat);
            if (_ringMat != null) Destroy(_ringMat);
            if (_glowTex != null) Destroy(_glowTex);
            if (_coreTex != null) Destroy(_coreTex);
            if (_streakTex != null) Destroy(_streakTex);
            if (_ringTex != null) Destroy(_ringTex);
            if (_ringSprite != null) Destroy(_ringSprite);
        }
    }
}
