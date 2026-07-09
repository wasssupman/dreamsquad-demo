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
            _sr.sprite = style.icon;
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
