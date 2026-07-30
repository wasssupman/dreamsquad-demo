# 1 — `AimMode.TileAim` 단일화 (카드 손패 고정 + 화살표 + 타일 리티클)

## 목적

Active 조준을 부착 카드와 같은 물리로 통일한다. 카드는 손패에 남고, 화살표가 타일을 겨눈다.
`ActiveTile` · `ActivePortal` 두 모드를 `TileAim` 하나로 접는다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` (포인터 추종 예외 제거)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`SetSkillAimCells` — unit 2 가 쓰는 API 를
  여기서 함께 추가해도 된다)

## 구현

1. **AimMode 재편**: `None, Defender, TileAim, EnemyMark`. `Classify` 에서 Active 는
   skill 이 있으면 **항상 `TileAim`**(포탈 포함, Portal 분기는 unit 2 가 `_portalEntryCell`
   상태로 처리). `SkillTargetType` 참조 제거.
2. **카드는 손패 고정**: `TileAim` 도 `Defender` 와 같이 `slot.rect.localScale = 1.08f` 강조 +
   `UpdateDragVisual` 에서 카드 위치를 건드리지 않는다.
   `IsPointerFollowing` 프로퍼티 삭제 → `DreamcatcherHandView.ApplyClearanceOffset` 의
   추종 카드 위치 보존 루프도 함께 삭제(손패 하강이 카드를 데려가도 문제없다).
   `IsPortalAiming` 기반 하강 유지 규칙은 그대로 둔다.
3. **화살표**: `TargetArrow.SetPath(cardTop, pointer, state, lockCenter)` 재사용.
   `lockCenter` = `bridge.GridToWorldCenterVector(cell)` → `MainCamera.WorldToScreenPoint`.
   조준 셀이 없으면(보드 밖) `lockCenter = null` + `ArrowState.Invalid`.
4. **조준 상태 갱신** (`OnDrag` 의 `TileAim` 분기):
   - `TryScreenToCell` → `_aimCell`.
   - 셀이 바뀐 프레임에만 `SetSkillAimRange(cell, skill)` 재호출(기존 `_lastRangeCell` 캐시).
   - `skill.TargetsAllies` 면 `bridge.CountDefendersInRange(cell, skill)` 로 `_aimAllyCount`
     갱신(셀 변경 시에만).
   - 유효성: 보드 안 AND (아군 대상이면 `_aimAllyCount > 0`).
5. **브리핑 문안**:
   - 조작법: `원하는 타일에서 놓으면 시전 · 손패로 놓으면 취소`
     (포탈은 unit 2 가 `놓아서 입구 지정 → 출구 타일 탭 …`).
   - 상태: 보드 밖 `타일 위로 끌어가세요` / 아군 대상 0기 `범위에 아군이 없습니다`(붉음) /
     아군 대상 N기 `놓으면 아군 N기에 시전`(초록) / 적 대상 `놓으면 이 위치에 시전`(초록).
6. **커밋**: 릴리즈 시 유효면 `CommitActiveTile(entryId, cell)` → `FlyCardToCell`.
   무효(보드 밖·아군 0기)는 `CancelDrag()`(무차감). Portal 은 unit 2.
7. **확정 비트**: `TileAim` 은 락온 엔티티가 없어 `Focus.TryCaptureConfirmCenter` 가 실패한다.
   조준 타일의 스크린 중심을 슬롯이 직접 캡처해 `CommitNow` 에 넘겨 `Focus.Confirm(center)`
   가 그 자리에서 터지게 한다. **`Focus.Begin` 은 호출하지 않는다** — 타일 조준에 dim/링/
   콜아웃을 얹지 않는다(범위 프리뷰가 이미 대상을 말한다).
8. `IsHoverAttachable` 의 "Active-DefenderUnit = 항상 유효" 가지 제거(이제 Defender 모드에
   Active 가 들어오지 않는다).

## 완료 기준

- [ ] 운석·감속장·회오리·공격폭증·속사 5종 모두: 카드가 손패에 남고 화살표가 타일을 가리키며
      범위 프리뷰가 따라온다. 릴리즈 = 시전.
- [ ] 아군 대상 2종: 범위에 아군이 없으면 화살표 붉음 + 릴리즈해도 무차감 취소.
- [ ] 손패 하강(드래그 중) 정상 — 카드가 화면에 남거나 튀지 않는다.
- [ ] 취소 4경로 무차감 유지. 콘솔 에러/워닝 0.

> 확인 2026-07-30 — 커밋 `e5cdb48a` · 사용자 Play 육안 확인 완료.
