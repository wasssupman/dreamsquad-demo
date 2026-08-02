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
        // ── 기하 4종: **드롭 하마(D&D)와 값이 동일하다** (사용자 지시 2026-07-29).
        //    `DragSwaySettings` ⑩ 의 dropRecoilDip / dropArcHeightFactor / dropArcMinHeight /
        //    dropLaunchControl / dropLandingHeight 와 1:1 로 맞춘다. 같은 함수를 쓰는데 값까지
        //    같으면 두 연출이 한 몸짓으로 읽히고, 눈으로 튜닝된 쪽이 기준선이 된다.
        //    ⚠ SO 를 참조하지는 않는다 — `DragSwaySettings` 는 드래그 UI 의 설정이고 전투 연출이
        //    거기에 매달리면 UI 튜닝이 전투를 조용히 바꾼다. 값만 맞추고 소유는 분리한다.
        //
        //    아치 높이 3종은 함께 움직이고, **제어점 높이 semantics** 이므로 실제 apex 는 제어점
        //    높이의 약 0.4배다(drop-dismount 0_dismount_arc_math 계약).
        //
        //    ⚠ unit 7 에서 **단위 의미가 바뀌었다.** 이전에는 아치를 sim 좌표에 넣어 ToView 가
        //    세로 성분을 버렸기 때문에, 0.95/8.5/1.25 는 그 손실을 메우려 부풀린 값이었다. 이제
        //    높이가 view 공간에 그대로 적용되므로 D&D 와 같은 대역이 성립한다.
        [Tooltip("반동으로 내려앉는 거리(월드). 아치 기저축(+Y) 반대 방향. = dropRecoilDip")]
        [SerializeField] private float bossLeapRecoilDip = 0.35f;
        [Tooltip("아치 높이 = 이동거리 × 이 계수 (하한은 아래 최소 높이). = dropArcHeightFactor")]
        [SerializeField] private float bossLeapArcHeightFactor = 0.5f;
        [Tooltip("아치 제어점 높이 하한(view 공간). 짧은 도약도 확실히 뜨게 한다. = dropArcMinHeight")]
        [SerializeField] private float bossLeapArcMinHeight = 3.5f;
        [Tooltip("발사 제어점 (x=진행비율, y=아치높이배수). y 를 올리면 더 솟구친다. = dropLaunchControl")]
        [SerializeField] private Vector2 bossLeapLaunchControl = new Vector2(0.25f, 1f);
        [Tooltip("착지 제어점 높이배수. 작을수록 수직으로 내리찍는다. = dropLandingHeight")]
        [SerializeField] private float bossLeapLandingHeight = 0.25f;
        // flight-lift-feel unit 3 — 리듬·착지 반응. 드롭(`DragSwaySettings` ⑩)과 값 1:1 유지.
        // 뜬 높이의 시각 반응(확대·그림자)은 여기 없다 — 원근 보상이라 화면 전역 단일 소유
        // (`BattleBridge` 의 lift* 노브). 이 구분을 뒤집지 말 것.
        [Tooltip("비행 구간 시간 리듬. 1=현행 등속. 낮출수록 초반 급상승→정점 체공→후반 급하강. = dropHangPower")]
        [Range(0.3f, 1f)]
        [SerializeField] private float bossLeapHangPower = 0.7f;
        [Tooltip("착지 눌림 세기(0=없음). = dropLandingSquash")]
        [Range(0f, 0.4f)]
        [SerializeField] private float bossLeapLandingSquash = 0.10f;
        [Tooltip("착지 눌림 복귀 시간(초). = dropLandingSquashSeconds")]
        [Range(0.02f, 0.3f)]
        [SerializeField] private float bossLeapLandingSquashSeconds = 0.05f;

        private NativeQueue<BossLeapVisualEvent> _bossLeapVisualQueue;

        // 비행 중 뷰 좌표. SyncMonoUnitViews 적 피드가 sim 좌표 대신 이 값을 쓴다.
        // **키의 존재 자체가 "비행 중" 이다** — 별도 in-flight 집합을 두지 않는다. 같은 생명주기를
        // 두 컬렉션이 나눠 들면 진입/이탈이 쌍으로 유지돼야 하고 한쪽만 잊히는 자리가 생긴다.
        // 이 단일 진실 덕에 취소도 공짜다: 키를 지우면 코루틴이 다음 프레임에 자진 종료한다
        // (drop-dismount 의 FinishDismountsInstant 와 같은 계약).
        // 값은 **(보드 평면 sim 좌표, view 공간 아치 높이)** 두 축으로 분리해서 든다 —
        // BoardSpace.ToView 가 sim-Y 를 버리므로 높이를 sim 좌표에 섞으면 화면에서 평면화되고
        // camUp 의 보드 평면 성분만 살아 "뜨는" 대신 옆으로 미끄러진다(unit 7 에서 고친 것).
        // 수평은 sim 으로 흘려 ToView 가 셀 정합을 잡고, 높이는 뷰가 변환 뒤에 더한다.
        private readonly Dictionary<Entity, (Unity.Mathematics.float3 simPos, float viewHeight)>
            _enemyViewOverride = new();

        internal bool TryGetEnemyViewOverride(
            Entity entity, out Unity.Mathematics.float3 simPos, out float viewHeight)
        {
            if (_enemyViewOverride.TryGetValue(entity, out var v))
            {
                simPos = v.simPos; viewHeight = v.viewHeight; return true;
            }
            simPos = default; viewHeight = 0f; return false;
        }

        // ── lifecycle 3점 세트 (공유 파일에서 호출) ──

        private void CreateBossLeapChannel()
        {
            if (_bossLeapVisualQueue.IsCreated) _bossLeapVisualQueue.Dispose();
            _bossLeapVisualQueue = new NativeQueue<BossLeapVisualEvent>(Allocator.Persistent);
            var singleton = _em.CreateEntity();
            _em.AddComponentData(singleton, new BossLeapVisualEventsSingleton { queue = _bossLeapVisualQueue });
        }

        // 매치 teardown / 컴포넌트 종료. 오버라이드를 비우면 진행 중 코루틴이 다음 프레임에
        // 자진 종료한다(키 부재 가드) — 공중에 뷰가 멈춘 채 남지 않는다. 큐 파괴와 한 함수에
        // 두어 "큐는 버렸는데 오버라이드는 남는" 조합을 구조적으로 배제한다.
        private void DisposeBossLeapChannel()
        {
            if (_bossLeapVisualQueue.IsCreated) _bossLeapVisualQueue.Dispose();
            _enemyViewOverride.Clear();
        }

        // ── 드레인 ──

        private void DrainBossLeapVisualEvents()
        {
            if (!_bossLeapVisualQueue.IsCreated) return;
            while (_bossLeapVisualQueue.TryDequeue(out var evt))
            {
                if (evt.entity == Entity.Null || !_em.Exists(evt.entity)) continue;
                // 키가 있으면 이미 비행 중 — 무시(경계 동시 관통 방어).
                if (_enemyViewOverride.ContainsKey(evt.entity)) continue;
                // **첫 프레임 오버라이드는 여기서** 걸어야 한다. 걸기 전에 sim 좌표가 한 프레임이라도
                // 소비되면 착지점으로 순간이동한 뒤 되돌아오는 팝이 보인다(rev 3 실측 증상).
                _enemyViewOverride[evt.entity] = (evt.fromWorld, 0f);
                // leap-flight-state unit 1 — 비행 창 시작. 창의 길이(뷰 시계 0.83s)를 아는 것은
                // 비행을 구동하는 브리지뿐이라 여기가 소유다(PendingDeployment 를
                // TryBeginDefenderDeployment/ActivateDeployedDefender 가 여닫는 선례).
                // 오버라이드 등록과 같은 지점 — "뷰는 나는데 심은 싸우는" 조합을 구조적으로 배제한다.
                if (HasLiveEntityManager() && !_em.HasComponent<LeapFlight>(evt.entity))
                    _em.AddComponent<LeapFlight>(evt.entity);
                StartCoroutine(RunBossLeap(evt));
            }
        }

        // ── 비행 ──

        private IEnumerator RunBossLeap(BossLeapVisualEvent evt)
        {
            var start = new Vector3(evt.fromWorld.x, evt.fromWorld.y, evt.fromWorld.z);
            var end = new Vector3(evt.toWorld.x, evt.toWorld.y, evt.toWorld.z);
            // **아치의 기저축은 카메라가 아니라 순수 +Y 다.** 앵커의 y 를 0 으로 눕히고 up 축으로
            // 궤적을 풀면 반환점의 xz 는 보드 평면 수평 경로, y 는 **순수 아치 높이**로 분리된다.
            // camUp 을 쓰면 그 성분이 sim 좌표에 섞이고 ToView 가 y 를 버려 평면화된다(unit 7).
            // 그래서 카메라 참조가 아예 필요 없다 — 조기 이탈 분기도 사라졌다.
            var flatStart = new Vector3(start.x, 0f, start.z);
            var flatEnd = new Vector3(end.x, 0f, end.z);
            float duration = Mathf.Max(0.05f, bossLeapTotalSeconds);
            float recoilFrac = Mathf.Clamp(bossLeapRecoilSeconds / duration, 1e-4f, 0.9f);

            // 출발 퍼프는 재생하지 않는다 (사용자 확정 2026-07-29). 도약 연출이
            // EarthSlamSpikes(바닥에서 솟는 스파이크)라 **착지에만** 어울린다 — 발이 뜨는
            // 자리에서 스파이크가 솟으면 인과가 거꾸로 읽힌다. 출발 전용 연출이 생기면
            // 그때 별도 dataIndex 로 되살린다(현 arm 은 단일 인덱스).
            // 오버라이드 최초 기입은 드레인이 소유한다(같은 프레임 LateUpdate 피드가 소비).
            bool abandoned = false;
            float t = 0f;
            while (t < duration)
            {
                // 취소·소멸·사망 → 즉시 종료. **오버라이드 키 부재도 취소 신호다** —
                // teardown 이 `_enemyViewOverride.Clear()` 로 비행을 끊는다.
                if (!_enemyViewOverride.ContainsKey(evt.entity)
                    || !_em.Exists(evt.entity)
                    || _em.HasComponent<Wassup.Battle.Units.DeadTag>(evt.entity))
                {
                    abandoned = true;
                    break;
                }

                // 배틀 도메인 델타 — 손패 슬로모(0.3x) 중에는 도약도 같이 느려져야 시뮬과 어긋나지
                // 않는다. 하마(드롭)가 unscaled 를 쓴 것은 UI 조작이라서이고, 여기는 전투 사건이다.
                t += TimeManager.Instance.DeltaTime(TimeDomain.Battle);

                // 시간 이징 없음(선형) — drop-dismount 가 구현 중 확정한 계약. Out* 이징은 끝속도를
                // 0 으로 죽여 내리찍는 임팩트가 물러진다. 착지 속도는 기하(끝접선)가 만든다.
                // flight-lift-feel unit 3 — ease-out-in 재매핑은 끝속도를 **키우므로** 그 계약과
                // 충돌하지 않는다. 비행 구간만 재분배하고 총 시간은 그대로다.
                float raw = Mathf.Clamp01(t / duration);
                float t01 = raw <= recoilFrac
                    ? raw
                    : recoilFrac + (1f - recoilFrac) * Wassup.UI.KeyringSim.FlightTimeRemap(
                          (raw - recoilFrac) / (1f - recoilFrac), bossLeapHangPower);
                Vector3 p = Wassup.UI.KeyringSim.DismountPoint(
                    flatStart, Vector3.zero, flatEnd, Vector3.up,
                    recoilFrac, bossLeapRecoilDip,
                    bossLeapArcHeightFactor, bossLeapArcMinHeight,
                    bossLeapLaunchControl, bossLeapLandingHeight,
                    t01);
                // 수평(xz)은 sim 으로, 높이(y)는 view 공간 오프셋으로 각자 흐른다.
                // sim y 는 지면 높이라 양 끝을 그대로 보간한다(아치와 무관).
                float groundY = Mathf.Lerp(evt.fromWorld.y, evt.toWorld.y, t01);
                _enemyViewOverride[evt.entity] =
                    (new Unity.Mathematics.float3(p.x, groundY, p.z), p.y);
                yield return null;
            }

            _enemyViewOverride.Remove(evt.entity);
            // leap-flight-state unit 1 — 비행 창 종료. 정상 착지·abandon **양쪽 모두** 해제한다:
            // 사망은 DeadTag 로 행동이 이미 끝나 있지만 시체에 태그를 남기지 않는 위생이 목적이고,
            // teardown 은 월드가 곧 해체되지만 남겨서 득이 없다. 오버라이드 제거와 co-locate 해
            // "뷰는 돌아왔는데 심은 잠긴" 조합을 구조적으로 배제한다.
            // ⚠ `HasLiveEntityManager` 가 먼저다 — abandon 경로는 teardown 을 포함하고, 루프는
            // 오버라이드 키 부재를 보고 `_em` 을 **건드리기 전에** 빠져나온다(단락 순서가 계약).
            // 여기서 무방비로 `_em` 을 만지면 해체된 월드에 접근하게 된다.
            // ⚠ **소유권 가드**: 같은 태그를 브리지(이 창)와 sim(궁극기 시퀀스)이 각자 붙였다 뗀다.
            // add 는 idempotent 라 무해하지만 remove 는 아니다 — 일반 도약 비행(피격 가능) 중에
            // 체력이 궁극기 경계를 넘으면 궁극기 창이 열리는데, 곧 끝나는 이 코루틴이 **남의
            // 잠금을 걷어간다**(무적인 채로 공격·이동하는 보스가 된다). 궁극기가 소유 중이면 둔다.
            if (HasLiveEntityManager() && _em.Exists(evt.entity)
                && _em.HasComponent<LeapFlight>(evt.entity)
                && !_em.HasComponent<UltimateLeapState>(evt.entity))
                _em.RemoveComponent<LeapFlight>(evt.entity);
            // **abandon 이면 착지 처리를 하지 않는다.** 매치 teardown·보스 사망으로 끊긴 비행에
            // 슬램을 발사하면 이미 해체된 월드에 투사체 요청을 내게 된다(브리지는 매치 간 생존).
            if (!abandoned) ResolveLanding(evt, end);
        }

        // 착지 처리. 슬램이 있으면 TileAoe 피해 요청을 스폰하고(그 요청의 히트 이벤트가 VFX 도
        // 그린다), 없으면 퍼프만 재생한다. 둘을 동시에 하면 같은 VFX 가 두 번 겹친다.
        private void ResolveLanding(BossLeapVisualEvent evt, Vector3 end)
        {
            // flight-lift-feel unit 3 — 착지 눌림. abandon 이면 이 함수 자체가 안 불리므로
            // 끊긴 비행에 스쿼시가 터지지 않는다(슬램과 같은 가드를 공유).
            PlayLandingSquash(evt.entity, bossLeapLandingSquash, bossLeapLandingSquashSeconds);

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
