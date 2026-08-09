using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Wassup.Battle.Effects;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // traversal-layers unit 0 — 셀 층 비트가 sim(FlowFieldSingleton)으로 넘어오는가.
    //
    // 이 unit 은 **값을 넘기기만** 한다. 소비자는 unit 2b 부터이므로, 여기서 검증할 것은
    // ⑴ 저작본이 정본이고 ⑵ 없으면 런타임과 같은 단일 정의로 파생되며 ⑶ walkMask 산출이
    // 그대로다(행동 변화 0) 세 가지다.
    public class CellLayersInstallTests
    {
        private World _world;
        private EntityManager _em;
        private SimFieldHandles _handles;

        [SetUp]
        public void SetUp()
        {
            _world = new World("CellLayersInstallTests");
            _em = _world.EntityManager;
            _handles = default;
        }

        [TearDown]
        public void TearDown()
        {
            SimFieldInstaller.Teardown(_world, _em, ref _handles);
            _world?.Dispose();
        }

        // 2x2: (0,0)=Walk (1,0)=Place (0,1)=Deco (1,1)=Walk, 골 = (1,1)
        private GeneratedMap MakeMap(bool withAuthoredMask, byte authoredValue = 0)
        {
            var gridSize = new int2(2, 2);
            int n = 4;
            var tiles = new NativeArray<MapTileType>(n, Allocator.Persistent);
            tiles[0] = MapTileType.Walk;
            tiles[1] = MapTileType.Place;
            tiles[2] = MapTileType.Deco;
            tiles[3] = MapTileType.Walk;

            var placeMask = default(NativeArray<byte>);
            if (withAuthoredMask)
            {
                placeMask = new NativeArray<byte>(n, Allocator.Persistent);
                for (int i = 0; i < n; i++) placeMask[i] = authoredValue;
            }

            var goals = new NativeArray<int2>(1, Allocator.Persistent);
            goals[0] = new int2(1, 1);
            var spawns = new NativeArray<int2>(1, Allocator.Persistent);
            spawns[0] = new int2(0, 0);

            return new GeneratedMap
            {
                tiles = tiles,
                placeMask = placeMask,
                spawns = spawns,
                goals = goals,
                goal = new int2(1, 1),
                gridSize = gridSize,
                generatorVersion = 1,
            };
        }

        private FlowFieldSingleton Install(GeneratedMap map)
        {
            SimFieldInstaller.InstallNavFields(_em, in map, tileSize: 1f, origin: float3.zero, ref _handles);
            return _em.GetComponentData<FlowFieldSingleton>(_handles.flowField);
        }

        [Test]
        public void AuthoredPlaceMask_IsCarriedIntoSim()
        {
            // 저작본이 정본이다 — 타일 종류와 직교하므로 파생값과 달라도 그대로 실린다.
            byte authored = (byte)(PlacementLayer.Ground | PlacementLayer.Path);
            var map = MakeMap(withAuthoredMask: true, authoredValue: authored);
            try
            {
                var field = Install(map);
                Assert.IsTrue(field.cellLayers.IsCreated);
                for (int i = 0; i < 4; i++)
                    Assert.AreEqual(authored, field.cellLayers[i], $"cell {i}");
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void AuthoredMask_IsSanitized()
        {
            // 정의되지 않은 비트는 떨어진다 — 런타임과 같은 규칙(PlacementLayers.Sanitize).
            var map = MakeMap(withAuthoredMask: true, authoredValue: 0xFF);
            try
            {
                var field = Install(map);
                Assert.AreEqual(PlacementLayers.CellBits, field.cellLayers[0],
                    "All(0xFF)은 유닛 쪽 표현이라 셀에서는 정의된 비트만 남는다");
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void NoAuthoredMask_DerivesFromTiles()
        {
            // 폴백은 런타임과 **같은 단일 정의**를 쓴다.
            var map = MakeMap(withAuthoredMask: false);
            try
            {
                var field = Install(map);
                Assert.AreEqual((byte)PlacementLayer.Path,   field.cellLayers[0], "Walk → Path");
                Assert.AreEqual((byte)PlacementLayer.Ground, field.cellLayers[1], "Place → Ground");
                Assert.AreEqual((byte)0,                     field.cellLayers[2], "Deco → 층 없음");
                Assert.AreEqual((byte)PlacementLayer.Path,   field.cellLayers[3], "Walk → Path");
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void CellLayers_LengthMatchesWalkMask()
        {
            var map = MakeMap(withAuthoredMask: false);
            try
            {
                var field = Install(map);
                Assert.AreEqual(field.walkMask.Length, field.cellLayers.Length);
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void WalkMask_IsUnchangedByThisUnit()
        {
            // 행동 변화 0 의 회귀 축. 층을 저작해도 walkMask 는 여전히 tiles==Walk 다.
            var map = MakeMap(withAuthoredMask: true, authoredValue: (byte)PlacementLayer.Ground);
            try
            {
                var field = Install(map);
                Assert.AreEqual((byte)1, field.walkMask[0], "Walk");
                Assert.AreEqual((byte)0, field.walkMask[1], "Place — 층을 저작해도 walk 는 아니다");
                Assert.AreEqual((byte)0, field.walkMask[2], "Deco");
                Assert.AreEqual((byte)1, field.walkMask[3], "Walk");
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void Teardown_DisposesCellLayers()
        {
            // ⚠ `IsCreated` 로는 해제를 확인할 수 없다. 여기 `field` 는 컴포넌트 **struct 의
            // 복사본**이고 `NativeArray.IsCreated` 는 그 복사본이 들고 있는 버퍼 포인터를 볼
            // 뿐이라, 원본이 Dispose 돼도 복사본에서는 계속 true 다.
            //
            // 실제 해제는 **접근이 던지는가**로 본다 — 메모리가 풀리고 세이프티 핸들이
            // 무효화되면 stale 복사본 접근이 예외를 던진다.
            var map = MakeMap(withAuthoredMask: false);
            try
            {
                var field = Install(map);
                Assert.IsTrue(field.cellLayers.IsCreated);
                Assert.DoesNotThrow(() => { var _ = field.cellLayers[0]; }, "해제 전엔 읽힌다");

                SimFieldInstaller.Teardown(_world, _em, ref _handles);

                Assert.Catch(() => { var _ = field.cellLayers[0]; },
                    "Teardown 이 cellLayers 를 해제해야 한다 (누수 없음)");
                Assert.IsFalse(_em.Exists(_handles.flowField) && _handles.flowField != Entity.Null,
                    "싱글턴 엔티티도 정리된다");
            }
            finally { map.Dispose(); }
        }

        // ── unit 1a — 슬롯 stride ──────────────────────────────────────────────

        [Test]
        public void Install_HasExactlyOneSlot_AndViewCoversWholeGrid()
        {
            // 지금은 슬롯 1개다. 이 등식이 «바이트 동일»의 근거 — 슬롯 뷰가 곧 전체 배열이다.
            var map = MakeMap(withAuthoredMask: false);
            try
            {
                var field = Install(map);
                Assert.AreEqual(1, field.MaskCount);
                Assert.AreEqual(4, field.CellCount);
                Assert.AreEqual(field.CellCount, field.FlowSlot(FlowFieldSingleton.PrimarySlot).Length);
                Assert.AreEqual(field.CellCount, field.DistSlot(FlowFieldSingleton.PrimarySlot).Length);
                Assert.AreEqual(field.flow.Length, field.MaskCount * field.CellCount);
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void PrimarySlot_RoutesThePathLayer()
        {
            // unit 1b 의 무변경 논거를 여기 고정한다: 현재 라우팅은 `walkMask`(= tiles==Walk)
            // 로 굽는데 `Walk` 는 `Path` 층을 연다(PlacementLayers.Derive). 따라서 1b 가
            // «(cellLayers & Path) != 0» 기반으로 갈아타도 **같은 집합**이 나온다.
            var map = MakeMap(withAuthoredMask: false);
            try
            {
                var field = Install(map);
                Assert.AreEqual((byte)PlacementLayer.Path, field.maskValues[FlowFieldSingleton.PrimarySlot]);
                for (int i = 0; i < field.CellCount; i++)
                {
                    bool walkable = field.walkMask[i] != 0;
                    bool opensPath = (field.cellLayers[i] & (byte)PlacementLayer.Path) != 0;
                    Assert.AreEqual(walkable, opensPath, $"cell {i} — walkMask 와 Path 층이 같은 집합");
                }
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void SlotFor_UnknownMask_FallsBackToPrimary()
        {
            // 마스크 미부착 엔티티·픽스처가 그대로 돌아야 한다(현행 동작 보존).
            var map = MakeMap(withAuthoredMask: false);
            try
            {
                var field = Install(map);
                Assert.AreEqual(FlowFieldSingleton.PrimarySlot,
                    field.SlotFor((byte)PlacementLayer.Ground), "등록 안 된 마스크 → primary");
                Assert.AreEqual(FlowFieldSingleton.PrimarySlot, field.SlotFor(0));
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void SlotFor_KnownMask_ReturnsItsSlot()
        {
            // unit 1b 가 마스크를 여러 개 실을 때의 계약. 여기선 손으로 구성해 술어만 본다.
            var masks = new NativeArray<byte>(2, Allocator.Temp);
            masks[0] = (byte)PlacementLayer.Ground;   // 오름차순(계약 5)
            masks[1] = (byte)PlacementLayer.Path;
            try
            {
                var field = new FlowFieldSingleton { maskValues = masks, gridSize = new int2(2, 2) };
                Assert.AreEqual(0, field.SlotFor((byte)PlacementLayer.Ground));
                Assert.AreEqual(1, field.SlotFor((byte)PlacementLayer.Path));
            }
            finally { masks.Dispose(); }
        }

        [Test]
        public void MaskCount_DefaultsToOne_WhenFixtureLeavesFlowUncreated()
        {
            // 직접 초기화하는 EditMode 픽스처 수십 개가 새 필드를 안 채워도 서야 한다 —
            // CellCount 를 gridSize 에서 파생하고 MaskCount 를 1 로 떨어뜨리는 이유다.
            var field = new FlowFieldSingleton { gridSize = new int2(4, 3) };
            Assert.AreEqual(12, field.CellCount);
            Assert.AreEqual(1, field.MaskCount);
            Assert.AreEqual(FlowFieldSingleton.PrimarySlot, field.SlotFor(123));
        }

        [Test]
        public void Teardown_IsIdempotent()
        {
            // 호출처 4곳에서 불리는 계약이다(CRITICAL #1). cellLayers 추가로 깨지지 않는지.
            var map = MakeMap(withAuthoredMask: false);
            try
            {
                Install(map);
                SimFieldInstaller.Teardown(_world, _em, ref _handles);
                Assert.DoesNotThrow(() => SimFieldInstaller.Teardown(_world, _em, ref _handles),
                    "두 번 불러도 double dispose 로 죽지 않는다");
            }
            finally { map.Dispose(); }
        }
    }
}
