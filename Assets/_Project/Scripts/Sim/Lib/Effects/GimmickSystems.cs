using System.Collections.Generic;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-J/1 — 사직서 임계 → 메테오 barrage 요청. 구
    /// `MeteorBarrageRequest` 이식. sim 은 **"몇 발"** 만 정하고 실제 캐스트는 소비 지점(18-K)이 한다.
    /// </summary>
    public struct MeteorBarrageRequest
    {
        public int meteorCount;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-J/1 — 캡처 **#20** · <see cref="SimPhase.PostMoveCast"/>(P5).
    /// 구 `ResignationThresholdSystem` 이식.
    ///
    /// 살아 있는 사직서가 임계에 도달하면 **그 수만큼 소모**하고 barrage 를 요청한다
    /// (번아웃 Consume 선례 — 임계 소모 후 재누적).
    ///
    /// ⚠ 게이트는 `SimConfig.ClockOut == null`(기믹 비활성)이다 — 구
    /// `RequireForUpdate&lt;ClockOutGimmickConfig&gt;`(분류 B)가 저작면으로 이사한 자리다.
    /// 채널 존재 게이트(`MeteorBarrageRequestsSingleton`, 분류 A)는 **증발**한다.
    ///
    /// ⚠ `threshold &lt;= 0` 은 **저작 오류 방어**다 — 0 이면 매 프레임 무한 트리거가 된다.
    /// </summary>
    public sealed class ResignationThresholdSystem
    {
        private readonly SimChannels _channels;
        private readonly List<SimEntityId> _resignations = new List<SimEntityId>();

        public ResignationThresholdSystem(SimChannels channels) => _channels = channels;

        public void Run(SimWorld world)
        {
            var config = world.Config.ClockOut;
            if (config == null) return;

            int threshold = config.ResignationThreshold;
            if (threshold <= 0) return;

            _resignations.Clear();
            foreach (SimEntityId e in world.With<Resignation>()) _resignations.Add(e);
            if (_resignations.Count < threshold) return;

            // 임계마다 barrage 1건. 한 프레임에 여러 임계를 넘겼으면(다중 퇴근) 그 배수만큼.
            int barrages = _resignations.Count / threshold;
            int toDestroy = barrages * threshold;

            // ⚠ 파괴 순서가 **순회 순서**(= 생성 순서)다. 구 sim 은 쿼리 배열 앞에서부터 소모했고,
            //   그게 "가장 오래된 사직서부터" 라는 읽힘을 준다.
            for (int i = 0; i < toDestroy; i++) world.Destroy(_resignations[i]);

            for (int b = 0; b < barrages; b++)
                _channels.MeteorBarrageRequest.Enqueue(new MeteorBarrageRequest
                {
                    meteorCount = config.MeteorCount,
                });
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-J/1 — 캡처 **#25** · <see cref="SimPhase.PostMoveCast"/>(P5).
    /// 구 `EffectTickSystem` 이식.
    ///
    /// Effects 맥락 **캐리어 엔티티**의 수명을 소유한다 — `remaining` 을 굴리고 만료하면 엔티티를
    /// 통째로 파괴한다(컴포넌트가 곧 그 효과다). Combat/Movement 는 읽기 전용 소비자로 남는다.
    ///
    /// ⚠ 파괴는 <see cref="SimWorld.Destroy"/> 를 **직접** 부른다 — 이건 수명 만료 계열이라
    /// `DeadTag` 릴레이(#41)에 참여하지 않는다(`HazardLifetime`·`ObstacleLifetime` 선례).
    ///
    /// ⚠ `AllyBuffField` 가 파괴되는 프레임에 `AllyBuffFieldSystem` 이 한 번 더 갱신할 수 있다 —
    /// 명시 순서를 얹지 말 것(수용된 지연이다).
    ///
    /// 구 `RequireAnyForUpdate`(분류 D)는 **증발한다** — 루프 밖 부수효과가 없고, 게이트 검사
    /// 자체가 루프와 같은 비용이라 이득이 없다(`HazardCastSystem` 이 게이트를 남긴 것과 다른 사정).
    /// </summary>
    public sealed class EffectTickSystem
    {
        private readonly List<SimEntityId> _expired = new List<SimEntityId>();

        public void Run(SimWorld world)
        {
            float dt = world.DeltaTime;

            // ⚠ 세 루프의 **순서가 구 sim 그대로**다(Tornado → AllyBuff → Portal).
            // 세 타입은 `remaining` 필드만 같을 뿐 공통 인터페이스를 갖지 않는다 — 구 코드에도
            // 없었고, 제네릭 제약을 위해 데이터 struct 에 인터페이스를 붙이는 것은 제약 8 위반이다.
            _expired.Clear();
            foreach (SimEntityId e in world.With<TornadoField>())
            {
                var c = world.Get<TornadoField>(e);
                c.remaining -= dt;
                world.Set(e, c);
                if (c.remaining <= 0f) _expired.Add(e);
            }
            for (int i = 0; i < _expired.Count; i++) world.Destroy(_expired[i]);

            _expired.Clear();
            foreach (SimEntityId e in world.With<AllyBuffField>())
            {
                var c = world.Get<AllyBuffField>(e);
                c.remaining -= dt;
                world.Set(e, c);
                if (c.remaining <= 0f) _expired.Add(e);
            }
            for (int i = 0; i < _expired.Count; i++) world.Destroy(_expired[i]);

            _expired.Clear();
            foreach (SimEntityId e in world.With<PortalLink>())
            {
                var c = world.Get<PortalLink>(e);
                c.remaining -= dt;
                world.Set(e, c);
                if (c.remaining <= 0f) _expired.Add(e);
            }
            for (int i = 0; i < _expired.Count; i++) world.Destroy(_expired[i]);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-J/1 — 캡처 **#24** · <see cref="SimPhase.PostMoveCast"/>(P5).
    /// 구 `HitFlashSystem` 이식.
    ///
    /// 피격 시 유닛을 잠깐 부풀렸다가 원래 스케일로 되돌린다. `Health` 는 읽지 않고 자기
    /// <see cref="SimTransform.Scale"/> 만 쓴다.
    ///
    /// ### 왜 뷰로 밀지 않고 이식하는가 (D5 살베지 판정, 2026-08-06)
    ///
    /// 청사진 P5 는 *"`Scale` 은 상태 해시의 제외 축"* 이라고 적었지만 **실제 기록기가 그렇지
    /// 않다**: `BattleBridge.LegacyTrace` 는 `LocalTransform` 을 통째로 남기고, 직렬화가
    /// **public 필드 전수 리플렉션**이라 `Scale` 이 상태 라인에 들어간다. 골든을 만드는 것은
    /// 기록기이므로 **기록기가 정본**이다.
    ///
    /// ⇒ 이 시스템은 해시에 실리는 값을 움직인다. 뷰로 밀면 A/B parity 에서 스케일이 갈린다.
    /// (`HitFlashTag` 자체는 기록되지 않는다 — 타이머는 해시 밖, 결과인 `Scale` 은 해시 안.)
    ///
    /// ⚠ 만료·저작 오류(`duration &lt;= 0`) 둘 다 **원본 스케일로 복원한 뒤** 태그를 뗀다 —
    /// 복원 없이 떼면 유닛이 부푼 채로 남는다.
    /// </summary>
    public sealed class HitFlashSystem
    {
        /// 최대 부풀림 비율. 구 sim 의 `PeakBonus` 상수 그대로.
        private const float PeakBonus = 0.2f;

        private readonly List<SimEntityId> _done = new List<SimEntityId>();

        public void Run(SimWorld world)
        {
            float dt = world.DeltaTime;
            _done.Clear();

            foreach (SimEntityId e in world.With<HitFlashTag>())
            {
                if (!world.TryGet<SimTransform>(e, out var xf)) continue;
                var flash = world.Get<HitFlashTag>(e);
                flash.remaining -= dt;
                world.Set(e, flash);

                if (flash.remaining <= 0f || flash.duration <= 0f)
                {
                    xf.Scale = flash.originalScale;
                    world.Set(e, xf);
                    _done.Add(e);
                    continue;
                }

                float t = flash.remaining / flash.duration;
                xf.Scale = flash.originalScale * (1f + PeakBonus * t);
                world.Set(e, xf);
            }

            for (int i = 0; i < _done.Count; i++) world.RemoveComponent<HitFlashTag>(_done[i]);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-J — 기믹·보스·임계·도약 클러스터.
    ///
    /// **이 클러스터는 네 phase 에 흩어진다**(P1 · P5 · P11 · P12). 앞선 클러스터들과 달리
    /// 여기서는 그 흩어짐이 "성격이 다른 것들의 모음" 이기 때문이다 — 공통점은 **남은 것들**
    /// 이라는 사실뿐이고, 그래서 `SimPipeline` 이 캡처 번호로 정렬해 주는 것이 특히 중요하다.
    ///
    /// ⚠ **#4 `BossPeriodicTrigger` 는 P1** 이라 `EnvironmentCluster` 의 phase 한가운데 끼어든다.
    /// 그 클러스터에 직접 넣으면 경계가 무너지므로, 여기서 신고하고 정렬은 파이프라인에 맡긴다.
    ///
    /// 조각별로 채워지는 중이다 — 지금 있는 것은 18-J/1~3(#20~#25 · #39 · #43).
    /// </summary>
    public sealed class GimmickCluster
    {
        public ResignationThresholdSystem ResignationThreshold { get; }
        public HeatAccrualSystem HeatAccrual { get; }
        public PickupSpawnSystem PickupSpawn { get; }
        public PickupConsumeSystem PickupConsume { get; }
        public HitFlashSystem HitFlash { get; }
        public EffectTickSystem EffectTick { get; }
        public DreamCocoonSystem DreamCocoon { get; }
        public Wassup.Sim.Combat.UltimateLeapSystem UltimateLeap { get; }

        public GimmickCluster(SimChannels channels)
        {
            ResignationThreshold = new ResignationThresholdSystem(channels);
            HeatAccrual = new HeatAccrualSystem();
            PickupSpawn = new PickupSpawnSystem();
            PickupConsume = new PickupConsumeSystem(channels);
            HitFlash = new HitFlashSystem();
            EffectTick = new EffectTickSystem();
            DreamCocoon = new DreamCocoonSystem(channels);
            UltimateLeap = new Wassup.Sim.Combat.UltimateLeapSystem(channels);
        }

        public IEnumerable<SimStep> Steps()
        {
            yield return new SimStep(20, SimPhase.PostMoveCast, nameof(ResignationThresholdSystem), ResignationThreshold.Run);
            yield return new SimStep(21, SimPhase.PostMoveCast, nameof(HeatAccrualSystem), HeatAccrual.Run);
            // ⚠ #22 → #23 순서가 계약이다 — 스폰이 놓은 픽업을 소비가 **같은 틱**에 먹는다.
            yield return new SimStep(22, SimPhase.PostMoveCast, nameof(PickupSpawnSystem), PickupSpawn.Run);
            yield return new SimStep(23, SimPhase.PostMoveCast, nameof(PickupConsumeSystem), PickupConsume.Run);
            yield return new SimStep(24, SimPhase.PostMoveCast, nameof(HitFlashSystem), HitFlash.Run);
            yield return new SimStep(25, SimPhase.PostMoveCast, nameof(EffectTickSystem), EffectTick.Run);
            // ⚠ **#39 는 #37(CcClear) 뒤 · #40(CcDecay) 앞**이어야 한다 — 그 사이가 아니면
            //    자연만료를 피격 파탄으로 오인한다. 캡처 번호가 그 자리에 있는 것이 우연이 아니다.
            yield return new SimStep(39, SimPhase.PostProcess, nameof(DreamCocoonSystem), DreamCocoon.Run);
            // ⚠ **#43 은 #44(BlinkApply) 앞** — 텔레포트 요청이 같은 틱에 적용된다.
            yield return new SimStep(43, SimPhase.Destruction, nameof(Wassup.Sim.Combat.UltimateLeapSystem), UltimateLeap.Run);
        }
    }
}
