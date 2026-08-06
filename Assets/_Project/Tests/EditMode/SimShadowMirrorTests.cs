using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Bridge;
using Wassup.Sim;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-N — **값 미러의 오라클.**
    ///
    /// `ShadowMirror` 는 리플렉션으로 구 컴포넌트를 신 컴포넌트로 옮긴다. 그 정당성의 전제는
    /// 18-M(필드 이름 일치)이고, 여기서 보는 것은 **옮기기가 실제로 값을 실어 나르는가**다.
    ///
    /// ⚠ 왜 이 테스트가 필요한가: copier 가 조용히 아무것도 안 해도(예: 필드 탐색이 늘 `null`)
    /// 그림자는 예외 없이 돌고 골든도 초록이다 — 라이브가 골든을 만들기 때문이다. 그림자가
    /// **빈 판**이라는 사실은 A/B 비교(18-Q)를 붙이기 전까지 드러나지 않는다.
    /// </summary>
    public class SimShadowMirrorTests
    {
        private World _world;
        private EntityManager _em;
        private SimWorld _sim;

        [SetUp]
        public void SetUp()
        {
            _world = new World("shadow-mirror-test");
            _em = _world.EntityManager;
            _sim = new SimWorld(new SimConfig(1u, 1u));
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated) _world.Dispose();
            _world = null;
        }

        private static SimEntityId NoEntities(Entity _) => SimEntityId.Null;

        private object Convert<TOld>(TOld value, Type newT) where TOld : struct
            => ShadowMirror.ConvertStruct(value, newT, NoEntities);

        // ── 타입 맵 ───────────────────────────────────────────────────────────

        [Test]
        public void 타입_맵이_스폰_컴포넌트를_전부_덮는다()
        {
            // 18-N 착수 노트의 적 스폰 세트 20종 중 매핑 대상. 하나라도 빠지면 그림자에
            // 그 상태가 없고, 그건 "규칙이 틀렸다" 로 오진된다.
            Type[] required =
            {
                typeof(LocalTransform), typeof(Wassup.Battle.Units.Health),
                typeof(Wassup.Battle.Units.FactionTag), typeof(Wassup.Battle.Units.AwakeningReward),
                typeof(Wassup.Battle.Units.KillScore), typeof(Wassup.Battle.Combat.AttackState),
                typeof(Wassup.Battle.Combat.AggroAttackProfile), typeof(Wassup.Battle.Combat.EnemyBehavior),
                typeof(Wassup.Battle.Combat.EnemyAiState), typeof(Wassup.Battle.Combat.FocusTarget),
                typeof(Wassup.Battle.Combat.EnemyTargetFilter), typeof(Wassup.Battle.Movement.PathFollowState),
                typeof(Wassup.Battle.Effects.ModifierStats), typeof(Wassup.Battle.Effects.ModifierStatsDirty),
                typeof(Wassup.Battle.Units.IncomingDamage), typeof(Wassup.Battle.Effects.CcEffect),
                typeof(Wassup.Battle.Effects.DotEffect), typeof(Wassup.Battle.Combat.AttackOutputElement),
                typeof(Wassup.Battle.Combat.Projectile.ProjectileRef),
            };
            var missing = required.Where(t => !ShadowMirror.TypeMap.ContainsKey(t))
                                  .Select(t => t.FullName).ToList();
            CollectionAssert.IsEmpty(missing, "미매핑 스폰 컴포넌트:\n  " + string.Join("\n  ", missing));
        }

        [Test]
        public void 비대상_목록은_이유가_있는_둘_뿐이다()
        {
            // ⚠ 이 목록이 늘어나는 것은 그림자에서 그만큼 상태를 포기하는 것이다.
            CollectionAssert.AreEquivalent(
                new[] { typeof(Wassup.Battle.Units.SimEntityId), typeof(Wassup.Battle.BattleTimeScale) },
                ShadowMirror.Skip);
        }

        [Test]
        public void enableable_은_ModifierStatsDirty_하나다()
        {
            // 3상태→2상태 접힘 특례가 하나뿐이라는 전제(착수 노트 실측)를 굳힌다.
            var enableable = ShadowMirror.TypeMap.Keys.Where(ShadowMirror.IsEnableable)
                                         .Select(t => t.Name).ToList();
            CollectionAssert.AreEqual(new[] { "ModifierStatsDirty" }, enableable);
        }

        // ── 값 변환 ───────────────────────────────────────────────────────────

        [Test]
        public void 스칼라와_enum_이_그대로_옮겨진다()
        {
            var old = new Wassup.Battle.Units.Health { value = 37.5f, max = 120f };
            var neu = (Wassup.Sim.Units.Health)Convert(old, typeof(Wassup.Sim.Units.Health));
            Assert.AreEqual(37.5f, neu.value);
            Assert.AreEqual(120f, neu.max);

            var ft = new Wassup.Battle.Units.FactionTag { value = Wassup.Battle.Units.Faction.Enemy };
            var nft = (Wassup.Sim.Units.FactionTag)Convert(ft, typeof(Wassup.Sim.Units.FactionTag));
            Assert.AreEqual((int)Wassup.Battle.Units.Faction.Enemy, (int)nft.value,
                "enum 은 정수로 옮긴다 — 이름이 아니라 값이 계약이다");
        }

        [Test]
        public void 중첩_벡터가_옮겨진다()
        {
            var old = new Wassup.Battle.Combat.AttackState
            {
                range = 3.5f, cooldownRemaining = 0.25f, targetMask = 7,
                committedDirection = new float2(0.6f, -0.8f), hasCommittedDirection = 1,
            };
            var neu = (Wassup.Sim.Combat.AttackState)Convert(old, typeof(Wassup.Sim.Combat.AttackState));
            Assert.AreEqual(3.5f, neu.range);
            Assert.AreEqual(0.25f, neu.cooldownRemaining);
            Assert.AreEqual(7, neu.targetMask);
            Assert.AreEqual(0.6f, neu.committedDirection.x);
            Assert.AreEqual(-0.8f, neu.committedDirection.y);
            Assert.AreEqual(1, neu.hasCommittedDirection);
        }

        [Test]
        public void 구에만_있는_필드는_자동_탈락한다()
        {
            // ⚠ `LocalTransform.Rotation` — 방향이 "신 필드를 훑는다" 이므로 그냥 빠진다.
            //   반대로 훑으면 sim 에 없는 필드에서 터진다.
            LocalTransform old = LocalTransform.FromPositionRotationScale(
                new float3(1f, 2f, 3f), quaternion.Euler(0.3f, 0.4f, 0.5f), 1.75f);
            var neu = (Wassup.Sim.Movement.SimTransform)Convert(old, typeof(Wassup.Sim.Movement.SimTransform));
            Assert.AreEqual(1f, neu.Position.x);
            Assert.AreEqual(2f, neu.Position.y);
            Assert.AreEqual(3f, neu.Position.z);
            Assert.AreEqual(1.75f, neu.Scale, "⚠ Scale 은 실려야 한다 — #24 가 이 값을 움직인다(F5)");
        }

        [Test]
        public void 난수는_state_가_비트까지_같다()
        {
            // 생성자를 거치면 두 난수열이 갈린다(18-K/2b·5c/2 가 같은 함정을 밟았다).
            // 동명 필드 복사라 `state` 가 그대로 온다.
            var old = new Wassup.Battle.Combat.BombLauncherState
            {
                rng = new Unity.Mathematics.Random(12345u), fuseSec = 1.5f, aoeTileRange = 2,
            };
            var neu = (Wassup.Sim.Combat.BombLauncherState)
                Convert(old, typeof(Wassup.Sim.Combat.BombLauncherState));
            Assert.AreEqual(old.rng.state, neu.rng.state);
            Assert.AreEqual(1.5f, neu.fuseSec);
            Assert.AreEqual(2, neu.aoeTileRange);
        }

        [Test]
        public void 엔티티_참조는_해석기를_거친다()
        {
            var old = new Wassup.Battle.Units.IncomingDamage { amount = 9f, source = Entity.Null };
            var resolved = (Wassup.Sim.Units.IncomingDamage)ShadowMirror.ConvertStruct(
                old, typeof(Wassup.Sim.Units.IncomingDamage), _ => new SimEntityId(5));
            Assert.AreEqual(9f, resolved.amount);
            Assert.AreEqual(5, resolved.source.Value, "라이브 Entity 번호가 아니라 해석 결과가 실린다");
        }

        [Test]
        public void 모르는_모양은_던진다()
        {
            // ⚠ 조용히 기본값을 남기면 그림자가 다른 초기 상태에서 출발하고, 그 사실이
            //   골든이 갈릴 때까지 드러나지 않는다.
            Assert.Throws<InvalidOperationException>(
                () => ShadowMirror.ConvertStruct(new OldOdd { payload = "x" }, typeof(NewOdd), NoEntities));
        }

        private struct OldOdd { public string payload; }
        private struct NewOdd { public int payload; }

        // ── 라이브 → 그림자 왕복 ──────────────────────────────────────────────

        [Test]
        public void 라이브_컴포넌트를_읽어_그림자에_쓴다()
        {
            Entity live = _em.CreateEntity();
            _em.AddComponentData(live, new Wassup.Battle.Units.Health { value = 11f, max = 22f });

            object boxed = ShadowMirror.ReadComponent(_em, live, typeof(Wassup.Battle.Units.Health));
            Assert.IsNotNull(boxed);

            SimEntityId se = _sim.Create();
            ShadowMirror.SetSimComponent(_sim, se, typeof(Wassup.Sim.Units.Health),
                ShadowMirror.ConvertStruct(boxed, typeof(Wassup.Sim.Units.Health), NoEntities));

            Assert.IsTrue(_sim.TryGet(se, out Wassup.Sim.Units.Health hp));
            Assert.AreEqual(11f, hp.value);
            Assert.AreEqual(22f, hp.max);
        }

        [Test]
        public void 버퍼가_원소_순서까지_옮겨진다()
        {
            Entity live = _em.CreateEntity();
            DynamicBuffer<Wassup.Battle.Effects.CcEffect> buf =
                _em.AddBuffer<Wassup.Battle.Effects.CcEffect>(live);
            buf.Add(new Wassup.Battle.Effects.CcEffect
            { kind = Wassup.Battle.Effects.CcKind.Stun, remainingTime = 1f });
            buf.Add(new Wassup.Battle.Effects.CcEffect
            { kind = Wassup.Battle.Effects.CcKind.Slow, remainingTime = 2f, scalar = 0.5f });

            List<object> read = ShadowMirror.ReadBuffer(_em, live, typeof(Wassup.Battle.Effects.CcEffect)).ToList();
            Assert.AreEqual(2, read.Count);

            SimEntityId se = _sim.Create();
            IList target = ShadowMirror.AddSimBuffer(_sim, se, typeof(Wassup.Sim.Effects.CcEffect));
            Assert.IsNotNull(target);
            foreach (object o in read)
                target.Add(ShadowMirror.ConvertStruct(o, typeof(Wassup.Sim.Effects.CcEffect), NoEntities));

            List<Wassup.Sim.Effects.CcEffect> simBuf = _sim.GetBuffer<Wassup.Sim.Effects.CcEffect>(se);
            Assert.AreEqual(2, simBuf.Count);
            Assert.AreEqual((int)Wassup.Battle.Effects.CcKind.Stun, (int)simBuf[0].kind);
            Assert.AreEqual(1f, simBuf[0].remainingTime);
            Assert.AreEqual((int)Wassup.Battle.Effects.CcKind.Slow, (int)simBuf[1].kind);
            Assert.AreEqual(0.5f, simBuf[1].scalar, "순서와 값이 함께 보존된다");
        }

        // ── 엔티티 단위 루프 (골든 14세션을 태운 등급의 버그를 여기서 잡는다) ────

        private SimEntityId MirrorWhole(Entity live, out List<Type> unmapped)
        {
            var missed = new List<Type>();
            SimEntityId se = _sim.Create();
            ShadowMirror.MirrorEntity(_em, live, _sim, se, NoEntities, missed.Add);
            unmapped = missed;
            return se;
        }

        [Test]
        public void 크기0_태그는_값을_읽지_않고_존재만_옮긴다()
        {
            // ⚠ **첫 골든 실행이 정확히 여기서 죽었다.** `GetComponentData<T>` 는 필드가 없는
            //   타입에 `ArgumentException` 을 던진다 — 태그를 값으로 읽으려 했기 때문이다.
            //   루프가 인스턴스 메서드였을 때는 EditMode 가 이걸 볼 수 없었다.
            Entity live = _em.CreateEntity();
            _em.AddComponent<Wassup.Battle.Units.AttackUnitTag>(live);
            _em.AddComponent<Wassup.Battle.Units.DeadTag>(live);

            SimEntityId se = MirrorWhole(live, out List<Type> unmapped);
            CollectionAssert.IsEmpty(unmapped);
            Assert.IsTrue(_sim.Has<Wassup.Sim.Units.AttackUnitTag>(se));
            Assert.IsTrue(_sim.Has<Wassup.Sim.Units.DeadTag>(se));
        }

        [Test]
        public void 아키타입_전체가_한_번에_옮겨진다()
        {
            // presence-driven — 경로별 차이가 "어떤 컴포넌트가 붙어 있나" 뿐이라는 계약의 증인.
            Entity live = _em.CreateEntity();
            _em.AddComponentData(live, LocalTransform.FromPositionRotationScale(
                new float3(4f, 0f, 5f), quaternion.identity, 0.5f));
            _em.AddComponent<Wassup.Battle.Units.AttackUnitTag>(live);
            _em.AddComponentData(live, new Wassup.Battle.Units.Health { value = 50f, max = 50f });
            _em.AddComponentData(live, new Wassup.Battle.Units.FactionTag
            { value = Wassup.Battle.Units.Faction.Enemy });
            _em.AddComponentData(live, new Wassup.Battle.Movement.PathFollowState { speed = 2.5f });
            _em.AddBuffer<Wassup.Battle.Units.IncomingDamage>(live);

            SimEntityId se = MirrorWhole(live, out List<Type> unmapped);
            CollectionAssert.IsEmpty(unmapped, "미매핑이 있으면 그림자에 상태가 빠진다");

            Assert.IsTrue(_sim.TryGet(se, out Wassup.Sim.Movement.SimTransform xf));
            Assert.AreEqual(4f, xf.Position.x);
            Assert.AreEqual(0.5f, xf.Scale);
            Assert.IsTrue(_sim.Has<Wassup.Sim.Units.AttackUnitTag>(se));
            Assert.IsTrue(_sim.TryGet(se, out Wassup.Sim.Units.Health hp));
            Assert.AreEqual(50f, hp.max);
            Assert.IsTrue(_sim.TryGet(se, out Wassup.Sim.Movement.PathFollowState pf));
            Assert.AreEqual(2.5f, pf.speed);
            Assert.IsTrue(_sim.HasBuffer<Wassup.Sim.Units.IncomingDamage>(se), "빈 버퍼도 존재로");
        }

        [Test]
        public void Unity_전용_컴포넌트는_조용히_건너뛴다()
        {
            // `LocalToWorld`·`Simulate` 같은 것은 sim 상태가 아니다 — 미매핑으로 시끄러워지면
            // 진짜 누락 신호가 묻힌다.
            Entity live = _em.CreateEntity();
            _em.AddComponentData(live, new LocalToWorld { Value = float4x4.identity });
            _em.AddComponentData(live, new Wassup.Battle.Units.Health { value = 1f, max = 1f });

            MirrorWhole(live, out List<Type> unmapped);
            CollectionAssert.IsEmpty(unmapped, "Unity 네임스페이스는 후보가 아니다");
        }

        [Test]
        public void 비활성_enableable_은_부재로_옮겨진다()
        {
            // ⚠ 구 3상태 → 신 2상태 접힘. 스폰은 `ModifierStatsDirty` 를 **부착+비활성**으로
            //   두는데, 그대로 Set 하면 그림자가 첫 틱에 가짜 재집계를 돈다.
            Entity live = _em.CreateEntity();
            _em.AddComponent<Wassup.Battle.Effects.ModifierStatsDirty>(live);
            _em.SetComponentEnabled<Wassup.Battle.Effects.ModifierStatsDirty>(live, false);

            SimEntityId off = MirrorWhole(live, out _);
            Assert.IsFalse(_sim.Has<Wassup.Sim.Effects.ModifierStatsDirty>(off), "비활성 → 부재");

            _em.SetComponentEnabled<Wassup.Battle.Effects.ModifierStatsDirty>(live, true);
            SimEntityId on = MirrorWhole(live, out _);
            Assert.IsTrue(_sim.Has<Wassup.Sim.Effects.ModifierStatsDirty>(on), "활성 → 존재");
        }

        [Test]
        public void 비대상_컴포넌트는_옮기지_않는다()
        {
            Entity live = _em.CreateEntity();
            _em.AddComponentData(live, new Wassup.Battle.Units.SimEntityId { value = 3 });
            _em.AddComponentData(live, new Wassup.Battle.BattleTimeScale { Value = 0.5f });

            SimEntityId se = MirrorWhole(live, out List<Type> unmapped);
            CollectionAssert.IsEmpty(unmapped, "Skip 은 미매핑이 아니다");
            Assert.AreEqual(0, _sim.GetBuffer<Wassup.Sim.Units.IncomingDamage>(se)?.Count ?? 0);
        }

        [Test]
        public void 빈_버퍼도_부재가_아니라_빈_버퍼로_옮겨진다()
        {
            // ⚠ **부재 ≠ 빈 버퍼.** 스폰이 빈 채로 선부착하는 3종이 있고, 소비자가
            //   `HasBuffer` 로 분기하므로 이 구분이 규칙이다.
            Entity live = _em.CreateEntity();
            _em.AddBuffer<Wassup.Battle.Units.IncomingDamage>(live);

            SimEntityId se = _sim.Create();
            IList target = ShadowMirror.AddSimBuffer(_sim, se, typeof(Wassup.Sim.Units.IncomingDamage));
            foreach (object o in ShadowMirror.ReadBuffer(_em, live, typeof(Wassup.Battle.Units.IncomingDamage)))
                target.Add(ShadowMirror.ConvertStruct(o, typeof(Wassup.Sim.Units.IncomingDamage), NoEntities));

            Assert.IsTrue(_sim.HasBuffer<Wassup.Sim.Units.IncomingDamage>(se), "부재가 아니다");
            Assert.AreEqual(0, _sim.GetBuffer<Wassup.Sim.Units.IncomingDamage>(se).Count);
        }
    }
}
