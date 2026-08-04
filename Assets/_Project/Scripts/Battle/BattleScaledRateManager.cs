using Unity.Core;
using Unity.Entities;

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
        // battle-sim-extraction unit 2 — 하네스 게이트. true 인 동안 플레이어 루프의
        // 프레임 구동을 전부 차단하고, ArmStep 으로 예약된 고정 dt 스텝만 통과시킨다.
        // 기본 false — 라이브 경로/기존 테스트(BattleScaledRateManagerTests) 무변.
        private bool _harnessGate;
        private bool _stepArmed;
        private float _stepDt;
        // 스케일된 델타만 누적하는 로컬 elapsed. 월드의 (스케일 안 된) ElapsedTime 을 읽으면
        // 정지 구간의 실시간이 재개 시 한 프레임에 점프한다(FixedRateSimpleManager 도 로컬 누산기 사용).
        private double _elapsedTime;

        public float Timestep { get; set; }

        public void SetHarnessGate(bool on)
        {
            _harnessGate = on;
            _stepArmed = false;
            if (!on) _stepDt = 0f;
        }

        // 다음 ShouldGroupUpdate 1회를 고정 dt 로 통과시킨다. 호출측(Bridge.StepOneTick)이
        // arm 직후 group.Update() 를 **동기** 호출하는 것이 계약 — 플레이어 루프에 맡기면
        // 스텝 시점이 렌더 프레임에 결합돼 에디터 비포커스 정지가 재현된다.
        public void ArmStep(float fixedDt)
        {
            if (fixedDt <= 0f || float.IsNaN(fixedDt) || float.IsInfinity(fixedDt))
            {
                _stepArmed = false;
                _stepDt = 0f;
                return;
            }
            _stepArmed = true;
            _stepDt = fixedDt;
        }

        public bool ShouldGroupUpdate(ComponentSystemGroup group)
        {
            if (_didPushTime)
            {
                group.World.PopTime();
                _didPushTime = false;
                return false;
            }

            if (_harnessGate)
            {
                if (!_stepArmed) return false; // 하네스 중 플레이어 루프 구동은 전부 skip.
                _stepArmed = false;
                _elapsedTime += _stepDt;
                group.World.PushTime(new TimeData(
                    elapsedTime: _elapsedTime,
                    deltaTime: _stepDt));
                _didPushTime = true;
                return true;
            }

            float scale = ReadScale(group);
            if (scale <= 0f)
                return false; // 정지: push 안 함 → pop 도 불필요, 그룹 멤버 전부 skip.

            double scaledDelta = group.World.Time.DeltaTime * scale;
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
