using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Wassup.Battle.Units;
using Wassup.Data;
using Wassup.Sim.Match;

namespace Wassup.Bridge
{
    // defender-relocation unit 0 — 배치된 방어유닛을 다른 Place 타일로 옮기는 relocate 절반.
    // 점유·바인딩·DefenderTile 스왑은 확정 프레임, LocalTransform 은 착지 프레임(Finish) —
    // 그 사이 뷰는 프리뷰가 비행한다(unit 3). 활성화는 기존 ActivateDeployedDefender 재사용:
    // _onPlaceTriggeredEntities 가드가 on-place/effect-tile 을 exactly-once 로 만들므로
    // 재배치가 어느 쪽도 재발화하지 않는다(spec README 계약 4).
    public partial class BattleBridge
    {
        // unit 15-C: 판정 본체는 `MatchPlacementRules.Relocation` 으로 이사했고 여기는 **포워더**다
        // (`SpatialPlacementCheck` 와 같은 처분 — 호출처와 EditMode 테스트가 이 이름을 쓴다).
        public static PlacementRejectReason RelocationCheck(
            GeneratedMap map, HashSet<Vector2Int> occupied, int2 from, int2 to,
            bool fromHasDefender, bool fromBusy)
            => MatchPlacementRules.Relocation(map, occupied, from, to, fromHasDefender, fromBusy);

        // unit 1 — 보드 유닛 조회 read seam (재배치 컨트롤러의 홀드 판정용). busy = 배치/이동
        // 진행 중(PendingDeployment) — 홀드 진입 자체를 막는다.
        public bool TryGetDefenderAt(Vector2Int cell, out Entity entity, out DefenderUnitData data, out bool busy)
        {
            if (_defenderByTile.TryGetValue(cell, out var b) && _em != null && _em.Exists(b.entity))
            {
                entity = b.entity;
                data = b.data;
                busy = _em.HasComponent<PendingDeployment>(b.entity);
                return true;
            }
            entity = Entity.Null;
            data = null;
            busy = false;
            return false;
        }

        // unit 5 — entity→cell 역참조 (선택 액션 플립북이 픽한 entity 로 이동모드 진입 시 소스 셀 해석).
        // 소규모 그리드라 선형 스캔 비용 무시. _defenderByTile 이 유일 소스.
        public bool TryGetDefenderCell(Entity entity, out Vector2Int cell)
        {
            foreach (var kv in _defenderByTile)
            {
                if (kv.Value.entity != entity) continue;
                cell = kv.Key;
                return true;
            }
            cell = default;
            return false;
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
            // summon-patrol-defender unit 4 — 소환사가 옮겨가면 순찰병의 거점도 따라간다.
            // 거점은 walk 셀이므로 새 배치 셀 기준으로 **재스냅**한다(계약 4). 재스냅 실패
            // (주변에 walk 타일 없음)면 기존 거점을 유지한다 — 순찰병을 죽이지 않는다.
            RelocatePatrolAnchorFor(entity, new int2(to.x, to.y));
#if UNITY_EDITOR
            _em.SetName(entity, $"Defender_{binding.data.displayName}_{to.x}_{to.y}");
#endif
            tileHealthGaugeLayer?.Hide(from); // 게이지 키 = 셀. 새 셀은 상시 sync 가 다시 그린다.
            RecomputeSynergyFor(from);        // 이탈 반영. to 쪽은 활성화가 수행(계약 6).
            RefreshPlacementHighlightIfShown();
            Debug.Log($"[BattleBridge] Relocation began: {binding.data.displayName} {from} -> {to}.");
            return true;
        }

        // summon-patrol-defender unit 4 — 소환사 재배치 시 순찰병 거점 재스냅.
        // 소환사가 아니거나(SummonerState 없음) 순찰병이 없으면 no-op.
        private void RelocatePatrolAnchorFor(Entity summoner, int2 newOwnerCell)
        {
            if (!_em.HasComponent<Wassup.Battle.Combat.SummonerState>(summoner)) return;

            var state = _em.GetComponentData<Wassup.Battle.Combat.SummonerState>(summoner);
            Entity patrol = state.current;
            if (patrol == Entity.Null || !_em.Exists(patrol)) return;
            if (!_em.HasComponent<Wassup.Battle.Movement.PatrolAnchor>(patrol)) return;

            var anchor = _em.GetComponentData<Wassup.Battle.Movement.PatrolAnchor>(patrol);
            if (!TryGetPatrolAnchorCell(newOwnerCell, anchor.tileRadius, out var anchorCell)) return;
            anchor.cell = anchorCell;
            _em.SetComponentData(patrol, anchor);
        }

