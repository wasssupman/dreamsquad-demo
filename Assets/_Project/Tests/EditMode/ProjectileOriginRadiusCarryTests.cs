using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using Wassup.Battle.Combat.Projectile;
using Wassup.Data;
using Wassup.Bridge;

namespace Wassup.Tests.EditMode
{
    // distance-based-range unit 23b — **원점 반경이 «요청 → 상태» 를 실제로 건너오는가.**
    //
    // ★ 이 파일은 소스 정규식 그물의 **대체**다. 리뷰가 지적한 대로, 종전 그물은 생산자 파일과
    // 소비자 파일의 문자열만 봤고 **그 사이의 복사 지점**은 어떤 단언도 지나지 않았다 —
    // 그래서 `state.originBodyRadius` 대입이 `BallisticArcToPoint` 분기에만 있고 자기 자리 폭발
    // 4종이 전부 `SkyFall` 인 상태에서 **모든 그물이 초록이었다.** unit 23b 의 런타임 효과가 0 이었고,
    // `0` 이 「자리형」과 「안 실었다」를 겸직해 관측상으로도 완전히 조용했다.
    //
    // 정규식은 **형태**를 고정하지만 **값이 도달하는지**는 못 본다(초기화자에 남긴 채 뒤에서 0 으로
    // 덮어써도 통과한다). 그래서 여기서는 실제로 스폰하고 **읽어서 확인한다.**
    //
    // ⚠ **movement 를 여러 개 도는 것이 요점이다.** 하나만 보면 「그 분기만 복사한다」는 결함이
    // 정확히 다시 통과한다 — 그게 실제로 일어난 일이다.
    public class ProjectileOriginRadiusCarryTests
    {
        private World _world;
        private GameObject _go;
        private BattleBridge _bridge;
        private ProjectileData _projData;

        [SetUp]
        public void SetUp()
        {
            _world = new World("ProjectileOriginCarryTestWorld");
            // inactive 로 붙여 `Awake`/씬 의존 validation 을 건너뛴다(`EnemyTierBakeTests` 관용구).
            _go = new GameObject("BattleBridge_OriginCarry");
            _go.SetActive(false);
            _bridge = _go.AddComponent<BattleBridge>();
            SetField(_bridge, "_world", _world);
            SetField(_bridge, "_em", _world.EntityManager);

            // `SpawnProjectile` 이 `_projectileDataByIndex[req.dataIndex]` 를 읽는다.
            // ⚠ **null 로는 부족하다** — `BallisticArcToPoint` 분기가 `projData.minFlightTime` 을
            // 역참조한다(`BattleBridge:5614`). 실제 인스턴스를 준다(값은 이 테스트의 관심사가 아니다).
            _projData = ScriptableObject.CreateInstance<ProjectileData>();
            var list = (List<ProjectileData>)typeof(BattleBridge)
                .GetField("_projectileDataByIndex", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_bridge);
            list.Clear();
            list.Add(_projData);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_projData != null) Object.DestroyImmediate(_projData);
            if (_world != null && _world.IsCreated) _world.Dispose();
        }

        private static void SetField(object target, string name, object value)
            => typeof(BattleBridge)
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);

        private Entity Spawn(MovementKind movement, float originBodyRadius)
        {
            var mi = typeof(BattleBridge).GetMethod("SpawnProjectile",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, "SpawnProjectile 을 찾지 못했다(이름 변경?)");
            var req = new ProjectileSpawnRequest
            {
                movement         = movement,
                payload          = PayloadKind.TileAoe,
                origin           = Unity.Mathematics.float3.zero,
                impact           = Unity.Mathematics.float3.zero,
                damage           = 1f,
                visualScale      = 1f,
                dataIndex        = 0,
                impactTileRange  = 2,
                flightTime       = 0f,
                originBodyRadius = originBodyRadius,
            };
            return (Entity)mi.Invoke(_bridge, new object[] { req, Entity.Null });
        }

        // ★ **movement 전 분기가 원점을 나른다.** 하나라도 빠지면 그 경로의 자기 자리 폭발이
        // 조용히 칸 반폭으로 잘린다 — 그게 unit 23b 를 런타임 효과 0 으로 만든 결함이다.
        [TestCase(MovementKind.SkyFall)]
        [TestCase(MovementKind.BallisticArcToPoint)]
        [TestCase(MovementKind.GrenadeToCell)]
        public void OriginBodyRadius_SurvivesRequestToState_ForEveryMovement(MovementKind movement)
        {
            var e = Spawn(movement, 1.5f);
            Assert.AreNotEqual(Entity.Null, e, $"{movement}: 스폰 실패");
            Assert.IsTrue(_world.EntityManager.HasComponent<ProjectileState>(e), $"{movement}: 상태 없음");

            var st = _world.EntityManager.GetComponentData<ProjectileState>(e);
            Assert.AreEqual(1.5f, st.originBodyRadius, 1e-5f,
                $"{movement} 분기가 원점 반경을 안 나른다 — 이 경로의 자기 자리 폭발이 "
                + "칸 반폭(0.5)으로 잘린다. 복사는 «공통 초기화 블록»에 있어야 한다(분기 아님).");
        }

        // 「안 실었다」는 0 으로 남아야 한다 — 그게 자리형이고 곧 종전 동작이다.
        [Test]
        public void NotCarrying_StaysZero_SoDeliveryFormKeepsLegacyBehaviour()
        {
            var e = Spawn(MovementKind.SkyFall, 0f);
            var st = _world.EntityManager.GetComponentData<ProjectileState>(e);
            Assert.AreEqual(0f, st.originBodyRadius, 1e-6f,
                "0 은 「이 자리에 주인이 없다」이고 판정에서 칸 반폭으로 접힌다 — 값을 지어내면 안 된다");
        }

        // 판정 쪽 자와 짝을 맞춘다 — 실려 온 값이 실제로 반경을 바꾸는가(순수 함수 수준).
        [Test]
        public void CarriedOrigin_ActuallyWidensTheImpactPredicate()
        {
            // 반경 2 · 대상 몸 0.25 · 거리 3.0
            //   주인 없음(0 → 칸 0.5): 2 + 0.5 + 0.25 = 2.75 < 3.0 → 밖
            //   주인 몸 1.5:           2 + 1.5 + 0.25 = 3.75 > 3.0 → 안
            Assert.IsFalse(Wassup.Skills.SkillMath.ReachFromImpact(3.0f, 0f, 2f, 0f, 0.25f),
                "주인 없는 착탄은 칸 반폭이라 3.0 이 밖이어야 한다");
            Assert.IsTrue(Wassup.Skills.SkillMath.ReachFromImpact(3.0f, 0f, 2f, 1.5f, 0.25f),
                "폭심의 주인이 몸 1.5 면 3.0 이 안이어야 한다 — 여기가 거짓이면 배관이 끊긴 것이다");
        }
    }
}
