using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Wassup.UI
{
    // Serialized tuning for the score's per-kill impact burst. Lives on ScoreHudView
    // (inspector-visible, Play-time tunable) and is passed to ScoreBurstPool.
    [System.Serializable]
    public class ScoreBurstStyle
    {
        [Tooltip("버스트 파티클 스프라이트. Null → 빌트인 소프트 원(Knob).")]
        public Sprite sprite;
        [Tooltip("골드 계열 색")]
        public Color color = new Color(1f, 0.82f, 0.32f, 1f);
        [Tooltip("1처치당 방출 개수")]
        public int countPerKill = 9;
        [Tooltip("전역 동시 쿼드 상한(모바일 안전)")]
        public int maxConcurrent = 72;
        [Tooltip("초기 방사 속도(px/s)")]
        public float speed = 340f;
        [Tooltip("속도 감쇠(1/s)")]
        public float drag = 2.6f;
        [Tooltip("중력(px/s^2, 아래로)")]
        public float gravity = 680f;
        [Tooltip("수명(초)")]
        public float lifetime = 0.55f;
        [Tooltip("시작 크기(px)")]
        public float startSize = 24f;
        [Tooltip("끝 크기 배수")]
        public float endSizeMul = 0.15f;
        [Tooltip("같은 프레임 다처치당 추가 개수")]
        public float extraPerKill = 4f;
        [Tooltip("같은 프레임 추가 개수 상한")]
        public int extraMax = 22;
    }

    // Pooled procedural UGUI Image-quad burst for the score HUD. Real ParticleSystems
    // don't composite on a ScreenSpaceOverlay canvas, so we drive lightweight Image
    // quads by hand (the project's "UiEmber" approach — see DraftCardVfxDriver — but
    // one-shot + pooled + deterministic spread, not the looping/Random ambient version).
    // Owned and ticked by ScoreHudView; not a MonoBehaviour.
    public class ScoreBurstPool
    {
        private RectTransform _root;
        private ScoreBurstStyle _style;
        private Sprite _sprite;
        private readonly List<Quad> _quads = new();
        private int _emitCounter;

        private class Quad
        {
            public RectTransform rt;
            public Image img;
            public Vector2 pos;
            public Vector2 vel;
            public float age;
            public float life;
            public bool active;
        }

        public void Init(RectTransform parent, ScoreBurstStyle style)
        {
            _style = style;
            var go = new GameObject("ScoreBurst", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            _root = (RectTransform)go.transform;
            _root.anchorMin = new Vector2(0.5f, 1f);
            _root.anchorMax = new Vector2(0.5f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.anchoredPosition = Vector2.zero;
            _root.sizeDelta = Vector2.zero;
            _root.SetAsFirstSibling(); // render behind caption + value

            _sprite = style.sprite != null
                ? style.sprite
                : Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        }

        // Emit one burst centered at `center` (local to the parent), intensity-scaled by
        // how many kills landed this frame. Respects the global concurrent ceiling.
        public void Emit(Vector2 center, int killCount)
        {
            if (_root == null || _style == null) return;

            int extra = Mathf.Min(Mathf.RoundToInt(Mathf.Max(0, killCount - 1) * _style.extraPerKill), _style.extraMax);
            int want = _style.countPerKill + extra;
            int room = _style.maxConcurrent - ActiveCount();
            int count = Mathf.Clamp(want, 0, Mathf.Max(0, room));

            // Golden-angle rotation per burst so consecutive bursts don't align; even
            // spread within a burst. Deterministic (emit counter index), not Random.
            float baseAngle = _emitCounter * 2.399963f; // golden angle (rad)
            for (int i = 0; i < count; i++)
            {
                float ang = baseAngle + (Mathf.PI * 2f) * i / Mathf.Max(1, count) + (i * 0.618f);
                float sp = _style.speed * (0.75f + 0.5f * ((i * 7 % 11) / 11f));
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                var q = Fetch();
                q.pos = center;
                q.vel = dir * sp + new Vector2(0f, _style.speed * 0.15f); // slight upward bias
                q.age = 0f;
                q.life = _style.lifetime * (0.85f + 0.3f * ((i * 5 % 7) / 7f));
                q.active = true;
                q.rt.gameObject.SetActive(true);
                q.rt.anchoredPosition = q.pos;
                q.rt.sizeDelta = new Vector2(_style.startSize, _style.startSize);
                q.img.color = _style.color;
            }
            _emitCounter++;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            for (int i = 0; i < _quads.Count; i++)
            {
                var q = _quads[i];
                if (!q.active) continue;
                q.age += dt;
                if (q.age >= q.life) { Release(q); continue; }
                float t = q.age / q.life;
                q.vel.y -= _style.gravity * dt;
                q.vel *= Mathf.Max(0f, 1f - _style.drag * dt);
                q.pos += q.vel * dt;
                q.rt.anchoredPosition = q.pos;
                float size = Mathf.Lerp(_style.startSize, _style.startSize * _style.endSizeMul, t);
                q.rt.sizeDelta = new Vector2(size, size);
                var c = _style.color;
                c.a *= 1f - t * t; // ease-out fade
                q.img.color = c;
            }
        }

        public void ClearAll()
        {
            for (int i = 0; i < _quads.Count; i++)
                if (_quads[i].active) Release(_quads[i]);
        }

        private int ActiveCount()
        {
            int n = 0;
            for (int i = 0; i < _quads.Count; i++) if (_quads[i].active) n++;
            return n;
        }

        private Quad Fetch()
        {
            for (int i = 0; i < _quads.Count; i++)
                if (!_quads[i].active) return _quads[i];

            var go = new GameObject("burst", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.AddComponent<Image>();
            img.sprite = _sprite;
            img.raycastTarget = false;
            var q = new Quad { rt = rt, img = img, active = false };
            _quads.Add(q);
            return q;
        }

        private void Release(Quad q)
        {
            q.active = false;
            if (q.rt != null) q.rt.gameObject.SetActive(false);
        }
    }
}
