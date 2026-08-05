using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Sim.Match
{
    /// <summary>
    /// battle-sim-extraction unit 15-B — 배치 적법성 판정의 단일 지점.
    ///
    /// 목적은 **커맨드 검증이 한 곳에서 닫히는 것**이다. 그 전에는 공간 판정은 Bridge static,
    /// 코스트는 MonoBehaviour 런타임, 쿨타임은 UI 에 있어서 `DeployDefender` 하나를 검증하려면
    /// 세 계층을 왕복했다(청사진 ① §10-2).
    ///
    /// **순수 함수**다 — 상태를 갖지 않고 plain 값 in / 사유 out. 통화·풀 조회처럼 호출자만
    /// 아는 것은 `bool` 로 받는다(그래서 이 타입은 `CostRuntime`·`GameManager` 를 모른다).
    ///
    /// **판정 순서가 계약이다.** 사유 우선순위가 바뀌면 UI 메시지와 receipt 의 거절 사유가
    /// 달라진다 — 뷰가 "왜 안 놓이는지"를 그 사유로 그린다(`DefenderDragPlacementController`).
    ///
    /// ⚠ **엔진 의존 잔재 — 이 파일은 `using UnityEngine` 을 갖는다**(`HashSet&lt;Vector2Int&gt;` 점유
    /// 집합 + `new Vector2Int(...)`). `Sim/Match/` 의 다른 타입들은 갖지 않으므로 **폴더 단위로
    /// "엔진 무참조" 라고 말하면 거짓**이다. unit 17 은 이 참조를 컴파일 에러로 만드는 것이 완료
    /// 기준이므로, 그때 `Vector2Int`·`GeneratedMap`(NativeArray)·`SpawnEntry`·`GeneratedWavePlan`
    /// 4종이 동시에 걸린다 — `17_sim_lib_skeleton.md` 에 목록으로 적어 뒀다. `int2` 로 갈아타려면
    /// `BattleBridge._occupiedTiles` 까지 함께 바꿔야 해서 이 unit 범위 밖이다.
    /// 드리프트 게이트는 `SimEngineIndependenceTests`.
    /// </summary>
    public static class MatchPlacementRules
    {
        /// <summary>
        /// 공간 조건만(맵 생성 여부·경계·타일 종류·점유). 배치 판정과 **하이라이트 셀 수집**이
        /// 이것을 공유해 어긋나지 않게 한다(계약: 하이라이트=공간, hover=전체 판정).
        /// </summary>
        public static PlacementRejectReason Spatial(
            GeneratedMap map, HashSet<Vector2Int> occupied, int2 cell)
        {
            if (!map.IsCreated) return PlacementRejectReason.MissingMap;
            if (cell.x < 0 || cell.x >= map.gridSize.x || cell.y < 0 || cell.y >= map.gridSize.y)
                return PlacementRejectReason.OutOfBounds;
            if (map.TileAt(cell) != MapTileType.Place) return PlacementRejectReason.NotBuildable;
            if (occupied != null && occupied.Contains(new Vector2Int(cell.x, cell.y)))
                return PlacementRejectReason.Occupied;
            return PlacementRejectReason.None;
        }

        /// <summary>
        /// battle-sim-extraction unit 15-C — 재배치 판정. 배치와 **같은 형태의 순수 규칙**이라
        /// 여기 산다(적출 전에는 `BattleBridge` 의 static 이었다 — MonoBehaviour 가 sim 사유를
        /// 반환하는 모양이라 unit 17 의 asmdef 분리에서 걸릴 자리였다).
        ///
        /// `to` 의 공간 판정은 <see cref="Spatial"/> 재사용. **`from == to` 검사가 선행해야 한다** —
        /// `from` 은 아직 점유 집합에 남아 있으므로, 순서를 바꾸면 제자리 재배치가 `Occupied` 로
        /// 오판된다.
        ///
        /// 배치와 달리 **쿨타임·코스트를 보지 않는다**: 같은 유닛을 옮기는 것이라 새 배치가 아니다.
        /// </summary>
        public static PlacementRejectReason Relocation(
            GeneratedMap map, HashSet<Vector2Int> occupied, int2 from, int2 to,
            bool fromHasDefender, bool fromBusy)
        {
            if (!fromHasDefender) return PlacementRejectReason.NoDefenderAtSource;
            if (fromBusy) return PlacementRejectReason.SourceBusy;
            if (from.Equals(to)) return PlacementRejectReason.SameCell;
            return Spatial(map, occupied, to);
        }

        /// <summary>
        /// 배치 전체 판정. `None` 이면 놓을 수 있다.
        ///
        /// `unitValid` 는 현재 "유닛이 있고 뷰 머티리얼이 배선됐는가"다 — 후자는 사실 프레젠테이션
        /// 조건이라 sim 규칙에 있을 이유가 없다(후속 정리 후보). 지금은 **행동 보존**을 위해 그대로
        /// 받는다: 여기서 빼면 뷰가 없는 유닛이 배치되고 렌더 단계에서 터진다.
        /// </summary>
        public static PlacementRejectReason Check(
            bool placementOpen,
            GeneratedMap map, HashSet<Vector2Int> occupied, int2 cell,
            bool unitValid, bool inPool, bool canAfford, bool cooldownReady)
        {
            if (!placementOpen) return PlacementRejectReason.NotRunningOrPlacementClosed;

            PlacementRejectReason spatial = Spatial(map, occupied, cell);
            if (spatial != PlacementRejectReason.None) return spatial;

            if (!unitValid) return PlacementRejectReason.InvalidUnit;
            if (!inPool) return PlacementRejectReason.NotInPickedPool;
            if (!canAfford) return PlacementRejectReason.InsufficientCost;
            if (!cooldownReady) return PlacementRejectReason.OnCooldown;
            return PlacementRejectReason.None;
        }
    }
}
