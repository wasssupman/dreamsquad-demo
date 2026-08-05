using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode
{
    // battle-sim-extraction unit 15 — 배치 쿨타임이 **배치 판정 안에서** 걸리는지.
    //
    // 왜 이 테스트가 필요한가: 그 전까지 쿨타임 판정은 `DefenderSelector`(UI)의 딤 처리에만
    // 있었다. 즉 뷰를 거치지 않는 배치 경로 — 세션 커맨드·클릭 배치·테스트 — 는 쿨타임을
    // **통째로 무시**했다. 골든은 이것을 잡지 못한다(하네스는 유닛 타입마다 1회만 배치해서
    // 쿨타임이 발동할 일이 없고, 쿨타임은 정규 상태 라인에도 없다). 그래서 여기가 유일한 증인이다.
    public class PlacementCooldownGateTests
    {
        private GameObject _bridgeGo;
        private GameObject _gmGo;
        private BattleBridge _bridge;
        private GameManager _gm;
        private PlacementCooldownRuntime _cooldown;
        private DefenderUnitData _unit;
        private Material _unitMaterial;
        private GeneratedMap _map;
        private GameManager _previousInstance;

        [SetUp]
        public void SetUp()
        {
            // `GameManager.Awake` 는 이미 Instance 가 있으면 **자기 gameObject 를 Destroy** 한다.
            // EditMode 도메인은 앞선 테스트가 남긴 싱글턴을 품고 있을 수 있어, 그대로 두면 이
            // 픽스처의 GameManager 가 죽고 게이트가 남의 런타임(대개 null)을 읽어 조용히 통과한다.
            // 그래서 Instance 를 **이 테스트가 소유**하고 TearDown 에서 되돌린다.
            _previousInstance = GameManager.Instance;
            SetInstance(null);

            _gmGo = new GameObject("GameManager_PlacementCooldownGateTests");
            _gm = _gmGo.AddComponent<GameManager>();
            _cooldown = _gmGo.AddComponent<PlacementCooldownRuntime>();
            typeof(GameManager)
                .GetField("cooldownRuntime", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_gm, _cooldown);
            SetInstance(_gm);

            // 픽스처가 조용히 어긋나면 게이트 테스트는 "통과"로 위장된다 — 배선을 단정한다.
            Assert.AreSame(_gm, GameManager.Instance, "이 테스트가 GameManager.Instance 를 소유해야 한다");
            Assert.AreSame(_cooldown, GameManager.Instance.CooldownRuntime, "쿨타임 런타임 배선");

            _unit = ScriptableObject.CreateInstance<DefenderUnitData>();
            _unit.id = "gate_unit";
            _unit.displayName = "Gate Unit";
            _unit.cost = 0;                 // 코스트 게이트를 비껴가 쿨타임만 남긴다
            _unit.placementCooldown = 5f;
            // `CanPlaceDefenderAt` 은 visualMaterial 미할당을 InvalidUnit 으로 거절한다 —
            // 그 게이트가 먼저라 쿨타임까지 오지 못한다(픽스처 필수 조건).
            _unitMaterial = new Material(Shader.Find("Sprites/Default"));
            _unit.visualMaterial = _unitMaterial;

            _bridgeGo = new GameObject("BattleBridge_PlacementCooldownGateTests");
            _bridge = _bridgeGo.AddComponent<BattleBridge>();

            // 4×4 전부 Place — 공간 판정을 통과시켜 쿨타임 게이트만 검증한다.
            _map = new GeneratedMap
            {
                gridSize = new int2(4, 4),
                tiles = new NativeArray<MapTileType>(16, Allocator.Persistent),
                spawns = new NativeArray<int2>(1, Allocator.Persistent),
            };
            for (int i = 0; i < 16; i++) _map.tiles[i] = MapTileType.Place;

            SetField(_bridge, "_generatedMap", _map);
            SetField(_bridge, "_placementAllowed", true);
            _bridge.SetDefenderPool(new[] { _unit });
        }

        [TearDown]
        public void TearDown()
        {
            _map.Dispose();
            if (_bridgeGo != null) Object.DestroyImmediate(_bridgeGo);
            if (_gmGo != null) Object.DestroyImmediate(_gmGo);
            if (_unit != null) Object.DestroyImmediate(_unit);
            if (_unitMaterial != null) Object.DestroyImmediate(_unitMaterial);
            SetInstance(_previousInstance);
        }

        private static void SetInstance(GameManager value)
            => typeof(GameManager).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { value });

        [Test]
        public void Ready_Unit_Passes_The_Gate()
        {
            Assert.IsTrue(_bridge.CanPlaceDefenderAt(1, 1, _unit, out var reason));
            Assert.AreEqual(PlacementRejectReason.None, reason);
        }

        // 핵심 — 쿨타임 중인 유닛은 **배치 판정에서** 거절된다(UI 를 거치지 않아도).
        [Test]
        public void Unit_On_Cooldown_Is_Rejected_By_The_Rule_Not_The_View()
        {
            _cooldown.StartCooldown(_unit, _unit.placementCooldown);

            Assert.IsFalse(_bridge.CanPlaceDefenderAt(1, 1, _unit, out var reason));
            Assert.AreEqual(PlacementRejectReason.OnCooldown, reason,
                "쿨타임 거절이 배치 판정에서 나와야 한다 — 뷰 우회 경로를 막는 지점이다");
        }

        // 쿨타임이 끝나면 다시 통과한다(래치가 아니라 시간이다).
        [Test]
        public void Gate_Reopens_When_Cooldown_Elapses()
        {
            _cooldown.StartCooldown(_unit, 5f);
            Assert.IsFalse(_bridge.CanPlaceDefenderAt(1, 1, _unit, out _));

            _cooldown.Tick(5f);

            Assert.IsTrue(_bridge.CanPlaceDefenderAt(1, 1, _unit, out var reason));
            Assert.AreEqual(PlacementRejectReason.None, reason);
        }

        // `placementCooldown == 0` 은 inert — 쿨타임을 걸지 않으므로 연속 배치가 가능하다.
        [Test]
        public void Zero_Cooldown_Unit_Is_Never_Gated()
        {
            _unit.placementCooldown = 0f;
            _cooldown.StartCooldown(_unit, _unit.placementCooldown); // no-op 계약

            Assert.IsTrue(_bridge.CanPlaceDefenderAt(1, 1, _unit, out var reason));
            Assert.AreEqual(PlacementRejectReason.None, reason);
        }

        private static void SetField(object target, string name, object value)
        {
            var type = target.GetType();
            FieldInfo fi = null;
            while (fi == null && type != null)
            {
                fi = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance
                                       | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(fi, $"Field '{name}' not found on {target.GetType().Name}");
            fi.SetValue(target, value);
        }
    }
}
