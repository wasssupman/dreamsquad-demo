# 0 — 최저 체력비율 아군 랭킹

## 목적

아군 타겟 방어 유닛(힐러·버프러)의 대상 선정을 "사거리 내 최근접 아군" → "사거리 내 최저 체력비율(%) 아군"으로 바꾼다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Combat/LowestHealthTargeting.cs` — 순수 비교자.
- 신규 `Assets/_Project/Tests/EditMode/LowestHealthTargetingTests.cs` — 비교자 단위 테스트.
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — 단일 후보 루프 + AoE 확장 루프 랭킹 분기.

## 구현

1. `LowestHealthTargeting` (`FrontmostTargeting` 미러):
   - `Candidate { float hpRatio; float sqDist; int entityIndex; int entityVersion; }`
   - `RanksBefore(in a, in b)` — hpRatio → sqDist → index → version 오름차순.
   - `SelectLowest(cands, count)` — 테스트/편의용 최소 인덱스.
2. `AttackSystem.OnUpdate`:
   - `rankByHealth = mask == (int)Faction.Defender && defenderTagLookup.HasComponent(attackerEntity)`.
   - **단일 후보 루프**: `rankByHealth` 면 각 in-range 후보를 `Health.ComputeRatio` 로 `Candidate` 화해 `RanksBefore` 로 heal-best 추적. 루프 종료 후 `healHasBest` 면 `bestTarget`/`bestTargetPos` override. (후보 집합은 최근접 스캔과 동일 필터라 발산 없음, 랭킹만 교체.)
   - **AoE 확장 pass 루프** (`desiredCount > 1`, non-guardian): `rankByHealth` 면 남은 후보를 비교자로 선정, 아니면 기존 `d2 < passSq` 최근접. seed(primary)는 이미 override 된 `bestTarget`.
   - guardian 경로(AggroCapacity) 무변경 — 힐러는 가디언이 아니다.

## 완료 기준

- compile 무에러 (신규 .cs 는 `refresh_unity scope=all`).
- EditMode: `LowestHealthTargetingTests` 그린 — hpRatio 지배 / 동률 시 거리 tie-break / 거리·비율 동률 시 Entity tie-break / `SelectLowest` 빈배열 -1.
- Play: 힐러 사거리 내에 다친 아군 + 풀피 아군이 섞였을 때 힐이 다친 쪽에 집중(사용자 확인).

---
완료 확인: 2026-07-25 사용자 Play 확인 — 힐이 사거리 내 최저 HP비율 아군에 집중됨. 투트랙 리뷰(code-reviewer·ecs-reviewer) 양측 APPROVE. 구현 커밋 2da3ccc9
