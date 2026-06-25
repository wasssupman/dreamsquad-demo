using UnityEngine;

namespace Wassup.Presentation
{
    // tilted-billboard unit 3 — 발밑 접지 블롭 그림자. 빌보드는 틸트가 제각각이라 진짜 그림자는
    // 일관성이 깨지므로, XZ 바닥에 평평한 타원 스프라이트를 깐다. 캐릭터/프랍 공용.
    // target(유닛 transform)을 따라가며 매 프레임 월드 transform 을 바닥 평면에 고정한다.
    [DisallowMultipleComponent]
    public class BlobShadow : MonoBehaviour
    {
        private Transform _target;
        private float _groundY;
        private float _size;
        private Vector2 _footprint;

        // 유닛 자식으로 생성 — 유닛 파괴 시 함께 사라진다. 월드 transform 은 LateUpdate 가 직접 구동.
        public static BlobShadow Attach(Transform target, Sprite sprite, float size,
            Vector2 footprint, Color color, float groundY, int sortingOrder)
        {
            var go = new GameObject("BlobShadow");
            go.transform.SetParent(target, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            var bs = go.AddComponent<BlobShadow>();
            bs._target = target;
            bs._size = size;
            bs._footprint = footprint;
            bs._groundY = groundY;
            return bs;
        }

        private void LateUpdate()
        {
            if (_target == null) return;
            // 발밑(셀 XZ) + 바닥 높이. 유닛 틸트와 무관하게 바닥에 평평(Euler 90,0,0 → 쿼드가 XZ 에 눕는다).
            Vector3 p = _target.position;
            transform.position = new Vector3(p.x, _groundY, p.z);
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            // 부모(유닛) 스케일 보정 — 월드 footprint 를 유지(타원: X=가로, Y(로컬)→Z(깊이)).
            Vector3 par = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            float sx = Mathf.Approximately(par.x, 0f) ? 1f : par.x;
            float sy = Mathf.Approximately(par.y, 0f) ? 1f : par.y;
            transform.localScale = new Vector3(_size * _footprint.x / sx, _size * _footprint.y / sy, 1f);
        }
    }
}
