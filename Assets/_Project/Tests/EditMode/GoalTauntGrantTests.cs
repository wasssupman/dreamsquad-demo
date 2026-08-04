using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // goal-stability unit 3 — 도발 병존: 도발 전에 이미 AttackState 를 가진 적
    // (walk-only goal-grant, mask=Goal 단독)은 도발 중 Defender 비트 OR, 해제 시
    // 마스크만 원복(AttackState/outputs 유지). 기존 무공격 적 grant/strip 경로는 보존.
    public class GoalTauntGrantTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;

        [SetUp]
        public void SetUp()
        {
            _world = new World("GoalTauntGrantTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<TauntAttackGrantSystem>());
        }

        [TearDown]
        public void TearDown() => _world?.Dispose();

        private void Tick(float dt = 0.016f)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        [Test]
        public void AttacklessEnemy_GrantAndStrip_Unchanged()
        {
            // 기존 경로 보존 — 무공격 적: 도발 시 AttackState 부여, 해제 시 통째 제거.
            var enemy = _em.CreateEntity();
            _em.AddComponentData(enemy, new AggroAttackProfile { damage = 2f, cooldown = 1f, range = 1.5f });
            _em.AddComponentData(enemy, new Aggroed { guardian = Entity.Null });

            Tick();

            Assert.IsTrue(_em.HasComponent<AttackState>(enemy), "도발 grant");
            Assert.AreEqual((int)Faction.Defender, _em.GetComponentData<AttackState>(enemy).targetMask);
            Assert.AreEqual(0, _em.GetComponentData<TauntAttackGranted>(enemy).previousTargetMask,
                "무공격 적 grant 는 previousTargetMask 0 (해제 시 통째 제거 신호)");

            _em.RemoveComponent<Aggroed>(enemy);
            Tick();

            Assert.IsFalse(_em.HasComponent<AttackState>(enemy), "해제 시 AttackState 제거 (현행)");
            Assert.IsFalse(_em.HasComponent<TauntAttackGranted>(enemy));
        }

        [Test]
        public void GoalGrantedEnemy_TauntOrsDefender_AndRestoresMask()
        {
            // walk-only goal-grant 적: 도발 중 Goal|Defender, 해제 시 Goal 로 원복 + 컴포넌트 유지.
            var enemy = _em.CreateEntity();
            _em.AddComponentData(enemy, new AttackState
            {
                range = 1.5f, cooldownDuration = 1f, attackTargetCount = 1,
                targetMask = (int)Faction.Goal,
            });
            _em.AddBuffer<AttackOutputElement>(enemy);
            _em.AddComponentData(enemy, new Aggroed { guardian = Entity.Null });

            Tick();

            Assert.AreEqual((int)(Faction.Goal | Faction.Defender),
                _em.GetComponentData<AttackState>(enemy).targetMask, "도발 중 Defender 비트 OR");
            Assert.AreEqual((int)Faction.Goal,
                _em.GetComponentData<TauntAttackGranted>(enemy).previousTargetMask);

            _em.RemoveComponent<Aggroed>(enemy);
            Tick();

            Assert.IsTrue(_em.HasComponent<AttackState>(enemy), "스폰 grant 소유물 — 해제해도 유지");
            Assert.AreEqual((int)Faction.Goal, _em.GetComponentData<AttackState>(enemy).targetMask,
                "해제 시 원래 마스크(Goal 단독)로 원복");
            Assert.IsTrue(_em.HasBuffer<AttackOutputElement>(enemy), "outputs 도 유지");
            Assert.IsFalse(_em.HasComponent<TauntAttackGranted>(enemy));
        }

        [Test]
        public void NormalEnemy_WithDefenderBit_NotTouched()
        {
            // 일반 공격 적(Defender 비트 보유)은 도발 grant 대상이 아니다 — aggro sticky 소관.
            int mask = (int)(Faction.Defender | Faction.BlockingHazard | Faction.Goal);
            var enemy = _em.CreateEntity();
            _em.AddComponentData(enemy, new AttackState
            {
                range = 1.5f, cooldownDuration = 1f, attackTargetCount = 1,
                targetMask = mask,
            });
            _em.AddComponentData(enemy, new Aggroed { guardian = Entity.Null });

            Tick();

            Assert.IsFalse(_em.HasComponent<TauntAttackGranted>(enemy),
                "Defender 비트가 이미 있으면 grant 비대상");
            Assert.AreEqual(mask, _em.GetComponentData<AttackState>(enemy).targetMask, "마스크 무변경");
        }
    }
}
