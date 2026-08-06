using System;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Sim
{
    /// <summary>
    /// battle-sim-extraction unit 18-K/5 — **조립 지점.** 한 매치의 sim 전체가 여기서 만들어진다.
    ///
    /// ## 왜 타입이 필요한가
    ///
    /// 지금까지 8 클러스터를 모아 파이프라인을 짓는 코드는 **테스트 안에만** 있었다
    /// (`SimThresholdAndPeriodicTests.EveryCaptureNumberIsRegisteredExactlyOnce`).
    /// 그림자를 무장하려면 프로덕션도 같은 조립을 해야 하는데, 조립이 두 벌이 되는 순간
    /// **A/B 가 서로 다른 파이프라인을 비교**하게 된다 — 그리고 그 차이는 골든이 갈릴 때까지
    /// 보이지 않는다. ⇒ 조립은 한 곳이고, 테스트도 이것을 쓴다.
    ///
    /// ## 클러스터 8 개 — `CcDotCluster` 를 빠뜨리기 쉽다
    ///
    /// ⚠ `CcDotCluster` 는 자기 파일이 없고 `ModifierCluster.cs` **안에** 산다. 전수 등록
    /// 단정의 초판이 실제로 이것을 빠뜨려 40/44 로 통과할 뻔했다. 여기 목록이 8 인지 세는 것보다
    /// `{1..44}` 전수 단정이 믿을 만한 이유다.
    ///
    /// ## P0/P13 은 클러스터가 소유하지 않는다
    ///
    /// 캡처 번호 1~44 는 P1~P12 이고 전부 클러스터 몫이다. P0(커맨드 반입·프레임 준비)과
    /// P13(post-sim 드레인·스탬프)은 **호스트가 무엇을 하느냐**에 달렸으므로 조립 지점이
    /// <see cref="RegisterHostStep"/> 로 받는다. 그 외 phase 는 거절한다 — 클러스터가 정한
    /// 순서에 호스트가 끼어들면 캡처 표가 정본이 아니게 된다.
    ///
    /// ## ⚠ 이 타입을 프로덕션이 참조하는 순간 그림자가 무장된다
    ///
    /// I2 검출기(`SimShadowIsolationTests`)가 **이 타입 이름을 감시한다.** 네임스페이스가
    /// `Wassup.Sim`(Core)이라 맥락 스캔만으로는 안 걸리기 때문이다 — 조립 지점은 정의상
    /// 모든 맥락을 끌어오므로, 이름을 명시적으로 감시 목록에 넣는 것이 유일한 집행 수단이다.
    /// </summary>
    public sealed class SimRuntime
    {
        public SimWorld World { get; }
        public SimChannels Channels { get; }

        private readonly SimPipeline _pipeline;
        private readonly SimTick _tick;
        private bool _started;

        public SimRuntime(SimConfig config)
        {
            World = new SimWorld(config);
            Channels = new SimChannels();

            _pipeline = new SimPipeline()
                .Add(new GimmickCluster(Channels).Steps())
                .Add(new AttackCluster(Channels).Steps())
                .Add(new ModifierCluster(Channels).Steps())
                .Add(new CcDotCluster(Channels).Steps())     // ⚠ ModifierCluster.cs 안에 산다
                .Add(new EnvironmentCluster(Channels).Steps())
                .Add(new MovementCluster(Channels).Steps())
                .Add(new DamageCluster(Channels).Steps())
                .Add(new ProjectileCluster(Channels).Steps());

            _tick = _pipeline.Build();
        }

        /// 진단·단정용. 캡처 번호 순으로 정렬되기 **전**의 신고 목록이다.
        public SimPipeline Pipeline => _pipeline;

        /// <summary>
        /// 호스트 스텝 등록 — **P0 두 조각과 P13 만** 받는다.
        ///
        /// ⚠ 첫 틱이 돈 뒤에는 거절한다. 도중에 스텝이 늘면 그 틱부터 다른 파이프라인이 되고,
        /// A/B 는 "규칙이 틀렸다" 가 아니라 **"다른 판"** 으로 갈린다 — 이 spec 에서 가장 찾기
        /// 어려운 실패 모양이다.
        /// </summary>
        public SimRuntime RegisterHostStep(SimPhase phase, Action<SimWorld> step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            if (_started)
                throw new InvalidOperationException(
                    "틱이 시작된 뒤에는 스텝을 등록할 수 없다 — 파이프라인이 판 중간에 바뀐다.");
            if (phase != SimPhase.CommandIntake && phase != SimPhase.FramePrologue
                && phase != SimPhase.PostSim)
                throw new InvalidOperationException(
                    $"{phase} 는 클러스터 몫이다. 호스트는 P0(CommandIntake·FramePrologue)과 " +
                    "P13(PostSim)에만 끼어든다 — 그 사이는 캡처 표가 정본이다.");

            _tick.Register(phase, step);
            return this;
        }

        /// 한 틱. 구 `BattleBridge.StepOneTick` 의 대응물이다(<see cref="SimTick.Run"/> 참조).
        public void StepOneTick(float deltaTime)
        {
            _started = true;
            _tick.Run(World, deltaTime);
        }

        /// <summary>
        /// 상태 해시의 원문. 아직 sim 밖에 있는 값들은 <paramref name="header"/> 로 받는다 —
        /// `battleClock`·`simEntityIdCounter` 는 sim 소유라 여기 없다(18-K/3).
        /// </summary>
        public string BuildStateCanonical(in SimLegacyTraceHeader header)
            => SimLegacyTrace.BuildStateCanonical(World, in header);
    }
}
