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

            // bonus-wave-pull — `CreateEnemyEntity` 는 뷰 풀 스폰까지 간다(`EnsureMonoViewPools`).
            // 그 경로가 `BoardSpace` 의 **정적** Grid 를 읽는데, 앞선 테스트가 남기고 간 것이
            // 이미 파괴돼 있어 MissingReferenceException 이 난다. 살아 있는 Grid 를 물려
            // 이 픽스처 안에서만 유효하게 만든다(정적이라 원복 API 가 없다 — 다음 소비자는
            // 지금과 똑같이 자기 Grid 를 물린다).
            _gridGo = new GameObject("BakeTestGrid");
            Wassup.Core.BoardSpace.Configure(
                Unity.Mathematics.float3.zero, 1f, _gridGo.AddComponent<Grid>());
        }

        private GameObject _gridGo;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_gridGo != null) Object.DestroyImmediate(_gridGo);
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

        // ── bonus-wave-pull unit 0 — DefenderHunterTag 부착 «지점» 가드 ─────────
        //
        // ★이 세 테스트가 지키는 것은 태그의 **존재**가 아니라 **어디서 붙느냐**다.
        // `BakeNightmareMechanics` 는 `nightmareMechanics` 가 비면 조기 반환한다. 그래서 태그를
        // 그 안(BossTag 옆)에 두면 — 가장 자연스러운 자리다 — **메커닉 없는 사냥꾼에게 태그가
        // 안 붙는다.** 보스는 메커닉을 갖고 있어 무회귀이고 위 테스트들도 전부 초록인 채,
        // 「보너스 적이 방어유닛을 무시한다」만 조용히 남는다.
        // `DefenderHunterGateTests` 는 **시스템 게이트**만 본다(태그를 손으로 붙인다) —
        // bake 경로는 그쪽 범위 밖이라 여기가 유일한 EditMode 그물이다.
        private Entity InvokeCreateEnemy(AttackUnitData unitType)
        {
            var mi = typeof(BattleBridge).GetMethod("CreateEnemyEntity",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, "CreateEnemyEntity 를 찾지 못했다(이름 변경?)");
            return (Entity)mi.Invoke(_bridge, new object[] { unitType, Vector3.zero, -1, -1 });
        }

        // 메커닉이 **없는** 유닛 — 위 MakeUnit 과 갈리는 지점이 이 테스트의 전부다.
        private static AttackUnitData MakeBareUnit(EnemyTier tier, bool hunts)
        {
            var u = ScriptableObject.CreateInstance<AttackUnitData>();
            u.displayName = $"BareUnit_{tier}_{hunts}";
            u.health = 100f;
            u.tier = tier;
            u.huntsDefenders = hunts;
            u.nightmareMechanics = null;              // ★조기 반환 경로
            // 뷰 풀이 `_MainTex` 를 세팅하므로 그 프로퍼티가 있는 셰이더여야 한다
            // (`Unlit/Color` 는 없어서 에러 로그가 나고 테스트가 그걸 미처리로 잡는다).
            u.visualMaterial = new Material(Shader.Find("Unlit/Texture"));
            return u;
        }

        [Test]
        public void 메커닉_없는_사냥꾼도_DefenderHunterTag_를_받는다()
        {
            var unitType = MakeBareUnit(EnemyTier.Normal, hunts: true);
            var e = InvokeCreateEnemy(unitType);

            Assert.AreNotEqual(Entity.Null, e, "적 생성에 실패했다");
            Assert.IsTrue(_world.EntityManager.HasComponent<DefenderHunterTag>(e),
                "메커닉이 없는 사냥꾼에게 태그가 안 붙었다 — 부착 지점이 " +
                "BakeNightmareMechanics 안으로 들어갔다(그 메서드는 메커닉이 비면 조기 반환한다)");
            Assert.IsFalse(_world.EntityManager.HasComponent<BossTag>(e),
                "Normal 인데 BossTag 가 붙었다");

            Object.DestroyImmediate(unitType.visualMaterial);
            Object.DestroyImmediate(unitType);
        }

        [Test]
        public void 메커닉_없는_보스도_사냥_태그를_받는다()
        {
            var unitType = MakeBareUnit(EnemyTier.Boss, hunts: false);
            var e = InvokeCreateEnemy(unitType);

            Assert.IsTrue(_world.EntityManager.HasComponent<DefenderHunterTag>(e),
                "보스가 사냥 태그를 못 받았다 — 부착 조건이 (tier == Boss || huntsDefenders) 여야 한다");

            Object.DestroyImmediate(unitType.visualMaterial);
            Object.DestroyImmediate(unitType);
        }

        [Test]
        public void 사냥꾼도_보스도_아니면_태그가_없다()
        {
            var unitType = MakeBareUnit(EnemyTier.Normal, hunts: false);
            var e = InvokeCreateEnemy(unitType);

            Assert.IsFalse(_world.EntityManager.HasComponent<DefenderHunterTag>(e),
                "일반 적에 사냥 태그가 붙었다 — 적 전원이 방어유닛을 쫓는다");

            Object.DestroyImmediate(unitType.visualMaterial);
            Object.DestroyImmediate(unitType);
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

        // skill-layer-migration unit 8 — **이 그물이 뒤집혔다.**
        // 예전 이름은 `OnDeath_WithNonSplitPayload_IsLoudlyWarned` 였고, 적의 작별 선물이
        // 「소비자가 없어 거절」되는 것을 고정했다. 이제 열렸으므로 **구워져야** 한다.
        //
        // ⚠ 여는 데 필요했던 것이 술어 한 줄이 아니었다는 게 이 그물의 요점이다.
        // 자기 죽음 감지자는 방어유닛 전용 루프였고, 적은 「죽었고 칸을 안 쓰는 것」을
        // 치우는 **일반 루프**에서 파괴된다. 거기 라우팅을 붙이고 진영을 엔티티별로
        // 도출해서야 열린다 — 리터럴을 쓰면 적의 사후 폭발이 자기 진영을 때린다.
        [Test]
        public void OnDeath_WithNonSplitPayload_IsNowBakedForEnemies()
        {
            var aoeView = ScriptableObject.CreateInstance<Wassup.Data.ProjectileData>();
            var unitType = ScriptableObject.CreateInstance<AttackUnitData>();
            unitType.displayName = "TestOnDeathAoe";
            unitType.tier = EnemyTier.Elite;
            unitType.nightmareMechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.OnDeath },
                    // ⚠ SelfTileAoe 는 폭발 «뷰» 탄이 필수다(없으면 폭발 자체가 드롭된다).
                    // 진영과 무관한 저작 요건이라 여기서 채워야 이 그물이 진영만 본다.
                    payload = new DcPayloadSpec
                    {
                        kind = DcPayloadKind.SelfTileAoe, magnitude = 50f, tileRange = 2,
                        projectile = aoeView,
                    },
                },
            };
            var e = _world.EntityManager.CreateEntity();
            InvokeBake(e, unitType);
            Assert.IsTrue(_world.EntityManager.HasBuffer<DcTriggerSlot>(e),
                "적의 작별 선물이 슬롯으로 구워져야 한다 — 열린 문이 안 열렸다");
            var slots = _world.EntityManager.GetBuffer<DcTriggerSlot>(e);
            Assert.AreEqual(1, slots.Length);
            Assert.AreEqual(DcTriggerKind.OnDeath, slots[0].trigger);
            Assert.AreNotEqual(Wassup.Skills.SkillRegistry.LegacyArmId, slots[0].skillId,
                "스킬 레이어로 라우팅돼야 한다(0 이면 arm 을 찾다가 조용히 죽는다)");
            Object.DestroyImmediate(unitType); Object.DestroyImmediate(aoeView);
        }
    }
}
