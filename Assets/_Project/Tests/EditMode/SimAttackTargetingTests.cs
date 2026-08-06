using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-I/2 arm C/3 + D — 타겟팅 스캔과 START.
    ///
    /// ⚠ **RESOLVE(arm E)가 아직 없다.** 이 스위트의 관측점은 피해가 아니라
    /// `UnitAttackVisualEvent`(누가 무엇을 겨눠 START 했나) + `AttackState`(쿨다운·지연) +
    /// `FrontmostAttackLock`/`FocusTarget` 이다. 구 오라클이 피해로 보던 것을 여기서는 **조준**으로 본다.
    ///
    /// 어서션 복제 출처(구 `AttackSystemUnifiedLoopTests`): `U4`(쿨다운 부분) · `U7` · `U8` ·
    /// `SelfExclusion_Attacker_Does_Not_Target_Itself` · `DeadTag_Excludes_Target_From_Pool`.
    ///
    /// **오버라이드 사슬이 이 스위트의 주제다**: 최근접 → 최저체력 → 우선순위 → FocusUntilDead →
    /// 어그로 → 최전방 → facing 레인(최종).
    /// </summary>
    public class SimAttackTargetingTests
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

        /// 흐름장을 깐다 — 최전방 랭킹은 `dist` 를 읽으므로 이게 없으면 후보가 전부 도달 불가다.
        /// `dist[x + y*w] = w - x` 로 두어 **x 가 클수록 골에 가깝다**(작은 dist = 최전방).
        private void FlowField(int w = 16, int h = 16)
        {
            var dist = new int[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    dist[y * w + x] = w - x;
            var f = _world.Create();
            _world.Set(f, new FlowFieldSingleton
            {
                flow = new SimVec2[w * h], dist = dist,
                gridSize = new SimInt2(w, h), tileSize = 1f, origin = default,
            });
        }

        private SimEntityId Attacker(
            Faction faction, SimVec3 pos, float range, float cooldownDuration, int targetMask,
            bool defenderTag = false, bool attackerTag = false, float hitDelaySec = 0f)
        {
            var e = _world.Create();
            _world.Set(e, SimTransform.FromPosition(pos));
            _world.Set(e, new FactionTag { value = faction });
            _world.Set(e, new Health { value = 10f, max = 10f });
            _world.Set(e, new AttackState
            {
                range = range, cooldownDuration = cooldownDuration, cooldownRemaining = 0f,
                attackTargetCount = 1, targetMask = targetMask, hitDelaySec = hitDelaySec,
            });
            if (defenderTag) _world.Set(e, new DefenderUnitTag());
            if (attackerTag) _world.Set(e, new AttackUnitTag());
            return e;
        }

        private SimEntityId Target(Faction faction, SimVec3 pos, float hp = 10f, float max = 10f,
                                   bool defenderTag = false, bool attackerTag = false)
        {
            var e = _world.Create();
            _world.Set(e, SimTransform.FromPosition(pos));
            _world.Set(e, new FactionTag { value = faction });
            _world.Set(e, new Health { value = hp, max = max });
            if (defenderTag) _world.Set(e, new DefenderUnitTag());
            if (attackerTag) _world.Set(e, new AttackUnitTag());
            return e;
        }

        /// START 한 공격 하나의 시각 이벤트(없으면 실패).
        private UnitAttackVisualEvent Fired()
        {
            var q = _channels.UnitAttackVisual.Drain();
            Assert.AreEqual(1, q.Count, "정확히 한 번 START 해야 한다");
            return q[0];
        }

        private void AssertNoFire(string because)
            => Assert.AreEqual(0, _channels.UnitAttackVisual.Count, because);

        private AttackState State(SimEntityId e) => _world.Get<AttackState>(e);

        // ── 기본 통합 루프 ────────────────────────────────────────────────────

        [Test]
        public void U7_DefenderFire_EnqueuesVisualEvent()
        {
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), 10f, 1f,
                                    (int)Faction.Enemy, defenderTag: true);
            var enemy = Target(Faction.Enemy, new SimVec3(2f, 0f, 0f), attackerTag: true);

            _sut.Run(_world);

            var ev = Fired();
            Assert.AreEqual(defender, ev.attacker);
            Assert.AreEqual(enemy, ev.target);
        }

        [Test]
        public void U8_EnemyFire_EnqueuesVisualEvent_OnTheSameChannel()
        {
            var enemy = Attacker(Faction.Enemy, new SimVec3(0f, 0f, 0f), 10f, 1f,
                                 (int)(Faction.Defender | Faction.BlockingHazard), attackerTag: true);
            var defender = Target(Faction.Defender, new SimVec3(2f, 0f, 0f), defenderTag: true);

            _sut.Run(_world);

            var ev = Fired();
            Assert.AreEqual(enemy, ev.attacker, "적도 같은 채널을 쓴다(통합 공격자 루프)");
            Assert.AreEqual(defender, ev.target);
        }

        [Test]
        public void SelfExclusion_AttackerDoesNotTargetItself()
        {
            Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), 10f, 1f,
                     (int)Faction.Defender, defenderTag: true); // 가드가 없으면 자기를 때린다

            _sut.Run(_world);

            AssertNoFire("공격자는 절대 자기 자신을 고르지 않는다");
        }

        [Test]
        public void DeadOrPending_ExcludesTargetFromPool()
        {
            Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), 10f, 1f,
                     (int)Faction.Enemy, defenderTag: true);
            _world.Set(Target(Faction.Enemy, new SimVec3(1f, 0f, 0f)), new DeadTag());
            _world.Set(Target(Faction.Enemy, new SimVec3(0f, 0f, 1f)), new PendingDeployment());

            _sut.Run(_world);

            AssertNoFire("죽었거나 배치 중인 적은 후보가 아니다");
        }

        [Test]
        public void Nearest_WinsAmongCandidates()
        {
            Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), 10f, 1f,
                     (int)Faction.Enemy, defenderTag: true);
            Target(Faction.Enemy, new SimVec3(5f, 0f, 0f));
            var near = Target(Faction.Enemy, new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            Assert.AreEqual(near, Fired().target);
        }

        [Test]
        public void RangeGate_IsChebyshevTiles()
        {
            Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), 2f, 1f,
                     (int)Faction.Enemy, defenderTag: true);
            Target(Faction.Enemy, new SimVec3(3f, 0f, 0f)); // 체비셰프 3 > range 2

            _sut.Run(_world);

            AssertNoFire("사거리 밖 적은 존재하지 않는 것과 같다");
        }

        [Test]
        public void TargetMask_FiltersFaction()
        {
            Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), 10f, 1f,
                     (int)Faction.BlockingHazard, defenderTag: true);
            Target(Faction.Enemy, new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            AssertNoFire("마스크에 없는 진영은 후보가 아니다");
        }

        // ── START 의 상태 전이 ────────────────────────────────────────────────

        [Test]
        public void U4_AttackSpeedMul_HalvesTheCooldownReset()
        {
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), 10f, 2f,
                                    (int)Faction.Enemy, defenderTag: true);
            _world.Set(defender, new ModifierStats { attackSpeedMul = 2f });
            Target(Faction.Enemy, new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            Assert.AreEqual(1f, State(defender).cooldownRemaining, 1e-4f,
                "리셋 = cooldownDuration / attackSpeedMul");
        }

        [Test]
        public void AttackAnimPeriod_IsMaxOfIntervalAndHitDelay()
        {
            // ⚠ `hitDelayRemaining > 0` 동안 다음 START 가 막히므로 `hitDelaySec > interval` 이면
            //   실주기는 `hitDelaySec` 이다 — 애니가 실발사보다 먼저 끝나면 안 된다.
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), 10f, 0.5f,
                                    (int)Faction.Enemy, defenderTag: true, hitDelaySec: 1.2f);
            Target(Faction.Enemy, new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            Assert.AreEqual(1.2f, Fired().attackAnimPeriod, 1e-4f);
        }

        [Test]
        public void HitDelay_BlocksANewStartWhileTicking()
        {
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), 10f, 0.001f,
                                    (int)Faction.Enemy, defenderTag: true, hitDelaySec: 0.1f);
            Target(Faction.Enemy, new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);
            Assert.AreEqual(0.1f, State(defender).hitDelayRemaining, 1e-4f, "START 가 지연을 건다");
            _channels.UnitAttackVisual.Drain();

            _sut.Run(_world);
            AssertNoFire("지연 중엔 새 START 를 하지 않는다");
            Assert.AreEqual(0.1f - 0.016f, State(defender).hitDelayRemaining, 1e-4f, "지연은 흐른다");
        }

        [Test]
        public void DoubleFireCharge_ZeroesTheCooldown_AndIsConsumed()
        {
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), 10f, 5f,
                                    (int)Faction.Enemy, defenderTag: true);
            _world.Set(defender, new NextAttackDoubleFire { charges = 1 });
            Target(Faction.Enemy, new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            Assert.AreEqual(0f, State(defender).cooldownRemaining, 1e-4f, "즉시 한 번 더 때린다");
            Assert.IsFalse(_world.Has<NextAttackDoubleFire>(defender), "보너스는 1발뿐 — charge 소비");
        }

        [Test]
        public void DoubleFire_IsDefenderOnly()
        {
            var enemy = Attacker(Faction.Enemy, new SimVec3(0f, 0f, 0f), 10f, 5f,
                                 (int)Faction.Defender, attackerTag: true);
            _world.Set(enemy, new NextAttackDoubleFire { charges = 1 });
            Target(Faction.Defender, new SimVec3(1f, 0f, 0f), defenderTag: true);

            _sut.Run(_world);

            Assert.AreEqual(5f, State(enemy).cooldownRemaining, 1e-4f);
            Assert.IsTrue(_world.Has<NextAttackDoubleFire>(enemy), "적에게는 이 arm 이 걸리지 않는다");
        }

        [Test]
        public void EnemyAiState_GatesFire_ToEngagingOrStandoff()
        {
            var enemy = Attacker(Faction.Enemy, new SimVec3(0f, 0f, 0f), 10f, 1f,
                                 (int)Faction.Defender, attackerTag: true);
            _world.Set(enemy, new EnemyAiState { value = AiState.Marching });
            Target(Faction.Defender, new SimVec3(1f, 0f, 0f), defenderTag: true);

            _sut.Run(_world);
            AssertNoFire("Marching 중엔 쏘지 않는다");

            _world.Set(enemy, new EnemyAiState { value = AiState.Standoff });
            _sut.Run(_world);
            Assert.AreEqual(enemy, Fired().attacker, "Standoff 는 발사 허용 상태다");
        }

        [Test]
        public void DefendersAreNotSubjectToTheAiStateGate()
        {
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), 10f, 1f,
                                    (int)Faction.Enemy, defenderTag: true);
            _world.Set(defender, new EnemyAiState { value = AiState.Marching });
            Target(Faction.Enemy, new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            Assert.AreEqual(defender, Fired().attacker, "방어유닛은 상태머신 대상이 아니다");
        }

        [Test]
        public void DirectionalProjectileWithoutFacing_SnapshotsTheDirectionAtStart()
        {
            // wind-up 뒤의 재판정이 이번 발사의 기준축을 바꾸지 못하게 방향만 얼린다.
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), 10f, 1f,
                                    (int)Faction.Enemy, defenderTag: true, hitDelaySec: 1f);
            _world.Set(defender, new ProjectileRef { movement = MovementKind.DirectionalLinear });
            Target(Faction.Enemy, new SimVec3(3f, 0f, 0f));

            _sut.Run(_world);

            var st = State(defender);
            Assert.AreEqual(1, st.hasCommittedDirection);
            Assert.AreEqual(1f, st.committedDirection.x, 1e-4f, "+X 로 정규화된 방향");
            Assert.AreEqual(0f, st.committedDirection.y, 1e-4f);
        }

        // ── 오버라이드 사슬 ───────────────────────────────────────────────────

        [Test]
        public void Healer_PicksTheMostHurtAlly_NotTheNearest()
        {
            var healer = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), 10f, 1f,
                                  (int)Faction.Defender, defenderTag: true);
            Target(Faction.Defender, new SimVec3(1f, 0f, 0f), hp: 10f, max: 10f, defenderTag: true);
            var hurt = Target(Faction.Defender, new SimVec3(4f, 0f, 0f), hp: 2f, max: 10f, defenderTag: true);

            _sut.Run(_world);

            Assert.AreEqual(hurt, Fired().target, "후보 집합은 같고 랭킹만 바뀐다");
        }

        [Test]
        public void TauntedEnemy_KeepsNearestTargeting_EvenWithTheAllyMask()
        {
            // ⚠ 도발당한 적도 mask == Defender 지만 `DefenderUnitTag` 가 없어 힐러 랭킹을 타지 않는다.
            var enemy = Attacker(Faction.Enemy, new SimVec3(0f, 0f, 0f), 10f, 1f,
                                 (int)Faction.Defender, attackerTag: true);
            var near = Target(Faction.Defender, new SimVec3(1f, 0f, 0f), hp: 10f, max: 10f, defenderTag: true);
            Target(Faction.Defender, new SimVec3(4f, 0f, 0f), hp: 2f, max: 10f, defenderTag: true);

            _sut.Run(_world);

            Assert.AreEqual(near, Fired().target);
        }

        [Test]
        public void ClassFilter_ExcludesDisallowedClasses()
        {
            var enemy = Attacker(Faction.Enemy, new SimVec3(0f, 0f, 0f), 10f, 1f,
                                 (int)Faction.Defender, attackerTag: true);
            _world.Set(enemy, new EnemyTargetFilter
            {
                classMask = 1 << (int)DefenderClass.Guardian,
                priorityClass = -1,
            });
            var ranger = Target(Faction.Defender, new SimVec3(1f, 0f, 0f), defenderTag: true);
            _world.Set(ranger, new DefenderClassTag { value = DefenderClass.Ranger });
            var guardian = Target(Faction.Defender, new SimVec3(4f, 0f, 0f), defenderTag: true);
            _world.Set(guardian, new DefenderClassTag { value = DefenderClass.Guardian });

            _sut.Run(_world);

            Assert.AreEqual(guardian, Fired().target, "허용되지 않은 클래스는 더 가까워도 후보가 아니다");
        }

        [Test]
        public void PriorityClass_OverridesNearest()
        {
            var enemy = Attacker(Faction.Enemy, new SimVec3(0f, 0f, 0f), 10f, 1f,
                                 (int)Faction.Defender, attackerTag: true);
            _world.Set(enemy, new EnemyTargetFilter
            {
                classMask = -1,
                priorityClass = (int)DefenderClass.Caster,
            });
            Target(Faction.Defender, new SimVec3(1f, 0f, 0f), defenderTag: true);
            var caster = Target(Faction.Defender, new SimVec3(4f, 0f, 0f), defenderTag: true);
            _world.Set(caster, new DefenderClassTag { value = DefenderClass.Caster });

            _sut.Run(_world);

            Assert.AreEqual(caster, Fired().target);
        }

        [Test]
        public void FocusUntilDead_KeepsTheLock_AndRangeOnlyGatesFiring()
        {
            var enemy = Attacker(Faction.Enemy, new SimVec3(0f, 0f, 0f), 2f, 1f,
                                 (int)Faction.Defender, attackerTag: true);
            _world.Set(enemy, new EnemyBehavior { targetMode = EnemyTargetMode.FocusUntilDead });
            var locked = Target(Faction.Defender, new SimVec3(2f, 0f, 0f), defenderTag: true);
            _world.Set(enemy, new FocusTarget { current = locked });
            Target(Faction.Defender, new SimVec3(1f, 0f, 0f), defenderTag: true); // 더 가깝지만 무시

            _sut.Run(_world);
            Assert.AreEqual(locked, Fired().target, "잠근 대상을 유지한다");

            // 사거리 밖으로 밀려나면 발사만 멈추고 **잠금은 유지**한다.
            _world.Set(locked, SimTransform.FromPosition(new SimVec3(9f, 0f, 0f)));
            _world.Set(enemy, new AttackState
            {
                range = 2f, cooldownDuration = 1f, cooldownRemaining = 0f,
                attackTargetCount = 1, targetMask = (int)Faction.Defender,
            });
            _sut.Run(_world);

            AssertNoFire("사거리 밖 → 발사 보류");
            Assert.AreEqual(locked, _world.Get<FocusTarget>(enemy).current, "잠금은 풀리지 않는다");
        }

        [Test]
        public void FocusUntilDead_AdoptsNearest_WhenTheLockIsInvalid()
        {
            var enemy = Attacker(Faction.Enemy, new SimVec3(0f, 0f, 0f), 10f, 1f,
                                 (int)Faction.Defender, attackerTag: true);
            _world.Set(enemy, new EnemyBehavior { targetMode = EnemyTargetMode.FocusUntilDead });
            _world.Set(enemy, new FocusTarget { current = SimEntityId.Null });
            var near = Target(Faction.Defender, new SimVec3(1f, 0f, 0f), defenderTag: true);

            _sut.Run(_world);

            Assert.AreEqual(near, Fired().target);
            Assert.AreEqual(near, _world.Get<FocusTarget>(enemy).current, "무효 잠금은 최근접을 채택한다");
        }

        [Test]
        public void Aggroed_TargetsOnlyItsGuardian()
        {
            var enemy = Attacker(Faction.Enemy, new SimVec3(0f, 0f, 0f), 10f, 1f,
                                 (int)Faction.Defender, attackerTag: true);
            var guardian = Target(Faction.Defender, new SimVec3(4f, 0f, 0f), defenderTag: true);
            _world.Set(enemy, new Aggroed { guardian = guardian });
            Target(Faction.Defender, new SimVec3(1f, 0f, 0f), defenderTag: true); // 더 가깝지만 무시

            _sut.Run(_world);

            Assert.AreEqual(guardian, Fired().target, "필터/우선순위/최근접/포커스를 전부 덮는다");
        }

        [Test]
        public void Aggroed_HoldsFire_WhenTheGuardianIsOutOfRange()
        {
            var enemy = Attacker(Faction.Enemy, new SimVec3(0f, 0f, 0f), 2f, 1f,
                                 (int)Faction.Defender, attackerTag: true);
            var guardian = Target(Faction.Defender, new SimVec3(8f, 0f, 0f), defenderTag: true);
            _world.Set(enemy, new Aggroed { guardian = guardian });
            Target(Faction.Defender, new SimVec3(1f, 0f, 0f), defenderTag: true);

            _sut.Run(_world);

            AssertNoFire("앵커로 걸어가며 발사를 보류한다 — 가까운 다른 적으로 갈아타지 않는다");
        }

        // ── 최전방 ────────────────────────────────────────────────────────────

        private SimEntityId FrontmostDefender(SimVec3 pos, float damageMul = 1.2f, float hitDelaySec = 0f)
        {
            var e = Attacker(Faction.Defender, pos, 10f, 1f, (int)Faction.Enemy,
                             defenderTag: true, hitDelaySec: hitDelaySec);
            _world.Set(e, new FrontmostAttackLock());
            _world.AddBuffer<DcAttackModSlot>(e).Add(new DcAttackModSlot
            {
                instanceId = 1, kind = DcAttackModKind.FrontmostTarget, damageMul = damageMul,
            });
            return e;
        }

        [Test]
        public void Frontmost_PicksTheLowestFlowDist_AndLocksTheMultiplier()
        {
            FlowField();
            var defender = FrontmostDefender(new SimVec3(3f, 0f, 3f));
            Target(Faction.Enemy, new SimVec3(2f, 0f, 3f));           // dist 14
            var ahead = Target(Faction.Enemy, new SimVec3(5f, 0f, 3f)); // dist 11 = 더 앞

            _sut.Run(_world);

            Assert.AreEqual(ahead, Fired().target, "골에 가까울수록 최전방 — 최근접이 아니다");
            var fmLock = _world.Get<FrontmostAttackLock>(defender);
            Assert.IsTrue(fmLock.active);
            Assert.AreEqual(ahead, fmLock.target);
            Assert.AreEqual(1.2f, fmLock.damageMulSnapshot, 1e-4f, "START 에서 배율을 얼린다");
            Assert.IsTrue(fmLock.targetIsPriority, "고른 대상이 배율 수령자다");
        }

        [Test]
        public void Frontmost_FallsBackToNearest_WithoutTheBonus()
        {
            // 흐름장이 없으면 후보가 전부 도달 불가 → 최근접 폴백이되 **배율 수령자는 아니다**.
            var defender = FrontmostDefender(new SimVec3(0f, 0f, 0f));
            var near = Target(Faction.Enemy, new SimVec3(1f, 0f, 0f));

            _sut.Run(_world);

            Assert.AreEqual(near, Fired().target);
            Assert.IsFalse(_world.Get<FrontmostAttackLock>(defender).targetIsPriority,
                "폴백은 카드가 약속한 대상이 아니다(계약 3)");
        }

        [Test]
        public void Frontmost_LapsesStrictly_WhenTheLockedTargetDiesMidAttack()
        {
            FlowField();
            var defender = FrontmostDefender(new SimVec3(3f, 0f, 3f), hitDelaySec: 1f);
            var ahead = Target(Faction.Enemy, new SimVec3(5f, 0f, 3f));
            Target(Faction.Enemy, new SimVec3(2f, 0f, 3f));

            _sut.Run(_world); // START — ahead 를 잠근다
            Assert.AreEqual(ahead, _world.Get<FrontmostAttackLock>(defender).target);
            _channels.UnitAttackVisual.Drain();

            // 준비 동작 중 잠근 대상이 죽는다 → **재선택 없이 불발**(strict lapse).
            _world.Set(ahead, new DeadTag());
            _sut.Run(_world);

            AssertNoFire("사망·소멸·사거리 이탈·유출에 재선택이 없다");
        }

        [Test]
        public void Frontmost_IgnoresLeakPendingEnemies()
        {
            FlowField();
            FrontmostDefender(new SimVec3(3f, 0f, 3f));
            var leaking = Target(Faction.Enemy, new SimVec3(5f, 0f, 3f));
            _world.Set(leaking, new PastGoalTag());
            var healthy = Target(Faction.Enemy, new SimVec3(2f, 0f, 3f));

            _sut.Run(_world);

            Assert.AreEqual(healthy, Fired().target, "유출 대기 적은 최전방이 아니다");
        }

        [Test]
        public void FrontmostLockWithoutASlot_IsNotFrontmost()
        {
            // 카드 회수 뒤 잠금 컴포넌트만 남을 수 있다 — 그때는 최전방 유닛이 아니다.
            FlowField();
            var defender = Attacker(Faction.Defender, new SimVec3(3f, 0f, 3f), 10f, 1f,
                                    (int)Faction.Enemy, defenderTag: true);
            _world.Set(defender, new FrontmostAttackLock());
            var near = Target(Faction.Enemy, new SimVec3(2f, 0f, 3f));
            Target(Faction.Enemy, new SimVec3(5f, 0f, 3f));

            _sut.Run(_world);

            Assert.AreEqual(near, Fired().target, "슬롯이 없으면 최근접 그대로");
            Assert.IsFalse(_world.Get<FrontmostAttackLock>(defender).active, "잠금도 걸지 않는다");
        }

        // ── facing 레인(최종 오버라이드) ──────────────────────────────────────

        [Test]
        public void Facing_OverridesEverything_WithTheLaneWitness()
        {
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), 5f, 1f,
                                    (int)Faction.Enemy, defenderTag: true);
            _world.Set(defender, new DeployedFacing { value = new SimInt2(1, 0) });
            Target(Faction.Enemy, new SimVec3(0f, 0f, 1f));            // 더 가깝지만 레인 밖
            var inLane = Target(Faction.Enemy, new SimVec3(3f, 0f, 0f)); // 레인 안

            _sut.Run(_world);

            Assert.AreEqual(inLane, Fired().target, "레인 밖 적은 존재하지 않는 것과 같다");
        }

        [Test]
        public void Facing_HoldsFire_WhenTheLaneIsEmpty()
        {
            var defender = Attacker(Faction.Defender, new SimVec3(0f, 0f, 0f), 5f, 1f,
                                    (int)Faction.Enemy, defenderTag: true);
            _world.Set(defender, new DeployedFacing { value = new SimInt2(1, 0) });
            Target(Faction.Enemy, new SimVec3(0f, 0f, 2f));

            _sut.Run(_world);

            AssertNoFire("레인이 비면 새 START 를 하지 않는다(탄 낭비 방지)");
        }

        [Test]
        public void Facing_DropsTheFrontmostBonus()
        {
            // ⚠ witness 는 "최전방" 이 아니라 "최근접" 이다 — 보너스를 실으면 카드가 약속한
            //   대상이 아닌 적이 배율을 받는다.
            FlowField();
            var defender = FrontmostDefender(new SimVec3(3f, 0f, 3f));
            _world.Set(defender, new DeployedFacing { value = new SimInt2(1, 0) });
            var inLane = Target(Faction.Enemy, new SimVec3(5f, 0f, 3f));

            _sut.Run(_world);

            Assert.AreEqual(inLane, Fired().target);
            Assert.IsFalse(_world.Get<FrontmostAttackLock>(defender).targetIsPriority,
                "방향 유닛은 레인이 타겟팅 규칙 전부라 보너스를 포기한다");
        }

        [Test]
        public void Lane_ExcludesTheAttackerOwnTile_AndIsOneTileWide()
        {
            Assert.IsFalse(LaneMath.IsInLane(new SimInt2(0, 0), new SimInt2(1, 0), 3, new SimInt2(0, 0)),
                "자기 타일은 레인이 아니다");
            Assert.IsTrue(LaneMath.IsInLane(new SimInt2(0, 0), new SimInt2(1, 0), 3, new SimInt2(3, 0)));
            Assert.IsFalse(LaneMath.IsInLane(new SimInt2(0, 0), new SimInt2(1, 0), 3, new SimInt2(4, 0)),
                "사거리 밖");
            Assert.IsFalse(LaneMath.IsInLane(new SimInt2(0, 0), new SimInt2(1, 0), 3, new SimInt2(2, 1)),
                "폭 1타일 — 옆줄은 레인이 아니다");
            Assert.IsFalse(LaneMath.IsInLane(new SimInt2(0, 0), new SimInt2(1, 0), 3, new SimInt2(-2, 0)),
                "뒤쪽은 레인이 아니다");
        }

        // ── 랭킹 순수 함수 ────────────────────────────────────────────────────

        [Test]
        public void FrontmostRank_OrdersByFlowDistThenDistanceThenSimId()
        {
            var a = new FrontmostTargeting.Candidate { flowDist = 3, sqDist = 9f, simId = 7 };
            var b = new FrontmostTargeting.Candidate { flowDist = 5, sqDist = 1f, simId = 2 };
            Assert.IsTrue(FrontmostTargeting.RanksBefore(a, b), "flowDist 가 1순위");

            b = new FrontmostTargeting.Candidate { flowDist = 3, sqDist = 4f, simId = 9 };
            Assert.IsTrue(FrontmostTargeting.RanksBefore(b, a), "동률이면 거리");

            b = new FrontmostTargeting.Candidate { flowDist = 3, sqDist = 9f, simId = 2 };
            Assert.IsTrue(FrontmostTargeting.RanksBefore(b, a), "그다음은 낮은 simId");
        }

        [Test]
        public void LowestHealthRank_OrdersByHpRatioThenDistanceThenSimId()
        {
            var a = new LowestHealthTargeting.Candidate { hpRatio = 0.2f, sqDist = 9f, simId = 7 };
            var b = new LowestHealthTargeting.Candidate { hpRatio = 0.8f, sqDist = 1f, simId = 2 };
            Assert.IsTrue(LowestHealthTargeting.RanksBefore(a, b), "더 다친 쪽이 앞");

            b = new LowestHealthTargeting.Candidate { hpRatio = 0.2f, sqDist = 4f, simId = 9 };
            Assert.IsTrue(LowestHealthTargeting.RanksBefore(b, a), "동률이면 거리");

            b = new LowestHealthTargeting.Candidate { hpRatio = 0.2f, sqDist = 9f, simId = 2 };
            Assert.IsTrue(LowestHealthTargeting.RanksBefore(b, a), "그다음은 낮은 simId");
        }
    }
}
