using System;
using Unity.Entities;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    // unit-health-display unit 2 — 적 피격 시 잠깐 뜨는 마이크로 체력바.
    // 살아있는 동안 적 뷰(anchor)를 따라가고, hold 후 fade. anchor 가 파괴되면
    // (막타) 마지막 위치에서 계속 페이드. bg + fill 스프라이트, 카메라 빌보드.
    // 모든 수치는 HealthDisplayStyle 에서. 스포너가 풀링한다.
    public class EnemyHitBarView : MonoBehaviour
    {
        private static Sprite _centerSprite;
        private static Sprite _leftSprite;

        private SpriteRenderer _bg;
        private SpriteRenderer _fill;
        private Transform _fillT;

        private Camera _camera;
        private HealthDisplayStyle _style;
        private Action<EnemyHitBarView> _onComplete;

        private Entity _entity;
        private Transform _anchor;
        private Vector3 _lastBase;   // 적 발치(view 좌표). anchor 살아있으면 매 프레임 갱신.
        private float _elapsed;
        private bool _playing;
        private Color _bgColor;
        private Color _fillColor;

        public Entity Entity => _entity;

        public void Play(Entity entity, Transform anchor, Vector3 fallbackBase, float hpRatio,
                         HealthDisplayStyle style, Camera cam, Action<EnemyHitBarView> onComplete)
        {
            _entity = entity;
            _style = style;
            _camera = cam;
            _onComplete = onComplete;
            EnsureBuilt();
            _anchor = anchor;
            _lastBase = anchor != null ? anchor.position : fallbackBase;
            SetRatio(hpRatio);
            transform.position = _lastBase + Vector3.up * _style.HitBarHeadYOffset;
            gameObject.SetActive(true);
            _elapsed = 0f;
            _playing = true;
            ApplyAlpha(1f);
        }

        // 같은 적이 연속 피격: 새 바를 쌓지 않고 fill 갱신 + hold 타이머 리셋(스택 금지).
        public void Refresh(Transform anchor, Vector3 fallbackBase, float hpRatio)
        {
            _anchor = anchor;
            _lastBase = anchor != null ? anchor.position : fallbackBase;
            SetRatio(hpRatio);
            _elapsed = 0f;
            _playing = true;
            ApplyAlpha(1f);
        }

        private void SetRatio(float hpRatio)
        {
            float r = HealthDisplayStyle.SafeRatio01(hpRatio);
            _bgColor = _style.HitBarBgColor;
            _fillColor = _style.EvaluateHitBarFill(r);
            Vector2 size = _style.HitBarSize;
            float pad = size.y * 0.18f;
            float maxFillW = Mathf.Max(0f, size.x - 2f * pad);
            _bg.transform.localScale = new Vector3(size.x, size.y, 1f);
            _fillT.localPosition = new Vector3(-size.x * 0.5f + pad, 0f, 0f);
            _fillT.localScale = new Vector3(maxFillW * r, Mathf.Max(0f, size.y - 2f * pad), 1f);
        }

        private void Update()
        {
            if (!_playing) return;
            if (_anchor != null) _lastBase = _anchor.position; // 파괴 시 마지막 위치 유지
            transform.position = _lastBase + Vector3.up * _style.HitBarHeadYOffset;

            _elapsed += Time.deltaTime;
            float hold = _style.HitBarHoldSec;
            if (_elapsed <= hold) { ApplyAlpha(1f); return; }
            float f = (_elapsed - hold) / _style.HitBarFadeSec;
            if (f >= 1f) { Finish(); return; }
            ApplyAlpha(1f - f);
        }

        private void LateUpdate()
        {
            if (!_playing || _camera == null) return;
            transform.rotation = _camera.transform.rotation; // full billboard
        }

        private void ApplyAlpha(float a)
        {
            var b = _bgColor; b.a = _bgColor.a * a; _bg.color = b;
            var fc = _fillColor; fc.a = _fillColor.a * a; _fill.color = fc;
        }

        private void Finish()
        {
            _playing = false;
            gameObject.SetActive(false);
            _anchor = null;
            var cb = _onComplete;
            _onComplete = null;
            cb?.Invoke(this);
        }

        // teardown 시 스포너 Clear 가 호출. 콜백 없이 즉시 비활성(풀 관리는 스포너가 직접).
        public void Deactivate()
        {
            _playing = false;
            _anchor = null;
            _onComplete = null;
            gameObject.SetActive(false);
        }

        private void EnsureBuilt()
        {
            if (_bg != null) return;
            _bg = MakeRenderer("bg", CenterSprite(), BoardSortOrder.HitBarOrder);
            _fill = MakeRenderer("fill", LeftSprite(), BoardSortOrder.HitBarOrder + 1);
            _fillT = _fill.transform;
        }

        private SpriteRenderer MakeRenderer(string childName, Sprite sprite, int order)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        // 1x1 월드 유닛 흰 스프라이트(shared). localScale 로 실제 크기 제어.
        private static Sprite CenterSprite()
            => _centerSprite != null ? _centerSprite
             : (_centerSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4),
                    new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect));

        // 좌측 피벗: localScale.x 를 줄이면 왼쪽 끝 고정한 채 오른쪽으로 줄어든다(fill 표현).
        private static Sprite LeftSprite()
            => _leftSprite != null ? _leftSprite
             : (_leftSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4),
                    new Vector2(0f, 0.5f), 4f, 0, SpriteMeshType.FullRect));
    }
}
