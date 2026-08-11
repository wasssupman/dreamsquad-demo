using Spine.Unity;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-orb-dock unit 2b — 항아리 안 미니 피규어(SkeletonGraphic 미니어처) 물리 더미.
    // JarFigurePhysics(unit 0) 순수 시뮬을 고정 스텝으로 Tick 하고 위치를 RectTransform 에 매핑.
    // 피규어 = 대표 유닛 스켈레톤을 특정 애니(기본 Idle) 마지막 프레임에 동결한 SkeletonGraphic.
    // 데이터/머티리얼 미배선 시 절차적 원 스프라이트로 폴백(무회귀). 개수는 뷰가 명시적으로 구동
    // (흡수 비행 도착 시 SpawnAtTop, 소비/리셋 때 RemoveTop). 주기적 통통 튕김(JostleAll).
    // RectTransform 은 항아리 인테리어를 채우고 pivot 하단중앙이라 시뮬 로컬좌표를 직접 매핑.
    public class JarFigurePile : MonoBehaviour
    {
        const float FixedDt = 1f / 60f; // Verlet 안정성 = 고정 dt

        private RectTransform _rt;
        private JarFigure[] _figs;
        private Graphic[] _views; // SkeletonGraphic 또는 Image(폴백)
        // unit 7 — 피규어 회전(누운 자세로도 낙하). 낙하 시 텀블 → rest 각으로 감쇠 스프링 정착.
        private float[] _rot;    // 현재 각(deg)
        private float[] _rotVel; // 각속도(deg/s)
        private int _max;
        private float _radius;
        private JarSimParams _params;
        private int _active;
        private float _accum;
        // 주기적 통통 튕김 — 정착해 잠들지 않게 주기적으로 위로 임펄스(사용자 요청).
        private float _jostleInterval = 1.4f;
        private float _jostleStrength = 3.2f;
        private float _jostleTimer;
        private float _jostlePhase;

        public int ActiveCount => _active;
        public int Capacity => _max;

        public void SetJostle(float interval, float strength)
        {
            _jostleInterval = Mathf.Max(0.05f, interval);
            _jostleStrength = strength;
        }

        // Spine 미니어처 풀 생성. visualData+material 있으면 SkeletonGraphic, 없으면 원 폴백.
        public void Configure(int max, float radius, JarSimParams p,
            ISpineUnitVisualData visualData, Material skeletonMaterial, float figureScale, string animName,
            Sprite fallbackSprite, Color[] fallbackTints)
        {
            _rt = (RectTransform)transform;
            _max = Mathf.Max(1, max);
            _radius = radius;
            _params = p;
            _figs = new JarFigure[_max];
            _views = new Graphic[_max];
            _rot = new float[_max];
            _rotVel = new float[_max];

            bool useSpine = SpineFigureBuilder.CanBuild(visualData, skeletonMaterial);
            for (int i = 0; i < _max; i++)
            {
                _views[i] = useSpine
                    ? BuildSkeletonFigure(i, visualData, skeletonMaterial, figureScale, animName)
                    : BuildSpriteFigure(i, radius, fallbackSprite, fallbackTints);
                _views[i].gameObject.SetActive(false);
            }
        }

        private Graphic BuildSpriteFigure(int i, float radius, Sprite sprite, Color[] tints)
        {
            var go = new GameObject("Figure" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var vr = (RectTransform)go.transform;
            vr.anchorMin = vr.anchorMax = new Vector2(0.5f, 0f);
            vr.pivot = new Vector2(0.5f, 0.5f);
            vr.sizeDelta = new Vector2(radius * 2f, radius * 2f);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = (tints != null && tints.Length > 0) ? tints[i % tints.Length] : Color.white;
            img.raycastTarget = false;
            return img;
        }

        // SkeletonGraphic 미니어처: 스킨 적용 + 지정 애니 마지막 프레임 동결. 셋업 시퀀스는
        // SquadCharacterPage/SpineUnitView 관례 + spine-unity 4.2 freeze(마지막 프레임 UpdateMesh 후 정지).
        private Graphic BuildSkeletonFigure(int i, ISpineUnitVisualData data, Material mat, float scale, string animName)
        {
            var go = new GameObject("Figure" + i, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var vr = (RectTransform)go.transform;
            vr.anchorMin = vr.anchorMax = new Vector2(0.5f, 0f);
            vr.pivot = new Vector2(0.5f, 0.5f);

            var sg = SpineFigureBuilder.Build(go, data, mat, animName);
            go.transform.localScale = Vector3.one * scale;
            return sg;
        }

        private JarBounds Bounds()
        {
            float w = _rt.rect.width;
            float h = _rt.rect.height;
            return new JarBounds { halfWidth = w * 0.5f, height = h };
        }

        public void SpawnAtTop() => SpawnAtTop(null);

        // dreamcatcher-orb-dock unit 6 — 활성화하는 피규어를 "죽은 유닛" 스킨으로 교체
        // (data==null 이면 빌드 시 대표 스킨 유지). 스켈레톤 불일치 시 Reskin 이 스킵.
        public void SpawnAtTop(ISpineUnitVisualData data)
        {
            if (_rt == null || _figs == null || _active >= _max) return;
            var b = Bounds();
            int idx = _active;
            float jitterX = (((idx * 53) % 100) / 100f - 0.5f) * b.halfWidth * 1.2f;
            float vx = (((idx * 37) % 100) / 100f - 0.5f) * b.halfWidth * 4f;
            float startY = b.height + _radius;
            _figs[idx] = JarFigurePhysics.Create(new float2(jitterX, startY), new float2(vx, -b.height), _radius, FixedDt);
            // unit 7 — 낙하 텀블: 초기 각속도(인덱스 결정론)로 회전하며 떨어져 rest 각으로 정착.
            _rot[idx] = 0f;
            _rotVel[idx] = (((idx * 47) % 2) == 0 ? 1f : -1f) * (140f + (idx * 29) % 90);
            if (data != null && _views[idx] is SkeletonGraphic sg) SpineFigureBuilder.Reskin(sg, data);
            _views[idx].gameObject.SetActive(true);
            _views[idx].rectTransform.anchoredPosition = new Vector2(jitterX, startY);
            _views[idx].rectTransform.localEulerAngles = Vector3.zero;
            _active++;
        }

        // unit 7 — 피규어별 rest 각(deg). 인덱스 결정론 변주: 다수 살짝 기울고, 일부는 눕는다.
        private static float RestRot(int i)
        {
            float h = ((i * 61) % 100) / 100f; // 0..1 해시
            if (h < 0.30f) return (h / 0.30f - 0.5f) * 36f;                 // -18..+18 (살짝 기욺)
            if (h < 0.62f) return ((h - 0.30f) / 0.32f - 0.5f) * 100f;      // -50..+50 (기울어짐)
            return (((i * 17) % 2) == 0 ? -1f : 1f) * Mathf.Lerp(70f, 93f, (h - 0.62f) / 0.38f); // ±70..93 (누움)
        }

        public void RemoveTop()
        {
            if (_active <= 0) return;
            _active--;
            _views[_active].gameObject.SetActive(false);
        }

        public void Clear()
        {
            while (_active > 0) RemoveTop();
        }

        // 주기적으로 통 안 피규어에 위로 임펄스를 가해 계속 통통 튕기게 한다(정착·수면 방지).
        // 인덱스 기반 결정론 변주(seeded RNG 대신). Verlet 임펄스 = prevPos 를 뒤로 밀기.
        private void JostleAll()
        {
            _jostlePhase += 1.7f;
            for (int i = 0; i < _active; i++)
            {
                var f = _figs[i];
                float dx = Mathf.Sin(i * 2.4f + _jostlePhase) * _jostleStrength;
                float dy = (0.5f + 0.5f * Mathf.Abs(Mathf.Cos(i * 1.7f + _jostlePhase))) * _jostleStrength;
                f.prevPos.x -= dx;
                f.prevPos.y -= dy; // 위로(양수 y = 상방)
                _figs[i] = f;
                // unit 7 — 튕길 때 살짝 흔들림(회전에도 생기 부여).
                _rotVel[i] += Mathf.Sin(i * 3.1f + _jostlePhase) * _jostleStrength * 6f;
            }
        }

        // unit 7 rev — 어필 바운스 리듬에 맞춘 1회 홉(들썩): 전 피규어에 위 임펄스 + 회전 킥.
        // 갇힌 꿈-피규어가 "꺼내줘" 하듯 잠깐 튄다. 주기적(어필 주기)이라 상시 노이즈 아님.
        public void Hop()
        {
            if (_figs == null || _active <= 0) return;
            _jostlePhase += 2.3f;
            for (int i = 0; i < _active; i++)
            {
                var f = _figs[i];
                f.prevPos.y -= _jostleStrength * 2.4f; // 위로(prevPos 낮추면 상방 속도)
                _figs[i] = f;
                _rotVel[i] += Mathf.Sin(i * 2.1f + _jostlePhase) * _jostleStrength * 10f;
            }
        }

        public void Tick(float dt)
        {
            if (_rt == null || _figs == null || _active <= 0) return;
            var b = Bounds();
            JarFigurePhysics.Step(_figs, _active, b, _params, dt, 6);
            // unit 7 — 각도 감쇠 스프링: 낙하 텀블 → rest 각 정착(누운 자세 포함).
            const float rotK = 55f, rotDamp = 7.5f;
            for (int i = 0; i < _active; i++)
            {
                _rotVel[i] += (RestRot(i) - _rot[i]) * rotK * dt;
                _rotVel[i] *= Mathf.Exp(-rotDamp * dt);
                _rot[i] += _rotVel[i] * dt;
                var vrt = _views[i].rectTransform;
                vrt.anchoredPosition = new Vector2(_figs[i].pos.x, _figs[i].pos.y);
                vrt.localEulerAngles = new Vector3(0f, 0f, _rot[i]);
            }
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (_active > 0 && _jostleStrength > 0f)
            {
                _jostleTimer -= dt;
                if (_jostleTimer <= 0f)
                {
                    JostleAll();
                    _jostleTimer = _jostleInterval;
                }
            }
            _accum += dt;
            int guard = 0;
            while (_accum >= FixedDt && guard++ < 6)
            {
                Tick(FixedDt);
                _accum -= FixedDt;
            }
        }
    }
}
