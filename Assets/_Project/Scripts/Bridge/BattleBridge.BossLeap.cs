using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Wassup.Battle.Combat;
using Wassup.Battle.Combat.Projectile;
using Wassup.Core.TimeControl;

namespace Wassup.Bridge
{
    // boss-jjangssen unit 6 — 보스 도약을 순간이동에서 아치 도약으로.
    //
    // sim 은 그대로 텔레포트한다(BlinkApplySystem). 이 파일은 **뷰만** 출발지에서 착지점까지
    // defender-drop-dismount 의 궤적(KeyringSim.DismountPoint: 반동 Hermite → 비행 베지어 →
    // 수직 끝접선)으로 날린다. 신규 궤적 수학 0 — 같은 문제라 같은 함수를 쓴다.
    //
    // 이 partial 로 분리한 이유: BattleBridge.cs 는 여러 세션이 동시 편집한다. 공유 파일에는
    // 오버라이드 소비 2줄과 큐 lifecycle 호출만 남기고 상태·API·코루틴은 전부 여기 둔다.
    public partial class BattleBridge
    {
        // ── 튜닝 (오브젝트 참조가 아니라 스칼라 → 씬 배선 불필요, 코드 기본값이 곧 초기값.
        //    BossWarningView 가 같은 방식으로 전 파라미터를 SerializeField 로 둔 선례) ──
        [Header("Boss leap flight (boss-jjangssen unit 6)")]
        [Tooltip("도약 총 시간(초). 배틀 도메인 기준 — 슬로모 중엔 함께 느려진다.")]
        [SerializeField] private float bossLeapTotalSeconds = 0.83f;
        [Tooltip("웅크리는 반동 시간(초). 총 시간 중 이만큼이 반동 구간.")]
        [SerializeField] private float bossLeapRecoilSeconds = 0.14f;
        [Tooltip("반동으로 내려앉는 거리(월드). camUp 반대 방향.")]
        [SerializeField] private float bossLeapRecoilDip = 0.45f;
        [Tooltip("아치 높이 = 이동거리 × 이 계수 (하한은 아래 최소 높이).")]
        [SerializeField] private float bossLeapArcHeightFactor = 0.55f;
        [Tooltip("아치 제어점 높이 하한(월드). 짧은 도약도 확실히 뜨게 한다.")]
        [SerializeField] private float bossLeapArcMinHeight = 4.5f;
        [Tooltip("발사 제어점 (x=진행비율, y=아치높이배수).")]
        [SerializeField] private Vector2 bossLeapLaunchControl = new Vector2(0.25f, 1f);
        [Tooltip("착지 제어점 높이배수. 작을수록 수직으로 내리찍는다.")]
        [SerializeField] private float bossLeapLandingHeight = 0.22f;

        private NativeQueue<BossLeapVisualEvent> _bossLeapVisualQueue;

        // 비행 중 뷰 좌표. SyncMonoUnitViews 적 피드가 sim 좌표 대신 이 값을 쓴다.
        private readonly Dictionary<Entity, Unity.Mathematics.float3> _enemyViewOverride = new();
        // 진행 중 비행 (중복 시작 방지 + teardown 일괄 종료).
        private readonly HashSet<Entity> _bossLeapInFlight = new();

        internal bool TryGetEnemyViewOverride(Entity entity, out Unity.Mathematics.float3 pos)
            => _enemyViewOverride.TryGetValue(entity, out pos);

        // ── lifecycle 3점 세트 (공유 파일에서 호출) ──

        private void CreateBossLeapChannel()
        {
            if (_bossLeapVisualQueue.IsCreated) _bossLeapVisualQueue.Dispose();
            _bossLeapVisualQueue = new NativeQueue<BossLeapVisualEvent>(Allocator.Persistent);
            var singleton = _em.CreateEntity();
            _em.AddComponentData(singleton, new BossLeapVisualEventsSingleton { queue = _bossLeapVisualQueue });
        }

        private void DisposeBossLeapChannel()
        {
            if (_bossLeapVisualQueue.IsCreated) _bossLeapVisualQueue.Dispose();
        }

        // 매치 teardown / 컴포넌트 종료: 공중에 뷰가 멈춘 채로 남는 것을 막는다.
        private void AbortAllBossLeaps()
        {
            _bossLeapInFlight.Clear();
            _enemyViewOverride.Clear();
        }

        // ── 드레인 ──

        private void DrainBossLeapVisualEvents()
        {
            if (!_bossLeapVisualQueue.IsCreated) return;
            while (_bossLeapVisualQueue.TryDequeue(out var evt))
            {
                if (evt.entity == Entity.Null || !_em.Exists(evt.entity)) continue;
                // 같은 엔티티의 비행이 이미 돌고 있으면 무시한다(경계 동시 관통 방어).
                if (!_bossLeapInFlight.Add(evt.entity)) continue;
                StartCoroutine(RunBossLeap(evt));
            }
        }

        // ── 비행 ──

