using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-I/2 arm C/2 — 소환사 분기(#33 P8)의 오라클.
    ///
    /// 어서션은 구 `PatrolSystemIntegrationTests` §"blind 소환 순환" + "초회 게이트" 9건에서
    /// **복제**했다.
    ///
    /// 계약 셋: **① 순찰병 1기 유지**(살아 있으면 건너뛴다) **② 첫 소환만 거점 구역 게이트**
    /// **③ 게이트가 닫혀 있으면 쿨다운도 리셋하지 않는다**(적이 들어온 프레임에 즉시 반응).
    /// </summary>
    public class SimAttackSummonerTests
    {
        private SimWorld _world;
        private SimChannels _channels;
        private AttackSystem _sut;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _channels = new SimChannels();
            _sut = new AttackSystem(_channels);
            _world.SetDeltaTime(0.016f);
        }

        /// 소환사 셀 (1,0), leash 2 → 구역 x∈[-1,3].
        private SimEntityId Summoner(float cooldownRemaining, SimEntityId current,
                                     bool hasSummonedOnce = true, int patrolDataIndex = 0)
        {
            var e = _world.Create();
            _world.Set(e, SimTransform.FromPosition(new SimVec3(1f, 0f, 0f)));
            _world.Set(e, new FactionTag { value = Faction.Defender });
            _world.Set(e, new Health { value = 100f, max = 100f });
            _world.Set(e, new AttackState
            {
                range = 1f, cooldownDuration = 5f, cooldownRemaining = cooldownRemaining,
                attackTargetCount = 1, targetMask = (int)Faction.Enemy,
            });
            _world.Set(e, new SummonerState
            {
                patrolDataIndex = patrolDataIndex,
                leashTileRadius = 2,
                current = current,
                hasSummonedOnce = hasSummonedOnce,
            });
            return e;
        }

        /// 타겟 스냅샷 조건(FactionTag + Health + 위치)을 채운 적.
        private SimEntityId EnemyAt(float x)
        {
            var e = _world.Create();
            _world.Set(e, SimTransform.FromPosition(new SimVec3(x, 0f, 0f)));
            _world.Set(e, new FactionTag { value = Faction.Enemy });
            _world.Set(e, new Health { value = 50f, max = 50f });
            return e;
        }

        /// 순찰병 자리표시자 — 생존 술어가 보는 것만 갖춘다.
        private SimEntityId Patrol(float hp = 50f, bool dead = false)
        {
            var e = _world.Create();
            _world.Set(e, new Health { value = hp, max = 50f });
            if (dead) _world.Set(e, new DeadTag());
            return e;
        }

        private int CarrierCount()
        {
            int n = 0;
            foreach (var _ in _world.With<PatrolRequestCarrier>()) n++;
            return n;
        }

        private float Cooldown(SimEntityId e) => _world.Get<AttackState>(e).cooldownRemaining;

        // ── blind 소환 순환 ───────────────────────────────────────────────────

        [Test]
        public void Stages_OneRequest_WhenNoPatrolAlive()
        {
            var summoner = Summoner(cooldownRemaining: 0f, current: SimEntityId.Null);

            _sut.Run(_world);

            Assert.AreEqual(1, CarrierCount(), "게이트가 소비된 뒤엔 적 없이도 재소환한다");
            Assert.AreEqual(5f, Cooldown(summoner), 1e-4f,
                "성사 여부와 무관하게 쿨다운이 리셋돼야 재스캔 스팸이 없다");
        }

        [Test]
        public void DoesNotStage_WhilePatrolAlive()
        {
            Summoner(cooldownRemaining: 0f, current: Patrol());

            _sut.Run(_world);

            Assert.AreEqual(0, CarrierCount(), "순찰병이 살아 있으면 소환을 건너뛴다(1기 고정)");
        }

        [Test]
        public void Restages_WhenCurrentHandleIsStale()
        {
            // ⚠ `current` 가 Null 이 아닌지만 보면 파괴된 순찰병의 stale 핸들로 영구 대기한다.
            var dead = Patrol();
            Summoner(cooldownRemaining: 0f, current: dead);
            _world.Destroy(dead);

            _sut.Run(_world);

            Assert.AreEqual(1, CarrierCount(), "stale 핸들이면 재소환해야 한다");
        }

        [Test]
        public void Restages_WhenCurrentIsDeadButNotDestroyed()
        {
            Summoner(cooldownRemaining: 0f, current: Patrol(hp: 0f, dead: true));

            _sut.Run(_world);

            Assert.AreEqual(1, CarrierCount());
        }

        [Test]
        public void Waits_WhileCooldownRemains()
        {
            Summoner(cooldownRemaining: 2f, current: SimEntityId.Null);

            _sut.Run(_world);

            Assert.AreEqual(0, CarrierCount(), "쿨다운 중엔 소환하지 않는다");
        }

        // ── 초회 게이트(거점 구역 기준) ───────────────────────────────────────

        [Test]
        public void FirstSummon_WaitsUntilAnEnemyEntersTheArea()
        {
            var summoner = Summoner(cooldownRemaining: 0f, current: SimEntityId.Null, hasSummonedOnce: false);

            _sut.Run(_world);

            Assert.AreEqual(0, CarrierCount(), "적이 없으면 첫 순찰병을 내지 않는다");
            Assert.AreEqual(0f, Cooldown(summoner), 1e-4f,
                "게이트가 닫혀 있으면 쿨다운을 리셋하지 않는다 — 적이 들어온 프레임에 즉시 반응해야 한다");
        }

        [Test]
        public void FirstSummon_FiresWhenEnemyIsInsideTheArea()
        {
            // 소환사 셀 (1,0), 반경 2 → 구역 x∈[-1,3]. 적 (2,0) 은 안.
            Summoner(cooldownRemaining: 0f, current: SimEntityId.Null, hasSummonedOnce: false);
            EnemyAt(2f);

            _sut.Run(_world);

            Assert.AreEqual(1, CarrierCount(), "구역 안 적이 첫 소환을 연다");
        }

        [Test]
        public void FirstSummon_IgnoresEnemyOutsideTheArea()
        {
            // 적 (4,0) 은 소환사 (1,0) 기준 Chebyshev 3 > 반경 2 → 구역 밖.
            Summoner(cooldownRemaining: 0f, current: SimEntityId.Null, hasSummonedOnce: false);
            EnemyAt(4f);

            _sut.Run(_world);

            Assert.AreEqual(0, CarrierCount(), "구역 밖 적은 소환 사유가 아니다");
        }

        [Test]
        public void FirstSummon_IgnoresPastGoalEnemyInTheArea()
        {
            Summoner(cooldownRemaining: 0f, current: SimEntityId.Null, hasSummonedOnce: false);
            _world.Set(EnemyAt(2f), new PastGoalTag());

            _sut.Run(_world);

            Assert.AreEqual(0, CarrierCount(), "유출 대기 적은 부르는 이유가 못 된다");
        }

        [Test]
        public void Respawn_IgnoresTheGate_OnceConsumed()
        {
            // "한 번 만들면 유지" — 게이트 소비 후엔 적이 사라져도 재소환이 끊기지 않는다.
            Summoner(cooldownRemaining: 0f, current: SimEntityId.Null, hasSummonedOnce: true);

            _sut.Run(_world);

            Assert.AreEqual(1, CarrierCount());
        }

        // ── 요청 페이로드 · 소유권 ────────────────────────────────────────────

        [Test]
        public void Request_CarriesTheSummonerCell_AndAuthoring()
        {
            var summoner = Summoner(cooldownRemaining: 0f, current: SimEntityId.Null, patrolDataIndex: 3);

            _sut.Run(_world);

            PatrolSpawnRequest req = default;
            foreach (var e in _world.With<PatrolRequestCarrier>()) req = _world.Get<PatrolSpawnRequest>(e);
            Assert.AreEqual(summoner, req.owner);
            Assert.AreEqual(new SimInt2(1, 0), req.ownerCell,
                "게이트 판정과 **같은 셀** — walk 스냅은 소비 지점의 몫이다");
            Assert.AreEqual(3, req.patrolDataIndex);
            Assert.AreEqual(2, req.leashTileRadius);
        }

        [Test]
        public void SummonerState_IsNeverWrittenHere()
        {
            // ⚠ `hasSummonedOnce` 의 writer 는 **순찰병이 실제로 생성된 시점** 하나다.
            //   여기서 켜면 스냅 실패로 취소된 소환도 게이트를 소비한다.
            var summoner = Summoner(cooldownRemaining: 0f, current: SimEntityId.Null, hasSummonedOnce: false);
            EnemyAt(2f);

            _sut.Run(_world);

            Assert.AreEqual(1, CarrierCount());
            Assert.IsFalse(_world.Get<SummonerState>(summoner).hasSummonedOnce,
                "요청을 stage 했다고 게이트가 소비되지는 않는다");
        }

        [Test]
        public void Summon_IsAnAttackEvent()
        {
            var summoner = Summoner(cooldownRemaining: 0f, current: SimEntityId.Null);

            _sut.Run(_world);

            var visual = _channels.UnitAttackVisual.Drain();
            Assert.AreEqual(1, visual.Count, "소환 = 이 유닛의 공격 사건");
            Assert.AreEqual(summoner, visual[0].attacker);
            Assert.AreEqual(new SimVec3(1f, 0f, 0f), visual[0].targetWorld, "소환사 자신을 본다");
            Assert.AreEqual(5f, visual[0].attackAnimPeriod, 1e-4f);
        }

        [Test]
        public void ActionLock_BlocksSummon_ButNotTheCooldownTick()
        {
            var summoner = Summoner(cooldownRemaining: 0.5f, current: SimEntityId.Null);
            _world.AddBuffer<CcEffect>(summoner).Add(new CcEffect { kind = CcKind.Sleep, remainingTime = 5f });

            _sut.Run(_world);

            Assert.AreEqual(0, CarrierCount());
            Assert.AreEqual(0.5f - 0.016f, Cooldown(summoner), 1e-4f, "쿨다운은 흐른다");
        }

        [Test]
        public void UnwiredAuthoring_IsInert()
        {
            // `patrolDataIndex < 0` = 미배선. 소환하지 않되 **쿨다운은 리셋한다**
            // (게이트가 이미 열려 있으면) — 게이트 축과 저작 축이 다르다.
            var summoner = Summoner(cooldownRemaining: 0f, current: SimEntityId.Null, patrolDataIndex: -1);

            _sut.Run(_world);

            Assert.AreEqual(0, CarrierCount());
            Assert.AreEqual(5f, Cooldown(summoner), 1e-4f);
        }
    }
}
