using Unity.Core;
using Unity.Entities;
using Wassup.Core.TimeControl;

namespace Wassup.Battle
{
    // time-manager Unit 2 — BattleSimGroup 의 시간 진행을 BattleTimeScale singleton 으로 제어.
    //
    //   scale <= 0  → PushTime 없이 skip → 그룹 멤버 시스템 전부 미실행(완전 정지, 유휴 tick 0)
    //   scale  > 0  → 스케일된 delta 로 정확히 1회 update(슬로우모/정상)
    //
    // World.Time.DeltaTime 은 우리가 timeScale 을 항상 1 로 두므로 실프레임 델타다. 여기에
    // scale 을 곱해 push 하면 그룹 내 모든 시스템의 SystemAPI.Time.DeltaTime 이 스케일된다.
    // elapsed 도 스케일된 델타로만 누적돼 countdown 기반 쿨다운 등이 일관되게 느려진다.
    //
    // (Entities 6.4 IRateManager 계약: ShouldGroupUpdate 는 false 를 반환할 때까지 반복 호출된다.
    //  FixedRateSimpleManager 와 동일하게 PushTime/PopTime 을 짝지어 프레임당 1회만 돌린다.
    //  스케일 델타를 Timestep 프로퍼티로 라우팅하지 않는다 — 그 setter 는 ≥0.0001 로 클램프한다.)
    //
    // 알려진 한계(M2, safe today): 정식 FixedRateSimpleManager 는 push 구간에 그룹 rewindable
    // 할당자를 swap/restore 한다(World.CurrentGroupAllocators / SetGroupAllocator / RestoreGroupAllocator).
    // 이 API 는 Unity.Entities internal 이라 유저 코드에서 접근 불가 — 리플렉션 재현은 Entities 업그레이드에
    // 더 취약하다. 현재 배틀 시스템은 WorldUpdateAllocator 를 쓰지 않고 ECB 는 전부 Allocator.Temp
    // in-place playback 이라 그룹 할당자 rewind 에 의존하지 않으므로 안전하다. 배틀 시스템이 향후
    // WorldUpdateAllocator 를 채택하면 재검토(spec Follow-up).
    public sealed class BattleScaledRateManager : IRateManager
    {
        private EntityQuery _scaleQuery;
        private bool _queryReady;
        private bool _didPushTime;
        // 스케일된 델타만 누적하는 로컬 elapsed. 월드의 (스케일 안 된) ElapsedTime 을 읽으면
        // 정지 구간의 실시간이 재개 시 한 프레임에 점프한다(FixedRateSimpleManager 도 로컬 누산기 사용).
        private double _elapsedTime;

        public float Timestep { get; set; }

        public bool ShouldGroupUpdate(ComponentSystemGroup group)
        {
            if (_didPushTime)
            {
                group.World.PopTime();
                _didPushTime = false;
                return false;
            }

            float scale = ReadScale(group);
            if (scale <= 0f)
                return false; // 정지: push 안 함 → pop 도 불필요, 그룹 멤버 전부 skip.

            // battle-sim-extraction M0 unit 2 — 하네스 구동. 「얼마나」와 「언제」를 둘 다
            // 스텝이 준다: dt 는 `StepDt`, 전진 여부는 스텝 요청의 소비 결과다.
            // 플레이어 루프도 매 프레임 이 그룹을 돌리려 오는데 요청이 없으면 여기서
            // false 로 죽는다 — **그래야 스텝이 렌더 프레임과 분리**되고, 에디터 비포커스로
            // 프레임이 멎어도 하네스가 완주한다. dt 만 상수로 꽂았다면 정반대가 된다
            // (프레임당 1회 갱신이 유지돼 게임 속도가 프레임레이트에 비례).
            double rawDelta = SimHarnessClock.Active
                ? SimHarnessClock.StepDt
                : group.World.Time.DeltaTime;
            if (SimHarnessClock.Active && !SimHarnessClock.ConsumeStep())
                return false;

            double scaledDelta = rawDelta * scale;
            _elapsedTime += scaledDelta; // 스케일된 델타만 누적 → 정지 후 재개 시 elapsed 점프 없음.
            group.World.PushTime(new TimeData(
                elapsedTime: _elapsedTime,
                deltaTime: (float)scaledDelta));
            _didPushTime = true;
            return true;
        }

        private float ReadScale(ComponentSystemGroup group)
        {
            if (!_queryReady)
            {
                _scaleQuery = group.World.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<BattleTimeScale>());
                _queryReady = true;
            }
            // 미생성(TryGetSingleton false)이면 정상 속도.
            return _scaleQuery.TryGetSingleton<BattleTimeScale>(out var bts) ? bts.Value : 1f;
        }
    }
}
