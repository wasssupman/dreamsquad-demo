using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Battle.Combat;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // elite-enemy-tier unit 0 — 이 spec 의 토대 계약: **특수 메커닉을 가진 «보스가 아닌 적»** 이
    // 성립하는가.
    //
    // 그 앞까지 `BakeNightmareMechanics` 는 「mechanics 가 비어있지 않다 = 보스」로 보고
    // BossTag·ThreatEntry·등장경보를 붙였다. BossTag 는 CC 면역(CcApplySystem·EffectSpawner)·
    // 어그로 면역(AggroStateSystem)·이동 분기(MovementSystem)·cleave 예외(AttackSystem)의 게이트라,
    // 엘리트에게 메커닉 하나를 주는 순간 그 전부가 딸려왔다.
    //
    // **bake 를 순수 함수로 추출해 테스트하지 않는다** — 걸리는 것은 EntityManager 를 만지는
    // 쪽뿐이므로 추출하면 한 번도 깨진 적 없는 절반에 초록불이 켜진다. reflection 으로 진짜
    // 메서드를 부르는 것이 요점이다(PatternBakeTests 와 같은 레시피·같은 fixture 근거).
    public class EnemyTierBakeTests
    {
        private World _world;
        private GameObject _go;
        private BattleBridge _bridge;

        [SetUp]
        public void SetUp()
        {
            _world = new World("EnemyTierBakeTests");
            _go = new GameObject("BattleBridge_EnemyTierBake");
            // inactive 상태에서 붙여 Awake/씬 의존 validation 을 실행하지 않는다.
            _go.SetActive(false);
            _bridge = _go.AddComponent<BattleBridge>();
            SetField(_bridge, "_world", _world);
            SetField(_bridge, "_em", _world.EntityManager);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _world?.Dispose();
        }

        private static void SetField(object target, string name, object value)
        {
            var f = target.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"{name} 필드를 찾지 못했다(이름 변경?)");
            f.SetValue(target, value);
        }

        private void InvokeBake(Entity entity, AttackUnitData unitType)
        {
            var mi = typeof(BattleBridge).GetMethod("BakeNightmareMechanics",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, "BakeNightmareMechanics 를 찾지 못했다(이름 변경?)");
            mi.Invoke(_bridge, new object[] { entity, unitType });
        }

        // 티어와 무관하게 arm 이 열려 있는 트리거(PeriodicTimer)로 만든다 —
        // 화이트리스트에 막혀 슬롯이 안 생기면 이 테스트가 티어를 검증하지 못한다.
        private static AttackUnitData MakeUnit(EnemyTier tier)
        {
            var u = ScriptableObject.CreateInstance<AttackUnitData>();
            u.displayName = $"TestUnit_{tier}";
            u.health = 100f;
            u.tier = tier;
            u.nightmareMechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.PeriodicTimer, periodSeconds = 1f },
                    payload = new DcPayloadSpec
                    {
                        kind = DcPayloadKind.AllyMoveSpeedAura,
                        magnitude = 20f, duration = 1.5f, tileRange = 3,
                    },
                },
            };
            return u;
        }

        [Test]
        public void EliteWithMechanic_GetsSlot_ButNoBossAttachments()
        {
            var em = _world.EntityManager;
            var e = em.CreateEntity();
            var unitType = MakeUnit(EnemyTier.Elite);

            InvokeBake(e, unitType);

            Assert.IsTrue(em.HasBuffer<DcTriggerSlot>(e),
                "엘리트도 메커닉 슬롯은 받아야 한다 — 못 받으면 특수 능력이 아예 안 돈다");
            Assert.AreEqual(1, em.GetBuffer<DcTriggerSlot>(e).Length);

            Assert.IsFalse(em.HasComponent<BossTag>(e),
                "엘리트에 BossTag 가 붙었다 — CC·어그로 면역이 딸려온다(이 spec 의 핵심 계약 위반)");
            Assert.IsFalse(em.HasBuffer<ThreatEntry>(e),
                "위협 테이블은 보스 전용 부속물이다");

            Object.DestroyImmediate(unitType);
        }

        [Test]
        public void BossWithMechanic_GetsSlotAndBossAttachments()
        {
            var em = _world.EntityManager;
            var e = em.CreateEntity();
            var unitType = MakeUnit(EnemyTier.Boss);

            InvokeBake(e, unitType);

            Assert.IsTrue(em.HasBuffer<DcTriggerSlot>(e));
            Assert.IsTrue(em.HasComponent<BossTag>(e), "보스는 BossTag 를 받아야 한다(무회귀)");
            Assert.IsTrue(em.HasBuffer<ThreatEntry>(e), "위협 테이블은 보스와 항상 동행한다");

            Object.DestroyImmediate(unitType);
        }

        // 폴백 검증 — 저작 누락(Normal)이 조용히 보스가 되지 않는다. 기존 적 14종이 이 경로다.
        [Test]
        public void NormalWithMechanic_GetsSlotOnly()
        {
            var em = _world.EntityManager;
            var e = em.CreateEntity();
            var unitType = MakeUnit(EnemyTier.Normal);

            InvokeBake(e, unitType);

            Assert.IsTrue(em.HasBuffer<DcTriggerSlot>(e));
            Assert.IsFalse(em.HasComponent<BossTag>(e));

            Object.DestroyImmediate(unitType);
        }

        [Test]
        public void NoMechanics_AttachesNothing()
        {
            var em = _world.EntityManager;
            var e = em.CreateEntity();
            var unitType = ScriptableObject.CreateInstance<AttackUnitData>();
            unitType.displayName = "TestPlainEnemy";

            InvokeBake(e, unitType);

            Assert.IsFalse(em.HasBuffer<DcTriggerSlot>(e));
            Assert.IsFalse(em.HasComponent<BossTag>(e));

            Object.DestroyImmediate(unitType);
        }

        // ── unit 5 — 분열 저작의 bake 거절 (spec 5_enemy_ondeath_split.md 완료 기준) ──
        // 이 세 분기는 `LogError` 뿐이라 실행 흔적이 콘솔 말고 없다. 그래서 여기서 못 박는다.

        private static AttackUnitData MakeSplitter(AttackUnitData child, float count)
        {
            var u = ScriptableObject.CreateInstance<AttackUnitData>();
            u.displayName = "TestSplitter";
            u.health = 100f;
            u.tier = EnemyTier.Elite;
            u.nightmareMechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.OnDeath },
                    payload = new DcPayloadSpec
                    {
                        kind = DcPayloadKind.SplitOnDeath,
                        magnitude = count,
                        splitUnit = child,
                    },
                },
            };
            return u;
        }

        [Test]
        public void Split_NullSplitUnit_IsLoudlyRejected()
        {
            var unitType = MakeSplitter(null, 2f);
            LogAssert.Expect(LogType.Error, new Regex("splitUnit 이 비었다"));
            InvokeBake(_world.EntityManager.CreateEntity(), unitType);
            Object.DestroyImmediate(unitType);
        }

        [Test]
        public void Split_CountBelowOne_IsLoudlyRejected()
        {
            var child = ScriptableObject.CreateInstance<AttackUnitData>();
            var unitType = MakeSplitter(child, 0f);
            LogAssert.Expect(LogType.Error, new Regex("< 1"));
            InvokeBake(_world.EntityManager.CreateEntity(), unitType);
            Object.DestroyImmediate(unitType); Object.DestroyImmediate(child);
        }

        // 조용한 clamp 는 clamp 를 둔 이유를 무력화한다 — 100 을 타이핑한 저작자가 8기를
        // 받고 아무 메시지도 못 받으면 안 된다(리뷰 A-M1).
        [Test]
        public void Split_CountAboveCap_IsLoudlyRejected()
        {
            var child = ScriptableObject.CreateInstance<AttackUnitData>();
            var unitType = MakeSplitter(child, 100f);
            LogAssert.Expect(LogType.Error, new Regex("잘린다"));
            InvokeBake(_world.EntityManager.CreateEntity(), unitType);
            Object.DestroyImmediate(unitType); Object.DestroyImmediate(child);
        }

        [Test]
        public void Split_CyclicChain_IsLoudlyRejected()
        {
            var unitType = ScriptableObject.CreateInstance<AttackUnitData>();
            unitType.displayName = "Ouroboros";
            unitType.tier = EnemyTier.Elite;
            unitType.nightmareMechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.OnDeath },
                    payload = new DcPayloadSpec
                    {
                        kind = DcPayloadKind.SplitOnDeath, magnitude = 2f, splitUnit = unitType,
                    },
                },
            };
            LogAssert.Expect(LogType.Error, new Regex("순환"));
            InvokeBake(_world.EntityManager.CreateEntity(), unitType);
            Object.DestroyImmediate(unitType);
        }

        // 적에게 소비자가 없는 OnDeath 조합은 침묵하지 않는다(방어유닛 쪽 SelfTileAoe 소비자는
        // WithAll<DeadTag, DefenderUnitTag> 라 적을 보지 않는다).
        [Test]
        public void OnDeath_WithNonSplitPayload_IsLoudlyWarned()
        {
            var unitType = ScriptableObject.CreateInstance<AttackUnitData>();
            unitType.displayName = "TestOnDeathAoe";
            unitType.tier = EnemyTier.Elite;
            unitType.nightmareMechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.OnDeath },
                    payload = new DcPayloadSpec { kind = DcPayloadKind.SelfTileAoe, magnitude = 50f, tileRange = 2 },
                },
            };
            LogAssert.Expect(LogType.Warning, new Regex("defender-gated|미개방"));
            InvokeBake(_world.EntityManager.CreateEntity(), unitType);
            Object.DestroyImmediate(unitType);
        }
    }
}
