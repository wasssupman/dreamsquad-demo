using System.Linq;
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/4 — 사망 릴레이 이식의 오라클.
    ///
    /// `#12 LethalTimer` 와 `#35 ResignationDrop` 은 **구 sim 특성화**(`DeathRelayCharacterizationTests`)
    /// 의 어서션 복제다. 나머지 셋(#11·#36·#41)은 레거시에 EditMode 오라클이 없어 여기서 처음
    /// 계약을 고정한다.
    ///
    /// 이 파일이 지키는 큰 계약은 하나다 — **마킹과 파괴는 다른 phase 다.** 그 사이 창이 없으면
    /// 사직서도, 방어유닛 사망 이벤트도, 순찰병 전파도 전부 관측 지점을 잃는다.
    /// </summary>
    public class SimDeathRelayTests
    {
        private SimWorld _world;
        private SimChannels _channels;

        private HealthDeathSystem _healthDeath;
        private LethalTimerSystem _lethalTimer;
        private PatrolLifecycleSystem _patrol;
        private UnitLifecycleSystem _lifecycle;

        private void Build(ClockOutConfig clockOut = null)
        {
            _world = new SimWorld(new SimConfig(1u, 1u, null, clockOut));
            _channels = new SimChannels();
            _healthDeath = new HealthDeathSystem();
            _lethalTimer = new LethalTimerSystem();
            _patrol = new PatrolLifecycleSystem();
            _lifecycle = new UnitLifecycleSystem(_channels);
            _world.SetDeltaTime(0.016f);
        }

        [SetUp]
        public void SetUp() => Build();

        // ═════ #11 HealthDeath (안전망) ═══════════════════════════════════════

        [Test]
        public void HealthDeath_TagsZeroHp_RegardlessOfHowItGotThere()
        {
            var dying = _world.Create();
            _world.Set(dying, new Health { value = 0f, max = 100f });
            var alive = _world.Create();
            _world.Set(alive, new Health { value = 0.01f, max = 100f });

            _healthDeath.Run(_world);

            Assert.IsTrue(_world.Has<DeadTag>(dying), "정확히 0 도 사망이다(<= 판정)");
            Assert.IsFalse(_world.Has<DeadTag>(alive));
        }

        [Test]
        public void HealthDeath_IgnoresEntitiesWithoutHealth()
        {
            var e = _world.Create(); // Health 없음
            Assert.DoesNotThrow(() => _healthDeath.Run(_world));
            Assert.IsFalse(_world.Has<DeadTag>(e));
        }

        // ═════ #12 LethalTimer (구 sim 특성화 복제) ═══════════════════════════

        private SimEntityId Bomber(float remaining, bool alreadyDead = false)
        {
            var e = _world.Create();
            _world.Set(e, new LethalTimer { remaining = remaining });
            if (alreadyDead) _world.Set(e, new DeadTag());
            return e;
        }

        private void TickLethal(float dt)
        {
            _world.SetDeltaTime(dt);
            _lethalTimer.Run(_world);
        }

        [Test]
        public void NoLethalTimer_SelfGate_DoesNotRun()
        {
            var e = _world.Create();
            Assert.DoesNotThrow(() => TickLethal(1f));
            Assert.IsFalse(_world.Has<DeadTag>(e));
        }

        [Test]
        public void CountsDown_WithoutFiring()
        {
            var e = Bomber(1f);
            TickLethal(0.25f);
            Assert.AreEqual(0.75f, _world.Get<LethalTimer>(e).remaining, 1e-5f);
            Assert.IsFalse(_world.Has<DeadTag>(e));
        }

        [Test]
        public void OnExpiry_AddsDeadTag_AndRemovesTheTimer()
        {
            var e = Bomber(0.1f);
            TickLethal(1f);
            Assert.IsTrue(_world.Has<DeadTag>(e), "자폭도 공용 사망 경로를 탄다.");
            Assert.IsFalse(_world.Has<LethalTimer>(e), "타이머는 제거된다(재발화 방지).");
        }

        [Test]
        public void Expiry_IsAtOrBelowZero()
        {
            var e = Bomber(1f);
            TickLethal(1f); // 정확히 0
            Assert.IsTrue(_world.Has<DeadTag>(e));
        }

        [Test]
        public void AlreadyDeadUnit_IsSkipped_SoItIsNeverDoubleTagged()
        {
            var e = Bomber(0.1f, alreadyDead: true);
            TickLethal(1f);
            Assert.AreEqual(0.1f, _world.Get<LethalTimer>(e).remaining, 1e-5f,
                "이미 죽은 유닛은 건드리지 않는다(타이머도 안 줄어든다).");
        }

        // ═════ #35 ResignationDrop (구 sim 특성화 복제) ═══════════════════════

        private static ClockOutConfig Gimmick() => new ClockOutConfig(3, 2, 50f, 1, 1f, 0.2f);

        private SimEntityId DeadDefender(SimInt2 cell)
        {
            var e = _world.Create();
            _world.Set(e, new DefenderTile { cell = cell });
            _world.Set(e, new DefenderUnitTag());
            _world.Set(e, new DeadTag());
            return e;
        }

        private int ResignationCount() => _world.With<Resignation>().Count();

        [Test]
        public void NoGimmickConfig_SelfGate_DropsNothing()
        {
            DeadDefender(new SimInt2(2, 3));
            new ResignationDropSystem().Run(_world);
            Assert.AreEqual(0, ResignationCount());
        }

        [Test]
        public void DeadDefender_DropsOneResignation_AtItsTile()
        {
            Build(Gimmick());
            DeadDefender(new SimInt2(2, 3));
            new ResignationDropSystem().Run(_world);

            Assert.AreEqual(1, ResignationCount());
            var letter = _world.With<Resignation>().First();
            Assert.AreEqual(new SimInt2(2, 3), _world.Get<Resignation>(letter).cell,
                "사망 셀은 DefenderTile 에서 읽는다.");
        }

        [Test]
        public void LivingDefender_DropsNothing()
        {
            Build(Gimmick());
            var e = _world.Create();
            _world.Set(e, new DefenderTile { cell = new SimInt2(1, 1) });
            _world.Set(e, new DefenderUnitTag());
            new ResignationDropSystem().Run(_world);
            Assert.AreEqual(0, ResignationCount());
        }

        [Test]
        public void DeadNonDefender_DropsNothing()
        {
            Build(Gimmick());
            var e = _world.Create();
            _world.Set(e, new DefenderTile { cell = new SimInt2(1, 1) });
            _world.Set(e, new DeadTag()); // DefenderUnitTag 없음
            new ResignationDropSystem().Run(_world);
            Assert.AreEqual(0, ResignationCount());
        }

        [Test]
        public void EachDeadDefender_DropsExactlyOne()
        {
            Build(Gimmick());
            DeadDefender(new SimInt2(1, 1));
            DeadDefender(new SimInt2(2, 2));
            new ResignationDropSystem().Run(_world);
            Assert.AreEqual(2, ResignationCount());
        }

        [Test]
        public void ResignationDrop_NeedsTheDeathWindow_NotJustTheTag()
        {
            // ⚠ 파괴가 먼저 돌면 관측 자체가 불가능하다 — 이 순서가 계약임을 코드로 고정.
            Build(Gimmick());
            DeadDefender(new SimInt2(4, 4));

            _lifecycle.Run(_world);                       // P12 를 먼저 돌리면
            new ResignationDropSystem().Run(_world);      // P10 이 볼 게 없다

            Assert.AreEqual(0, ResignationCount(), "창이 닫힌 뒤엔 사직서가 나오지 않는다");
        }

        // ═════ #36 PatrolLifecycle (소환사 사망 3중 판정) ═════════════════════

        private SimEntityId Patrol(SimEntityId owner)
        {
            var e = _world.Create();
            _world.Set(e, new SummonedBy { owner = owner });
            return e;
        }

        [Test]
        public void Patrol_SurvivesWhileOwnerIsHealthy()
        {
            var owner = _world.Create();
            _world.Set(owner, new Health { value = 10f, max = 10f });
            var p = Patrol(owner);

            _patrol.Run(_world);

            Assert.IsFalse(_world.Has<DeadTag>(p));
        }

        [Test]
        public void Patrol_DiesOnEachOfTheThreeOwnerDeathSignals()
        {
            // ① 파괴됨 ② 같은 틱 DeadTag ③ HP <= 0 — 셋 다 독립으로 잡혀야 한다.
            var destroyedOwner = _world.Create();
            _world.Set(destroyedOwner, new Health { value = 10f, max = 10f });
            var pDestroyed = Patrol(destroyedOwner);
            _world.Destroy(destroyedOwner);

            var taggedOwner = _world.Create();
            _world.Set(taggedOwner, new Health { value = 10f, max = 10f }); // HP 는 아직 멀쩡
            _world.Set(taggedOwner, new DeadTag());
            var pTagged = Patrol(taggedOwner);

            var zeroHpOwner = _world.Create();
            _world.Set(zeroHpOwner, new Health { value = 0f, max = 10f }); // 태그는 아직 없음
            var pZeroHp = Patrol(zeroHpOwner);

            _patrol.Run(_world);

            Assert.IsTrue(_world.Has<DeadTag>(pDestroyed), "① 파괴된 소환사");
            Assert.IsTrue(_world.Has<DeadTag>(pTagged), "② 같은 틱 DeadTag — HP 만 보면 놓친다");
            Assert.IsTrue(_world.Has<DeadTag>(pZeroHp), "③ HP<=0 — 태그만 보면 놓친다");
        }

        [Test]
        public void Patrol_WithoutOwner_DiesImmediately()
        {
            // 소유자 없는 순찰병에는 SummonedBy 를 붙이지 않는 것이 저작 계약이다.
            // 붙었다면(Null owner) 살려둘 근거가 없다 — 소환사가 없는 것과 죽은 것이 같다.
            var p = Patrol(SimEntityId.Null);
            _patrol.Run(_world);
            Assert.IsTrue(_world.Has<DeadTag>(p));
        }

        [Test]
        public void Patrol_OwnerWithoutHealth_CountsAsDead()
        {
            var owner = _world.Create(); // Health 없음
            var p = Patrol(owner);
            _patrol.Run(_world);
            Assert.IsTrue(_world.Has<DeadTag>(p), "3중 판정은 Health 부재도 사망으로 읽는다");
        }

        // ═════ #41 UnitLifecycle (유일한 파괴자) ══════════════════════════════

        [Test]
        public void GoalReached_EmitsEventAndDestroys_WithoutKillReward()
        {
            var e = _world.Create();
            _world.Set(e, new PastGoalTag());
            _world.Set(e, new AttackUnitTag());

            _lifecycle.Run(_world);

            var events = _channels.GoalReached.Drain();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(e, events[0].entity);
            Assert.IsFalse(_world.Exists(e));
            Assert.AreEqual(0, _channels.EnemyKilled.Count, "유출은 점수를 남기지 않는다");
        }

        [Test]
        public void GoalReached_NonEnemy_IsNotDestroyed()
        {
            var e = _world.Create();
            _world.Set(e, new PastGoalTag()); // AttackUnitTag 없음

            _lifecycle.Run(_world);

            Assert.IsTrue(_world.Exists(e));
            Assert.AreEqual(0, _channels.GoalReached.Count);
        }

        [Test]
        public void DefenderDeath_BakesTileAndOnDeathAoe_BeforeDestroying()
        {
            var d = DeadDefender(new SimInt2(5, 6));
            var slots = _world.AddBuffer<DcTriggerSlot>(d);
            slots.Add(new DcTriggerSlot { trigger = DcTriggerKind.OnKill, payload = DcPayloadKind.SelfTileAoe, patternIndex = -1 });
            slots.Add(new DcTriggerSlot
            {
                trigger = DcTriggerKind.OnDeath, payload = DcPayloadKind.SelfTileAoe,
                magnitude = 33f, tileRange = 2, projectileDataIndex = 4, patternIndex = -1,
            });
            slots.Add(new DcTriggerSlot
            {
                trigger = DcTriggerKind.OnDeath, payload = DcPayloadKind.SelfTileAoe,
                magnitude = 999f, patternIndex = -1,
            });

            _lifecycle.Run(_world);

            var events = _channels.DefenderDeath.Drain();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(new SimInt2(5, 6), events[0].cell);
            Assert.IsTrue(events[0].hasOnDeathAoe);
            Assert.AreEqual(33f, events[0].aoeDamage, 1e-4f, "첫 OnDeath 슬롯만 (v1)");
            Assert.AreEqual(2, events[0].aoeTileRange);
            Assert.AreEqual(4, events[0].aoeDataIndex);
            Assert.IsFalse(_world.Exists(d), "이벤트를 낸 뒤 파괴한다");
        }

        [Test]
        public void DefenderDeath_WithoutOnDeathSlot_ReportsNoExplosion()
        {
            var d = DeadDefender(new SimInt2(1, 1));

            _lifecycle.Run(_world);

            var evt = _channels.DefenderDeath.Drain()[0];
            Assert.IsFalse(evt.hasOnDeathAoe);
            Assert.AreEqual(0f, evt.aoeDamage);
        }

        [Test]
        public void HazardDestroyed_BakesPositionAndCell_BeforeDestroying()
        {
            var h = _world.Create();
            _world.Set(h, new DeadTag());
            _world.Set(h, new BlockingHazard { hazardSoIndex = 7, maxHp = 50f });
            _world.Set(h, new Obstacle { cell = new SimInt2(3, 4) });
            _world.Set(h, SimTransform.FromPosition(new SimVec3(1f, 0f, 2f)));

            _lifecycle.Run(_world);

            var events = _channels.HazardDestroyed.Drain();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(7, events[0].hazardSoIndex);
            Assert.AreEqual(new SimInt2(3, 4), events[0].centerCell);
            Assert.AreEqual(new SimVec3(1f, 0f, 2f), events[0].worldPosition);
            Assert.IsFalse(_world.Exists(h));
        }

        [Test]
        public void GeneralDeadLoop_DestroysEnemies_ButNotTwice()
        {
            var enemy = _world.Create();
            _world.Set(enemy, new DeadTag());
            _world.Set(enemy, new AttackUnitTag());
            var defender = DeadDefender(new SimInt2(0, 0));

            _lifecycle.Run(_world);

            Assert.IsFalse(_world.Exists(enemy));
            Assert.IsFalse(_world.Exists(defender));
            Assert.AreEqual(1, _channels.DefenderDeath.Count, "방어유닛은 전용 루프에서 한 번만");
        }

        [Test]
        public void DeadDefenderWithoutTile_FallsIntoTheGeneralLoop()
        {
            // 순찰병이 정확히 이 모양이다 — DefenderTile 이 없어서 일반 루프로 떨어진다.
            var patrol = _world.Create();
            _world.Set(patrol, new DeadTag());
            _world.Set(patrol, new DefenderUnitTag());

            _lifecycle.Run(_world);

            Assert.IsFalse(_world.Exists(patrol));
            Assert.AreEqual(0, _channels.DefenderDeath.Count, "타일이 없으면 사망 이벤트도 없다");
        }

        [Test]
        public void TiledNonDefender_IsNeverDestroyed_PreservedLegacyHole()
        {
            // ⚠ 구 sim 의 실제 동작이다: DefenderTile 은 있는데 DefenderUnitTag 가 없는 죽은
            // 엔티티는 전용 루프(태그 필요)도, 일반 루프(타일 제외)도 잡지 않는다.
            // 정상 스폰은 항상 쌍으로 붙이므로 실전에 없다. **고치면 골든이 갈린다.**
            var orphan = _world.Create();
            _world.Set(orphan, new DeadTag());
            _world.Set(orphan, new DefenderTile { cell = new SimInt2(9, 9) });

            _lifecycle.Run(_world);

            Assert.IsTrue(_world.Exists(orphan), "재현 대상인 구멍 — 메우지 말 것");
        }

        [Test]
        public void HazardWithoutObstacle_IsNeverDestroyed_PreservedLegacyHole()
        {
            var orphan = _world.Create();
            _world.Set(orphan, new DeadTag());
            _world.Set(orphan, new BlockingHazard { hazardSoIndex = 1 });
            // Obstacle / SimTransform 없음

            _lifecycle.Run(_world);

            Assert.IsTrue(_world.Exists(orphan), "해저드 쪽 같은 구멍 — 대칭으로 재현된다");
            Assert.AreEqual(0, _channels.HazardDestroyed.Count);
        }

        // ═════ 릴레이 전체 (마킹 → 창 → 파괴) ════════════════════════════════

        [Test]
        public void FullRelay_OwnerDeath_PropagatesToPatrol_WithinOneTick()
        {
            Build(Gimmick());
            var owner = _world.Create();
            _world.Set(owner, new Health { value = 0f, max = 100f });
            _world.Set(owner, new DefenderUnitTag());
            _world.Set(owner, new DefenderTile { cell = new SimInt2(2, 2) });
            var patrol = Patrol(owner);

            // P3 마킹 → P10 창(사직서·순찰병 전파) → P12 파괴
            _healthDeath.Run(_world);
            Assert.IsTrue(_world.Has<DeadTag>(owner), "P3 이 안전망으로 마킹");
            Assert.IsTrue(_world.Exists(owner), "아직 파괴되지 않았다");

            new ResignationDropSystem().Run(_world);
            _patrol.Run(_world);
            Assert.AreEqual(1, ResignationCount(), "창에서 사직서가 떨어진다");
            Assert.IsTrue(_world.Has<DeadTag>(patrol), "같은 틱에 순찰병까지 전파");

            _lifecycle.Run(_world);
            Assert.IsFalse(_world.Exists(owner));
            Assert.IsFalse(_world.Exists(patrol), "순찰병도 같은 틱에 사라진다");
            Assert.AreEqual(1, _channels.DefenderDeath.Count);
        }

        [Test]
        public void Lifecycle_IsTheOnlyDestroyer_MarkersLeaveEntitiesAlive()
        {
            var bomber = Bomber(0.01f);
            _world.Set(bomber, new Health { value = 5f, max = 5f });

            TickLethal(1f);
            _healthDeath.Run(_world);

            Assert.IsTrue(_world.Has<DeadTag>(bomber));
            Assert.IsTrue(_world.Exists(bomber), "마킹 시스템은 파괴하지 않는다");

            _lifecycle.Run(_world);
            Assert.IsFalse(_world.Exists(bomber));
        }
    }
}
