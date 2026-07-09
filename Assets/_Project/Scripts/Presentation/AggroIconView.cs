using Unity.Entities;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    // aggro-targeting Unit 13 — 어그로된 적 머리 위 아이콘. 상태 구동: 스포너가
    // Aggroed 유무로 Show/Hide 를 reconcile 한다(히트바처럼 fade 수명 없음).
    // 살아있는 동안 적 뷰(anchor)를 따라가고 카메라 빌보드. 모든 수치는
    // AggroIconStyle 에서. 스포너가 풀링한다.
    public class AggroIconView : MonoBehaviour
    {
        // 아트 스프라이트 미할당 시 절차적 폴백(느낌표 원반) — 마이크로바/게이지가
        // 절차 스프라이트를 쓰는 것과 동일 선례. 색/크기는 AggroIconStyle 이 결정.
        private static Sprite _fallbackSprite;

        private SpriteRenderer _sr;
        private Camera _camera;
        private AggroIconStyle _style;
        private Transform _anchor;
        private Vector3 _lastBase;
        private Entity _entity;
        private bool _active;

        public Entity Entity => _entity;

        public void Show(Entity entity, Transform anchor, AggroIconStyle style, Camera cam)
        {
            _entity = entity;
            _style = style;
            _camera = cam;
            EnsureBuilt();
            _sr.sprite = style.icon != null ? style.icon : FallbackSprite();
            _sr.color = style.tint;
            _sr.sortingOrder = style.sortingOrder;
            _anchor = anchor;
            _lastBase = anchor != null ? anchor.position : _lastBase;
            _active = true;
            gameObject.SetActive(true);
            Follow();
        }

        // 같은 적 유지: anchor 갱신만.
        public void Refresh(Transform anchor)
        {
            _anchor = anchor;
            if (anchor != null) _lastBase = anchor.position;
        }

        private void EnsureBuilt()
        {
            if (_sr != null) return;
            var go = new GameObject("Icon");
            go.transform.SetParent(transform, false);
            _sr = go.AddComponent<SpriteRenderer>();
        }

        // 느낌표(!) 모양 흰색 스프라이트를 절차적으로 1회 생성. 색은 tint 로 곱해진다.
        private static Sprite FallbackSprite()
        {
            if (_fallbackSprite != null) return _fallbackSprite;
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var clear = new Color(1, 1, 1, 0);
            var px = new Color[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            // 텍스처 y=0 은 하단. "!"가 바로 서도록 막대는 위(높은 y), 점은 아래(낮은 y).
            int cx = S / 2;
            for (int y = 22; y < 52; y++) // 세로 막대(위)
                for (int x = cx - 5; x <= cx + 5; x++)
                    px[y * S + x] = Color.white;
            for (int y = 8; y < 18; y++)  // 점(아래)
                for (int x = cx - 5; x <= cx + 5; x++)
                    px[y * S + x] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
            return _fallbackSprite;
        }

        private void Update()
        {
            if (!_active) return;
            Follow();
        }

        private void Follow()
        {
            if (_anchor != null) _lastBase = _anchor.position;
            transform.position = _lastBase + Vector3.up * _style.headYOffset;
            float s = _style.SampleScale(Time.time);
            transform.localScale = new Vector3(s, s, s);
        }

        private void LateUpdate()
        {
            if (!_active || _camera == null) return;
            transform.rotation = _camera.transform.rotation; // full billboard
        }

        public void Hide()
        {
            _active = false;
            _anchor = null;
            gameObject.SetActive(false);
        }
    }
}
