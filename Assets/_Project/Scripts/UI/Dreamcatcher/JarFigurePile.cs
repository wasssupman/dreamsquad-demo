using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Wassup.UI
{
    // dreamcatcher-orb-dock unit 2a/3 — 항아리 안 미니 피규어 물리 더미.
    // JarFigurePhysics(unit 0) 순수 시뮬을 고정 스텝으로 Tick 하고 위치를 RectTransform 에 매핑.
    // 피규어 비주얼은 절차적 스프라이트(최종). 개수는 이 컴포넌트가 아니라 **뷰가** 명시적으로
    // 구동한다(unit 3: 흡수 비행이 도착할 때 SpawnAtTop, 소비/리셋 때 RemoveTop). RectTransform 은
    // 항아리 인테리어를 채우고 pivot 하단중앙이라 시뮬 로컬좌표(x∈[-halfWidth,halfWidth], y=0
    // 바닥)를 anchoredPosition 에 직접 쓸 수 있다.
    public class JarFigurePile : MonoBehaviour
    {
        const float FixedDt = 1f / 60f; // Verlet 안정성 = 고정 dt

        private RectTransform _rt;
        private JarFigure[] _figs;
        private Image[] _views;
        private int _max;
        private float _radius;
        private JarSimParams _params;
        private int _active;
        private float _accum;

        public int ActiveCount => _active;
        public int Capacity => _max;

        public void Configure(int max, float radius, JarSimParams p, Sprite figureSprite, Color[] tints)
        {
            _rt = (RectTransform)transform;
            _max = Mathf.Max(1, max);
            _radius = radius;
            _params = p;
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

        private JarBounds Bounds()
        {
            float w = _rt.rect.width;
            float h = _rt.rect.height;
            return new JarBounds { halfWidth = w * 0.5f, height = h };
        }

        // 통 위쪽에서 결정론적 x 분산 + 하향 속도로 한 개 스폰(흡수 비행 도착 시 호출).
        public void SpawnAtTop()
        {
            if (_rt == null || _figs == null || _active >= _max) return;
            var b = Bounds();
            int idx = _active;
            float jitterX = (((idx * 53) % 100) / 100f - 0.5f) * b.halfWidth * 1.2f;
            float vx = (((idx * 37) % 100) / 100f - 0.5f) * b.halfWidth * 4f;
            float startY = b.height + _radius;
            _figs[idx] = JarFigurePhysics.Create(new float2(jitterX, startY), new float2(vx, -b.height), _radius, FixedDt);
            _views[idx].gameObject.SetActive(true);
            _views[idx].rectTransform.anchoredPosition = new Vector2(jitterX, startY);
            _active++;
        }

        // 위에서부터 한 개 제거(소비/리셋).
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

        // 물리 한 고정 스텝(개수 조작 없음 — add/remove 는 외부가 구동).
        public void Tick(float dt)
        {
            if (_rt == null || _figs == null || _active <= 0) return;
            var b = Bounds();
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
