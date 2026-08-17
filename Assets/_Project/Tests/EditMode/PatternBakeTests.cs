using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Battle.Combat;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Combat.Projectile.Emission;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // projectile-emission-pattern — bake 경로 게이트. 리뷰에서 잡힌 CRITICAL 하나가
    // 정확히 여기 있었다: PatternSlot 핸들을 AddBuffer 2회 뒤에 써서 dangling 이 됐고,
    // ENABLE_UNITY_COLLECTIONS_CHECKS(에디터 기본 on) 아래에서 예외가 SpawnUnit 위로
    // 던져져 그 프레임 스폰이 통째로 죽는 형태였다. 이 테스트를 수정 전 코드에 대고
    // 돌리면 그 자리에서 잡힌다.
    //
    // **bake 를 순수 함수로 추출해 테스트하지 않는다** — 물었던 버그는 EntityManager 를
    // 만지는 쪽에만 존재하므로, 추출하면 한 번도 깨진 적 없는 절반에 초록불이 켜진다.
    // reflection 으로 진짜 메서드를 부르는 것이 요점이다.
    //
    // Fixture: BattleBridge 는 [ExecuteAlways] 가 없어 EditMode AddComponent 로는
    // Awake/Start 가 돌지 않는다. bake 가 요구하는 상태는 _world/_em 둘뿐이라
    // reflection 주입으로 충분하다(BattleBridgeDraftMapTests 와 같은 레시피).
    public class PatternBakeTests
    {
        private World _world;
        private GameObject _go;
        private BattleBridge _bridge;
        private ProjectileData _barrel;
        private ProjectilePatternData _patternA, _patternB;

        [SetUp]
        public void SetUp()
        {
            _world = new World("PatternBakeTests");

            _go = new GameObject("BattleBridge_PatternBake");
            // inactive 상태에서 붙여 Awake/씬 의존 validation 을 실행하지 않는다.
            // 이 fixture 는 필요한 bake 필드만 아래 reflection 으로 주입한다.
            _go.SetActive(false);
            _bridge = _go.AddComponent<BattleBridge>();
            SetField(_bridge, "_world", _world);
            SetField(_bridge, "_em", _world.EntityManager);

            _barrel = ScriptableObject.CreateInstance<ProjectileData>();
            _barrel.id = "test_barrel";
            _barrel.flightMode = ProjectileFlightMode.BezierHoming;

            _patternA = MakePattern("pattern_a", 40f);
            _patternB = MakePattern("pattern_b", 150f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_barrel != null) Object.DestroyImmediate(_barrel);
            if (_patternA != null) Object.DestroyImmediate(_patternA);
            if (_patternB != null) Object.DestroyImmediate(_patternB);
            _world?.Dispose();
        }

        private ProjectilePatternData MakePattern(string id, float damage)
        {
            var p = ScriptableObject.CreateInstance<ProjectilePatternData>();
            p.id = id;
            p.barrel = _barrel;
            p.damage = damage;
            p.minAngleDeg = 0f;
            p.maxAngleDeg = 0f;
            p.shots = new[]
            {
                new ProjectileShotStep { directionT = 0.5f, intervalAfterPreviousSec = 0f },
            };
            return p;
        }

        private static DcMechanic PatternMechanic(ProjectilePatternData pattern, float periodSeconds)
            => new DcMechanic
            {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.PeriodicTimer, periodSeconds = periodSeconds },
                payload = new DcPayloadSpec { kind = DcPayloadKind.EmitProjectilePattern, pattern = pattern },
            };

        private void InvokeBake(Entity entity, AttackUnitData unitType)
        {
            var mi = typeof(BattleBridge).GetMethod("BakeNightmareMechanics",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, "BakeNightmareMechanics 를 찾지 못했다(이름 변경?)");
            mi.Invoke(_bridge, new object[] { entity, unitType });
        }

        private void InvokeDefenderBake(Entity entity, DefenderUnitData unitType, int barrelDataIndex)
        {
            var mi = typeof(BattleBridge).GetMethod("BakeDefenderDirectionalPattern",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, "BakeDefenderDirectionalPattern 를 찾지 못했다(이름 변경?)");
            mi.Invoke(_bridge, new object[] { entity, unitType, barrelDataIndex });
        }

        [Test]
        public void DefenderBake_AttachesDirectionPatternBuffers_AfterStructuralChanges()
        {
            var barrel = ScriptableObject.CreateInstance<ProjectileData>();
            barrel.id = "direction_barrel";
            barrel.flightMode = ProjectileFlightMode.Directional;
            barrel.speed = 20f;

            var pattern = ScriptableObject.CreateInstance<ProjectilePatternData>();
            pattern.id = "defender_direction";
            pattern.barrel = barrel;
            pattern.selection = PatternSelectionRule.None;
            pattern.minAngleDeg = -10f;
            pattern.maxAngleDeg = 10f;
            pattern.shots = new[]
            {
                new ProjectileShotStep { directionT = 0.25f, intervalAfterPreviousSec = 0f },
                new ProjectileShotStep { directionT = 0.75f, intervalAfterPreviousSec = 0.05f },
            };

            var ability = ScriptableObject.CreateInstance<DirectionalVolleyAbility>();
            ability.pattern = pattern;
            var unit = ScriptableObject.CreateInstance<DefenderUnitData>();
            unit.displayName = "DirectionDefender";
            unit.projectile = barrel;
            unit.abilities.Add(ability);

            var em = _world.EntityManager;
            var entity = em.CreateEntity();
            InvokeDefenderBake(entity, unit, 9);

            Assert.IsTrue(em.HasBuffer<PatternSlot>(entity));
            Assert.IsTrue(em.HasBuffer<EmitterInstance>(entity));
            var slots = em.GetBuffer<PatternSlot>(entity);
            Assert.AreEqual(1, slots.Length);
            Assert.AreEqual(9, slots[0].spec.barrelDataIndex);
            Assert.AreEqual(2, slots[0].spec.shots.Length);
            Assert.AreEqual(ProjectileTargetFaction.Enemy, slots[0].template.targetFaction);
            Assert.AreEqual(entity, slots[0].template.owner);

            Object.DestroyImmediate(unit);
            Object.DestroyImmediate(ability);
            Object.DestroyImmediate(pattern);
            Object.DestroyImmediate(barrel);
        }

        [Test]
        public void DefenderBake_MismatchedBarrel_IsRejectedLoudly()
        {
            var unitBarrel = ScriptableObject.CreateInstance<ProjectileData>();
            unitBarrel.flightMode = ProjectileFlightMode.Directional;
            var otherBarrel = ScriptableObject.CreateInstance<ProjectileData>();
            otherBarrel.flightMode = ProjectileFlightMode.Directional;
            var pattern = ScriptableObject.CreateInstance<ProjectilePatternData>();
            pattern.barrel = otherBarrel;
            pattern.selection = PatternSelectionRule.None;
            var ability = ScriptableObject.CreateInstance<DirectionalVolleyAbility>();
            ability.pattern = pattern;
            var unit = ScriptableObject.CreateInstance<DefenderUnitData>();
            unit.displayName = "MismatchedDefender";
            unit.projectile = unitBarrel;
            unit.abilities.Add(ability);

            var em = _world.EntityManager;
            var entity = em.CreateEntity();
            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("pattern barrel must match defender projectile"));
            InvokeDefenderBake(entity, unit, 2);

            Assert.IsFalse(em.HasBuffer<PatternSlot>(entity));
            Assert.IsFalse(em.HasBuffer<EmitterInstance>(entity));

            Object.DestroyImmediate(unit);
            Object.DestroyImmediate(ability);
            Object.DestroyImmediate(pattern);
            Object.DestroyImmediate(otherBarrel);
            Object.DestroyImmediate(unitBarrel);
        }

        // 패턴 2개 + 비패턴 1개 — 실제 보스 SO 형상(폭격·채찍질·미사일)의 축소판.
        [Test]
        public void Bake_AttachesPatternSlots_AndIndexesThem_LeavingNonPatternAtMinusOne()
        {
            var unitType = ScriptableObject.CreateInstance<AttackUnitData>();
            unitType.displayName = "TestBoss";
            unitType.health = 1000f;
            unitType.nightmareMechanics = new[]
            {
                PatternMechanic(_patternA, 10f),
                new DcMechanic // 비패턴 — patternIndex 가 -1 로 남아야 한다
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.PeriodicTimer, periodSeconds = 0.5f },
                    payload = new DcPayloadSpec
                    {
                        kind = DcPayloadKind.AllyMoveSpeedAura,
                        magnitude = 20f, duration = 0.6f, tileRange = 3,
                    },
                },
                PatternMechanic(_patternB, 0.5f),
            };

            var em = _world.EntityManager;
            var e = em.CreateEntity();

            // 이 호출이 dangling 핸들에 쓰면 여기서 터진다(수정 전 회귀 핀).
            InvokeBake(e, unitType);

            Assert.IsTrue(em.HasBuffer<PatternSlot>(e), "패턴 mechanic 이 있으면 PatternSlot 버퍼가 붙는다");
            Assert.IsTrue(em.HasBuffer<EmitterInstance>(e), "발사 인스턴스 버퍼도 사전 부착된다");

            var pats = em.GetBuffer<PatternSlot>(e);
            Assert.AreEqual(2, pats.Length, "패턴 mechanic 수만큼 슬롯이 쌓인다");

            var slots = em.GetBuffer<DcTriggerSlot>(e);
            Assert.AreEqual(3, slots.Length);
            Assert.AreEqual(0, slots[0].patternIndex);
            Assert.AreEqual(-1, slots[1].patternIndex, "비패턴 슬롯은 -1 (default 0 은 유효 index 라 오발사)");
            Assert.AreEqual(1, slots[2].patternIndex);

            Assert.AreEqual(40f, pats[0].spec.damage);
            Assert.AreEqual(150f, pats[1].spec.damage);
            Assert.AreEqual(1, pats[0].spec.shots.Length);
            Assert.AreEqual(0.5f, pats[0].spec.shots[0].directionT);
            Assert.AreEqual(0, pats[0].fireCountBase, "영속 카운터는 0 에서 시작한다");

            Object.DestroyImmediate(unitType);
        }

        // 패턴 mechanic 이 없으면 버퍼 자체가 붙지 않는다 — 기존 유닛 chunk 비용 0.
        [Test]
        public void Bake_WithoutPatternMechanic_DoesNotAttachPatternBuffers()
        {
            var unitType = ScriptableObject.CreateInstance<AttackUnitData>();
            unitType.displayName = "PlainBoss";
            unitType.health = 500f;
            unitType.nightmareMechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.PeriodicTimer, periodSeconds = 0.5f },
                    payload = new DcPayloadSpec
                    {
                        kind = DcPayloadKind.AllyMoveSpeedAura,
                        magnitude = 20f, duration = 0.6f, tileRange = 3,
                    },
                },
            };

            var em = _world.EntityManager;
            var e = em.CreateEntity();
            InvokeBake(e, unitType);

            Assert.IsFalse(em.HasBuffer<PatternSlot>(e));
            Assert.IsFalse(em.HasBuffer<EmitterInstance>(e));

            Object.DestroyImmediate(unitType);
        }

        // barrel 없는 패턴은 loud 거절 — 조용한 no-op 금지.
        [Test]
        public void Bake_PatternWithoutBarrel_IsRejectedLoudly()
        {
            var broken = ScriptableObject.CreateInstance<ProjectilePatternData>();
            broken.id = "no_barrel";
            broken.barrel = null;

            var unitType = ScriptableObject.CreateInstance<AttackUnitData>();
            unitType.displayName = "BrokenBoss";
            unitType.health = 100f;
            unitType.nightmareMechanics = new[] { PatternMechanic(broken, 1f) };

            var em = _world.EntityManager;
            var e = em.CreateEntity();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("EmitProjectilePattern needs a pattern with a barrel"));
            InvokeBake(e, unitType);

            Assert.AreEqual(0, em.GetBuffer<DcTriggerSlot>(e).Length, "거절된 mechanic 은 슬롯을 만들지 않는다");

            Object.DestroyImmediate(broken);
            Object.DestroyImmediate(unitType);
        }

        [TestCase("empty")]
        [TestCase("over_capacity")]
        [TestCase("reversed_angles")]
        public void Bake_InvalidShotSequence_IsRejectedLoudly(string invalidCase)
        {
            switch (invalidCase)
            {
                case "empty":
                    _patternA.shots = new ProjectileShotStep[0];
                    break;
                case "over_capacity":
                    _patternA.shots = new ProjectileShotStep[ProjectilePatternData.MaxShotCount + 1];
                    break;
                case "reversed_angles":
                    _patternA.minAngleDeg = 20f;
                    _patternA.maxAngleDeg = -20f;
                    break;
            }

            var unitType = ScriptableObject.CreateInstance<AttackUnitData>();
            unitType.displayName = "InvalidSequenceBoss";
            unitType.health = 100f;
            unitType.nightmareMechanics = new[] { PatternMechanic(_patternA, 1f) };

            var em = _world.EntityManager;
            var entity = em.CreateEntity();

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("invalid projectile shot sequence"));
            InvokeBake(entity, unitType);

            Assert.AreEqual(0, em.GetBuffer<DcTriggerSlot>(entity).Length);
            Assert.AreEqual(0, em.GetBuffer<PatternSlot>(entity).Length);

            Object.DestroyImmediate(unitType);
        }

        [Test]
        public void TryToSpec_SnapshotsAndClampsAuthoredSteps()
        {
            Assert.AreEqual(15, ProjectilePatternData.MaxShotCount,
                "8-byte step의 FixedList128Bytes 계약이 바뀌면 authoring 상한도 재검토한다");

            _patternA.minAngleDeg = -27.5f;
            _patternA.maxAngleDeg = 27.5f;
            _patternA.randomizeShotsPerTrigger = true;
            _patternA.randomIntervalMinSec = 0.006f;
            _patternA.randomIntervalMaxSec = 0.018f;
            _patternA.shots = new[]
            {
                new ProjectileShotStep { directionT = -1f, intervalAfterPreviousSec = -0.1f },
                new ProjectileShotStep { directionT = 2f, intervalAfterPreviousSec = 0.125f },
            };

            Assert.IsTrue(_patternA.TryToSpec(7, out var spec));
            Assert.AreEqual(7, spec.barrelDataIndex);
            Assert.AreEqual(-27.5f, spec.minAngleDeg);
            Assert.AreEqual(27.5f, spec.maxAngleDeg);
            Assert.AreEqual(2, spec.shots.Length);
            Assert.AreEqual(0f, spec.shots[0].directionT);
            Assert.AreEqual(0f, spec.shots[0].intervalAfterPreviousSec);
            Assert.AreEqual(1f, spec.shots[1].directionT);
            Assert.AreEqual(0.125f, spec.shots[1].intervalAfterPreviousSec);
            Assert.IsTrue(spec.randomizeShotsPerTrigger);
            Assert.AreEqual(0.006f, spec.randomIntervalMinSec);
            Assert.AreEqual(0.018f, spec.randomIntervalMaxSec);
        }

        [Test]
        public void TryToSpec_RejectsReversedRandomIntervalRange()
        {
            _patternA.randomizeShotsPerTrigger = true;
            _patternA.randomIntervalMinSec = 0.02f;
            _patternA.randomIntervalMaxSec = 0.01f;

            Assert.IsFalse(_patternA.TryToSpec(0, out _));
        }

        [Test]
        public void TryToSpec_RejectsSelectionAndMovementBindingMismatch()
        {
            _barrel.flightMode = ProjectileFlightMode.BezierHoming;
            _patternA.selection = PatternSelectionRule.None;
            Assert.IsFalse(_patternA.TryToSpec(0, out _),
                "타겟 추적탄에 None을 쓰면 조용히 전 발이 소모된다");

            _barrel.flightMode = ProjectileFlightMode.Directional;
            _patternA.selection = PatternSelectionRule.RoundRobin;
            Assert.IsFalse(_patternA.TryToSpec(0, out _),
                "방향탄에 타겟 선택 규칙을 남기면 무타겟 계약이 authoring에 드러나지 않는다");

            _patternA.selection = PatternSelectionRule.None;
            Assert.IsTrue(_patternA.TryToSpec(0, out _));
        }

        // dreamcatcher-content-5 unit 5 — 카드(defender) 경로 **개통**. 이 테스트는 여태
        // 「거절된다」를 고정했고(그때는 그것이 계약이었다) 이제 「실제로 붙고 쏜다」를 고정한다.
        // 핵심 단언은 여전히 «조용한 no-op 금지» 다 — 붙었다면 patternIndex 가 진짜 슬롯을
        // 가리켜야 하고, 아니면 붙지 말아야 한다.
        [Test]
        public void CardPath_EmitProjectilePattern_AttachesWithARealPatternSlot()
        {
            var em = _world.EntityManager;
            var defender = em.CreateEntity();
            em.AddComponent<Wassup.Battle.Units.DefenderUnitTag>(defender);

            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = "test_pattern_card";
            card.type = CardType.Unit;
            card.mechanics = new[] { PatternMechanic(_patternA, 1f) };

            // 반환값은 «부착 개수» 가 아니라 **오라 핸들**이다(오라 없으면 0). 아무것도
            // 안 붙었을 때만 -1 이라, 개통 여부의 단언은 «-1 이 아니다» 다.
            int handle = _bridge.ApplyDreamcatcherCardToUnit(defender, card);

            Assert.AreNotEqual(-1, handle, "붙었으면 -1 이 아니다 — 개통 전에는 -1 이었다");
            Assert.IsTrue(em.HasBuffer<DcTriggerSlot>(defender));
            var slots = em.GetBuffer<DcTriggerSlot>(defender);
            Assert.AreEqual(1, slots.Length);

            // 발동 arm 이 보는 세 조건(patternIndex >= 0 · 두 버퍼 존재)이 전부 성립해야
            // 실제로 발사된다. 하나라도 빠지면 «붙었는데 아무 일 없는» 카드가 된다.
            Assert.GreaterOrEqual(slots[0].patternIndex, 0, "슬롯이 진짜 패턴을 가리켜야 한다");
            Assert.IsTrue(em.HasBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(defender));
            Assert.IsTrue(em.HasBuffer<Wassup.Battle.Combat.Projectile.Emission.EmitterInstance>(defender));
            Assert.AreEqual(1,
                em.GetBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(defender).Length);

            Object.DestroyImmediate(card);
        }

        // ECS 리뷰 H2/M2 — 발동 arm 은 주기 트리거 하나뿐이라 다른 트리거로 저작하면
        // **붙는데 영영 안 터진다.** 거절이 패턴 슬롯 append 보다 **앞**이어야 주인 없는
        // 슬롯도 안 남는다(그게 M2 였다). 두 가지를 한 테스트로 고정한다.
        [Test]
        public void CardPath_NonPeriodicTrigger_IsRejected_AndLeavesNoPatternSlot()
        {
            var em = _world.EntityManager;
            var defender = em.CreateEntity();
            em.AddComponent<Wassup.Battle.Units.DefenderUnitTag>(defender);

            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = "attackn_pattern_card";
            card.type = CardType.Unit;
            var mech = PatternMechanic(_patternA, 1f);
            mech.trigger.kind = DcTriggerKind.AttackN;   // 발동 arm 이 없는 축
            mech.trigger.period = 3;
            card.mechanics = new[] { mech };

            LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("PeriodicTimer 만 배선"));
            int handle = _bridge.ApplyDreamcatcherCardToUnit(defender, card);

            Assert.AreEqual(-1, handle, "붙은 게 없으면 -1");
            Assert.IsFalse(em.HasBuffer<DcTriggerSlot>(defender)
                           && em.GetBuffer<DcTriggerSlot>(defender).Length > 0,
                "트리거 슬롯이 붙으면 안 된다");
            bool leaked = em.HasBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(defender)
                          && em.GetBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(defender).Length > 0;
            Assert.IsFalse(leaked, "거절됐는데 주인 없는 패턴 슬롯이 남으면 안 된다");

            Object.DestroyImmediate(card);
        }

        // 살아 있는 유닛에 두 장이 붙어도 서로의 슬롯을 지우지 않는다 — 유닛 스폰 경로를
        // 그대로 복붙하면(무조건 새 버퍼) 두 번째가 첫 번째를 밟아 첫 카드의 인덱스가
        // stale 이 된다. 카드는 «전투 중 살아 있는 엔티티» 에 붙는다는 것이 그 차이다.
        [Test]
        public void CardPath_TwoPatternCards_KeepIndependentSlots()
        {
            var em = _world.EntityManager;
            var defender = em.CreateEntity();
            em.AddComponent<Wassup.Battle.Units.DefenderUnitTag>(defender);

            var cardA = ScriptableObject.CreateInstance<DreamcatcherCard>();
            cardA.id = "pattern_card_a";
            cardA.type = CardType.Unit;
            cardA.mechanics = new[] { PatternMechanic(_patternA, 1f) };

            var cardB = ScriptableObject.CreateInstance<DreamcatcherCard>();
            cardB.id = "pattern_card_b";
            cardB.type = CardType.Unit;
            cardB.mechanics = new[] { PatternMechanic(_patternA, 2f) };

            _bridge.ApplyDreamcatcherCardToUnit(defender, cardA);
            _bridge.ApplyDreamcatcherCardToUnit(defender, cardB);

            var patternSlots = em.GetBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(defender);
            Assert.AreEqual(2, patternSlots.Length, "두 번째가 첫 번째를 지우면 안 된다");

            var slots = em.GetBuffer<DcTriggerSlot>(defender);
            Assert.AreEqual(2, slots.Length);
            Assert.AreNotEqual(slots[0].patternIndex, slots[1].patternIndex,
                "두 카드가 같은 슬롯을 가리키면 발사 카운터를 서로 밟는다");
            Assert.GreaterOrEqual(slots[0].patternIndex, 0);
            Assert.Less(slots[1].patternIndex, patternSlots.Length);

            Object.DestroyImmediate(cardA);
            Object.DestroyImmediate(cardB);
        }

        private static void SetField(object target, string name, object value)
        {
            var type = target.GetType();
            FieldInfo fi = null;
            while (fi == null && type != null)
            {
                fi = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance
                                       | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                type = type.BaseType;
            }
            Assert.IsNotNull(fi, $"field '{name}' 을 찾지 못했다");
            fi.SetValue(target, value);
        }
    }
}
