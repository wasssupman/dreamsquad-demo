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