        // relocation unit 6 — 비행 중 뷰 위치 오버라이드. sim(LocalTransform)은 착지 프레임까지 옛 위치에
        // 머무르므로, SyncMonoUnitViews 의 defender 피드가 이 값을 대신 쓰게 해 실제 유닛 뷰를
        // 컨트롤러가 직접 날린다(프리뷰 신설 없음). 값은 **VIEW 좌표**(ToView 완료) — 평면 정면뷰가
        // sim 높이를 버리므로 아치를 view 공간에서 계산해 넘긴다. SyncMonoUnitViews 는 ToView 재적용 없이 직배치.
        // defender-drop-dismount unit 1 — 소비처가 2개(재배치 비행·드롭 하마)가 되어 이름 중립화.
        // 같은 entity 동시 소유는 없다: 드롭 창 = pending 창이고 재배치 진입은 busy(pending) 거부.
        // flight-lift-feel unit 2 — lift(지면에서 뜬 높이)를 **동반 전달**한다. 좌표 체계는 그대로
        // 절대 view 좌표다: 보스가 (평면, 높이) 2축으로 나눈 것은 ToView 가 sim-Y 를 버리기 때문이고
        // 이 경로엔 그 문제가 없다 — 잘 도는 좌표계를 높이를 알기 위해 재구성하지 않는다.
        // 아치 높이가 좌표에 통합돼 있어 뷰가 "얼마나 떴는지"를 역산할 수 없으므로 값만 같이 싣는다.
        private readonly Dictionary<Entity,
            (Unity.Mathematics.float3 pos, float lift, Unity.Mathematics.float3 ground)> _defenderViewOverride = new();

        // lift 기본값 0 = 반응 없음, groundAnchor 기본값 zero = 그림자가 종전대로 유닛을 따라감.
        // 뒤 두 인자를 안 주는 호출처가 있어도 항등이다.
        public void SetDefenderViewOverride(Entity entity, Vector3 viewPos, float lift = 0f,
            Vector3 groundAnchor = default)
            => _defenderViewOverride[entity] =
                (new Unity.Mathematics.float3(viewPos.x, viewPos.y, viewPos.z), lift,
                 new Unity.Mathematics.float3(groundAnchor.x, groundAnchor.y, groundAnchor.z));

        public void ClearDefenderViewOverride(Entity entity)
            => _defenderViewOverride.Remove(entity);

        // flight-lift-feel unit 3 — 착지 눌림. 드롭 하마·보스 도약이 착지 프레임에 **명시 호출**한다.
        // "lift 가 0 으로 떨어지면 자동 착지" 로 만들지 않는다 — 비행 취소·teardown 도 0 이라 오탐이
        // 터진다. 폴백 quad 뷰는 스쿼시 슬롯이 없어 조용히 건너뛴다(개발용 폴백).
        public void PlayLandingSquash(Entity entity, float amount, float seconds)
        {
            if (amount <= 0f || seconds <= 0f) return;
            if (spineUnitPool != null && spineUnitPool.TryGet(entity, out var view) && view != null)
                view.PlayLandingSquash(amount, seconds);
        }

        internal bool TryGetDefenderViewOverride(Entity entity, out Unity.Mathematics.float3 pos,
            out float lift, out Unity.Mathematics.float3 ground)
        {
            if (_defenderViewOverride.TryGetValue(entity, out var v))
            {
                pos = v.pos; lift = v.lift; ground = v.ground; return true;
            }
            pos = default; lift = 0f; ground = default; return false;
        }

        // 비행 앵커 — **VIEW 좌표**(셀 중심의 ToView). 컨트롤러가 view 공간 던지기 곡선의 양 끝으로 쓴다.
        // sim 이 아니라 view 로 주는 이유: 평면 정면뷰(BoardSpace.ToView)가 sim 높이를 버려, sim 공간
        // 아치는 화면에서 평면으로 보인다. view 공간에서 camUp 아치를 태워야 화면 세로로 던져진다.
        public bool TryGetRelocationAnchors(Vector2Int from, Vector2Int to, out Vector3 start, out Vector3 end)
        {
            start = end = default;
            if (!_generatedMap.IsCreated) return false;
            start = (Vector3)Wassup.Core.BoardSpace.ToView((float3)GridToWorldCenter(from, spawnHeight));
            end = (Vector3)Wassup.Core.BoardSpace.ToView((float3)GridToWorldCenter(to, spawnHeight));
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
