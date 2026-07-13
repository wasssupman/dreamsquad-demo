# 2 — 타일 타겟 일반화 + 배선

## 목적

Attach(유닛) 외 **Active 스킬 커밋**(Active-Defender 셀 / Active-Tile / Active-Portal)도 같은 카드 비행 +
찰싹 흡수를 받는다. 타겟이 유닛이 아닌 **타일(셀)** 이므로 고정 view 중심으로 비행하고, 유닛 펀치/플래시 없이
**공통 월드 반응**(링/버스트 + SFX + 카메라 킥)만 발생. unit 0/1 의 presenter·반응을 재사용.

## 변경 대상

- **수정** `Bridge/BattleBridge.cs` — `GridCellToViewCenter(Vector2Int)` 게이트웨이(셀 sim→view).
- **수정** `UI/Dreamcatcher/DreamcatcherHandView.cs` — `FlyCardToCell(...)` + 공통 반응 `FireAbsorbImpactWorld` 분리.
- **수정** `UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — Active-Defender / ActiveTile / ActivePortal 커밋 성공 발화.

## 구현

### ⚠ sim/view 좌표 (load-bearing)
`GridToWorldCenterVector` 는 `_boardOrigin + cell*tileSize` = **sim** 공간. 유닛 케이스는 `transform.position` =
**view**. 좌표계를 맞추려면 타일도 view 여야 함(비행 스크린 투영·임팩트 VFX 가 sim/view 혼용으로 어긋나지 않게).
→ 게이트웨이 `GridCellToViewCenter` = `BoardSpace.ToView(GridToWorldCenterVector(cell))`. 변환은 bridge 가 소유.
평면 타일맵이라 sim-Y drop 은 무해([[project_boardspace_drops_sim_y]]) — 타일은 지면.

### 타겟별 발사
- **Active-Defender**(Defender 모드, 카드 손패 고정): 셀 = 방어유닛 위치. 손패 → 셀 view 중심 비행. 스킬은 셀
  캐스트라 유닛 펀치 없이 공통 반응.
- **Active-Tile**(카드-follow, 포인터가 타겟 위): 발사점이 이미 타겟 근처 → **짧은 찰싹**(사실상 즉시 흡수). 자연스러움.
- **Active-Portal**(two-tap): 두 번째 탭(출구)에서 확정 → **출구 타일**로 비행.

### 고정 타겟 provider
`FlyCardToCell` 의 provider 는 매프레임 같은 값(`GridCellToViewCenter(cell)`) 반환 — 추적 로직 재사용하되 정지 타겟.
onImpact = `FireAbsorbImpactWorld`(유닛 조회 없이 링/버스트+SFX+킥).

## 완료 기준

- [ ] compile 클린, 콘솔 에러 0.
- [ ] Play: Active-Tile 스킬(예: Meteor)을 타일에 캐스트 → 그 타일에 찰싹(링/버스트+SFX+킥).
- [ ] Active-Portal 확정 시 출구 타일에 찰싹.
- [ ] Active-Defender 스킬 캐스트 시 해당 셀에 찰싹.
- [ ] 링/버스트가 **정확한 타일 위치**(sim/view 이중변환 없음).
- [ ] 취소/off-board 탭 시 반응 전무(비용 0).
- [ ] ECS 시뮬 변경 0.

---
**확인 2026-07-13**: compile 클린. Attach/Active-Defender/Tile/Portal 4경로 발화, 셀 view 중심 고정 비행.
사용자 Play 검증 통과. 커밋: units 1+2 통합.
