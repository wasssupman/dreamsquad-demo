using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // ⚠ 진단용 임시 재현 — Whirlpot 이 「멈추는데 데미지를 안 넣는다」는 보고를 경계별로 계측한다.
    //
    // 기존 AggroAoeWidthTests 는 EnemyAiState 를 **안 붙인다.** 그러면 AttackSystem 의
    // `aiStateLookup.HasComponent` 가 false 라 stateAllowsFire 가 무조건 true 가 되고,
    // FSM 게이트(AttackSystem.cs:933)를 한 번도 통과시키지 않는다. 라이브 적은 그 컴포넌트를
    // 가지므로 그 차이를 여기서 재현한다.
    //
    // 경계 3개를 각각 단언한다: ① FSM 이 Engaging 인가 ② 발사가 성사되는가 ③ 광역이 퍼지는가
    public class WhirlpotEngageRepro
    {
        private World _world;
        private EntityManager _em;

        [SetUp]
        public void SetUp()
        {
            _world = new World("WhirlpotReproWorld");
            _em = _world.EntityManager;
            _world.CreateSystem<EnemyAiStateSystem>();
            _world.CreateSystem<AttackSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated) _world.Dispose();
        }

        // 라이브 순서 재현: EnemyAiStateSystem(UpdateBefore Movement) → AttackSystem(UpdateAfter Movement).
        private void Tick()
        {
            _world.GetExistingSystem<EnemyAiStateSystem>().Update(_world.Unmanaged);
            _world.GetExistingSystem<AttackSystem>().Update(_world.Unmanaged);
        }

        // Enemy_Whirlpot.asset 의 저작값을 그대로 옮긴다. hitDelaySec 만 인자다 —
        // 로스터 23종 중 Whirlpot 만 0 이고 나머지 22종이 0.25~0.3 이라, 그 축을 분리해서 본다.
        private Entity MakeWhirlpot(float3 pos, float hitDelaySec)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            _em.AddComponentData(e, new Health { value = 320f, max = 320f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponent<AttackUnitTag>(e);
            _em.AddComponentData(e, new AttackState
            {
                range = 2f,
                cooldownDuration = 0.6f,
                cooldownRemaining = 0f,
                attackTargetCount = 10,
                targetMask = EnemyTargetDefaults.Resolve(0),   // targetFactions 0 = 미저작 → 기본
                hitDelaySec = hitDelaySec,
            });
            var ob = _em.AddBuffer<AttackOutputElement>(e);
            ob.Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 5f },
            });
            _em.AddComponentData(e, new EnemyBehavior
            {
                targetMode = EnemyTargetMode.Nearest,
                engageMovement = EngageMovement.Halt,
            });
            _em.AddComponentData(e, new EnemyTargetFilter
            {
                classMask = -1,
                priorityClass = 0,
                factionMask = EnemyTargetDefaults.Resolve(0),
            });
            // ★라이브에만 있고 기존 테스트에 없는 것.
            _em.AddComponentData(e, new EnemyAiState { value = AiState.Marching });
            return e;
        }

        private Entity MakeDefender(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddComponent<DefenderUnitTag>(e);
            _em.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private int Hits(Entity e) => _em.GetBuffer<IncomingDamage>(e).Length;

        // ── 경계 ① FSM ──
        [Test]
        public void Boundary1_FsmReachesEngaging_WhenDefenderAdjacent()
        {
            var whirlpot = MakeWhirlpot(float3.zero, hitDelaySec: 0f);
            MakeDefender(new float3(1f, 0f, 0f));

            Tick();

            Assert.AreEqual(AiState.Engaging, _em.GetComponentData<EnemyAiState>(whirlpot).value,
                "FSM 이 Engaging 이 아니면 AttackSystem 의 stateAllowsFire 가 false 라 영영 발사하지 않는다.");
        }

        // ── 경계 ② 발사 ── 사용자 문장의 단언: 「멈춰는 있는데 데미지가 안 들어간다」
        [Test]
        public void Boundary2_AdjacentDefender_TakesDamage()
        {
            MakeWhirlpot(float3.zero, hitDelaySec: 0f);
            var defender = MakeDefender(new float3(1f, 0f, 0f));

            Tick();

            Assert.Greater(Hits(defender), 0,
                "★붙어 있는 방어유닛이 한 대도 안 맞는다 = 보고된 증상 재현.");
        }

        // ⚠ hitDelaySec > 0 대조군은 여기서 만들 수 없다 — 수동으로 Update 하는 월드는
        // DeltaTime 이 0 이라 hitDelayRemaining 이 영영 줄지 않는다. 그 축은 PlayMode 소관.

        // ── 경계 ③ 광역 ── 회오리가 반경 안 전원에 퍼지는가.
        [Test]
        public void Boundary3_WhirlSpreadsToEveryoneInRadius()
        {
            MakeWhirlpot(float3.zero, hitDelaySec: 0f);
            var near = MakeDefender(new float3(1f, 0f, 0f));
            var diagonal = MakeDefender(new float3(2f, 0f, 2f));   // Chebyshev 2 = 반경 경계
            var outside = MakeDefender(new float3(4f, 0f, 0f));

            Tick();

            Assert.Greater(Hits(near), 0, "최근접이 primary 다.");
            Assert.Greater(Hits(diagonal), 0, "반경 2 대각도 회오리 안이다(Chebyshev).");
            Assert.AreEqual(0, Hits(outside), "반경 밖은 안 맞는다.");
        }
    }
}
