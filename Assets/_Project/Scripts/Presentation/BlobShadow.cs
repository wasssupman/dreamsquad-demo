using UnityEngine;
using Wassup.Bridge;

namespace Wassup.Presentation
{
    // tilted-billboard unit 3 — 발밑 접지 블롭 그림자. 빌보드는 틸트가 제각각이라 진짜 그림자는
    // 일관성이 깨지므로, XZ 바닥에 평평한 원형 스프라이트를 깐다. 캐릭터/프랍 공용.
    // shadow-polish unit 6 — 프랍 블롭은 프리팹 authored(생성기가 굽고 프랍별 프리팹에서 미세조정).
    // 이미지마다 피벗/여백이 달라 런타임 계산으로는 위치가 정규화되지 않던 문제의 최종 해법.
    // 유닛(이동)만 런타임 Attach 경로를 쓴다.
    [DisallowMultipleComponent]
    public class BlobShadow : MonoBehaviour
    {
        [Tooltip("프리팹에 authoring 된 블롭(프랍 전용). 위치 XZ/스케일/회전은 프리팹 값이 확정값. sprite/color/sort/바닥 Y 는 Awake 에서 전역값(BattleBridge)으로 정규화.")]
        [SerializeField] private bool authoredInPrefab;

        private Transform _target;
        private float _groundY;
        private float _size;
        private bool _live;

        // 에디터 생성기(PropDataEditor) 전용 — 프리팹 저장 전에 authored 플래그를 굽는다.
        public void MarkAuthored() => authoredInPrefab = true;

        // authored 블롭: 외형(sprite/color/sort)만 전역값 적용. transform 은 일절 건드리지 않는다 —
        // 위치/회전/크기 전부 프리팹 소유. (월드 Y 스냅은 90°X 부모 좌표계에서 authored 오프셋을
        // 인스턴스화 타이밍 의존으로 왜곡시켜 제거함. 바닥 높이도 프리팹에 굽는다.)
        private void Awake()
        {
            if (!authoredInPrefab) return; // 런타임 Attach 경로는 Attach() 가 전부 세팅
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) return;
            if (BattleBridge.BlobShadowSprite != null) sr.sprite = BattleBridge.BlobShadowSprite;
            sr.color = BattleBridge.BlobShadowColor;
            sr.sortingOrder = BoardSortOrder.ShadowOrder;
        }

        // 유닛 자식으로 생성 — 유닛 파괴 시 함께 사라진다.
        // live=false(레거시 프랍 폴백): 스폰 시 transform 한 번 굽고 끝. live=true(유닛): LateUpdate 가 매 프레임 따라간다.
        public static BlobShadow Attach(Transform target, Sprite sprite, float size,
            Color color, float groundY, int sortingOrder, bool live = false)
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
            bs._groundY = groundY;
            bs._live = live;
            bs.ApplyTransform(); // 스폰 시 1회 — 정적 프랍은 이걸로 끝.
            return bs;
        }

        private void LateUpdate()
        {
            // 정적(프랍)은 스폰 1회 세팅으로 끝. 움직이는 유닛만 매 프레임 재고정.
            if (_live) ApplyTransform();
        }

        // 발밑(피벗 XZ) + 바닥 높이에 평평하게(Euler 90,0,0 → 쿼드가 XZ 에 눕는다). 유닛/프랍 틸트와 무관.
        // 부모 스케일 보정으로 월드 지름을 _size(타일)로 고정. 원형(바닥평면+퍼스펙티브가 화면상 타원).
        private void ApplyTransform()
        {
            if (_target == null) return;
            Vector3 p = _target.position;
            transform.position = new Vector3(p.x, _groundY, p.z);
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Vector3 par = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            float sx = Mathf.Approximately(par.x, 0f) ? 1f : par.x;
            float sy = Mathf.Approximately(par.y, 0f) ? 1f : par.y;
            transform.localScale = new Vector3(_size / sx, _size / sy, 1f);
        }
    }
}
