using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // elite-whirlpot unit 0 — 어그로와 광역 폭의 관계를 고정한다.
    //
    // 예전엔 `aggro-targeting` unit 8(MEDIUM 2)이 어그로된 적의 `attackTargetCount` 를 1 로
    // 접었고 **그 판단에 테스트가 없었다.** 그래서 「어그로가 적의 공격 형태를 바꾼다」는
    // 부작용(광역 적이 붙잡히면 단일 적이 되어 덜 때린다)이 아무 그물에도 걸리지 않았다.
    // 이번엔 두 축을 각각 못 박는다:
    //
    //   ① 광역 «폭» 은 어그로와 무관하다        ← 이 unit 이 바꾼 것
    //   ② primary «선정» 은 여전히 어그로가 지배 ← 절대 되돌리면 안 되는 것
    //
    // ② 가 load-bearing 인 이유: 「가디언이 사거리 밖이면 미발사」를 풀면, 가디언에게 걸어가는
    // 도중 옆 방어유닛이 사거리에 들어오는 순간 `EngageMovement.Halt` 로 멈춰 싸우고 가디언에
    // 영영 도착하지 않는다 — 어그로 루프(적이 스스로 가디언으로 보행해 겹쳐 정지)가 깨진다.
    public class AggroAoeWidthTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;

        [SetUp]
        public void SetUp()
        {
            _world = new World("AggroAoeWidthTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<AttackSystem>());
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated) _world.Dispose();
        }

        // 광역 근접 적. attackTargetCount 를 저작으로 받는다 — 이 축이 테스트의 주어다.
        private Entity MakeAoeEnemy(float3 pos, int attackTargetCount)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponent<AttackUnitTag>(e);
            _em.AddComponentData(e, new AttackState
            {
                range = 4f, cooldownDuration = 1f, cooldownRemaining = 0f,
                attackTargetCount = attackTargetCount,
                targetMask = (int)Faction.DefenderUnit,
            });
            var ob = _em.AddBuffer<AttackOutputElement>(e);
            ob.Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 7f },
            });
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

        // ─── ① 폭은 어그로와 무관하다 (이 unit 이 바꾼 것) ───

        [Test]
        public void AggroedEnemy_AoeStillReachesNeighbor_NotFoldedToSingleTarget()
        {
            var enemy = MakeAoeEnemy(float3.zero, attackTargetCount: 2);
            // 가디언을 **더 멀리** 둔다 — 이웃이 primary 로 뽑힐 수 없게 해서
            // 「가디언이 primary + 이웃이 보조」라는 조합만 관측되게 만든다.
            var guardian = MakeDefender(new float3(3f, 0f, 0f));
            var neighbor = MakeDefender(new float3(1f, 0f, 0f));
            _em.AddComponentData(enemy, new Aggroed { guardian = guardian });

            _simGroup.Update();

            Assert.Greater(Hits(guardian), 0,
                "어그로된 적의 primary 는 가디언이다(sticky override).");
            Assert.Greater(Hits(neighbor), 0,
                "★어그로여도 광역 폭은 줄지 않는다 — 이웃 방어유닛도 맞아야 한다. "
                + "0 이면 attackTargetCount 를 1 로 접는 로직이 되살아난 것이다.");
        }

        [Test]
        public void NotAggroed_Aoe_HitsBoth_Control()
        {
            var enemy = MakeAoeEnemy(float3.zero, attackTargetCount: 2);
            var far = MakeDefender(new float3(3f, 0f, 0f));
            var near = MakeDefender(new float3(1f, 0f, 0f));

            _simGroup.Update();

            Assert.Greater(Hits(near), 0, "비어그로 primary 는 최근접이다.");
            Assert.Greater(Hits(far), 0, "비어그로 광역이 둘째 대상까지 닿는다(대조군).");
        }

        [Test]
        public void AggroedEnemy_SingleTargetAuthoring_StillHitsOnlyGuardian()
        {
            // 저작이 1 이면 어그로와 무관하게 1 이다 — 이 unit 이 «광역이 아닌 적» 을
            // 광역으로 만들지 않았음을 고정한다(적 17종 중 12종이 count 1).
            var enemy = MakeAoeEnemy(float3.zero, attackTargetCount: 1);
            var guardian = MakeDefender(new float3(3f, 0f, 0f));
            var neighbor = MakeDefender(new float3(1f, 0f, 0f));
            _em.AddComponentData(enemy, new Aggroed { guardian = guardian });

            _simGroup.Update();

            Assert.Greater(Hits(guardian), 0, "가디언만 맞는다.");
            Assert.AreEqual(0, Hits(neighbor),
                "attackTargetCount 1 은 어그로와 무관하게 단일 대상이다.");
        }

        // ─── ② primary 선정의 배타성 (되돌리면 어그로 루프가 깨진다) ───

        [Test]
        public void AggroedEnemy_HoldsFire_WhenGuardianOutOfRange_EvenWithNeighborAdjacent()
        {
            var enemy = MakeAoeEnemy(float3.zero, attackTargetCount: 2);
            // 가디언은 사거리(4) 밖, 이웃은 바로 옆.
            var guardian = MakeDefender(new float3(20f, 0f, 0f));
            var neighbor = MakeDefender(new float3(1f, 0f, 0f));
            _em.AddComponentData(enemy, new Aggroed { guardian = guardian });

            _simGroup.Update();

            Assert.AreEqual(0, Hits(neighbor),
                "★가디언이 사거리 밖이면 **아무도** 때리지 않는다(미발사). 여기서 이웃이 맞으면 "
                + "sticky primary override 가 풀린 것이고, 적이 가디언에 도착하지 못한다.");
            Assert.AreEqual(0, Hits(guardian), "사거리 밖 가디언도 당연히 안 맞는다.");
        }

        // ─── ③ 몸이 큰 가디언의 «휘두르는데 안 맞는» 밴드 (2026-09-04 사용자 버그 보고) ───
        //
        // 증상: 배스티온(3×2 · 사거리 1 · 몸 1.5)이 공격 모션은 나가는데 피해가 0.
        // 재현 대상은 그 문장 그대로다 — **공격이 성사됐는데 IncomingDamage 가 비어 있다.**
        //
        // 두 술어가 갈려 있다:
        //   · 발사 게이트  = `AttackReach.InReach`(몸 기반 연속) → 1 + 1.5 + 0.25 = 2.75타일
        //   · 피해 대상 선정(가디언 분기) = `AggroTargeting` 의 `TileAoe.IsInRadius`
        //     (칸 기반 · **공격자 몸을 0.5 로 가정**) → 1 + 0.5 = 1.5칸
        // 그 사이(1.5, 2.75] 가 사각지대다. 배스티온은 **자기 몸 반경이 1.5** 라 옆구리에
        // 붙은 적이 이미 그 밴드 안이고, 그래서 «항상» 안 맞는 것처럼 보인다.
        private Entity MakeWideGuardian(float3 pos, float bodyRadius)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.DefenderUnit });
            _em.AddComponent<DefenderUnitTag>(e);
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponentData(e, new HitRadius { value = bodyRadius });
            _em.AddComponentData(e, new AggroCapacity { max = 2, held = 0 });
            _em.AddComponentData(e, new AttackState
            {
                range = 1f, cooldownDuration = 1f, cooldownRemaining = 0f,
                attackTargetCount = 1,
                targetMask = (int)Faction.EnemyUnit,
            });
            var ob = _em.AddBuffer<AttackOutputElement>(e);
            ob.Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 9f },
            });
            return e;
        }

        private Entity MakeTargetEnemy(float3 pos, float bodyRadius = 0.25f)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            _em.AddComponent<AttackUnitTag>(e);
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddComponentData(e, new HitRadius { value = bodyRadius });
            return e;
        }

        [Test]
        public void WideGuardian_DamagesEnemyTouchingItsBody_NotJustCellNeighbor()
        {
            // 배스티온의 옆구리에 붙은 적: 몸 1.5 + 상대 0.25 = 1.75타일에서 몸이 맞닿는다.
            // 게이트는 2.75 까지 허용하므로 **공격은 반드시 성사된다.**
            var bastion = MakeWideGuardian(float3.zero, bodyRadius: 1.5f);
            var enemy = MakeTargetEnemy(new float3(1.75f, 0f, 0f));

            _simGroup.Update();

            // 경계 계측 — 「발사했나」와 「맞혔나」를 갈라 본다. 둘을 뭉뚱그리면 증상이
            // «사거리 밖이라 안 쏨» 인지 «쏘고 못 맞힘» 인지 알 수 없다(사용자 보고는 후자).
            var st = _em.GetComponentData<AttackState>(bastion);
            Assert.Greater(st.cooldownRemaining, 0f,
                "가디언이 **발사조차** 안 했다 — 그러면 원인은 피해 선정이 아니라 게이트다.");
            Assert.Greater(Hits(enemy), 0,
                "★몸이 맞닿은 적에게 피해가 0 이다 — 발사 게이트(몸 기반 2.75)와 피해 대상 "
                + "선정(칸 기반 1.5)이 갈렸다. 가디언 분기가 공격자 몸을 모르는 술어를 쓴다.");
        }

        // 가디언의 도달 경계가 **게이트와 같은 곡선**인지 거리를 훑어 고정한다.
        // 몸 1.5 · 사거리 1 · 상대 몸 0.25 → 도달 2.75. 그 안은 전부 맞고 밖은 안 맞아야 한다.
        // (이 형태로 찍었기에 「경계가 정확히 1.5」라는 실측이 나왔고, 그 값이 옛 칸 술어의
        //  상수와 일치한다는 것이 진단의 결정적 근거였다.)
        [Test]
        public void WideGuardian_ReachCurve_MatchesGate()
        {
            float[] inside = { 0.9f, 1.51f, 1.75f, 2.0f, 2.7f };
            float[] outside = { 2.9f, 3.5f };
            var pattern = new System.Text.StringBuilder();
            foreach (float d in inside)
            {
                TearDown(); SetUp();
                MakeWideGuardian(float3.zero, bodyRadius: 1.5f);
                var e = MakeTargetEnemy(new float3(d, 0f, 0f));
                _simGroup.Update();
                if (Hits(e) == 0) pattern.Append("안쪽인데 MISS:").Append(d.ToString("0.00")).Append(' ');
            }
            foreach (float d in outside)
            {
                TearDown(); SetUp();
                MakeWideGuardian(float3.zero, bodyRadius: 1.5f);
                var e = MakeTargetEnemy(new float3(d, 0f, 0f));
                _simGroup.Update();
                if (Hits(e) > 0) pattern.Append("바깥인데 HIT:").Append(d.ToString("0.00")).Append(' ');
            }
            Assert.AreEqual(string.Empty, pattern.ToString().Trim(),
                "가디언 도달 곡선이 게이트(사거리+내몸+상대몸 = 2.75)와 갈렸다");
        }

        [Test]
        public void WideGuardian_AndPlainDefender_AgreeOnWhoIsHittable()
        {
            // 같은 자리·같은 사거리인데 **가디언이냐 아니냐**로 답이 갈리면 안 된다.
            // (비-가디언 분기는 이미 `AttackReach.InReach` 로 수렴돼 있다 — AttackSystem 주석
            //  「다중타격의 2번째 이후 대상도 첫 대상과 같은 술어를 지난다」와 같은 규율.)
            var guardian = MakeWideGuardian(float3.zero, bodyRadius: 1.5f);
            var enemyA = MakeTargetEnemy(new float3(1.75f, 0f, 0f));
            _simGroup.Update();
            int guardianHits = Hits(enemyA);

            TearDown();
            SetUp();
            var plain = MakeWideGuardian(float3.zero, bodyRadius: 1.5f);
            _em.RemoveComponent<AggroCapacity>(plain);   // 유일한 차이 = 가디언 여부
            var enemyB = MakeTargetEnemy(new float3(1.75f, 0f, 0f));
            _simGroup.Update();
            int plainHits = Hits(enemyB);

            Assert.AreEqual(plainHits > 0, guardianHits > 0,
                $"가디언 여부가 「누가 맞는가」를 바꿨다 — 일반 {plainHits}회 / 가디언 {guardianHits}회. "
                + "도발 능력은 대상 «선정 우선순위» 만 바꿔야지 사거리를 바꾸면 안 된다.");
        }
    }
}
