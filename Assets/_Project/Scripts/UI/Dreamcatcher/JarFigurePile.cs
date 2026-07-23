using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Wassup.UI
{
    // dreamcatcher-orb-dock unit 2a — 항아리 안에 게이지 비례 미니 피규어를 물리로 쌓는 뷰.
    // JarFigurePhysics(unit 0) 순수 시뮬을 고정 스텝으로 Tick 하고 결과 위치를 RectTransform 에
    // 매핑한다. 피규어 비주얼은 절차적 스프라이트(placeholder) — unit 2b 에서 SkeletonGraphic
    // 프리즌 스킨으로 교체 예정. RectTransform 은 항아리 인테리어를 채우고 pivot 은 하단중앙이라
    // 시뮬 로컬좌표(x∈[-halfWidth,halfWidth], y=0 바닥)를 anchoredPosition 에 직접 쓸 수 있다.
    public class JarFigurePile : MonoBehaviour
    {
        const float FixedDt = 1f / 60f; // Verlet 안정성 = 고정 dt

        private RectTransform _rt;
        private JarFigure[] _figs;
        private Image[] _views;
        private int _max;
        private float _radius;
        private JarSimParams _params;
        private int _target;
        private int _active;
        private float _spawnCooldown;
        private float _accum;
        private float _spawnInterval = 0.06f;

        public void Configure(int max, float radius, JarSimParams p, Sprite figureSprite,
            Color[] tints, float spawnInterval)
        {
            _rt = (RectTransform)transform;
            _max = Mathf.Max(1, max);
            _radius = radius;
            _params = p;
            _spawnInterval = spawnInterval;
            _figs = new JarFigure[_max];
            _views = new Image[_max];
            for (int i = 0; i < _max; i++)
            {
                var go = new GameObject("Figure" + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var vr = (RectTransform)go.transform;
                vr.anchorMin = vr.anchorMax = new Vector2(0.5f, 0f); // 하단중앙 원점
                vr.pivot = new Vector2(0.5f, 0.5f);
                vr.sizeDelta = new Vector2(radius * 2f, radius * 2f);
                var img = go.GetComponent<Image>();
                img.sprite = figureSprite;
                img.color = (tints != null && tints.Length > 0) ? tints[i % tints.Length] : Color.white;
                img.raycastTarget = false;
                go.SetActive(false);
                _views[i] = img;
            }
        }

        public void SetTargetLevel(float normalized)
        {
            _target = Mathf.Clamp(Mathf.RoundToInt(normalized * _max), 0, _max);
        }

        private JarBounds Bounds()
        {
            float w = _rt.rect.width;
            float h = _rt.rect.height;
            return new JarBounds { halfWidth = w * 0.5f, height = h };
        }

        // 통 위쪽에서 결정론적 x 분산 + 하향 속도로 스폰(인덱스 기반 결정론 — seeded RNG 대신).
        private void SpawnAt(int idx, in JarBounds b)
        {
            float jitterX = (((idx * 53) % 100) / 100f - 0.5f) * b.halfWidth * 1.2f;
            float vx = (((idx * 37) % 100) / 100f - 0.5f) * b.halfWidth * 4f;
            float startY = b.height + _radius;
            _figs[idx] = JarFigurePhysics.Create(new float2(jitterX, startY), new float2(vx, -b.height), _radius, FixedDt);
            _views[idx].gameObject.SetActive(true);
        }

        // 한 고정 스텝: 목표 향해 스폰(프레임당 최대 1, 리듬)/제거, 물리 스텝, 위치 반영.
        public void Tick(float dt)
        {
            if (_rt == null || _figs == null) return;
            var b = Bounds();
            _spawnCooldown -= dt;
            if (_active < _target && _spawnCooldown <= 0f)
            {
                SpawnAt(_active, b);
                _active++;
                _spawnCooldown = _spawnInterval;
            }
            else if (_active > _target)
            {
                _active--;
                _views[_active].gameObject.SetActive(false); // 위에서부터 pop
            }
            if (_active <= 0) return;
            JarFigurePhysics.Step(_figs, _active, b, _params, dt, 6);
            for (int i = 0; i < _active; i++)
                _views[i].rectTransform.anchoredPosition = new Vector2(_figs[i].pos.x, _figs[i].pos.y);
        }

        private void Update()
        {
            _accum += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            int guard = 0;
            while (_accum >= FixedDt && guard++ < 6)
            {
                Tick(FixedDt);
                _accum -= FixedDt;
            }
        }
    }
}
