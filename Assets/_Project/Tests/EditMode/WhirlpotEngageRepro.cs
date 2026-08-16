using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // 광역 근접 적의 sim 거동 가드. 저작값 pin 이 아니다(그건 WhirlpotAuthoringTests 소유).
    //
    // ★존재 이유 — 기존 `AggroAoeWidthTests` 는 `EnemyAiState` 를 **안 붙인다.** 그러면
    // AttackSystem 의 `aiStateLookup.HasComponent` 가 false 라 `stateAllowsFire` 가 무조건
    // true 가 되고, 「적은 Engaging|Standoff 에서만 발사」 게이트(AttackSystem.cs:933)를
    // **한 번도 통과시키지 않는다.** 라이브 적은 그 컴포넌트를 가지므로 그 차이를 여기서 메운다.
    //
    // 단언 4축: ① FSM 이 Engaging 에 닿는가 ② 발사가 성사되는가 ③ 시전자/아군을 제외하는가
    //           ④ 광역이 반경 경계까지 퍼지고 밖은 안 닿는가
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

        // ⚠ 이 값들은 에셋의 **사본이 아니라 대표값**이다. 초판 주석은 「Enemy_Whirlpot.asset 의
        // 저작값을 그대로 옮긴다」였는데, 밸런스를 한 번 조정하자마자 어긋났다(에셋 8/0.3 vs
        // 여기 5/0.6). 이 어셈블리는 AssetDatabase 를 안 쓰므로 에셋을 읽어 동기화할 수도 없다.
        //
        // 그래서 **역할을 갈랐다**: 저작값 pin 은 `WhirlpotAuthoringTests`(EditModeAssets) 소유이고,
        // 이 파일은 «광역 근접 적의 sim 거동»(FSM 발사 게이트 · 자기/아군 제외 · 반경 확산)만 본다.
        // 그 단언들은 구체적 수치와 무관하다 — 단 `Range` 만은 예외라 상수로 묶는다(반경 경계
        // 테스트의 배치 좌표가 이 값에 매여 있어서, 둘이 따로 놀면 조용히 무의미해진다).
        private const float Range = 2f;

        private Entity MakeWhirlpot(float3 pos)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            _em.AddComponentData(e, new Health { value = 320f, max = 320f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponent<AttackUnitTag>(e);
            _em.AddComponentData(e, new AttackState
            {
                range = Range,
                cooldownDuration = 0.6f,
                cooldownRemaining = 0f,
                attackTargetCount = 10,
                targetMask = EnemyTargetDefaults.Resolve(0),   // targetFactions 0 = 미저작 → 기본
                // 0 = 즉시 RESOLVE. 수동 Update 월드는 DeltaTime 이 0 이라 hitDelay 를 tick 할 수
                // 없으므로 이 어셈블리에서 0 이외의 값은 관측 불가다(그 축은 PlayMode 소관).
                hitDelaySec = 0f,
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
            var whirlpot = MakeWhirlpot(float3.zero);
            MakeDefender(new float3(1f, 0f, 0f));

            Tick();

            Assert.AreEqual(AiState.Engaging, _em.GetComponentData<EnemyAiState>(whirlpot).value,
                "FSM 이 Engaging 이 아니면 AttackSystem 의 stateAllowsFire 가 false 라 영영 발사하지 않는다.");
        }

        // ── 경계 ② 발사 ── 사용자 문장의 단언: 「멈춰는 있는데 데미지가 안 들어간다」
        [Test]
        public void Boundary2_AdjacentDefender_TakesDamage()
        {
            MakeWhirlpot(float3.zero);
            var defender = MakeDefender(new float3(1f, 0f, 0f));

            Tick();

            Assert.Greater(Hits(defender), 0,
                "★붙어 있는 방어유닛이 한 대도 안 맞는다 = 보고된 증상 재현.");
        }

        // ⚠ hitDelaySec > 0 대조군은 여기서 만들 수 없다 — 수동으로 Update 하는 월드는
        // DeltaTime 이 0 이라 hitDelayRemaining 이 영영 줄지 않는다. 그 축은 PlayMode 소관.

        // ── 자기 피해 ── 「셀프 데미지를 입는 느낌」 보고(2026-08-16)를 그대로 단언한다.
        // 방어유닛은 AttackState 가 없어 반격할 수 없으므로, 팽이 HP 가 줄면 출처는 자기 공격뿐이다.
        [Test]
        public void Whirl_DoesNotDamageItsOwnCaster()
        {
            var whirlpot = MakeWhirlpot(float3.zero);
            MakeDefender(new float3(1f, 0f, 0f));
            MakeDefender(new float3(0f, 0f, 1f));
            float hp0 = _em.GetComponentData<Health>(whirlpot).value;

            for (int i = 0; i < 5; i++) Tick();

            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(whirlpot).Length,
                "★팽이 자신의 IncomingDamage 에 항목이 들어갔다 = 회오리가 시전자를 때린다.");
            Assert.AreEqual(hp0, _em.GetComponentData<Health>(whirlpot).value, 0.001f,
                "★반격할 수 없는 방어유닛만 있는데 팽이 HP 가 줄었다.");
        }

        // 동료 적도 때리지 않는다 — 광역의 진영 술어. 「셀프」로 보이는 또 다른 후보다.
        [Test]
        public void Whirl_DoesNotDamageFellowEnemies()
        {
            MakeWhirlpot(float3.zero);
            var ally = MakeWhirlpot(new float3(1f, 0f, 0f));
            MakeDefender(new float3(0f, 0f, 1f));   // 발사 조건(사거리 안 방어유닛)
            float allyHp0 = _em.GetComponentData<Health>(ally).value;

            for (int i = 0; i < 5; i++) Tick();

            Assert.AreEqual(allyHp0, _em.GetComponentData<Health>(ally).value, 0.001f,
                "★회오리가 같은 진영 적을 때린다 — targetMask 가 무너진 것이다.");
        }

        // ── 경계 ③ 광역 ── 회오리가 반경 안 전원에 퍼지는가.
        [Test]
        public void Boundary3_WhirlSpreadsToEveryoneInRadius()
        {
            MakeWhirlpot(float3.zero);
            var near = MakeDefender(new float3(1f, 0f, 0f));
            // 좌표를 Range 에서 유도한다 — 리터럴로 두면 반경을 바꿨을 때 「경계」와 「밖」이
            // 조용히 둘 다 안쪽이 되어 테스트가 통과한 채 의미를 잃는다.
            var boundary = MakeDefender(new float3(Range, 0f, Range));       // Chebyshev == Range
            var outside = MakeDefender(new float3(Range + 2f, 0f, 0f));      // Chebyshev > Range

            Tick();

            Assert.Greater(Hits(near), 0, "최근접이 primary 다.");
            Assert.Greater(Hits(boundary), 0, $"반경 {Range} 대각도 회오리 안이다(Chebyshev).");
            Assert.AreEqual(0, Hits(outside), "반경 밖은 안 맞는다.");
        }
    }
}
