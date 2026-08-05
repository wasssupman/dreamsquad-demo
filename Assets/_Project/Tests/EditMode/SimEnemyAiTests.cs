// battle-sim-extraction unit 18-F/3 — #13 TauntAttackGrant · #14 EnemyAiState 이식 핀.
// `Evaluate` 는 구 sim 도 public static 순수 함수라 **어서션이 그대로 복제**된다.
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    public class SimAiStateEvaluateTests
    {
        [Test]
        public void Aggro_TakesPriority_OverFireTarget()
        {
            // 어그로가 걸리면 사격 가능 여부와 무관하게 가디언 기준으로만 판단한다.
            Assert.AreEqual(AiState.Standoff,
                EnemyAiStateSystem.Evaluate(aggroed: true, guardianInRange: true, hasFireTarget: true));
            Assert.AreEqual(AiState.Chasing,
                EnemyAiStateSystem.Evaluate(aggroed: true, guardianInRange: false, hasFireTarget: true));
        }

        [Test]
        public void NonAggro_IsEngagingOnlyWhenItCouldActuallyFire()
        {
            Assert.AreEqual(AiState.Engaging,
                EnemyAiStateSystem.Evaluate(false, false, hasFireTarget: true));
            Assert.AreEqual(AiState.Marching,
                EnemyAiStateSystem.Evaluate(false, false, hasFireTarget: false));
        }

        [Test]
        public void GuardianInRange_IsIgnored_WhenNotAggroed()
            => Assert.AreEqual(AiState.Marching,
                EnemyAiStateSystem.Evaluate(false, guardianInRange: true, hasFireTarget: false));
    }

    public class SimTauntAttackGrantTests
    {
        private SimWorld _world;
        private TauntAttackGrantSystem _sys;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _sys = new TauntAttackGrantSystem();
        }

        private SimEntityId TauntEnemy(bool aggroed)
        {
            var e = _world.Create();
            _world.Set(e, new AggroAttackProfile { damage = 3f, cooldown = 2f, range = 1.5f });
            if (aggroed) _world.Set(e, new Aggroed { guardian = _world.Create() });
            return e;
        }

        [Test]
        public void NoAggroAndNoGrant_DoesNotRun()
        {
            var e = TauntEnemy(aggroed: false);
            _sys.Run(_world);
            Assert.IsFalse(_world.Has<AttackState>(e));
        }

        [Test]
        public void Grants_AttackState_AndDamageOutput_FromProfile()
        {
            var e = TauntEnemy(aggroed: true);
            _sys.Run(_world);

            Assert.IsTrue(_world.Has<TauntAttackGranted>(e));
            var atk = _world.Get<AttackState>(e);
            Assert.AreEqual(1.5f, atk.range, 1e-5f);
            Assert.AreEqual(2f, atk.cooldownDuration, 1e-5f);
            Assert.AreEqual(0f, atk.cooldownRemaining, 1e-5f, "즉시 발사 가능.");
            Assert.AreEqual(1, atk.attackTargetCount);
            Assert.AreEqual((int)Faction.Defender, atk.targetMask, "도발 공격은 방어유닛만 친다.");

            var outputs = _world.GetBuffer<AttackOutputElement>(e);
            Assert.AreEqual(1, outputs.Count);
            Assert.AreEqual(AttackOutputKind.Damage, outputs[0].value.kind);
            Assert.AreEqual(3f, outputs[0].value.magnitude, 1e-5f);
        }

        [Test]
        public void DoesNotGrant_WhenEnemyAlreadyHasItsOwnAttack()
        {
            var e = TauntEnemy(aggroed: true);
            _world.Set(e, new AttackState { range = 99f });
            _sys.Run(_world);

            Assert.IsFalse(_world.Has<TauntAttackGranted>(e), "자기 공격이 있으면 부여하지 않는다.");
            Assert.AreEqual(99f, _world.Get<AttackState>(e).range, 1e-5f, "원래 공격을 덮지 않는다.");
        }

        [Test]
        public void Strips_WhenAggroIsReleased()
        {
            var e = TauntEnemy(aggroed: true);
            _sys.Run(_world);
            Assert.IsTrue(_world.Has<AttackState>(e));

            _world.RemoveComponent<Aggroed>(e);
            _sys.Run(_world);

            Assert.IsFalse(_world.Has<AttackState>(e));
            Assert.IsFalse(_world.Has<TauntAttackGranted>(e));
            Assert.IsFalse(_world.HasBuffer<AttackOutputElement>(e),
                "출력 버퍼는 **없앤다**(비우는 게 아니다).");
        }

        [Test]
        public void StripPassStaysAlive_WhenNoEnemyIsAggroedAnymore()
        {
            // OR 게이트의 존재 이유 — AND 면 어그로가 0 이 되는 순간 회수가 멈춰
            // 적이 도발 공격을 영구히 들고 다닌다.
            var e = TauntEnemy(aggroed: true);
            _sys.Run(_world);
            _world.RemoveComponent<Aggroed>(e);   // 판에 Aggroed 가 하나도 없다

            _sys.Run(_world);

            Assert.IsFalse(_world.Has<AttackState>(e), "TauntAttackGranted 만으로도 돌아야 한다.");
        }

        [Test]
        public void DoesNotStrip_NativeAttackOfANonGrantedEnemy()
        {
            var e = _world.Create();
            _world.Set(e, new AttackState { range = 5f });
            _world.Set(e, new Aggroed { guardian = _world.Create() });
            _sys.Run(_world);
            _world.RemoveComponent<Aggroed>(e);
            _sys.Run(_world);

            Assert.IsTrue(_world.Has<AttackState>(e),
                "부여 표식이 없으면 원래 공격은 건드리지 않는다.");
        }
    }

    public class SimEnemyAiStateSystemTests
    {
        private static readonly SimInt2 Grid = new SimInt2(16, 16);

        private SimWorld _world;
        private EnemyAiStateSystem _sys;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _sys = new EnemyAiStateSystem();
            int n = Grid.x * Grid.y;
            _world.Set(_world.Create(), new FlowFieldSingleton
            {
                flow = new SimVec2[n], dist = new int[n],
                gridSize = Grid, tileSize = 1f, origin = SimVec3.Zero, goalCell = new SimInt2(15, 15),
            });
        }

        private SimEntityId Enemy(SimInt2 cell, float range = 2f, int mask = (int)Faction.Defender)
        {
            var e = _world.Create();
            _world.Set(e, default(EnemyAiState));
            _world.Set(e, SimTransform.FromPosition(new SimVec3(cell.x, 0f, cell.y)));
            if (range > 0f) _world.Set(e, new AttackState { range = range, targetMask = mask });
            return e;
        }

        private SimEntityId Defender(SimInt2 cell, DefenderClass cls = DefenderClass.None,
                                     bool pending = false, bool dead = false)
        {
            var e = _world.Create();
            _world.Set(e, new FactionTag { value = Faction.Defender });
            _world.Set(e, new Health { value = 10f, max = 10f });
            _world.Set(e, SimTransform.FromPosition(new SimVec3(cell.x, 0f, cell.y)));
            if (cls != DefenderClass.None) _world.Set(e, new DefenderClassTag { value = cls });
            if (pending) _world.Set(e, default(PendingDeployment));
            if (dead) _world.Set(e, default(DeadTag));
            return e;
        }

        private AiState StateOf(SimEntityId e) => _world.Get<EnemyAiState>(e).value;

        [Test]
        public void NoTargetInRange_IsMarching()
        {
            var e = Enemy(new SimInt2(2, 2));
            Defender(new SimInt2(10, 10));
            _sys.Run(_world);
            Assert.AreEqual(AiState.Marching, StateOf(e));
        }

        [Test]
        public void TargetInRange_IsEngaging()
        {
            var e = Enemy(new SimInt2(2, 2), range: 2f);
            Defender(new SimInt2(4, 2));   // 체비셰프 2
            _sys.Run(_world);
            Assert.AreEqual(AiState.Engaging, StateOf(e));
        }

        [Test]
        public void EnemyWithoutAttackState_NeverEngages()
        {
            var e = Enemy(new SimInt2(2, 2), range: 0f);   // AttackState 없음
            Defender(new SimInt2(2, 2));
            _sys.Run(_world);
            Assert.AreEqual(AiState.Marching, StateOf(e));
        }

        [Test]
        public void PendingOrDeadDefenders_AreNotFireTargets()
        {
            var e = Enemy(new SimInt2(2, 2), range: 2f);
            Defender(new SimInt2(3, 2), pending: true);
            Defender(new SimInt2(2, 3), dead: true);
            _sys.Run(_world);
            Assert.AreEqual(AiState.Marching, StateOf(e), "공격 루프와 같은 후보 풀이어야 한다.");
        }

        [Test]
        public void FactionMask_GatesTheCandidate()
        {
            var e = Enemy(new SimInt2(2, 2), range: 2f, mask: (int)Faction.BlockingHazard);
            Defender(new SimInt2(3, 2));
            _sys.Run(_world);
            Assert.AreEqual(AiState.Marching, StateOf(e), "마스크에 없는 진영은 타겟이 아니다.");
        }

        [Test]
        public void ClassFilter_ExcludesUnlistedClasses_ButUntaggedTargetsBypass()
        {
            var e = Enemy(new SimInt2(2, 2), range: 2f);
            _world.Set(e, new EnemyTargetFilter { classMask = 1 << (int)DefenderClass.Ranger });

            Defender(new SimInt2(3, 2), DefenderClass.Guardian);
            _sys.Run(_world);
            Assert.AreEqual(AiState.Marching, StateOf(e), "필터에 없는 클래스는 제외.");

            Defender(new SimInt2(2, 3));   // 태그 없음 → 마스크 우회
            _sys.Run(_world);
            Assert.AreEqual(AiState.Engaging, StateOf(e), "클래스 태그가 없으면 필터를 우회한다.");
        }

        [Test]
        public void Aggroed_IsChasing_UntilGuardianIsInRange()
        {
            var g = Defender(new SimInt2(10, 2));
            var e = Enemy(new SimInt2(2, 2), range: 2f);
            _world.Set(e, new Aggroed { guardian = g });
            _sys.Run(_world);
            Assert.AreEqual(AiState.Chasing, StateOf(e));

            _world.Set(g, SimTransform.FromPosition(new SimVec3(4, 0, 2)));   // 체비셰프 2
            _sys.Run(_world);
            Assert.AreEqual(AiState.Standoff, StateOf(e));
        }

        [Test]
        public void AggroedWithoutAttackState_StaysChasing()
        {
            // 사거리 판정 수단이 없으면 영원히 Chasing 이다 — #8 의 NoAttack 거부가 그 원천을 막는다.
            var g = Defender(new SimInt2(2, 2));
            var e = Enemy(new SimInt2(2, 2), range: 0f);
            _world.Set(e, new Aggroed { guardian = g });
            _sys.Run(_world);
            Assert.AreEqual(AiState.Chasing, StateOf(e));
        }

        [Test]
        public void FocusUntilDead_OnlyEngagesWhenTheLockedTargetIsInRange()
        {
            var far = Defender(new SimInt2(12, 12));
            var near = Defender(new SimInt2(3, 2));
            var e = Enemy(new SimInt2(2, 2), range: 2f);
            _world.Set(e, new EnemyBehavior { targetMode = EnemyTargetMode.FocusUntilDead });
            _world.Set(e, new FocusTarget { current = far });

            _sys.Run(_world);
            Assert.AreEqual(AiState.Marching, StateOf(e),
                "락 타겟이 멀면 사거리 안의 다른 적이 있어도 Engaging 이 아니다(데드락 방지 계약).");

            _world.Set(e, new FocusTarget { current = near });
            _sys.Run(_world);
            Assert.AreEqual(AiState.Engaging, StateOf(e));
        }

        [Test]
        public void FocusUntilDead_FallsBackToNearest_WhenTheLockIsInvalid()
        {
            var dead = Defender(new SimInt2(12, 12), dead: true);
            Defender(new SimInt2(3, 2));
            var e = Enemy(new SimInt2(2, 2), range: 2f);
            _world.Set(e, new EnemyBehavior { targetMode = EnemyTargetMode.FocusUntilDead });
            _world.Set(e, new FocusTarget { current = dead });

            _sys.Run(_world);
            Assert.AreEqual(AiState.Engaging, StateOf(e), "무효 락은 nearest 경로로 떨어진다.");
        }

        [Test]
        public void WithoutFlowField_UsesFallbackGridAndStillEvaluates()
        {
            var w = new SimWorld(new SimConfig(1u, 1u));
            var e = w.Create();
            w.Set(e, default(EnemyAiState));
            w.Set(e, SimTransform.FromPosition(new SimVec3(2, 0, 2)));
            w.Set(e, new AttackState { range = 2f, targetMask = (int)Faction.Defender });

            var d = w.Create();
            w.Set(d, new FactionTag { value = Faction.Defender });
            w.Set(d, new Health { value = 10f, max = 10f });
            w.Set(d, SimTransform.FromPosition(new SimVec3(3, 0, 2)));

            new EnemyAiStateSystem().Run(w);
            Assert.AreEqual(AiState.Engaging, w.Get<EnemyAiState>(e).value,
                "필드가 없어도 tileSize 1 · 128×128 폴백으로 평가한다(합성 테스트 월드 계약).");
        }
    }
}
