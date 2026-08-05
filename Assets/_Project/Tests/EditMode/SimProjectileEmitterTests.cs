using System;
using System.Collections.Generic;
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
    /// battle-sim-extraction unit 18-H/4 — 발사 명세(#38)와 그 순수 계층, 그리고 18-H 클러스터.
    ///
    /// 이 조각의 핵심은 **로직과 아키텍처의 분리**다 — 스케줄(EmitterTick)·선택(PatternTargeting)·
    /// 명령(PatternLogic)은 엔티티를 모르고, 시스템은 그 명령을 번역만 한다. 그래서 순수 계층은
    /// 월드 없이 검증되고 시스템 테스트는 번역만 본다.
    /// </summary>
    public class SimProjectileEmitterTests
    {
        private SimWorld _world;
        private ProjectileEmitterSystem _sut;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _sut = new ProjectileEmitterSystem();
            _world.SetDeltaTime(0.1f);
        }

        private void Field(float tileSize = 1f)
        {
            var f = _world.Create();
            _world.Set(f, new FlowFieldSingleton
            {
                flow = new SimVec2[1], dist = new int[1],
                gridSize = new SimInt2(64, 64), tileSize = tileSize, origin = default,
            });
        }

        private static PatternSpec Spec(int shotCount, float interval = 0f,
            PatternSelectionRule rule = PatternSelectionRule.RoundRobin, bool reselect = true)
        {
            var shots = new PatternShotSpec[shotCount];
            for (int i = 0; i < shotCount; i++)
                shots[i] = new PatternShotSpec { directionT = 0.5f, intervalAfterPreviousSec = i == 0 ? 0f : interval };
            return new PatternSpec
            {
                shots = shots, damage = 3f, barrelDataIndex = 2, selection = rule,
                reselectPerShot = reselect, minAngleDeg = -30f, maxAngleDeg = 30f, telegraphSec = 1.5f,
            };
        }

        // ═════ EmitterTick (순수) ════════════════════════════════════════════

        [Test]
        public void Begin_SeedsFromDurableCounter_SoSelectionIsNotStuck()
        {
            // ⚠ 0 으로 시드하면 RoundRobin 이 영원히 rank 0 — 같은 대상만 맞는다.
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, Spec(3), baseFireCount: 7);
            Assert.AreEqual(3, rt.burstRemaining);
            Assert.AreEqual(0f, rt.timer);
            Assert.AreEqual(7, rt.fireCount, "durable 카운터를 이어받는다");
            Assert.AreEqual(0, rt.shotIndex);
        }

        [Test]
        public void Advance_FiresFirstShotOnTheStartingFrame()
        {
            var spec = Spec(3, interval: 1f);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);

            Assert.AreEqual(1, EmitterTick.Advance(ref rt, 0.016f, spec), "timer 0 에서 시작 = 첫 발 즉시");
            Assert.AreEqual(0, EmitterTick.Advance(ref rt, 0.5f, spec), "간격 미달");
            Assert.AreEqual(1, EmitterTick.Advance(ref rt, 0.5f, spec));
        }

        [Test]
        public void Advance_ZeroInterval_FiresEverythingInOneFrame()
        {
            var spec = Spec(4, interval: 0f);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);

            Assert.AreEqual(4, EmitterTick.Advance(ref rt, 0.016f, spec));
            Assert.IsTrue(EmitterTick.IsComplete(rt));
        }

        [Test]
        public void Advance_SlowFrame_ReturnsSeveralShots_AndCarriesRemainder()
        {
            var spec = Spec(5, interval: 0.1f);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);

            // 0.25s = 첫 발 + 0.1 + 0.1 → 3발, 잔여 -0.05 이월.
            Assert.AreEqual(3, EmitterTick.Advance(ref rt, 0.25f, spec));
            Assert.AreEqual(2, rt.burstRemaining);
        }

        [Test]
        public void Advance_UsesTheNextStepInterval_NotTheConsumedOne()
        {
            // ⚠ 스케줄 진행도는 스케줄러 소유 — 소비자가 shotIndex 를 안 올려도 다음 간격이 맞아야.
            var shots = new[]
            {
                new PatternShotSpec { intervalAfterPreviousSec = 99f }, // index 0 은 무시된다
                new PatternShotSpec { intervalAfterPreviousSec = 0.2f },
                new PatternShotSpec { intervalAfterPreviousSec = 5f },
            };
            var spec = new PatternSpec { shots = shots };
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);

            Assert.AreEqual(1, EmitterTick.Advance(ref rt, 0.016f, spec), "첫 발은 index 0 간격을 무시");
            Assert.AreEqual(1, EmitterTick.Advance(ref rt, 0.2f, spec), "다음은 index 1 의 0.2");
            Assert.AreEqual(0, EmitterTick.Advance(ref rt, 1f, spec), "그 다음은 index 2 의 5초");
        }

        [Test]
        public void TotalDuration_IgnoresTheFirstInterval()
        {
            var shots = new[]
            {
                new PatternShotSpec { intervalAfterPreviousSec = 99f },
                new PatternShotSpec { intervalAfterPreviousSec = 0.3f },
                new PatternShotSpec { intervalAfterPreviousSec = 0.2f },
            };
            Assert.AreEqual(0.5f, EmitterTick.TotalDuration(new PatternSpec { shots = shots }), 1e-4f);
        }

        // ═════ PatternTargeting (순수) ═══════════════════════════════════════

        [Test]
        public void Select_RoundRobin_WalksCellKeyRank_NotSnapshotOrder()
        {
            // 스냅샷 순서를 일부러 뒤집어 둔다 — rank 는 row-major 셀 키가 정한다.
            var cells = new List<SimInt2> { new SimInt2(3, 0), new SimInt2(1, 0), new SimInt2(2, 0) };
            var grid = new SimInt2(64, 64);

            Assert.AreEqual(1, PatternTargeting.Select(cells, PatternSelectionRule.RoundRobin, 0, grid), "rank 0 = 키 1");
            Assert.AreEqual(2, PatternTargeting.Select(cells, PatternSelectionRule.RoundRobin, 1, grid), "rank 1 = 키 2");
            Assert.AreEqual(0, PatternTargeting.Select(cells, PatternSelectionRule.RoundRobin, 2, grid), "rank 2 = 키 3");
            Assert.AreEqual(1, PatternTargeting.Select(cells, PatternSelectionRule.RoundRobin, 3, grid), "한 바퀴");
        }

        [Test]
        public void Select_NoneRule_AndEmptyPool_ReturnMinusOne()
        {
            var cells = new List<SimInt2> { new SimInt2(1, 1) };
            var grid = new SimInt2(64, 64);
            Assert.AreEqual(-1, PatternTargeting.Select(cells, PatternSelectionRule.None, 0, grid));
            Assert.AreEqual(-1, PatternTargeting.Select(new List<SimInt2>(), PatternSelectionRule.RoundRobin, 0, grid));
        }

        [Test]
        public void Select_Shuffle_IsStableForTheSameFireCount()
        {
            var cells = new List<SimInt2> { new SimInt2(0, 0), new SimInt2(1, 0), new SimInt2(2, 0), new SimInt2(3, 0) };
            var grid = new SimInt2(64, 64);
            for (int fc = 0; fc < 8; fc++)
            {
                int a = PatternTargeting.Select(cells, PatternSelectionRule.DeterministicShuffle, fc, grid);
                int b = PatternTargeting.Select(cells, PatternSelectionRule.DeterministicShuffle, fc, grid);
                Assert.AreEqual(a, b, $"fireCount {fc}: 같은 입력 같은 결과(리플레이 가능)");
                Assert.GreaterOrEqual(a, 0);
            }
        }

        [Test]
        public void Select_NegativeFireCount_IsHandled()
        {
            var cells = new List<SimInt2> { new SimInt2(0, 0), new SimInt2(1, 0) };
            var grid = new SimInt2(64, 64);
            Assert.GreaterOrEqual(PatternTargeting.Select(cells, PatternSelectionRule.RoundRobin, -3, grid), 0);
            Assert.GreaterOrEqual(PatternTargeting.Select(cells, PatternSelectionRule.DeterministicShuffle, -3, grid), 0);
        }

        // ═════ PatternDirection / Randomizer ═════════════════════════════════

        [Test]
        public void Direction_LerpsBetweenAngles_AndSaturatesT()
        {
            var baseDir = new SimVec2(1f, 0f);
            var mid = PatternDirection.Resolve(baseDir, -90f, 90f, 0.5f);
            Assert.AreEqual(1f, mid.x, 1e-4f, "t=0.5 → 0도 = 기준 방향 그대로");
            Assert.AreEqual(0f, mid.y, 1e-4f);

            var over = PatternDirection.Resolve(baseDir, -90f, 90f, 5f);
            var atOne = PatternDirection.Resolve(baseDir, -90f, 90f, 1f);
            Assert.AreEqual(atOne.x, over.x, 1e-4f, "t 는 포화된다");
            Assert.AreEqual(atOne.y, over.y, 1e-4f);
        }

        [Test]
        public void Randomizer_MakesANewArray_SoTheSourceSlotIsNotPoisoned()
        {
            // ⚠ 이식의 핵심 위험 — 구 FixedList 는 값 타입이라 복사가 목록까지 복사했다.
            var original = Spec(3);
            original.randomizeShotsPerTrigger = true;
            original.randomIntervalMinSec = 0.1f;
            original.randomIntervalMaxSec = 0.5f;
            var originalShots = original.shots;

            var copy = original;
            PatternShotRandomizer.Apply(ref copy, seed: 7u);

            Assert.AreNotSame(originalShots, copy.shots, "새 배열이어야 원본 슬롯이 안전하다");
            Assert.AreEqual(0.5f, originalShots[1].directionT, 1e-4f, "원본은 그대로");
        }

        [Test]
        public void Randomizer_SameSeedSameShots_AndFirstIntervalIsZero()
        {
            var a = Spec(4); a.randomizeShotsPerTrigger = true; a.randomIntervalMinSec = 0.1f; a.randomIntervalMaxSec = 0.4f;
            var b = a;
            PatternShotRandomizer.Apply(ref a, 42u);
            PatternShotRandomizer.Apply(ref b, 42u);

            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(a.shots[i].directionT, b.shots[i].directionT, 1e-6f);
                Assert.AreEqual(a.shots[i].intervalAfterPreviousSec, b.shots[i].intervalAfterPreviousSec, 1e-6f);
            }
            Assert.AreEqual(0f, a.shots[0].intervalAfterPreviousSec, "첫 탄은 트리거 프레임 — 간격 0");
        }

        [Test]
        public void Randomizer_IsInertWhenFlagIsOff()
        {
            var spec = Spec(3);
            var before = spec.shots;
            PatternShotRandomizer.Apply(ref spec, 1u);
            Assert.AreSame(before, spec.shots, "플래그가 꺼져 있으면 손대지 않는다");
        }

        [Test]
        public void CreateFromIndex_RejectsTheDegenerateIndex()
        {
            Assert.Throws<ArgumentException>(() => SimRandom.CreateFromIndex(uint.MaxValue),
                "해시가 0 이 되어 난수열이 죽는 값 — 조용히 돌리면 판이 결정론을 잃는다");
        }

        // ═════ MovementBinding ═══════════════════════════════════════════════

        [Test]
        public void MovementBinding_CoversEveryKind_AndTheCountPinCatchesNewOnes()
        {
            var kinds = Enum.GetValues(typeof(MovementKind)).Cast<MovementKind>().ToArray();
            Assert.AreEqual(MovementBinding.KnownKindCount, kinds.Length,
                "⚠ 새 MovementKind 를 추가했다면 MovementBinding.Of 분류와 이 상수를 함께 갱신할 것");

            Assert.AreEqual(BindingClass.Entity, MovementBinding.Of(MovementKind.HomingToEntity));
            Assert.AreEqual(BindingClass.Entity, MovementBinding.Of(MovementKind.BezierHomingToEntity));
            Assert.AreEqual(BindingClass.Cell, MovementBinding.Of(MovementKind.BallisticArcToPoint));
            Assert.AreEqual(BindingClass.Cell, MovementBinding.Of(MovementKind.SkyFall));
            Assert.AreEqual(BindingClass.Cell, MovementBinding.Of(MovementKind.GrenadeToCell));
            Assert.AreEqual(BindingClass.Direction, MovementBinding.Of(MovementKind.DirectionalLinear));
        }

        // ═════ 시스템 (번역) ══════════════════════════════════════════════════

        private SimEntityId Host(bool defender, SimVec3 pos, EmitterInstance inst)
        {
            var e = _world.Create();
            if (defender) _world.Set(e, new DefenderUnitTag());
            else _world.Set(e, new AttackUnitTag());
            _world.Set(e, SimTransform.FromPosition(pos));
            _world.AddBuffer<EmitterInstance>(e).Add(inst);
            return e;
        }

        private List<ProjectileSpawnRequest> Carriers()
            => _world.With<ProjectileRequestCarrier>()
                     .Select(e => _world.Get<ProjectileSpawnRequest>(e))
                     .ToList();

        [Test]
        public void Emitter_EntityBinding_TargetsAnEnemy_AndCarriesSwingIndex()
        {
            Field();
            var enemy = _world.Create();
            _world.Set(enemy, new AttackUnitTag());
            _world.Set(enemy, SimTransform.FromPosition(new SimVec3(3f, 0, 0)));

            var spec = Spec(2, interval: 0f);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);
            Host(defender: true, new SimVec3(0, 0, 0), new EmitterInstance
            {
                spec = spec, runtime = rt,
                template = new ProjectileSpawnRequest { movement = MovementKind.HomingToEntity },
            });

            _sut.Run(_world);

            var reqs = Carriers();
            Assert.AreEqual(2, reqs.Count, "간격 0 = 같은 프레임에 두 발");
            Assert.IsTrue(reqs.All(r => r.target == enemy));
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, reqs.Select(r => r.swingIndex).ToArray(),
                "베지어 스윙 소스는 버스트 내 순번");
            Assert.AreEqual(3f, reqs[0].damage, 1e-4f, "명령이 정한 값 — template 이 아니다");
            Assert.AreEqual(2, reqs[0].dataIndex);
        }

        [Test]
        public void Emitter_CellBinding_UsesCellCenterAndTelegraph()
        {
            Field(tileSize: 2f);
            var defender = _world.Create();
            _world.Set(defender, new DefenderUnitTag());
            _world.Set(defender, SimTransform.FromPosition(new SimVec3(4f, 0, 2f)));

            var spec = Spec(1);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);
            Host(defender: false, new SimVec3(0, 0, 0), new EmitterInstance
            {
                spec = spec, runtime = rt,
                template = new ProjectileSpawnRequest { movement = MovementKind.SkyFall },
            });

            _sut.Run(_world);

            var req = Carriers()[0];
            Assert.AreEqual(GridMath.CellToWorldCenter(new SimInt2(2, 1), 2f, 0f, default), req.impact);
            Assert.AreEqual(1.5f, req.flightTime, 1e-4f, "예고 시간은 패턴이 소유한다");
        }

        [Test]
        public void Emitter_DirectionBinding_NeedsNoPool()
        {
            Field();
            var spec = Spec(3, interval: 0f, rule: PatternSelectionRule.None);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);
            Host(defender: true, new SimVec3(0, 0, 0), new EmitterInstance
            {
                spec = spec, runtime = rt,
                template = new ProjectileSpawnRequest
                {
                    movement = MovementKind.DirectionalLinear, direction = new SimVec2(1f, 0f),
                },
            });

            _sut.Run(_world); // 적이 하나도 없다

            Assert.AreEqual(3, Carriers().Count, "무타겟 경로는 후보 풀이 필요 없다");
        }

        [Test]
        public void Emitter_LocksTarget_WhenReselectIsOff()
        {
            Field();
            var a = _world.Create(); _world.Set(a, new AttackUnitTag()); _world.Set(a, SimTransform.FromPosition(new SimVec3(1f, 0, 0)));
            var b = _world.Create(); _world.Set(b, new AttackUnitTag()); _world.Set(b, SimTransform.FromPosition(new SimVec3(2f, 0, 0)));

            var spec = Spec(3, interval: 0f, reselect: false);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);
            Host(defender: true, new SimVec3(0, 0, 0), new EmitterInstance
            {
                spec = spec, runtime = rt,
                template = new ProjectileSpawnRequest { movement = MovementKind.HomingToEntity },
            });

            _sut.Run(_world);

            var reqs = Carriers();
            Assert.AreEqual(3, reqs.Count);
            Assert.AreEqual(1, reqs.Select(r => r.target).Distinct().Count(), "집중 사격 — 세 발이 한 대상");
        }

        [Test]
        public void Emitter_RemovesTheInstance_WhenTheBurstCompletes()
        {
            Field();
            var enemy = _world.Create(); _world.Set(enemy, new AttackUnitTag());
            _world.Set(enemy, SimTransform.FromPosition(new SimVec3(1f, 0, 0)));

            var spec = Spec(2, interval: 0f);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);
            var host = Host(defender: true, new SimVec3(0, 0, 0), new EmitterInstance
            {
                spec = spec, runtime = rt,
                template = new ProjectileSpawnRequest { movement = MovementKind.HomingToEntity },
            });

            _sut.Run(_world);

            Assert.AreEqual(0, _world.GetBuffer<EmitterInstance>(host).Count, "완주하면 제거 — 영구 적재 금지");
        }

        [Test]
        public void Emitter_SkipsHostsWithoutAFaction()
        {
            Field();
            var spec = Spec(1);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);
            var e = _world.Create();
            _world.Set(e, SimTransform.FromPosition(new SimVec3(0, 0, 0))); // 진영 태그 없음
            _world.AddBuffer<EmitterInstance>(e).Add(new EmitterInstance
            {
                spec = spec, runtime = rt,
                template = new ProjectileSpawnRequest { movement = MovementKind.HomingToEntity },
            });

            _sut.Run(_world);

            Assert.AreEqual(0, Carriers().Count, "진영 불명 host = no-op");
            Assert.AreEqual(1, _world.GetBuffer<EmitterInstance>(e).Count, "tick 도 하지 않는다");
        }

        [Test]
        public void Emitter_EnemyPool_ExcludesDeadLeakedAndOutOfPlay()
        {
            Field();
            var dead = _world.Create(); _world.Set(dead, new AttackUnitTag()); _world.Set(dead, SimTransform.FromPosition(new SimVec3(1f, 0, 0))); _world.Set(dead, new DeadTag());
            var leaked = _world.Create(); _world.Set(leaked, new AttackUnitTag()); _world.Set(leaked, SimTransform.FromPosition(new SimVec3(2f, 0, 0))); _world.Set(leaked, new PastGoalTag());
            var away = _world.Create(); _world.Set(away, new AttackUnitTag()); _world.Set(away, SimTransform.FromPosition(new SimVec3(3f, 0, 0))); _world.Set(away, new UltimateLeapState { remaining = 2f });

            var spec = Spec(1);
            var rt = default(EmitterRuntime);
            EmitterTick.Begin(ref rt, spec, 0);
            Host(defender: true, new SimVec3(0, 0, 0), new EmitterInstance
            {
                spec = spec, runtime = rt,
                template = new ProjectileSpawnRequest { movement = MovementKind.HomingToEntity },
            });

            _sut.Run(_world);

            Assert.AreEqual(0, Carriers().Count, "후보 0 = 발사 소모(화면 밖 보스에 쏘지 않는다)");
        }

        // ═════ 클러스터 ═══════════════════════════════════════════════════════

        [Test]
        public void Cluster_KeepsMoveAndHitAdjacent_ButEmitterLate()
        {
            var steps = new ProjectileCluster(new SimChannels()).Steps().ToList();
            CollectionAssert.AreEqual(new[] { 26, 27, 38 }, steps.Select(s => s.Order).ToArray());

            Assert.AreEqual(SimPhase.Projectiles, steps[0].Phase);
            Assert.AreEqual(SimPhase.Projectiles, steps[1].Phase, "#26 → #27 은 같은 phase 에 붙어 있다");
            Assert.AreEqual(SimPhase.PostProcess, steps[2].Phase, "#38 은 공격(P8) 뒤라야 그 프레임에 첫 발이 나간다");
        }

        [Test]
        public void Cluster_PutsHitBeforeDamageResolve()
        {
            var steps = new ProjectileCluster(new SimChannels()).Steps().ToList();
            Assert.Less((int)steps[1].Phase, (int)SimPhase.DamageResolve,
                "착탄이 넣은 피해는 같은 틱 #34 가 소비한다");
        }
    }
}
