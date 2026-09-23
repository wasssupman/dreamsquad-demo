using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat;
using Wassup.BattleCore.Move;
using Wassup.Core;
using Wassup.Presentation;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild unit 5b — **예고선.** 옛 `SpawnAlertPresenter`(553줄)의 후계다.
    //
    // 「저기서 나와서 이 길로 온다」를 첫 적이 나오기 몇 초 전에 그린다.
    //
    // ⚠ **경로도 거점 선택도 여기서 계산하지 않는다**(M18).
    //   · 경로 = `SpawnPathPreview.Build` — 이동이 매 틱 쓰는 그 평활화·그 NavGrid·그 슬롯.
    //   · 거점 = `BattleMatch.AiMove.TryPickStructure` — 이동이 매 틱 쓰는 그 후보 배열
    //     (죽은 자리와 방패에 가린 마음은 이미 0 이다).
    //   옛 전투는 브리지가 후보를 **다시 모으고** `StructureChoice` 만 공유했는데, 방패 배제가
    //   한쪽에만 들어가 **예고선은 마음으로 가는 길을 그리는데 적은 본능으로 갔다.** 자를 하나
    //   더 만들면 그 버그가 그대로 돌아온다.
    //
    // 화면 어휘는 옛것에서 **코어 실선 + 스폰 링** 둘만 옮겼다. 광휘·스트릭은 절차적 텍스처
    // 넷과 가산 머티리얼 셋을 요구하는데, 그 머티리얼들이 `Shader.Find` 로 서 있어 그대로
    // 복사하면 모바일 stripping 에서 null 이 된다(추가 제약). 룩은 5c 이후에 다시 본다 —
    // 이 unit 의 질문은 「예고가 실제 이동선과 같은가」이다.
    [DisallowMultipleComponent]
    public sealed class CoreSpawnAlertPresenter : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;

        [Tooltip("첫 적 등장 몇 초 전부터 예고선을 띄울지.")]
        [SerializeField, Min(0f)] private float _leadSeconds = 2.5f;
        [Tooltip("스폰→골로 선이 그어지는 데 걸리는 시간(초).")]
        [SerializeField, Min(0f)] private float _drawSeconds = 0.55f;
        [Tooltip("첫 적 등장 후 꼬리가 골로 수렴하며 사라지는 시간(초). 0 = 즉시.")]
        [SerializeField, Min(0f)] private float _retractSeconds = 1f;

        [Header("선")]
        [SerializeField] private Color _lineColor = new Color(1f, 0.16f, 0.12f, 1f);
        [SerializeField, Min(0.01f)] private float _lineWidth = 0.14f;
        [Tooltip("골 쪽 끝단 알파(스폰 쪽이 진하다).")]
        [SerializeField, Range(0f, 1f)] private float _tailAlpha = 0.55f;

        [Header("스폰 링")]
        [Tooltip("스폰 지점 맥동 링 크기(타일). 0 = 끔.")]
        [SerializeField, Min(0f)] private float _ringTiles = 1.1f;
        [SerializeField, Min(0.05f)] private float _ringPeriod = 0.9f;

        [Tooltip("보드 평면 법선(카메라 쪽) 띄움. 바닥과의 z-fighting 회피 전용.")]
        [SerializeField, Min(0f)] private float _surfaceOffset = 0.06f;

        private sealed class Guide
        {
            public int Lane = -1;
            public int PathIndex = -1;
            public LineRenderer Line;
            public SpriteRenderer Ring;
            public readonly List<Vector3> Points = new List<Vector3>(64);
            public readonly List<float> CumLen = new List<float>(64);
            public float TotalLen;
            public float ShowStartClock;
            public bool Shown;
            public bool Retracting;
            public float RetractStartClock;
        }

        private readonly List<Guide> _guides = new List<Guide>(4);
        private readonly List<WaveScheduler.SpawnForecast> _forecast =
            new List<WaveScheduler.SpawnForecast>(8);
        private readonly List<float3> _pathBuffer = new List<float3>(64);
        private readonly List<Vector3> _drawBuffer = new List<Vector3>(64);

        private Sprite _ringSprite;
        private Texture2D _ringTex;
        private Material _material;
        private MaterialPropertyBlock _mpb;
        private Camera _camera;

        private void Update()
        {
            if (_driver == null || !_driver.Running || !BoardSpace.IsConfigured) return;

            var match = _driver.Match;
            // **전투 시계**다. 배치 창도 세는 `Tick` 을 쓰면 예고가 배치 중에 뜬다.
            float clock = match.Clock.BattleTime;
            int count = match.Waves.CollectForecast(_forecast);
            EnsureGuides(count);

            for (int i = 0; i < _guides.Count; i++)
            {
                var guide = _guides[i];
                bool has = i < count;

                // 예보 자체가 없으면(판 종료·전멸 직후) 잔상 없이 즉시 정리한다.
                if (!has) { if (guide.Shown) Hide(guide); continue; }

                var f = _forecast[i];
                bool inWindow = f.FirstSpawnSec >= 0f
                                && clock >= f.FirstSpawnSec - _leadSeconds
                                && clock < f.FirstSpawnSec;

                if (inWindow)
                {
                    if (!guide.Shown)
                    {
                        // 표시 시작마다 경로를 **다시** 뜬다 — 장애물·거점 붕괴로 길이 바뀐다.
                        if (!Capture(guide, f)) continue;
                        guide.ShowStartClock = clock;
                        guide.Shown = true;
                        SetVisible(guide, true);
                    }
                    guide.Retracting = false;

                    float drawT = _drawSeconds > 0f
                        ? Mathf.Clamp01((clock - guide.ShowStartClock) / _drawSeconds) : 1f;
                    BuildSub(guide, 0f, guide.TotalLen * drawT);
                    Push(guide, 1f);
                    PaintRing(guide, drawT, clock, 1f);
                    continue;
                }

                if (!guide.Shown) continue;

                // 창 종료(= 첫 적 등장) → 꼬리가 골로 수렴하며 사라진다. 머리는 골에 고정.
                if (!guide.Retracting)
                {
                    guide.Retracting = true;
                    guide.RetractStartClock = clock;
                }
                float rt = _retractSeconds > 0f
                    ? Mathf.Clamp01((clock - guide.RetractStartClock) / _retractSeconds) : 1f;
                if (rt >= 1f) { Hide(guide); continue; }

                float tail = guide.TotalLen * Mathf.SmoothStep(0f, 1f, rt);
                if (guide.TotalLen - tail < 1e-3f) { Hide(guide); continue; }
                BuildSub(guide, tail, guide.TotalLen);
                // ⚠ `Mathf.SmoothStep(a,b,t)` 는 a~b 를 t 로 보간하는 함수이지 GLSL 의
                // `smoothstep(edge0,edge1,x)` 가 아니다. 구간을 edge 로 넘기면 rt=0 에서
                // 곧바로 0.85 를 돌려줘 알파가 시작하자마자 붕괴한다 — 먼저 0~1 로 정규화한다.
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, rt));
                Push(guide, fade);
                PaintRing(guide, 1f, clock, (1f - rt) * (1f - rt));
            }
        }

        // ── 경로 뜨기 ────────────────────────────────────────────────────────
        private bool Capture(Guide guide, in WaveScheduler.SpawnForecast f)
        {
            var match = _driver.Match;
            var def = match.Definition;
            if (f.EnemyIndex < 0 || f.EnemyIndex >= def.Enemies.Length) return false;
            ref var enemy = ref def.Enemies[f.EnemyIndex];

            byte layers = (byte)enemy.TraversalLayers;
            var map = match.Map;

            // 거점 선택은 **스폰 자리에서** 일어난다(웨이포인트를 밟기 전 좌표).
            float3 spawn = map.CenterOf(map.Snapshot.Spawns[
                Mathf.Clamp(f.Lane, 0, map.Snapshot.Spawns.Length - 1)]);
            int mask = TargetDefaults.ResolveEnemy(enemy.TargetFactions);
            bool hasStructure = match.AiMove.TryPickStructure(
                new float2(spawn.x, spawn.z), mask, out int2 structureCell);

            if (!SpawnPathPreview.Build(map, f.Lane, f.PathIndex, layers,
                                        def.Movement.AgentRadiusTiles,
                                        hasStructure, structureCell, _pathBuffer))
                return false;

            Simplify(_pathBuffer);

            Vector3 lift = SurfaceLift();
            guide.Lane = f.Lane;
            guide.PathIndex = f.PathIndex;
            guide.Points.Clear();
            guide.CumLen.Clear();
            for (int i = 0; i < _pathBuffer.Count; i++)
            {
                Vector3 p = (Vector3)BoardSpace.ToView(_pathBuffer[i]) + lift;
                guide.Points.Add(p);
                guide.CumLen.Add(i == 0 ? 0f : guide.CumLen[i - 1] + Vector3.Distance(guide.Points[i - 1], p));
            }
            guide.TotalLen = guide.CumLen[guide.CumLen.Count - 1];
            if (guide.Points.Count < 2 || guide.TotalLen <= 1e-4f) return false;

            if (guide.Ring != null)
            {
                guide.Ring.transform.position = guide.Points[0];
                guide.Ring.transform.rotation = Quaternion.LookRotation(BoardSpace.RaycastPlane().normal);
            }
            return true;
        }

        // 연속 3점이 일직선이면 가운데 점 제거(in-place). 격자 경로 전제라 오차 여유 불요.
        private static void Simplify(List<float3> pts)
        {
            if (pts.Count < 3) return;
            int write = 1;
            for (int i = 1; i < pts.Count - 1; i++)
            {
                float3 prev = pts[write - 1];
                if (math.lengthsq(math.cross(pts[i] - prev, pts[i + 1] - pts[i])) > 1e-6f)
                    pts[write++] = pts[i];
            }
            pts[write++] = pts[pts.Count - 1];
            pts.RemoveRange(write, pts.Count - write);
        }

        // ── 구간 ─────────────────────────────────────────────────────────────
        private void BuildSub(Guide guide, float fromLen, float toLen)
        {
            _drawBuffer.Clear();
            fromLen = Mathf.Clamp(fromLen, 0f, guide.TotalLen);
            toLen = Mathf.Clamp(toLen, fromLen, guide.TotalLen);

            _drawBuffer.Add(PointAt(guide, fromLen));
            for (int i = 0; i < guide.Points.Count; i++)
            {
                float c = guide.CumLen[i];
                if (c <= fromLen) continue;
                if (c >= toLen) break;
                _drawBuffer.Add(guide.Points[i]);
            }
            _drawBuffer.Add(PointAt(guide, toLen));
        }

        private static Vector3 PointAt(Guide guide, float dist)
        {
            if (dist <= 0f) return guide.Points[0];
            int last = guide.Points.Count - 1;
            if (dist >= guide.TotalLen) return guide.Points[last];
            for (int i = 1; i <= last; i++)
            {
                if (guide.CumLen[i] < dist) continue;
                float seg = guide.CumLen[i] - guide.CumLen[i - 1];
                float f = seg > 1e-5f ? (dist - guide.CumLen[i - 1]) / seg : 0f;
                return Vector3.Lerp(guide.Points[i - 1], guide.Points[i], f);
            }
            return guide.Points[last];
        }

        private void Push(Guide guide, float fade)
        {
            guide.Line.positionCount = _drawBuffer.Count;
            for (int i = 0; i < _drawBuffer.Count; i++) guide.Line.SetPosition(i, _drawBuffer[i]);

            var head = _lineColor;
            head.a = _lineColor.a * fade;
            var tail = head;
            tail.a = head.a * _tailAlpha;
            guide.Line.startColor = head;
            guide.Line.endColor = tail;
        }

        private void PaintRing(Guide guide, float drawT, float clock, float fade)
        {
            if (guide.Ring == null) return;
            float phase = _ringPeriod > 0f ? Mathf.Repeat(clock / _ringPeriod, 1f) : 0f;
            float scale = _ringTiles * _driver.TileSize * Mathf.Lerp(0.35f, 1f, phase);
            guide.Ring.transform.localScale = new Vector3(scale, scale, scale);
            var c = Color.Lerp(_lineColor, Color.white, 0.25f);
            c.a = _lineColor.a * (1f - phase) * (1f - phase) * drawT * fade;
            // 스프라이트의 색은 **프로퍼티 블록**으로 민다(`CoreOverlayMaterial` 헤더 참조).
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            guide.Ring.GetPropertyBlock(_mpb);
            _mpb.SetColor(CoreOverlayMaterial.BaseColorId, c);
            guide.Ring.SetPropertyBlock(_mpb);
        }

        // ── 풀 ───────────────────────────────────────────────────────────────
        private void EnsureGuides(int count)
        {
            while (_guides.Count < count)
            {
                int idx = _guides.Count;
                var guide = new Guide
                {
                    Line = CreateLine($"SpawnAlertLine_{idx}", _lineWidth, BoardSortOrder.SpawnAlertOrder + 2),
                    Ring = _ringTiles > 0f ? CreateRing($"SpawnAlertRing_{idx}") : null,
                };
                SetVisible(guide, false);
                _guides.Add(guide);
            }
        }

        private LineRenderer CreateLine(string n, float width, int order)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 6;
            line.numCapVertices = 4;
            line.widthMultiplier = width;
            line.sortingOrder = order;
            line.sharedMaterial = Material();
            line.startColor = line.endColor = _lineColor;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            return line;
        }

        private SpriteRenderer CreateRing(string n)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RingSprite();
            sr.sharedMaterial = Material();
            sr.sortingOrder = BoardSortOrder.SpawnAlertOrder + 3;
            return sr;
        }

        private static void SetVisible(Guide guide, bool on)
        {
            guide.Line.enabled = on;
            if (guide.Ring != null) guide.Ring.enabled = on;
        }

        private static void Hide(Guide guide)
        {
            SetVisible(guide, false);
            guide.Shown = false;
            guide.Retracting = false;
        }

        // 선과 링이 **머티리얼 하나**를 공유한다(색은 정점색이 정한다).
        // ⚠ `AddComponent<LineRenderer>()` 는 머티리얼을 안 준다 — 안 넣으면 마젠타다.
        private Material Material()
            => _material != null ? _material : (_material = CoreOverlayMaterial.Create());

        private Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int n = 64;
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)(n - 1), v = y / (float)(n - 1);
                float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.13f, 2f));
                float core = Mathf.Exp(-Mathf.Pow(r / 0.3f, 2f)) * 0.55f;
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(ring + core));
            }
            _ringTex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            _ringTex.SetPixels(px);
            _ringTex.Apply(false, true);
            _ringSprite = Sprite.Create(_ringTex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return _ringSprite;
        }

        // 보드 평면 법선을 카메라 쪽으로 정렬해 그만큼 띄운다(화면상 위치 불변, 깊이만 분리).
        private Vector3 SurfaceLift()
        {
            if (_surfaceOffset <= 0f) return Vector3.zero;
            Vector3 n = BoardSpace.RaycastPlane().normal;
            if (EnsureCamera() && Vector3.Dot(n, _camera.transform.forward) > 0f) n = -n;
            return n * _surfaceOffset;
        }

        private bool EnsureCamera()
        {
            if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
            return _camera != null;
        }

        private void OnDestroy()
        {
            if (_ringSprite != null) Destroy(_ringSprite);
            if (_ringTex != null) Destroy(_ringTex);
            if (_material != null) Destroy(_material);
        }
    }
}
