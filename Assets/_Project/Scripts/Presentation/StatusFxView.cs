using Unity.Entities;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    // unit-status-fx Unit 1 — 상태 연출 뷰(구 AggroIconView 일반화). registry 프리팹을
    // Instantiate 해 유닛을 따라가고(옵션 빌보드), prefab 없으면 절차적 "!" 폴백(현
    // 어그로 외형 유지, 약한 펄스). 스포너가 kind별 풀링하며 Show/Hide 로 구동.
    public class StatusFxView : MonoBehaviour
    {
        private static Sprite _fallbackSprite;

        private Camera _camera;
        private Transform _anchor;
        private Vector3 _offset;
        private float _scale;
        private bool _billboard;
        private bool _usesFallback;

        private Entity _entity;
        private StatusFxKind _kind;
        private bool _built;
        private bool _active;
        private GameObject _prefabInstance;
        private SpriteRenderer _fallbackSr;

        public Entity Entity => _entity;
        public StatusFxKind Kind => _kind;

        public void Show(Entity entity, StatusFxKind kind, Transform anchor, StatusFxRegistry.Entry entry, Camera cam)
        {
            _entity = entity;
            _kind = kind;
            _anchor = anchor;
            _camera = cam;
            _offset = entry.localOffset;
            _scale = entry.scale <= 0f ? 1f : entry.scale;
            _billboard = entry.billboard;
            EnsureBuilt(entry);
            _active = true;
            gameObject.SetActive(true);
            Follow();
        }

        // 같은 유닛 유지: anchor 갱신만.
        public void Refresh(Transform anchor) => _anchor = anchor;

        private void EnsureBuilt(StatusFxRegistry.Entry entry)
        {
            if (_built) return; // 풀은 kind별이라 재사용 뷰는 이미 올바른 프리팹 보유.
            _built = true;
            if (entry.prefab != null)
            {
                _usesFallback = false;
                _prefabInstance = Instantiate(entry.prefab, transform);
                _prefabInstance.transform.localPosition = Vector3.zero;
            }
            else
            {
                _usesFallback = true;
                var go = new GameObject("Fallback");
                go.transform.SetParent(transform, false);
                _fallbackSr = go.AddComponent<SpriteRenderer>();
                _fallbackSr.sprite = FallbackSprite();
                _fallbackSr.color = entry.fallbackTint.a > 0f ? entry.fallbackTint : Color.white;
                _fallbackSr.sortingOrder = 15000;
            }
        }

        private void Update()
        {
            if (_active) Follow();
        }

        private void Follow()
        {
            Vector3 basePos = _anchor != null ? _anchor.position : transform.position;
            transform.position = basePos + _offset;
            // 폴백 "!"만 약한 펄스(현 어그로 외형 보존). 프리팹은 자체 애니메이션.
            float s = _scale;
            if (_usesFallback) s *= 1f + 0.12f * Mathf.Sin(Time.time * 3f);
            transform.localScale = new Vector3(s, s, s);
        }

        private void LateUpdate()
        {
            if (_active && _billboard && _camera != null)
                transform.rotation = _camera.transform.rotation;
        }

        public void Hide()
        {
            _active = false;
            _anchor = null;
            gameObject.SetActive(false);
        }

        // 느낌표(!) 흰색 스프라이트 절차 생성(1회). 색은 fallbackTint 로 곱.
        private static Sprite FallbackSprite()
        {
            if (_fallbackSprite != null) return _fallbackSprite;
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var clear = new Color(1, 1, 1, 0);
            var px = new Color[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = clear;
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
    }
}
