using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Wassup.Battle.Units;
using Wassup.Core;   // unit 8 — GameManager.CostRuntime (재배치 코스트)
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
            bool fromHasDefender, bool fromBusy, PlacementLayer layers)
        {
            if (!fromHasDefender) return PlacementRejectReason.NoDefenderAtSource;
            if (fromBusy) return PlacementRejectReason.SourceBusy;
            // unit 9 — 같은 칸 = **제자리 재정비 확정**(was: SameCell 거부 → 컨트롤러가 취소로 해석).
            // ⚠ 이 검사는 SpatialPlacementCheck 앞에 그대로 있어야 한다 — from 이 아직 점유 집합에
            // 남아 있어서, 순서가 바뀌면 자기 자리가 Occupied 로 오판된다.
            // PlacementRejectReason.SameCell 은 enum 에서 지우지 않는다(직렬화 값 보존) — 생산자만 없다.
            if (from.Equals(to)) return PlacementRejectReason.None;
            // unit 4 — 옮기는 그 유닛의 층으로 목적 셀을 본다(배치와 같은 규칙).
            return SpatialPlacementCheck(map, occupied, to, layers);
        }

        // defender-footprint unit 1 — footprint 재배치 판정. to-rect 검사에서 **자기 from-rect 안
        // 셀의 Occupied 는 무시**한다 — 2×2 를 한 칸 옮기기가 자기 점유에 막히면 안 된다(위
        // RelocationCheck 의 from==to 선행 검사와 같은 이유의 일반화). 같은 앵커 = 제자리 재정비.
        public static PlacementRejectReason RelocationFootprintCheck(
            GeneratedMap map, HashSet<Vector2Int> occupied, Vector2Int fromAnchor, Vector2Int toAnchor,
            Vector2Int size, bool fromHasDefender, bool fromBusy, PlacementLayer layers,
            List<FootprintCellReason> perCell = null)
        {
            if (!fromHasDefender) return PlacementRejectReason.NoDefenderAtSource;
            if (fromBusy) return PlacementRejectReason.SourceBusy;
            if (fromAnchor == toAnchor) return PlacementRejectReason.None;
            return SpatialFootprintCheck(map, occupied, toAnchor, size, layers, perCell,
                ignoreOccupied: FootprintMath.Cells(fromAnchor, size));
        }

        // unit 1 — 보드 유닛 조회 read seam (재배치 컨트롤러의 홀드 판정용). busy = 배치/이동
        // 진행 중(PendingDeployment) — 홀드 진입 자체를 막는다.
        public bool TryGetDefenderAt(Vector2Int cell, out Entity entity, out DefenderUnitData data, out bool busy)
        {
            // defender-footprint unit 1 — footprint 임의 칸 → 대표 셀 해석(1×1 항등).
            if (TryResolveDefenderKey(cell, out var key)) cell = key;
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
            // defender-footprint unit 1 — from 은 footprint 임의 칸일 수 있다 → 대표 셀 해석.
            // to 는 목적 footprint 의 **앵커**다(배치 진입과 같은 의미).
            if (TryResolveDefenderKey(from, out var fromKey)) from = fromKey;
            bool has = _defenderByTile.TryGetValue(from, out var binding)
                       && _em != null && _em.Exists(binding.entity);
            bool busy = has && _em.HasComponent<PendingDeployment>(binding.entity);
            // unit 4 — 옮기는 유닛(소스 바인딩)의 층. 바인딩 부재는 위 has=false 가 사유를 낸다.
            var layers = (has && binding.data != null)
                ? binding.data.EffectivePlacementLayers : PlacementLayer.Ground;
            var size = (has && binding.data != null) ? binding.data.Footprint : Vector2Int.one;
            // 제자리 재정비 정규화 — 기존 호출자(선택 패널 등)는 «유닛 셀 = 대표 셀»을 to 로 넘긴다.
            // to 는 앵커 의미라, 짝수 변에서 대표 셀 ≠ 앵커면 제자리가 이동으로 오독된다.
            if (to == from) to = FootprintMath.AnchorFromPrimary(from, size);
            reason = RelocationFootprintCheck(_generatedMap, _occupiedTiles,
                FootprintMath.AnchorFromPrimary(from, size), to, size, has, busy, layers);
            if (reason != PlacementRejectReason.None) return false;

            // unit 8 — 코스트는 **공간 판정 뒤**에 본다. 구조 사유가 자원 사유를 이긴다
            // (defender-board-limit 계약 4 와 같은 순서: 기다려도 안 풀리는 사유가 먼저 보여야 한다).
            if (!HasCostForRelocation(binding.data))
            {
                reason = PlacementRejectReason.InsufficientCost;
                return false;
            }
            return true;
        }

        // unit 8 — 재배치 코스트 = 그 유닛의 배치 코스트 전액. 배율 노브를 두지 않는다(값이 두 곳에
        // 살면 갈린다 — 튜닝이 필요해지면 그때 만든다). CostRuntime 부재(테스트 하네스)는 통과시킨다.
        private bool HasCostForRelocation(DefenderUnitData data)
        {
            if (data == null) return true;
            var costRuntime = GameManager.Instance != null ? GameManager.Instance.CostRuntime : null;
            return costRuntime == null || costRuntime.CanAfford(data.cost);
        }

        // 확정 프레임 원자 처리 (spec README 계약 5): 점유·바인딩·DefenderTile 을 from→to 로
        // 스왑하고 PendingDeployment 를 재부착(비타겟·비무장·시너지 제외 — 계약 2).
        // 엔티티 스폰·컷신·PlacementCommitted 는 지나지 않는다(계약 8).
        //
        // unit 8 rev — 코스트는 **지난다**. 순서가 계약이다: 판정 → 차감 → 스왑.
        // 차감 뒤에 실패로 빠지는 경로가 하나라도 생기면 코스트가 증발한다.
        public bool TryBeginDefenderRelocation(Vector2Int from, Vector2Int to, out Entity entity, out PlacementRejectReason reason)
        {
            entity = Entity.Null;
            // defender-footprint unit 1 — from = 대표 셀 해석, to = 목적 footprint 앵커.
            if (TryResolveDefenderKey(from, out var fromKey)) from = fromKey;
            if (!CanRelocateDefender(from, to, out reason))
            {
                Debug.Log($"[BattleBridge] Relocation rejected {from} -> {to}: {reason}");
                return false;
            }

            var binding = _defenderByTile[from];
            entity = binding.entity;

            // unit 8 — 차감. CanRelocateDefender 가 이미 봤지만 TrySpend 가 원자 판정이라
            // 그 사이 코스트를 쓴 다른 경로(각성 카드 등)가 있어도 초과 지출이 없다.
            var costRuntime = GameManager.Instance != null ? GameManager.Instance.CostRuntime : null;
            if (binding.data != null && costRuntime != null && !costRuntime.TrySpend(binding.data.cost))
            {
                reason = PlacementRejectReason.InsufficientCost;
                Debug.Log($"[BattleBridge] Relocation rejected {from} -> {to}: {reason}");
                entity = Entity.Null;
                return false;
            }

            // defender-footprint unit 1 — footprint 단위 스왑. 바인딩 키·DefenderTile 은 새 대표 셀.
            var size = binding.data != null ? binding.data.Footprint : Vector2Int.one;
            // unit 2 — 제자리 재정비 정규화(CanRelocateDefender 와 같은 규칙 — 두 곳이 갈리면
            // 판정은 제자리인데 스왑이 이동해 점유가 어긋난다).
            if (to == from) to = FootprintMath.AnchorFromPrimary(from, size);
            var toPrimary = FootprintMath.PrimaryCell(to, size);
            ReleaseDefenderFootprint(from);
            OccupyDefenderFootprint(to, size);
            _defenderByTile.Remove(from);
            _defenderByTile[toPrimary] = binding;
            _em.SetComponentData(entity, new DefenderTile { cell = new int2(toPrimary.x, toPrimary.y) });
            _em.AddComponent<PendingDeployment>(entity);
            // unit 8 — on-place 재무장(계약 4). 이 한 줄이 재발동의 전부다: 착지 후 활성화가 부르는
            // TriggerDeploymentOnPlaceSkill 이 가드를 통과하게 된다. 효과 타일은 자기 가드를
            // 따로 쓰므로 여기 딸려오지 않는다(ApplyEffectTileOnce).
            _onPlaceTriggeredEntities.Remove(entity);
            // summon-patrol-defender unit 4 — 소환사가 옮겨가면 순찰병의 담당 구역도 따라간다.
            // unit 9 로 계약이 바뀌었다: 중심 = 새 소환사 셀, 집 = 그 **주변**의 통행 가능 칸.
            // 선정 실패(주변에 설 칸 없음)면 기존 값을 유지한다 — 순찰병을 죽이지 않는다.
            RelocatePatrolAnchorFor(entity, new int2(toPrimary.x, toPrimary.y));
#if UNITY_EDITOR
            _em.SetName(entity, $"Defender_{binding.data.displayName}_{toPrimary.x}_{toPrimary.y}");
#endif
            tileHealthGaugeLayer?.Hide(from); // 게이지 키 = 셀. 새 셀은 상시 sync 가 다시 그린다.
            RecomputeSynergyFor(from);        // 이탈 반영. to 쪽은 활성화가 수행(계약 6).
            RefreshPlacementHighlightIfShown();
            Debug.Log($"[BattleBridge] Relocation began: {binding.data.displayName} {from} -> {to}.");
            return true;
        }

        // summon-patrol-defender unit 4 — 소환사 재배치 시 순찰병 거점 재스냅.
        // 소환사가 아니거나(SummonerState 없음) 순찰병이 없으면 no-op.
        //
        // unit 9 — 거점 = 새 소환사 셀. 통행 층 판정은 스폰과 **같은 함수**를 쓴다.
        // 층은 순찰병 엔티티에서 읽는다(스폰 시 주입된 값이 여기 살아 있다) — SO 를 다시
        // 뒤지지 않는 편이 "지금 이 개체가 어디를 밟을 수 있나"라는 질문에 정확하다.
        private void RelocatePatrolAnchorFor(Entity summoner, int2 newOwnerCell)
        {
            if (!_em.HasComponent<Wassup.Battle.Combat.SummonerState>(summoner)) return;

            var state = _em.GetComponentData<Wassup.Battle.Combat.SummonerState>(summoner);
            Entity patrol = state.current;
            if (patrol == Entity.Null || !_em.Exists(patrol)) return;
            if (!_em.HasComponent<Wassup.Battle.Movement.PatrolAnchor>(patrol)) return;

            byte layers = _em.HasComponent<Wassup.Battle.Movement.PathFollowState>(patrol)
                ? _em.GetComponentData<Wassup.Battle.Movement.PathFollowState>(patrol).traversalLayers
                : (byte)0;

            var anchor = _em.GetComponentData<Wassup.Battle.Movement.PatrolAnchor>(patrol);
            if (!TryGetPatrolHomeCell(newOwnerCell, anchor.tileRadius, layers, out var homeCell)) return;
            anchor.cell = newOwnerCell;   // 중심은 소환사를 따라간다(프리뷰와 같은 칸)
            anchor.homeCell = homeCell;
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
        public bool TryGetRelocationAnchors(Vector2Int from, Vector2Int to, out Vector3 start, out Vector3 end,
            DefenderUnitData unit = null)
        {
            start = end = default;
            if (!_generatedMap.IsCreated) return false;
            // defender-footprint unit 2 — 비행 양끝도 뷰 피드와 같은 기하 중심(짝수 변 +0.5칸).
            // from = 옛 대표 셀, to = 스왑 후 대표 셀(앵커가 와도 해석). 오프셋은 뷰 전용 — sim 불변.
            if (TryResolveDefenderKey(to, out var toKey)) to = toKey;
            var fpOff = FootprintViewOffset(unit);
            var off3 = new float3(fpOff.x, 0f, fpOff.y);
            start = (Vector3)Wassup.Core.BoardSpace.ToView((float3)GridToWorldCenter(from, spawnHeight) + off3);
            end = (Vector3)Wassup.Core.BoardSpace.ToView((float3)GridToWorldCenter(to, spawnHeight) + off3);
            return true;
        }

        // unit 8 — 재배치 활성화 꼬리. 재전개가 끝나 전투에 복귀하는 순간에 일어나는 세 가지를
        // 한 곳에 묶는다: 밀치기 → 활성화(= on-place 재발동) → 회복. 한 사건으로 읽혀야 한다.
        //
        // 밀치기를 확정이 아니라 여기서 부르는 이유: 확정 시점엔 유닛이 아직 비행 중이라 **빈 칸을
        // 민다**. 즉시 배치 경로(TriggerOnPlaceAndSynergy)가 on-place 와 밀치기를 한 묶음으로
        // 부르는 것과 같은 모양이고, 드래그 배치만 확정 시점에 부른다.
        //
        // healRatio 는 인자로 받는다 — 노브는 컨트롤러(RelocationSettings)가 소유하고 브리지는
        // 값만 쓴다(RelocationSettings 를 브리지가 직접 참조하면 씬에 두 번째 할당 지점이 생긴다).
        public void ActivateRelocatedDefender(Vector2Int cell, Entity entity, float healRatio)
        {
            // ⚠ 바인딩 확인을 **맨 앞에서** 하고 통째로 물러난다. ActivateDeployedDefender 는
            // 바인딩이 안 맞으면 조용히 리턴하므로, 순서대로 늘어놓기만 하면 활성화가 실패해도
            // 회복만 들어가는 구멍이 생긴다.
            if (_em == null || entity == Entity.Null || !_em.Exists(entity)) return;
            if (!_defenderByTile.TryGetValue(cell, out var binding) || binding.entity != entity) return;

            ActivateDeployedDefender(cell, entity);
            ApplyRefitHeal(entity, healRatio);
        }

        // unit 8 — 재정비 회복. Units 소유 버퍼에 브리지가 직접 append 하는 것은 DefenderTile 을
        // 직접 쓰는 것과 같은 격이다(계약 9). 신규 채널 0 — 회복 숫자와 VFX 는 기존
        // HealAppliedEventsSingleton 경로가 IncomingHeal 배수 시점에 알아서 낸다.
        private void ApplyRefitHeal(Entity entity, float healRatio)
        {
            if (healRatio <= 0f) return;
            if (!_em.HasComponent<Health>(entity)) return;
            if (!_em.HasBuffer<Wassup.Battle.Units.IncomingHeal>(entity)) return;

            var health = _em.GetComponentData<Health>(entity);
            float amount = health.max * healRatio;
            if (amount <= 0f) return;
            // 상한 클램프는 DamageApplicationSystem 이 배수하며 수행한다 — 여기서 미리 자르지 않는다
            // (자르면 그 프레임의 다른 피해와 순서가 얽혀 두 곳이 같은 규칙을 갖게 된다).
            _em.GetBuffer<Wassup.Battle.Units.IncomingHeal>(entity)
               .Add(new Wassup.Battle.Units.IncomingHeal { amount = amount });
            Debug.Log($"[BattleBridge] Refit heal {amount:F0} to {entity.Index} (ratio {healRatio:F2}).");
        }

        // 착지 프레임 — 시뮬 월드 위치를 목적 셀로 (스폰과 같은 y 규칙, 회전·스케일 유지).
        // 활성화(ActivateRelocatedDefender)는 재전개 대기 후 호출자가 수행한다(unit 3).
        public void FinishDefenderRelocation(Vector2Int to, Entity entity)
        {
            if (_em == null || entity == Entity.Null || !_em.Exists(entity)) return;
            // defender-footprint unit 1 — 호출자가 앵커를 넘겨도 바인딩 키(대표 셀)로 해석.
            if (TryResolveDefenderKey(to, out var key)) to = key;
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
                // unit 9 — 제자리도 유효 목적지가 됐으므로 명시로 제외한다. 이 디버그 진입점의
                // 목적은 "이동이 되는가" 를 눈으로 보는 것이라, 소스 칸을 집으면 아무 일도 안 한 것처럼 보인다.
                if (to == from) continue;
                if (!CanRelocateDefender(from, to, out _)) continue;
                if (!TryBeginDefenderRelocation(from, to, out var entity, out _)) return false;
                FinishDefenderRelocation(to, entity);
                // 회복 0 — 디버그 경로엔 RelocationSettings 가 없다. 비율을 여기 적으면
                // 에셋과 갈리므로 명시적으로 안 준다(코스트·재발동은 라이브와 동일하게 지난다).
                ActivateRelocatedDefender(to, entity, 0f);
                return true;
            }
            Debug.LogWarning("[BattleBridge] Debug relocate: no valid target cell.");
            return false;
        }
#endif
    }
}
