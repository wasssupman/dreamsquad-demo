# 2 — MovementSystem 추격 분기 확장

## 목적

`MovementSystem` 의 `Chasing` 분기를 확장해, aggro 가 없으면 `HuntTarget` 을 anchor 로 self-walk 한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`

## 구현

기존 chasing 분기(`MovementSystem.cs:68`):
```csharp
if (ai == AiState.Chasing && aggroLookup.HasComponent(entity)) {
    // guardian anchor 로 self-walk ... continue;
}
```
확장:
```csharp
if (ai == AiState.Chasing) {
    float3 anchor; bool hasAnchor = false;
    if (aggroLookup.HasComponent(entity) && guardianPos.TryGetValue(guardian, out var gpos)) {
        anchor = gpos; hasAnchor = true;                    // 기존 aggro
    } else if (huntTargetLookup.HasComponent(entity)) {     // 신규 헌터
        var t = huntTargetLookup[entity].value;
        if (t != Entity.Null && transformLookup.HasComponent(t)) { anchor = pos(t); hasAnchor = true; }
    }
    if (hasAnchor) { /* step toward anchor + MovementCellTrim.Apply (기존 로직 공유) */ }
    continue; // anchor 없으면 정지(guardian/target 소멸)
}
```

- **anchor step 로직은 기존 aggro 코드 재사용** — 두 소스가 같은 step/cell-trim 통과. 코드 중복 최소(anchor 만 분기).
- `huntTargetLookup` = `GetComponentLookup<HuntTarget>(true)`. `transformLookup`(RO)로 타겟 위치 조회(HuntTarget 은 Entity 만 들고, 위치는 라이브 조회 — 방어유닛 이동/사망 반영).
- **무회귀**: aggro 있는 기존 Chasing 은 첫 분기로 동일 동작. 헌터 Chasing 은 nightmare-catcher 이전엔 존재 불가(Evaluate 가 aggro 로만 Chasing 반환했으므로) → 신규 경로만 추가.

## 완료 기준

- [x] 보스가 HuntTarget 방어유닛으로 self-walk(cell-trim 통과, walk 타일 유지) — 코드. Play 확인은 unit 3.
- [x] 사거리 도달 → FSM 이 Engaging 전환 → chasing 분기 이탈(anchor 무관, ai!=Chasing).
- [x] 기존 aggro Chasing 무변경 — anchor 분기 첫 가지가 guardian(기존)과 동일, EditMode 649/651 무회귀.
- [ ] (렌즈 B) huntTargetLookup RO·맥락 경계 — units 1·2 묶음 실행 중.

## 구현 소결정 (aliasing 회피)

- **위치 조회는 `defenderPos` 스냅샷**(별도 RO 쿼리)로 확정 — spec 초안의 "transformLookup 라이브 조회"는 이동 루프의 `RefRW<LocalTransform>` 와 **aliasing 위반**(ComponentLookup<LocalTransform> RO + query RW 동시 = 금지)이라 불가. guardianPos 선례와 동일 패턴(별도 쿼리는 안전). 방어유닛은 타일 고정이라 프레임 내 스냅샷 = 라이브와 동등.

확인 2026-07-11 — 컴파일 클린 + EditMode 649/651 그린 + 커밋 `43e23954`.

## rev — 축 분리 wall-slide (spawn-freeze 버그, 실플레이 진단 2026-07-11)

**증상**: 헌터 보스가 스폰 자리에서 안 움직이고 갇힘(Chasing 상태인데 9.6초간 이동 0). 방어유닛에 접근·교전 실패.

**근본 원인(런타임 증거)**: 이 맵은 **적 경로가 단일 walk 레인**(y=1)이고 **방어유닛은 경로 밖 벽 셀**(y=2/y=7)에 배치된다. 직선 추격(대각선)의 첫 스텝이 곧장 벽 셀로 넘어가 `MovementCellTrim`이 매 프레임 제자리로 clamp → 고착. aggro 추격은 가디언이 보통 경로 위라 안 겪던 케이스(직선 추격의 가정 = 타겟이 walkable 직선상).

**수정**: 직선 스텝이 clamp되면(벽) **축 분리 슬라이드**(x만 → 실패 시 z만)로 walkable 축을 타고 접근. 보스는 레인을 따라 x=9까지 미끄러져 방어유닛 사거리(2타일)에 진입 → FSM 이 Engaging 으로 전환 → 정지·공격. 둘 다 막히면 제자리(fully-boxed, 드묾).

**한계(후속)**: wall-slide 는 greedy 근사라 오목(concave) 지형에선 완전 도달 실패 가능(그 경우 인접 도달분만 교전). 진짜 타겟 지향 pathfinding(수비유닛 flow-field)은 비용 커서 후속.
