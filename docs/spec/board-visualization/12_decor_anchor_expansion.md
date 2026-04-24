# 12. Decor Anchor Expansion

## 목적

`BoardDecorAnchorType` 을 5 종으로 확장하고 `BoardVisualPlanBuilder.AddDecorAnchors` 를 재작성한다. anchor 는 11 번 Prop distribution 의 seed 재료다. O(N²) 로 돌던 `members.Contains` 도 제거한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/BoardDecorAnchorType.cs`
- `Assets/_Project/Scripts/Data/BoardDecorAnchor.cs`
- `Assets/_Project/Scripts/Data/BoardVisualPlanBuilder.cs` (`AddDecorAnchors`)
- `Assets/_Project/Tests/EditMode/BoardVisualPlanBuilderTests.cs`

## 구현 가이드

1. `BoardDecorAnchorType` 에 추가:
   - `RegionCenter` (existing)
   - `RegionEdge` (existing, 복수 생성으로 변경)
   - `OuterBorder`
   - `NearWalkButSafe`
   - `Filler`
2. `AddDecorAnchors` 재작성:
   - `RegionCenter`: region 당 1 개, `anchorCell`.
   - `RegionEdge`: region 외곽 셀 전체. 기존 `break` 제거.
   - `OuterBorder`: region 외곽 셀 중 `borderProximity <= 1`.
   - `NearWalkButSafe`: Env region 셀 중 `pathProximity == 1`.
   - `Filler`: region 내부 중 위 카테고리에 속하지 않은 셀의 subset (예: region 크기 4 셀 당 1 셀 샘플).
3. `members.Contains(cell + offset)` O(N) 스캔 제거. 대신 `regionIds[index] == regionId` 로 O(1) 검사.
4. anchor 는 deterministic 순서로 저장 (x 오름차순 → y 오름차순).
5. 테스트:
   - 3×3 Env + 내부 Walk 1 셀 시나리오에서 각 anchor type 이 기대 위치에서 생성됨.
   - 큰 Env region (10×10) 에서 anchor 수량이 기대 범위.
   - Walk / Place region 에 anchor 타입 분포 제한 확인 (NearWalkButSafe 는 Env 에만).

## 완료 기준

- `BoardDecorAnchorType` 5 종 enum.
- `AddDecorAnchors` 가 5 종 모두 생성.
- `members.Contains` 사용 0 건.
- 테스트 5 건 이상 통과 (각 anchor type 당 최소 1 건).
- 같은 map 에서 anchor 수량이 seed 에 관계없이 일정 (map 구조 의존 only).

## 주의

- anchor 수는 region 크기 선형. 무제한 생성으로 대량 anchor 가 생기지 않도록 Filler 는 샘플링 비율 (예: cellCount/4) 로 제한.
- anchor 생성 순서는 deterministic 유지. 이후 11 번의 seed 선택이 같은 순서 전제.

확인 일자: 2026-04-24 / 커밋 해시: abcea4c
