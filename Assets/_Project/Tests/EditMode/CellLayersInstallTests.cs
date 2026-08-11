using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
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

        // 3x1 전부 Walk — 스폰(0,0) → 골(2,0) 이 **직교로** 이어진다.
        // 2x2 픽스처는 스폰·골이 대각이라 코너컷 방지로 애초에 도달 불가라서
        // "경로가 살아 있나" 를 물을 수 없다(이 함정을 두 번 밟았다).
        private GeneratedMap MakeLinearMap()
        {
            int n = 3;
            var tiles = new NativeArray<MapTileType>(n, Allocator.Persistent);
            for (int i = 0; i < n; i++) tiles[i] = MapTileType.Walk;
            var placeMask = new NativeArray<byte>(n, Allocator.Persistent);
            for (int i = 0; i < n; i++) placeMask[i] = (byte)PlacementLayer.Path;
            var goals = new NativeArray<int2>(1, Allocator.Persistent);
            goals[0] = new int2(2, 0);
            var spawns = new NativeArray<int2>(1, Allocator.Persistent);
            spawns[0] = new int2(0, 0);
            return new GeneratedMap
            {
                tiles = tiles, placeMask = placeMask, spawns = spawns, goals = goals,
                goal = new int2(2, 0), gridSize = new int2(3, 1), generatorVersion = 1,
            };
        }

        private FlowFieldSingleton Install(GeneratedMap map)
        {
            SimFieldInstaller.InstallNavFields(_em, in map, tileSize: 1f, origin: float3.zero, ref _handles);
            return _em.GetComponentData<FlowFieldSingleton>(_handles.flowField);
        }

        [Test]
        public void AuthoredPlaceMask_IsIgnored_TraversalDerivesFromTiles()
        {
            // ⚠ 계약이 뒤집혔다 (1b 회귀 수리). 처음엔 저작된 `placeMask` 를 통행 층의
            // 정본으로 삼았는데 **틀렸다** — 그 저작의 의미는 «어느 유닛이 여기 설 수
            // 있나»(배치)이지 «지날 수 있나»(통행)가 아니다.
            //
            // 실측 근거: MapDocument_Test 는 `Walk` 칸 23개에 마스크 0 을 저작해 뒀다.
            // "여기 배치 금지"인데, 그걸 통행으로 읽으면 통로 23칸이 라우팅에서 사라진다.
            byte authored = (byte)PlacementLayer.Ground;   // 파생(Walk→Path)과 다르게 저작
            var map = MakeMap(withAuthoredMask: true, authoredValue: authored);
            try
            {
                var field = Install(map);
                Assert.AreEqual((byte)(PlacementLayer.Path | PlacementLayer.Air), field.cellLayers[0],
                    "Walk 칸은 저작이 뭐든 Path 로 파생된다 — 배치 저작이 통행을 바꾸지 않는다");
                Assert.AreEqual((byte)(PlacementLayer.Ground | PlacementLayer.Air),
                    field.cellLayers[1], "Place → Ground|Air");
                Assert.AreEqual((byte)PlacementLayer.Air, field.cellLayers[2], "Deco → Air");
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void CellLayers_AreTheNBitGeneralizationOfWalkMask()
        {
            // 파생만 쓰므로 두 정의가 **갈릴 수 없다**. 이것이 1b 회귀 수리의 요점 —
            // 라우팅(cellLayers)과 벽 충돌(walkMask)이 다른 지형을 보면 필드가 벽 안쪽을
            // 가리키고 trim 이 막는다("적이 통로에서 안 움직인다").
            var map = MakeMap(withAuthoredMask: true, authoredValue: 0xFF);   // 저작을 극단으로
            try
            {
                var field = Install(map);
                for (int i = 0; i < field.CellCount; i++)
                {
                    bool walkable = field.walkMask[i] != 0;
                    bool opensPath = (field.cellLayers[i] & (byte)PlacementLayer.Path) != 0;
                    Assert.AreEqual(walkable, opensPath, $"cell {i} — 저작이 뭐든 두 정의는 같다");
                }
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void SpawnAndGoalLayersClosed_FieldStillRoutes()
        {
            // ★ 라이브 경로 회귀. `BattleBridge` 는 필드를 굽기 **직전**(:1061-1070) 스폰·골
            // 칸의 `placeMask` 를 0 으로 닫는다(`CloseCellLayers` — "적이 튀어나오는 칸 위에
            // 방어유닛을 못 세운다"는 배치 불변식).
            //
            // unit 1b 초판은 `cellLayers = Sanitize(placeMask)` 였다. 그러면 **골의 통행 층이
            // 0** 이 되고 → `FlowFieldBuilder` 가 `walkMask[srcIdx]==0` 인 소스를 버려 →
            // **유효 소스 0 → 전 셀 MaxValue/zero → 적이 한 발도 안 움직인다.**
            //
            // 파생만 쓰면 배치 저작이 통행에 닿을 수 없어 구조적으로 불가능해진다.
            // 기존 테스트들이 이걸 못 잡은 이유: `GeneratedMap` 을 직접 만들어 폐쇄를 안 거친다.
            var map = MakeLinearMap();
            try
            {
                // 라이브가 하는 짓을 그대로 — 스폰(0,0)과 골(2,0)의 배치 층을 닫는다.
                map.placeMask[map.CellIndex(new int2(0, 0))] = 0;
                map.placeMask[map.CellIndex(new int2(2, 0))] = 0;

                var field = Install(map);
                var dist = field.DistSlot(FlowFieldSingleton.PrimarySlot);

                Assert.AreEqual(0, dist[2], "골은 배치가 닫혀도 통행 가능해야 한다 — dist 0");
                Assert.AreNotEqual(int.MaxValue, dist[0],
                    "스폰에서 골까지 경로가 살아 있어야 한다 (초판이면 여기가 MaxValue)");
                Assert.AreEqual(20, dist[0], "직교 2칸 = 10+10");
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
                Assert.AreEqual((byte)(PlacementLayer.Path | PlacementLayer.Air),
                    field.cellLayers[0], "Walk → Path|Air");
                Assert.AreEqual((byte)(PlacementLayer.Ground | PlacementLayer.Air),
                    field.cellLayers[1], "Place → Ground|Air");
                Assert.AreEqual((byte)PlacementLayer.Air,
                    field.cellLayers[2], "Deco → Air");
                Assert.AreEqual((byte)(PlacementLayer.Path | PlacementLayer.Air),
                    field.cellLayers[3], "Walk → Path|Air");
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
                Assert.AreEqual(1, field.SlotCount);
                Assert.AreEqual(4, field.CellCount);
                Assert.AreEqual(field.CellCount, field.FlowSlot(FlowFieldSingleton.PrimarySlot).Length);
                Assert.AreEqual(field.CellCount, field.DistSlot(FlowFieldSingleton.PrimarySlot).Length);
                Assert.AreEqual(field.flow.Length, field.SlotCount * field.CellCount);
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
                    field.SlotFor(FlowFieldSingleton.GoalSentinel, (byte)PlacementLayer.Ground),
                    "등록 안 된 마스크 → primary");
                Assert.AreEqual(FlowFieldSingleton.PrimarySlot,
                    field.SlotFor(FlowFieldSingleton.GoalSentinel, 0));
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
                Assert.AreEqual(0,
                    field.SlotFor(FlowFieldSingleton.GoalSentinel, (byte)PlacementLayer.Ground));
                Assert.AreEqual(1,
                    field.SlotFor(FlowFieldSingleton.GoalSentinel, (byte)PlacementLayer.Path));
            }
            finally { masks.Dispose(); }
        }

        [Test]
        public void SlotCount_DefaultsToOne_WhenFixtureLeavesFlowUncreated()
        {
            // 직접 초기화하는 EditMode 픽스처 수십 개가 새 필드를 안 채워도 서야 한다 —
            // CellCount 를 gridSize 에서 파생하고 SlotCount 를 1 로 떨어뜨리는 이유다.
            var field = new FlowFieldSingleton { gridSize = new int2(4, 3) };
            Assert.AreEqual(12, field.CellCount);
            Assert.AreEqual(1, field.SlotCount);
            Assert.AreEqual(FlowFieldSingleton.PrimarySlot,
                field.SlotFor(FlowFieldSingleton.GoalSentinel, 123));
        }

        // ── unit 1b — 마스크 집합 → 슬롯 N개 ──────────────────────────────────

        [Test]
        public void TwoSlots_EachRoutesItsOwnLayer()
        {
            // 슬롯이 2개면 라우팅이 2벌이고, 각자 «셀 층 ∩ 자기 마스크» 로 굽는다.
            // Path 슬롯은 Walk 칸만, Ground 슬롯은 Place 칸만 걸을 수 있다.
            var masks = new NativeArray<byte>(2, Allocator.Temp);
            masks[0] = (byte)PlacementLayer.Ground;   // 오름차순(계약 5)
            masks[1] = (byte)PlacementLayer.Path;
            var map = MakeMap(withAuthoredMask: false);
            try
            {
                SimFieldInstaller.InstallNavFields(_em, in map, 1f, float3.zero, ref _handles, masks);
                var field = _em.GetComponentData<FlowFieldSingleton>(_handles.flowField);

                Assert.AreEqual(2, field.SlotCount);
                Assert.AreEqual(field.CellCount * 2, field.flow.Length, "stride");

                // waypoint-routing unit 1 — 입력 순서와 무관하게 골 Path 슬롯은 primary 고정.
                int pathSlot = field.SlotFor(FlowFieldSingleton.GoalSentinel, (byte)PlacementLayer.Path);
                int groundSlot = field.SlotFor(FlowFieldSingleton.GoalSentinel, (byte)PlacementLayer.Ground);
                Assert.AreEqual(FlowFieldSingleton.PrimarySlot, pathSlot);

                // 골(1,1)은 Walk 라 Path 층에서만 도달 가능하다.
                var pathDist   = field.DistSlot(pathSlot);
                var groundDist = field.DistSlot(groundSlot);
                int goalIdx = 3;   // (1,1)
                // 결과가 다르다 = 두 슬롯이 같은 stride 를 앨리어싱하지 않는다.
                Assert.AreEqual(0, pathDist[goalIdx], "Path 슬롯: 골이 자기 층 위라 dist 0");
                Assert.AreEqual(int.MaxValue, groundDist[goalIdx],
                    "Ground 슬롯: 골 칸이 자기 층을 안 열어 도달 불가");

                // ⚠ 다른 셀로 «라우팅이 다르다»를 더 보이려 하지 말 것 — 이 2x2 픽스처에서
                // (0,0)은 두 슬롯 모두 도달 불가다(골과 대각인데 코너컷 방지로 막힌다).
                // 처음 쓴 `AreNotEqual(pathDist[0], groundDist[0])` 이 그래서 빨갛게 났고,
                // 사실 위 두 줄이 이미 같은 것을 증명하고 있었다.
            }
            finally { masks.Dispose(); map.Dispose(); }
        }

        [Test]
        public void SingleDefaultSlot_MatchesWalkMaskRouting()
        {
            // unit 1b 의 무변경 축. 슬롯을 명시하지 않으면 DefaultMask(Path) 1개이고,
            // 그 walk 집합이 walkMask(= tiles==Walk)와 **셀 단위로 같아야** 한다.
            var map = MakeMap(withAuthoredMask: false);
            try
            {
                var field = Install(map);
                Assert.AreEqual(1, field.SlotCount);
                Assert.AreEqual(TraversalSlots.DefaultMask, field.MaskAt(FlowFieldSingleton.PrimarySlot));

                var slotWalk = new NativeArray<byte>(field.CellCount, Allocator.Temp);
                try
                {
                    TraversalSlots.FillWalkMask(in field.cellLayers, field.MaskAt(0), slotWalk);
                    for (int i = 0; i < field.CellCount; i++)
                        Assert.AreEqual(field.walkMask[i], slotWalk[i], $"cell {i}");
                }
                finally { slotWalk.Dispose(); }
            }
            finally { map.Dispose(); }
        }

        [Test]
        public void FillWalkMask_IsTheIntersectionRule()
        {
            // 이 spec 의 정의식이 한 곳에만 있는지 — (셀 층 & 슬롯 마스크) != 0
            var layers = new NativeArray<byte>(4, Allocator.Temp);
            var outMask = new NativeArray<byte>(4, Allocator.Temp);
            try
            {
                layers[0] = (byte)PlacementLayer.Ground;
                layers[1] = (byte)PlacementLayer.Path;
                layers[2] = (byte)(PlacementLayer.Ground | PlacementLayer.Path);
                layers[3] = 0;

                TraversalSlots.FillWalkMask(in layers, (byte)PlacementLayer.Path, outMask);
                Assert.AreEqual(0, outMask[0], "Ground 칸은 Path 유닛이 못 지난다");
                Assert.AreEqual(1, outMask[1]);
                Assert.AreEqual(1, outMask[2], "두 층을 다 여는 칸은 둘 다 지난다");
                Assert.AreEqual(0, outMask[3], "층을 안 여는 칸은 아무도 못 지난다");
            }
            finally { layers.Dispose(); outMask.Dispose(); }
        }

        [Test]
        public void MultiSlot_InstallTeardownReinstall_NoLeak()
        {
            // ECS 리뷰 M3 — 다중 슬롯이 unit 1b 의 핵심 기여인데 설치→소멸 사이클이
            // 단일 슬롯으로만 검증돼 있었다. maskValues 가 multi-element 일 때도
            // Teardown 이 전부 회수하고 재설치가 깨끗한지 본다.
            var masks = new NativeArray<byte>(2, Allocator.Temp);
            masks[0] = (byte)PlacementLayer.Ground;
            masks[1] = (byte)PlacementLayer.Path;
            var map = MakeLinearMap();
            try
            {
                SimFieldInstaller.InstallNavFields(_em, in map, 1f, float3.zero, ref _handles, masks);
                var first = _em.GetComponentData<FlowFieldSingleton>(_handles.flowField);
                Assert.AreEqual(2, first.SlotCount);

                SimFieldInstaller.Teardown(_world, _em, ref _handles);
                Assert.Catch(() => { var _ = first.maskValues[0]; }, "maskValues 도 회수된다");

                // 재설치가 깨끗한가 — 이관 전 실패·이중 해제 없이 다시 선다.
                SimFieldInstaller.InstallNavFields(_em, in map, 1f, float3.zero, ref _handles, masks);
                var second = _em.GetComponentData<FlowFieldSingleton>(_handles.flowField);
                Assert.AreEqual(2, second.SlotCount);
                int pathSlot = second.SlotFor(
                    FlowFieldSingleton.GoalSentinel, (byte)PlacementLayer.Path);
                Assert.AreEqual(0, second.DistSlot(pathSlot)[2], "재설치 후에도 Path 슬롯이 골을 찾는다");
            }
            finally { masks.Dispose(); map.Dispose(); }
        }

        // ── unit 3 — 층 인지 walk 마스크 ────────────────────────────────────────

        [Test]
        public void LayerAwareWalkMask_GroundUnit_WalksPlaceCellsOnly()
        {
            // unit 3 의 계약. 같은 맵에서 층이 다르면 **다른 지형**을 본다.
            // (2x2: (0,0)=Walk (1,0)=Place (0,1)=Deco (1,1)=Walk)
            var map = MakeMap(withAuthoredMask: false);
            var outMask = new NativeArray<byte>(4, Allocator.Temp);
            try
            {
                var field = Install(map);

                MovementCellTrim.FillWalkMask(in field, (byte)PlacementLayer.Ground,
                    hasObstacles: false, obstacles: default, outMask);
                Assert.AreEqual(0, outMask[0], "Walk 칸 — 지면 유닛은 못 지난다");
                Assert.AreEqual(1, outMask[1], "Place 칸 — 지면 유닛의 지형");
                Assert.AreEqual(0, outMask[2], "Deco");
                Assert.AreEqual(0, outMask[3], "Walk");
            }
            finally { outMask.Dispose(); map.Dispose(); }
        }

        [Test]
        public void LayerAwareWalkMask_PathUnit_ReproducesWalkMask()
        {
            // 폴백 층(Path)에서는 **현행과 셀 단위로 같다** — unit 3 의 «행동 변화 0» 축.
            var map = MakeMap(withAuthoredMask: false);
            var outMask = new NativeArray<byte>(4, Allocator.Temp);
            try
            {
                var field = Install(map);
                MovementCellTrim.FillWalkMask(in field, TraversalSlots.DefaultMask,
                    hasObstacles: false, obstacles: default, outMask);
                for (int i = 0; i < 4; i++)
                    Assert.AreEqual(field.walkMask[i], outMask[i], $"cell {i}");
            }
            finally { outMask.Dispose(); map.Dispose(); }
        }

        [Test]
        public void AirLayer_OpensEveryTile_AndIgnoresObstacleOverlay()
        {
            // waypoint-routing unit 4 — 비행의 규칙 정체성. 같은 cellLayers/허용 술어를
            // 쓰되 Air 는 모든 타일에 열리고 지상 차단 해저드도 벽으로 합성하지 않는다.
            var map = MakeMap(withAuthoredMask: false);
            var masks = new NativeArray<byte>(1, Allocator.Temp);
            var outMask = new NativeArray<byte>(4, Allocator.Temp);
            var blocked = new NativeHashSet<int2>(1, Allocator.Temp);
            masks[0] = (byte)PlacementLayer.Air;
            blocked.Add(new int2(1, 0)); // Place 셀을 지상 차단
            try
            {
                SimFieldInstaller.InstallNavFields(
                    _em, in map, 1f, float3.zero, ref _handles, masks);
                var field = _em.GetComponentData<FlowFieldSingleton>(_handles.flowField);
                var obstacles = new ObstacleSingleton { blockedCells = blocked };

                MovementCellTrim.FillWalkMask(
                    in field, (byte)PlacementLayer.Air, true, in obstacles, outMask);

                for (int i = 0; i < outMask.Length; i++)
                    Assert.AreEqual(1, outMask[i], $"Air cell {i} — 타일 종류·장애물과 무관");

                int airSlot = field.SlotFor(
                    FlowFieldSingleton.GoalSentinel, (byte)PlacementLayer.Air);
                Assert.AreNotEqual(FlowFieldSingleton.PrimarySlot, airSlot, "Air 전용 슬롯이 설치됨");
            }
            finally
            {
                blocked.Dispose();
                outMask.Dispose();
                masks.Dispose();
                map.Dispose();
            }
        }

        [Test]
        public void LayerAwareWalkMask_InPlaceWriteIsSafe()
        {
            // 층 마스크를 outMask 에 먼저 쓰고 그대로 staticWalk 로 넘긴다(임시 배열 없음).
            // MaterializeWalkMask 가 셀마다 자기 인덱스만 읽고 쓰므로 안전하다 — 이 성질이
            // 깨지면 결과가 조용히 오염되므로 테스트로 고정한다.
            var map = MakeMap(withAuthoredMask: false);
            var inPlace = new NativeArray<byte>(4, Allocator.Temp);
            var viaTemp = new NativeArray<byte>(4, Allocator.Temp);
            try
            {
                var field = Install(map);
                MovementCellTrim.FillWalkMask(in field, TraversalSlots.DefaultMask, false, default, inPlace);

                // 같은 계산을 임시 배열 경유로 — 결과가 같아야 한다.
                TraversalSlots.FillWalkMask(in field.cellLayers, TraversalSlots.DefaultMask, viaTemp);
                var tmp = new NativeArray<byte>(4, Allocator.Temp);
                try
                {
                    new NavGrid(viaTemp, default, false, field.gridSize, field.tileSize, field.origin)
                        .MaterializeWalkMask(tmp);
                    for (int i = 0; i < 4; i++)
                        Assert.AreEqual(tmp[i], inPlace[i], $"cell {i} — in-place 가 오염되지 않는다");
                }
                finally { tmp.Dispose(); }
            }
            finally { inPlace.Dispose(); viaTemp.Dispose(); map.Dispose(); }
        }

        // ───────── unit 5 — 충돌 그리드도 층을 본다 ─────────
        //
        // 라이브에서 순찰병이 `PatrolStep.dir` 을 받고도 한 칸도 못 움직였다: 경로 탐색만
        // 층 인지였고 **충돌/트림 NavGrid 는 `walkMask`(Path 전용)** 를 계속 봤기 때문이다.
        // 배치지에 선 유닛은 자기 칸이 벽으로 읽혀 영원히 clamp 됐다.

        [Test]
        public void LayeredNav_TreatsPlacementGroundAsWalkable_ForGroundUnits()
        {
            var map = MakeMap(withAuthoredMask: false);
            var scratch = new NativeArray<byte>(4, Allocator.Temp);
            try
            {
                var field = Install(map);
                var place = new int2(1, 0);   // 픽스처의 Place 칸
                var obstacles = default(ObstacleSingleton);

                var pathOnly = MovementCellTrim.BuildNavGrid(
                    in field, (byte)PlacementLayer.Path, false, in obstacles, scratch);
                Assert.IsTrue(pathOnly.IsBlocked(place), "Path 전용 유닛에게 배치지는 벽이다");

                var ground = MovementCellTrim.BuildNavGrid(
                    in field, (byte)(PlacementLayer.Ground | PlacementLayer.Path), false, in obstacles, scratch);
                Assert.IsFalse(ground.IsBlocked(place),
                    "Ground 를 여는 유닛에게 배치지는 통행 가능해야 한다 — 아니면 자기 칸에 갇힌다");
            }
            finally { scratch.Dispose(); map.Dispose(); }
        }

        [Test]
        public void LayeredNav_WithDefaultMask_MatchesLegacyWalkMaskNav()
        {
            // 무변경 축 — 적(층 Path)이 보는 벽은 예전 nav 와 셀 단위로 같아야 한다.
            var map = MakeMap(withAuthoredMask: false);
            var scratch = new NativeArray<byte>(4, Allocator.Temp);
            try
            {
                var field = Install(map);
                var obstacles = default(ObstacleSingleton);
                var legacy = MovementCellTrim.BuildNavGrid(in field, false, in obstacles);
                var layered = MovementCellTrim.BuildNavGrid(
                    in field, TraversalSlots.DefaultMask, false, in obstacles, scratch);
                for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                {
                    var c = new int2(x, y);
                    Assert.AreEqual(legacy.IsBlocked(c), layered.IsBlocked(c), $"cell ({x},{y})");
                }
            }
            finally { scratch.Dispose(); map.Dispose(); }
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
