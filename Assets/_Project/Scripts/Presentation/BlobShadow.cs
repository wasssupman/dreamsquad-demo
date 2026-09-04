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
        private float _lift;
        private float _size;
        private bool _live;
        // placement-enemy-see-through unit 0 — dim 페이드용 원색 캐시.
        private SpriteRenderer _sr;
        private Color _baseColor = Color.white;

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
            _sr = sr;
            _baseColor = sr.color;
        }

        // 유닛 자식으로 생성 — 유닛 파괴 시 함께 사라진다.
        // live=false(레거시 프랍 폴백): 스폰 시 transform 한 번 굽고 끝. live=true(유닛): LateUpdate 가 매 프레임 따라간다.
        public static BlobShadow Attach(Transform target, Sprite sprite, float size,
            Color color, float lift, int sortingOrder, bool live = false)
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
            bs._lift = lift;
            bs._live = live;
            bs._sr = sr;
            bs._baseColor = color;
            bs.ApplyTransform(); // 스폰 시 1회 — 정적 프랍은 이걸로 끝.
            return bs;
        }

        // placement-enemy-see-through unit 0 — 드래그 배치 중 blob 그림자도 함께 페이드.
        // 원색 알파에 배수만 적용(색조/정렬/transform 불변). factor=1 이면 원상 복구.
        public void SetDimAlpha(float factor)
        {
            _dimFactor = Mathf.Clamp01(factor);
            ApplyColor();
        }

        // flight-lift-feel unit 1 — 유닛이 뜨면 그림자는 **지면에 남은 채** 옅어진다.
        // ⚠ 크기는 안 변한다(distance-based-range unit 15): 지름은 판정 몸이라 높이로 흔들면
        // 「그림자가 링에 닿으면 사거리 안」이 공중에서 거짓이 된다. `scaleMul` 인자는 그
        // 결정 이후 **항상 1** 이다(`UnitLiftVisual.Resolve` 가 shadowScale 을 1로 고정).
        // 지면 Y 고정(ApplyTransform)은 원래부터 있었고, 여기서 반응만 얹는다.
        // 배율 2종은 각자 보관해 곱한다 — SetDimAlpha 가 base 색을 통째로 덮어쓰던 구조라
        // 그대로 두면 배치 dim 과 비행 알파가 서로를 지운다.
        public void SetFlight(float scaleMul, float alphaMul)
        {
            _flightScale = Mathf.Max(0f, scaleMul);
            _flightAlphaFactor = Mathf.Clamp01(alphaMul);
            ApplyColor();
        }

        private float _dimFactor = 1f;
        private float _flightAlphaFactor = 1f;
        private float _flightScale = 1f;

        // flight-lift-feel — 비행 중 접지 XZ 앵커 override.
        // 기본은 부모(유닛) 위치를 따라간다. 그런데 드롭·재배치 아치는 **camUp 방향**이라
        // (배틀 카메라 pitch 60° → camUp.z = 0.866) 유닛의 월드 Z 가 아치 높이만큼 밀린다 —
        // 그림자가 착지 타일에서 2타일 가까이 미끄러졌다 돌아온다. 그림자는 "어느 칸에 내려앉나"를
        // 알려주는 앵커인데 하필 그 순간에 엉뚱한 칸을 가리키는 셈이다.
        // 그래서 비행 중엔 **아치 기저선**(출발→도착 직선)을 앵커로 받는다. 실제 그림자도 그렇게 움직인다.
        // 보스 도약·넉업은 아치가 순수 +Y 라 XZ 가 안 밀리므로 이 경로를 쓰지 않는다.
        private bool _hasGroundAnchor;
        private Vector3 _groundAnchor;

        public void SetGroundAnchor(Vector3 worldPos)
        {
            _hasGroundAnchor = true;
            _groundAnchor = worldPos;
        }

        public void ClearGroundAnchor() => _hasGroundAnchor = false;

        private void ApplyColor()
        {
            if (_sr == null) return;
            _sr.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b,
                                  _baseColor.a * _dimFactor * _flightAlphaFactor);
        }

        private void LateUpdate()
        {
            // 정적(프랍)은 스폰 1회 세팅으로 끝. 움직이는 유닛만 매 프레임 재고정.
            if (_live) ApplyTransform();
        }

        // 발밑(피벗 XZ) + **보드 평면** 위에 평평하게(Euler 90,0,0 → 쿼드가 XZ 에 눕는다). 유닛/프랍 틸트와 무관.
        //
        // tilted-billboard unit 7 — 높이는 절대 월드 Y 상수가 아니라 **스테이지가 선언한 평면**에서 온다.
        // MapStage 마다 발바닥 평면이 다르다(gridOriginLocal.y: Hello 0 / Duel·Street·Subway 0.19 /
        // StreetDay 0.87). 구 상수 0.216 은 0.19 스테이지에 손으로 맞춘 값이라 StreetDay 에서 0.65
        // 만큼 바닥 아래로 파묻혔다. 평면의 소유자는 grid 이고 BoardSpace 가 그것을 노출한다.
        // 부모 스케일 보정으로 월드 지름을 _size(타일)로 고정. 원형(바닥평면+퍼스펙티브가 화면상 타원).
        private void ApplyTransform()
        {
            if (_target == null) return;
            Vector3 p = _hasGroundAnchor ? _groundAnchor : _target.position;
            // XZ 는 unit 7 범위 밖 — 종전대로 앵커 그대로다. **Y 만** 평면에서 가져온다.
            // ⚠ `plane.normal` 로 띄우면 안 된다: grid 가 `Euler(90,0,0)` 이라 forward=(0,−1,0),
            //    즉 법선이 **아래**를 향해 블롭이 바닥 밑으로 내려간다(TilemapMapView.cs:218 ·
            //    같은 이유로 그리드 자식들은 local −Z 를 «카메라 쪽»으로 쓴다). 리프트는 월드 +Y.
            // ⚠ 이 표현식은 **평면이 수평(법선 ±Y)임을 전제**한다 — `ClosestPointOnPlane(p).y` 가
            //    «(p.x, p.z) 에서의 평면 높이» 인 것은 그때뿐이다. `BoardSpace.RaycastPlane` 은 grid
            //    회전을 따라간다고 광고하므로(XY 정면뷰도 커버) 그 계약보다 좁게 쓰는 셈이다.
            //    현재 grid 는 `TilemapMapView.cs:218` 이 `Euler(90,0,0)` 으로 무조건 세운다.
            //    보드를 다시 세우는 날 여기가 조용히 «대상 자신의 높이» 로 퇴화한다.
            // 맵 미빌드 하네스(IngameCharacterTest 등)에는 평면이 없다 → 대상 자신의 높이를 쓴다.
            // ⚠ 이 폴백은 «오브젝트별» 높이다(옛 절대 상수는 «공유 지면선» 이었다). 한 캐릭터의
            //    여러 파츠에 블롭을 붙이는 하네스에서는 블롭이 파츠 높이로 흩어진다.
            float groundY = Wassup.Core.BoardSpace.IsConfigured
                ? Wassup.Core.BoardSpace.RaycastPlane().ClosestPointOnPlane(p).y
                : p.y;
            transform.position = new Vector3(p.x, groundY + _lift, p.z);
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            // 부모 스케일 보정이 여기 있는 덕에, 유닛이 lift 로 커져도 그림자 월드 지름은 안 딸려
            // 올라간다(비행 반응은 아래 _flightScale 만으로 온다) — flight-lift-feel unit 1.
            // ⚠ 리뷰 L-3(2026-09-04) — **로컬 Y 를 나눌 값은 부모의 Z 다.** 이 쿼드는
            // `Euler(90,0,0)` 이라 로컬 +Y 가 월드 +Z 로 간다(로컬 X 만 월드 X 그대로).
            // 부모 스케일이 균일한 동안은 par.y 로 나눠도 동치라 오래 숨어 있었는데,
            // 비균일 소유자가 하나 있다 — 착지 스쿼시(`SpineUnitView._squash` =
            // (1+ak, 1−ak, 1+ak)). 거기서 그림자가 Z 로 (1+ak)/(1−ak) 배(궁극기 a=0.14 → 1.33배)
            // 늘어나 원이 아니게 됐다 = 그 프레임의 「그림자가 링에 닿으면」이 거짓.
            Vector3 par = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            float sx = Mathf.Approximately(par.x, 0f) ? 1f : par.x;
            float sz = Mathf.Approximately(par.z, 0f) ? 1f : par.z;
            float size = _size * _flightScale;
            transform.localScale = new Vector3(size / sx, size / sz, 1f);
        }
    }
}
