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
    /// 엔진 의존 잔재: `GeneratedMap`(NativeArray)·`Vector2Int` 를 받는다. `MatchWaveSchedule` 과
    /// 같은 이유로 이 unit 의 범위 밖이고, 데이터 계층의 엔진 분리는 unit 18 이 맡는다.
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
