using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Battle.Combat;
using Wassup.Battle.Combat.Projectile;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-content-5 — **bake 와 드레인의 회귀 핀.**
    //
    // 왜 별도 파일인가: unit 1 의 완료 기준이 「순수 함수 테스트만으로는 드레인 분기 누락이
    // 전혀 안 잡힌다 — 그게 초판의 실제 결함이었다」고 못박았는데, `BoomerangTests`(순수)와
    // `PathHitRehitCooldownTests`(ProjectileState 를 손으로 조립)는 **둘 다 `SpawnProjectile`
    // 을 한 줄도 지나가지 않는다.** 드레인이 `origin`/`prevPos`/`direction`/`maxDistance` 를
    // 안 채우면 부메랑은 맵 원점 기준으로 움직이거나 태어나자마자 죽는데, 그 두 픽스처는
    // 전부 초록이다. 그래서 **브리지를 실제로 부르는** 픽스처가 따로 필요하다.
    //
    // Fixture 레시피는 PatternBakeTests 와 같다 — BattleBridge 는 [ExecuteAlways] 가 없어
    // EditMode 에서 Awake 가 안 돌므로 필요한 필드만 reflection 으로 주입한다.
    public class BoomerangBakeAndDrainTests
    {
        private World _world;
        private EntityManager _em;
        private GameObject _go;
        private BattleBridge _bridge;
        private ProjectileData _boomerangSo;
        private ProjectileData _homingSo;

        [SetUp]
        public void SetUp()
        {
            _world = new World("BoomerangBakeAndDrainTests");
            _em = _world.EntityManager;

            _go = new GameObject("BattleBridge_BoomerangBake");
            _go.SetActive(false);
            _bridge = _go.AddComponent<BattleBridge>();
            SetField(_bridge, "_world", _world);
            SetField(_bridge, "_em", _em);

            _boomerangSo = ScriptableObject.CreateInstance<ProjectileData>();
            _boomerangSo.id = "test_boomerang";
            _boomerangSo.flightMode = ProjectileFlightMode.Boomerang;
            _boomerangSo.speed = 6f;
            _boomerangSo.hitThreshold = 0.5f;
            _boomerangSo.pierceCount = 1;
            _boomerangSo.rehitCooldownSec = 0.3f;
            _boomerangSo.knockbackDistance = 1.5f;
            _boomerangSo.knockbackDuration = 0.3f;

            _homingSo = ScriptableObject.CreateInstance<ProjectileData>();
            _homingSo.id = "test_homing";
            _homingSo.flightMode = ProjectileFlightMode.Homing;   // 비수와 같은 저작
            _homingSo.speed = 10f;
            _homingSo.hitThreshold = 0.35f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_boomerangSo != null) Object.DestroyImmediate(_boomerangSo);
            if (_homingSo != null) Object.DestroyImmediate(_homingSo);
            _world?.Dispose();
        }

        // ── 드레인 (unit 1 완료 기준: «탄 하나를 실제로 스폰해 …») ────────────────

        private Entity Spawn(float3 origin, float2 dir, float speed, float maxDistance)
        {
            var req = new ProjectileSpawnRequest
            {
                movement = MovementKind.BoomerangReturn,
                payload = PayloadKind.PathHit,
                origin = origin,
                direction = dir,
                speed = speed,
                maxDistance = maxDistance,
                damage = 25f,
                hitThreshold = 0.5f,
                visualScale = 1f,
                // ⚠ dataIndex 는 **레지스트리 인덱스**다 — -1 은 드레인이 맨 앞에서 거절한다.
                // 탄 SO 를 실제로 등록해 얻는다(브리지가 bake 에서 하는 것과 같은 경로).
                dataIndex = RegisterProjectile(_boomerangSo),
            };
            var mi = typeof(BattleBridge).GetMethod("SpawnProjectile",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, "SpawnProjectile 을 찾지 못했다 — 시그니처가 바뀌었나?");
            return (Entity)mi.Invoke(_bridge, new object[] { req, Entity.Null });
        }

        [Test]
        public void Drain_FillsEveryFieldTheTrajectoryNeeds()
        {
            var origin = new float3(3f, 0f, -2f);
            var e = Spawn(origin, new float2(1f, 0f), speed: 6f, maxDistance: 4f);

            Assert.AreNotEqual(Entity.Null, e, "정상 저작이 거절되면 안 된다");
            var st = _em.GetComponentData<ProjectileState>(e);

            // ⚠ 이 넷이 초판에서 통째로 0 이었다(드레인 분기 자체가 없었다).
            Assert.AreEqual(origin.x, st.origin.x, 1e-4f, "origin = 발사점");
            Assert.AreEqual(origin.z, st.origin.z, 1e-4f);
            // prevPos 가 0 이면 첫 스윕 선분이 **맵 원점 → 발사점** 이 되어 그 선 위 적 전원을
            // 때린다(궤도가 content-4 리뷰 M3 에서 겪은 결함).
            Assert.AreEqual(origin.x, st.prevPos.x, 1e-4f, "prevPos = 발사점(원점 방사선 차단)");
            Assert.AreEqual(origin.z, st.prevPos.z, 1e-4f);
            Assert.AreEqual(1f, math.length(st.direction), 1e-4f, "발사 축은 정규화된다");
            Assert.AreEqual(4f, st.maxDistance, 1e-4f, "편도 거리");
            Assert.Greater(st.pierceRemaining, 0, "관통 예산은 탄 SO 가 준다");
        }

        [Test]
        public void Drain_RejectsDegenerateAuthoring_SoNoImmortalProjectile()
        {
            // 셋 다 «왕복 완료 조건이 영원히 거짓» → 재타격 탄은 예산도 안 깎아 불멸이 된다.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Boomerang cannot travel"));
            Assert.AreEqual(Entity.Null, Spawn(float3.zero, new float2(1f, 0f), speed: 0f, maxDistance: 4f),
                "속도 0");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Boomerang cannot travel"));
            Assert.AreEqual(Entity.Null, Spawn(float3.zero, new float2(1f, 0f), speed: 6f, maxDistance: 0f),
                "편도 거리 0");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Boomerang cannot travel"));
            Assert.AreEqual(Entity.Null, Spawn(float3.zero, float2.zero, speed: 6f, maxDistance: 4f),
                "축 0");
        }

        // ── bake (unit 3 완료 기준: «비수 무회귀를 자동 테스트로 고정») ────────────

        private DreamcatcherCard MakeCard(string id, DcPayloadKind payload, DcTriggerKind trigger,
                                          ProjectileData projectile, int tileRange, float magnitude = 25f)
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = id;
            card.type = CardType.Unit;
            card.mechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = trigger, period = 5 },
                    payload = new DcPayloadSpec
                    {
                        kind = payload,
                        magnitude = magnitude,
                        projectile = projectile,
                        tileRange = tileRange,
                    },
                },
            };
            return card;
        }

        private int RegisterProjectile(ProjectileData so)
        {
            var mi = typeof(BattleBridge).GetMethod("GetOrCreateProjectileDataIndex",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, "GetOrCreateProjectileDataIndex 를 찾지 못했다");
            return (int)mi.Invoke(_bridge, new object[] { so });
        }

        // 부착 preflight 는 host **종속** 조건을 본다 — `ProjectileToTarget` 은 「적을 겨누는
        // host」를 요구하므로(힐러 거절) 마스크가 없는 맨 엔티티는 내 가드에 닿기도 전에
        // 거절된다. 실제 방어유닛이 스폰 시 갖는 최소 상태를 여기서 재현한다.
        private Entity NewDefender()
        {
            var e = _em.CreateEntity();
            _em.AddComponent<Wassup.Battle.Units.DefenderUnitTag>(e);
            _em.AddComponentData(e, LocalTransform.FromPosition(float3.zero));
            _em.AddComponentData(e, new AttackState
            {
                range = 3f,
                cooldownDuration = 1f,
                targetMask = (int)Wassup.Battle.Units.Faction.EnemyUnit,
            });
            return e;
        }

        // **무회귀 핀** — 비수(호밍 탄)의 궤적 축이 종전 그대로여야 한다. `SpawnNeedleCarrier`
        // 가 하드코딩을 버리고 슬롯의 축을 존중하게 됐으므로, 이 값이 어긋나면 기존 카드가
        // 조용히 다른 탄이 된다.
        [Test]
        public void Bake_HomingCard_KeepsLegacyAxes()
        {
            var defender = NewDefender();
            var card = MakeCard("test_needle", DcPayloadKind.ProjectileToTarget,
                                DcTriggerKind.AttackN, _homingSo, tileRange: 4);
            _bridge.ApplyDreamcatcherCardToUnit(defender, card);

            var slots = _em.GetBuffer<DcTriggerSlot>(defender);
            Assert.AreEqual(1, slots.Length);
            Assert.AreEqual(MovementKind.HomingToEntity, slots[0].projectileMovement,
                "기존 카드의 궤적이 바뀌면 안 된다");
            Assert.AreEqual(PayloadKind.SingleSplash, slots[0].projectilePayload);
            Object.DestroyImmediate(card);
        }

        [Test]
        public void Bake_BoomerangCard_CarriesTheBoomerangAxes()
        {
            var defender = NewDefender();
            var card = MakeCard("test_boomerang_card", DcPayloadKind.ProjectileToTarget,
                                DcTriggerKind.AttackN, _boomerangSo, tileRange: 4);
            _bridge.ApplyDreamcatcherCardToUnit(defender, card);

            var slots = _em.GetBuffer<DcTriggerSlot>(defender);
            Assert.AreEqual(1, slots.Length);
            Assert.AreEqual(MovementKind.BoomerangReturn, slots[0].projectileMovement);
            Assert.AreEqual(PayloadKind.PathHit, slots[0].projectilePayload);
            Object.DestroyImmediate(card);
        }

        // 경로 스윕 탄에게 hitThreshold 0 은 «정상으로 날아갔다 돌아오는데 아무도 못 맞히는»
        // 탄이다 — 런타임 증거가 0줄이라 bake 가 안 막으면 영영 안 잡힌다(ECS 리뷰 H1).
        [Test]
        public void Bake_DirectionBinding_RejectsSilentlyUselessAuthoring()
        {
            var defender = NewDefender();
            _boomerangSo.hitThreshold = 0f;
            var card = MakeCard("test_blind_boomerang", DcPayloadKind.ProjectileToTarget,
                                DcTriggerKind.AttackN, _boomerangSo, tileRange: 4);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("hitThreshold<=0"));
            int handle = _bridge.ApplyDreamcatcherCardToUnit(defender, card);

            Assert.AreEqual(-1, handle, "붙은 게 없으면 -1");
            Assert.IsFalse(_em.HasBuffer<DcTriggerSlot>(defender)
                           && _em.GetBuffer<DcTriggerSlot>(defender).Length > 0);
            Object.DestroyImmediate(card);
        }

        [Test]
        public void Bake_DirectionBinding_RejectsZeroFlightDistance()
        {
            var defender = NewDefender();
            var card = MakeCard("test_zero_range_boomerang", DcPayloadKind.ProjectileToTarget,
                                DcTriggerKind.AttackN, _boomerangSo, tileRange: 0);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("날아가는 거리"));
            Assert.AreEqual(-1, _bridge.ApplyDreamcatcherCardToUnit(defender, card));
            Object.DestroyImmediate(card);
        }

        // 셀 바인딩 탄(하늘낙하·포물선)은 이 payload 에 미배선이다 — 발사 arm 이 착탄점을
        // 안 채워서 **보드 원점에 떨어진다.** 궤적을 저작에 개방한 것이 이 뒷문을 열었다.
        [Test]
        public void Bake_CellBindingProjectile_IsRejected_NotSilentlyDroppedAtOrigin()
        {
            var defender = NewDefender();
            var skyFallSo = ScriptableObject.CreateInstance<ProjectileData>();
            skyFallSo.id = "test_skyfall";
            skyFallSo.flightMode = ProjectileFlightMode.SkyFall;
            skyFallSo.speed = 5f;
            skyFallSo.hitThreshold = 0.5f;

            var card = MakeCard("test_cell_binding", DcPayloadKind.ProjectileToTarget,
                                DcTriggerKind.AttackN, skyFallSo, tileRange: 4);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("셀 바인딩 탄"));
            Assert.AreEqual(-1, _bridge.ApplyDreamcatcherCardToUnit(defender, card));
            Assert.IsFalse(_em.HasBuffer<DcTriggerSlot>(defender)
                           && _em.GetBuffer<DcTriggerSlot>(defender).Length > 0);

            Object.DestroyImmediate(card);
            Object.DestroyImmediate(skyFallSo);
        }

        // ── 잿불 bake (unit 4 완료 기준) ──────────────────────────────────────────

        [Test]
        public void Bake_SpawnHazard_WithoutHazardSo_IsLoudlyRejected()
        {
            var defender = NewDefender();
            var card = MakeCard("test_ember_null", DcPayloadKind.SpawnHazard,
                                DcTriggerKind.OnKill, projectile: null, tileRange: 0, magnitude: 0f);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("SpawnHazard without HazardSO"));
            Assert.AreEqual(-1, _bridge.ApplyDreamcatcherCardToUnit(defender, card));
            Assert.IsFalse(_em.HasBuffer<DcTriggerSlot>(defender)
                           && _em.GetBuffer<DcTriggerSlot>(defender).Length > 0,
                "슬롯이 붙으면 «부착됨» 으로 집계된다");
            Object.DestroyImmediate(card);
        }

        [Test]
        public void Bake_SpawnHazard_WithWrongTrigger_IsLoudlyRejected()
        {
            var defender = NewDefender();
            var hazard = ScriptableObject.CreateInstance<HazardSO>();
            var card = MakeCard("test_ember_wrong_trigger", DcPayloadKind.SpawnHazard,
                                DcTriggerKind.AttackN, projectile: null, tileRange: 0, magnitude: 0f);
            card.mechanics[0].payload.hazard = hazard;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("OnKill 만 배선"));
            Assert.AreEqual(-1, _bridge.ApplyDreamcatcherCardToUnit(defender, card));

            Object.DestroyImmediate(card);
            Object.DestroyImmediate(hazard);
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
