using Spine.Unity;
using UnityEngine;

namespace Wassup.Presentation
{
    // spine-weapon-trail unit 3 — 무기 궤적 리그의 자립 컴포넌트.
    //
    // 궤적을 "어떤 유닛·구조물에도 붙는 기능"으로 만드는 지점이다. 호스트는 이 두 메서드만
    // 알면 된다 — Bind 로 스켈레톤을 물리고, 공격 사건마다 Play 를 부른다. 부착 대상이
    // 무엇인지(디펜더/적/보스/구조물), 어떤 본을 따라가는지, 어떤 룩인지는 전부 리그
    // 프리팹 authoring 이 소유한다.
    //
    // ⚠ Bind(null) 은 오류가 아니라 **구조물 경로**다. 본이 없는 호스트는 BoneFollower 가
    // 빠진 리그 Variant 를 쓰고, Point A/B 가 부모 트랜스폼을 그대로 따라간다.
    // HS_SwordMeshTrail 은 두 Transform 만 보므로 스켈레톤 유무를 알지 못한다.
    [DisallowMultipleComponent]
    public class WeaponTrailRig : MonoBehaviour
    {
        private Hovl.HS_SwordMeshTrail _trail;
        private BoneFollower _follower;
        private float _stopAt;
        private bool _stopPending;

        private void Awake()
        {
            _trail = GetComponent<Hovl.HS_SwordMeshTrail>();
            _follower = GetComponent<BoneFollower>();
            // 컴포넌트 순서상 HS_SwordMeshTrail.Awake 가 먼저 돌아 프리셋의 pointA 이펙트가
            // 이미 자식으로 붙어 있다. 여기서 정렬 대역을 못 박는다.
            ApplyEffectSorting();
        }

        // 궤적 파티클은 리본과 **같은 대역**에 있어야 한다. 벤더 프리팹은 sortingOrder 0 으로
        // 들어오고, 호스트가 자식 렌더러를 일괄 정렬하면 유닛 대역으로 끌려간다.
        // 리본(WeaponTrailOrder) 바로 위에 세워 파티클이 리본에 묻히지 않게 한다.
        // (호스트 쪽 스윕 제외는 SpineUnitView.UpdateSortingOrder 가 담당 — 둘이 한 쌍이다.)
        private void ApplyEffectSorting()
        {
            var fx = GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < fx.Length; i++)
                fx[i].sortingOrder = BoardSortOrder.WeaponTrailOrder + 1;
        }

        // 스켈레톤 주입. 프리팹의 initializeOnAwake 는 false 라 여기서 직접 Initialize 해야
        // 본이 잡힌다(unit 0 계약). 스켈레톤 Initialize 이후에 불려야 한다.
        public void Bind(SkeletonRenderer skeleton)
        {
            if (_follower == null || skeleton == null) return;
            _follower.skeletonRenderer = skeleton;
            _follower.Initialize();
        }

        // 방출 시작 + seconds 뒤 자동 정지. 연속 호출은 정지 시각을 뒤로 밀 뿐
        // 코루틴을 겹쳐 만들지 않는다.
        public void Play(float seconds)
        {
            if (_trail == null || seconds <= 0f) return;
            _trail.StartTrail();
            // 시간 기준은 Time.time — 궤적 자체가 그 위에서 돈다(README 계약 7).
            _stopAt = Time.time + seconds;
            if (!_stopPending) StartCoroutine(StopRoutine());
        }

        public void StopNow()
        {
            if (_trail != null) _trail.StopTrail();
            _stopAt = Time.time;
        }

        private System.Collections.IEnumerator StopRoutine()
        {
            _stopPending = true;
            while (_trail != null && Time.time < _stopAt) yield return null;
            if (_trail != null) _trail.StopTrail();
            _stopPending = false;
        }
    }
}