        private IEnumerator RunBossLeap(BossLeapVisualEvent evt)
        {
            var start = new Vector3(evt.fromWorld.x, evt.fromWorld.y, evt.fromWorld.z);
            var end = new Vector3(evt.toWorld.x, evt.toWorld.y, evt.toWorld.z);
            var cam = Camera.main;
            // camUp 이 없으면(카메라 부재) 아치를 만들 축이 없다 — 비행을 포기하고 sim 좌표를 쓴다.
            if (cam == null)
            {
                _bossLeapInFlight.Remove(evt.entity);
                ResolveLanding(evt, end);
                yield break;
            }
            Vector3 camUp = cam.transform.up;

            float duration = Mathf.Max(0.05f, bossLeapTotalSeconds);
            float recoilFrac = Mathf.Clamp(bossLeapRecoilSeconds / duration, 1e-4f, 0.9f);

            // 출발 퍼프는 발이 뜨는 자리에서 즉시. 착지 퍼프는 뷰가 도착한 뒤(아래).
            PlayLeapPuff(evt.dataIndex, start);

            // 첫 프레임부터 오버라이드를 걸어둔다 — 걸기 전에 한 프레임이라도 sim 좌표가
            // 소비되면 착지점으로 순간이동한 뒤 되돌아오는 팝이 보인다.
            _enemyViewOverride[evt.entity] = evt.fromWorld;

            float t = 0f;
            while (t < duration)
            {
                // 비행 중 소멸/사망 → 공중에 멈추지 않게 즉시 정리.
                if (!_em.Exists(evt.entity) || _em.HasComponent<Wassup.Battle.Units.DeadTag>(evt.entity))
                    break;

                // 배틀 도메인 델타 — 손패 슬로모(0.3x) 중에는 도약도 같이 느려져야 시뮬과 어긋나지
                // 않는다. 하마(드롭)가 unscaled 를 쓴 것은 UI 조작이라서이고, 여기는 전투 사건이다.
                var tm = TimeManager.Instance;
                t += tm != null ? tm.DeltaTime(TimeDomain.Battle) : Time.deltaTime;

                // 시간 이징 없음(선형) — drop-dismount 가 구현 중 확정한 계약. Out* 이징은 끝속도를
                // 0 으로 죽여 내리찍는 임팩트가 물러진다. 착지 속도는 기하(끝접선)가 만든다.
                Vector3 p = Wassup.UI.KeyringSim.DismountPoint(
                    start, Vector3.zero, end, camUp,
                    recoilFrac, bossLeapRecoilDip,
                    bossLeapArcHeightFactor, bossLeapArcMinHeight,
                    bossLeapLaunchControl, bossLeapLandingHeight,
                    Mathf.Clamp01(t / duration));
                _enemyViewOverride[evt.entity] = new Unity.Mathematics.float3(p.x, p.y, p.z);
                yield return null;
            }

            _enemyViewOverride.Remove(evt.entity);
            _bossLeapInFlight.Remove(evt.entity);
            // 착지 임팩트 — 뷰가 실제로 도착한 이 프레임.
            ResolveLanding(evt, end);
        }

        // 착지 처리. 슬램이 있으면 TileAoe 피해 요청을 스폰하고(그 요청의 히트 이벤트가 VFX 도
        // 그린다), 없으면 퍼프만 재생한다. 둘을 동시에 하면 같은 VFX 가 두 번 겹친다.
        private void ResolveLanding(BossLeapVisualEvent evt, Vector3 end)
        {
            if (evt.slamDamage <= 0f)
            {
                PlayLeapPuff(evt.dataIndex, end);
                return;
            }

            // shooter = Entity.Null: 보스의 AttackOutput 버퍼를 스냅샷하지 않기 위함이다.
            // 슬램은 기본공격 출력이 아니라 **고정 피해**다(메테오 barrage 와 같은 규약).
            // owner 는 요청에 직접 실어 킬 귀속만 보스로 남긴다.
            SpawnProjectile(new ProjectileSpawnRequest
            {
                movement        = MovementKind.SkyFall,
                payload         = PayloadKind.TileAoe,
                origin          = end,
                impact          = end,
                damage          = evt.slamDamage,
                impactTileRange = evt.slamTileRange,
                flightTime      = 0f,   // 즉발 — 뷰가 이미 도착했다
                arcHeight       = 0f,
                dataIndex       = evt.dataIndex,
                visualScale     = 1f,
                owner           = evt.entity,
                targetFaction   = ProjectileTargetFaction.Defender, // 보스 슬램은 방어유닛을 때린다
            }, Entity.Null);
        }

        // 퍼프 재생. DrainProjectileHitEvents 의 hitPrefab 라우팅만 재사용한다(절차 폴백·줌 펄스는
        // 착탄 전용이라 제외 — 도약은 피해 사건이 아니다).
        private void PlayLeapPuff(int dataIndex, Vector3 pos)
        {
            if (dataIndex < 0 || dataIndex >= _projectileDataByIndex.Count) return;
            var data = _projectileDataByIndex[dataIndex];
            if (data == null || data.hitPrefab == null) return;
            _projectileViewPool?.PlayHit(data.hitPrefab, pos, data.hitVfxLifetime,
                data.visualHeightOffset, data.hitVfxScale);
        }
    }
}
