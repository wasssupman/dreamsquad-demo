# 2 — 포탈 2단계 (입구 확정 → 화살표 기점 이동)

## 목적

포탈만 갖는 두 타일 입력을 통일된 문법 안에 넣는다. 1단계는 다른 Active 와 완전히 동일하고,
2단계에서 **화살표 기점이 손패 카드 → 입구 타일**로 옮겨가 선 자체가 입구→출구를 그린다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`SetSkillAimCells`)

## 구현

1. **1단계** = unit 1 의 `TileAim` 그대로. 릴리즈 시 `_portalEntryCell = cell` 을 잡고
   `EndInteraction` 을 타지 않는다(조준 유지: 화살표·`IsAiming`·툴팁 계속).
   보드 밖 릴리즈 / 손패 영역 릴리즈 = 취소.
2. **2단계 조준 루프** (`Update()`, 기존 폴링 유지):
   - 매 프레임 포인터 위치로 출구 후보 셀 계산.
   - 화살표: 시작점 = **입구 타일 스크린 중심**, 끝점 = 포인터, `lockCenter` = 출구 후보 타일
     중심. 유효 = 출구가 보드 안.
   - 점등: `bridge.SetSkillAimCells([입구, 출구후보])` — 타일맵 range/cells 는 서로를 지우는
     단일 채널이라 두 셀을 **한 번에** 칠해야 입구 표식이 유지된다(계약 8).
     출구 후보가 없으면(보드 밖) 입구만.
   - 상태줄: `입구 지정됨 — 출구 타일을 탭하세요` / 출구가 유효하면
     `놓으면 여기로 연결` (초록).
3. **커밋**: 두 번째 press → `CommitActivePortal(entryId, entry, exit)` → `FlyCardToCell(출구)`.
   확정 비트는 출구 타일 중심.
4. **취소**: 손패 영역 탭 · 보드 밖 탭 · ESC · phase 이탈 — `EndInteraction` 이
   `_portalEntryCell` 과 점등을 함께 걷는다(기존 깔때기 그대로).
5. `SetSkillAimCells` 는 `SetPlacementCells(cells, 1f, aimStyle: true)` + `SetRangeOwner(SkillAim)`.
   해제는 기존 `ClearSkillAimRange` 가 같은 owner 를 반납하므로 신규 정리 경로 없음.

## 완료 기준

- [ ] 포탈: 1단계가 다른 Active 와 시각적으로 동일. 릴리즈 후 화살표 기점이 입구 타일로 이동.
- [ ] 2단계 중 입구 타일 점등이 유지되고, 출구 후보가 함께 점등된다.
- [ ] 두 번째 탭에 포탈 생성(입구 진입 적이 출구로 이동). 손패로 탭 = 취소·무차감.
- [ ] 2단계 대기 중 매치 종료(phase 이탈) → 조준·점등·`IsAiming` 누수 없음.
- [ ] 콘솔 에러/워닝 0.
