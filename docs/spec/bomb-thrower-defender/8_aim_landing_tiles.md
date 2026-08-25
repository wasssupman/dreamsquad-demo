# 8 — 조준 rework: 착지 타일 클릭 (Play 후속)

> ⚠ **은퇴 — 역사 기록.** unit 9(2026-08-21)가 조준 페이즈 자체를 없앴다. 폭탄맨은
> 사거리 안 최근접 적의 칸에 던진다. 여기 적힌 `PaintLandingCells`·`DirectionAimController`
> 폭탄 분기는 코드에서 삭제됐다. 지금의 계약은 `9_nearest_target_rework.md` 를 볼 것.

## 목적

폭탄맨 조준을 머신거너식(전 레인 십자 + 화살표, 방향 누르기)이 아니라 **자신 기준
상하좌우 N타일(착지 후보)만 하이라이트 → 그 타일 클릭 = 방향 확정**으로 바꾼다.
"어디로 쏘나"보다 "어디에 떨어지나"가 폭탄의 판단 기준이라 착지 셀이 곧 조준점.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SetAimGuide` 폭탄병 모드(착지셀, 화살표 없음) + `PaintLandingCells`
- `Assets/_Project/Scripts/UI/DirectionAimController.cs` — `Resolve` range = N 분기

## 구현

- **`SetAimGuide` 분기**(`unit.bombLandingTiles > 0`): `PaintLanes`(전 레인) 대신
  `PaintLandingCells(center, N, selected, alpha)` — 미선택이면 4 착지 셀(center±N cardinal)
  dim, 선택되면 그 착지 셀 1개만 full. `AimCardinals`+`_laneCellScratch`+기존
  `tilemapMapView.SetPlacementCells` 재사용. **화살표 없음** → `ClearAimArrows()` 후 return
  (레인+화살표는 머신거너 전용, 두 모드가 실제로 다름). 머신거너 경로 무변경.
- **`DirectionAimController.Resolve`**: range = 폭탄병이면 `bombLandingTiles`, else
  `RangeToTiles(attackRange)`. `DirectionAimLogic.Evaluate(caster, cell, range)` **그대로
  재사용**(lenient — 그 방향 N칸 내 탭 = 확정). `SetAimGuide` 는 unit 을 받아 자체 분기하므로
  Begin/RepaintGuideIfChanged 호출부는 무변경.
- `DirectionAimLogic` **무변경**. sim(AttackSystem) **무변경** — 확정 cardinal→DeployedFacing→
  기존 ResolveCell(caster, facing, N). 신규 시스템/채널 0.

## 완료 기준

- [ ] compile 0.
- [ ] (Play) 폭탄맨 배치 시 상하좌우 N(=3)칸 착지 타일 4개만 하이라이트(레인/화살표 없음) →
  한 타일 탭 → 그 방향으로 착지 확정. 머신거너 조준(레인+화살표) 무변경.
