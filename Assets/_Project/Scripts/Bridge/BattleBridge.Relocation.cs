using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Bridge
{
    // defender-relocation unit 0 — 배치된 방어유닛을 다른 Place 타일로 옮기는 relocate 절반.
    // 점유·바인딩·DefenderTile 스왑은 확정 프레임, LocalTransform 은 착지 프레임(Finish) —
    // 그 사이 뷰는 프리뷰가 비행한다(unit 3). 활성화는 기존 ActivateDeployedDefender 재사용:
    // _onPlaceTriggeredEntities 가드가 on-place/effect-tile 을 exactly-once 로 만들므로
    // 재배치가 어느 쪽도 재발화하지 않는다(spec README 계약 4).
    public partial class BattleBridge
    {
        // 순수 판정 (plain 값 in → reason out) — EditMode 테스트 대상 (CLAUDE.md 제약 10).
        // to 의 공간 판정은 SpatialPlacementCheck 재사용. from 은 점유 집합에 남아 있으므로
        // from == to 검사가 선행되어야 "자기 자리 = Occupied" 로 오판하지 않는다.
        public static PlacementRejectReason RelocationCheck(
            GeneratedMap map, HashSet<Vector2Int> occupied, int2 from, int2 to,
            bool fromHasDefender, bool fromBusy)
        {
            if (!fromHasDefender) return PlacementRejectReason.NoDefenderAtSource;
            if (fromBusy) return PlacementRejectReason.SourceBusy;
            if (from.Equals(to)) return PlacementRejectReason.SameCell;
            return SpatialPlacementCheck(map, occupied, to);
        }

        // read-only 사전 검증 (컨트롤러 hover/reject 피드백용) — 상태 변경 없음.
        // 페이즈 게이트는 CanPlaceDefenderAt 과 동일 규칙(_running || _placementAllowed).
        public bool CanRelocateDefender(Vector2Int from, Vector2Int to, out PlacementRejectReason reason)
        {
            if (!_running && !_placementAllowed)
            {
                reason = PlacementRejectReason.NotRunningOrPlacementClosed;
                return false;
            }
            bool has = _defenderByTile.TryGetValue(from, out var binding)
                       && _em != null && _em.Exists(binding.entity);
            bool busy = has && _em.HasComponent<PendingDeployment>(binding.entity);
            reason = RelocationCheck(_generatedMap, _occupiedTiles,
                new int2(from.x, from.y), new int2(to.x, to.y), has, busy);
            return reason == PlacementRejectReason.None;
        }

        // 확정 프레임 원자 처리 (spec README 계약 5): 점유·바인딩·DefenderTile 을 from→to 로
        // 스왑하고 PendingDeployment 를 재부착(비타겟·비무장·시너지 제외 — 계약 2).
        // 코스트·on-place·컷신·PlacementCommitted 는 지나지 않는다(계약 1·4·8).
        public bool TryBeginDefenderRelocation(Vector2Int from, Vector2Int to, out Entity entity, out PlacementRejectReason reason)
        {
            entity = Entity.Null;
            if (!CanRelocateDefender(from, to, out reason))
            {
                Debug.Log($"[BattleBridge] Relocation rejected {from} -> {to}: {reason}");
                return false;
            }

            var binding = _defenderByTile[from];
            entity = binding.entity;

            _occupiedTiles.Remove(from);
            _occupiedTiles.Add(to);
            _defenderByTile.Remove(from);
            _defenderByTile[to] = binding;
            _em.SetComponentData(entity, new DefenderTile { cell = new int2(to.x, to.y) });
            _em.AddComponent<PendingDeployment>(entity);
#if UNITY_EDITOR
            _em.SetName(entity, $"Defender_{binding.data.displayName}_{to.x}_{to.y}");
#endif
            tileHealthGaugeLayer?.Hide(from); // 게이지 키 = 셀. 새 셀은 상시 sync 가 다시 그린다.
            RecomputeSynergyFor(from);        // 이탈 반영. to 쪽은 활성화가 수행(계약 6).
            RefreshPlacementHighlightIfShown();
            Debug.Log($"[BattleBridge] Relocation began: {binding.data.displayName} {from} -> {to}.");
            return true;
        }

        // 착지 프레임 — 시뮬 월드 위치를 목적 셀로 (스폰과 같은 y 규칙, 회전·스케일 유지).
        // 활성화(ActivateDeployedDefender)는 재전개 대기 후 호출자가 수행한다(unit 3).
        public void FinishDefenderRelocation(Vector2Int to, Entity entity)
        {
            if (_em == null || entity == Entity.Null || !_em.Exists(entity)) return;
            if (!_defenderByTile.TryGetValue(to, out var binding) || binding.entity != entity) return;
            var lt = _em.GetComponentData<LocalTransform>(entity);
            lt.Position = GridToWorldCenter(to, spawnHeight);
            _em.SetComponentData(entity, lt);
        }

#if UNITY_EDITOR
        // unit 0 디버그 진입점 — 첫 활성 방어유닛을 첫 유효 셀로 즉시형(비행 0초) 이동.
        // 연출 unit 3 이전의 단독 검증용. RelocationDebugMenu 가 호출.
        public bool DebugRelocateFirstDefender()
        {
            if (_em == null || !_generatedMap.IsCreated) return false;

            Vector2Int from = default;
            bool found = false;
            foreach (var kv in _defenderByTile)
            {
                if (!_em.Exists(kv.Value.entity) || _em.HasComponent<PendingDeployment>(kv.Value.entity)) continue;
                from = kv.Key;
                found = true;
                break;
            }
            if (!found)
            {
                Debug.LogWarning("[BattleBridge] Debug relocate: no active defender on board.");
                return false;
            }

            int w = _generatedMap.gridSize.x, h = _generatedMap.gridSize.y;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var to = new Vector2Int(x, y);
                if (!CanRelocateDefender(from, to, out _)) continue;
                if (!TryBeginDefenderRelocation(from, to, out var entity, out _)) return false;
                FinishDefenderRelocation(to, entity);
                ActivateDeployedDefender(to, entity);
                return true;
            }
            Debug.LogWarning("[BattleBridge] Debug relocate: no valid target cell.");
            return false;
        }
#endif
    }
}
