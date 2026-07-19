# 9 — 헤더 유닛 슬롯 탭 = 제거 → 선택(상세 보기)로 변경

## 목적

편성 헤더의 찬 유닛 슬롯을 탭하면 **즉시 제거+저장**되던 동작을, **해당 유닛을 선택해 좌측 상세 패널에 표시**하는 동작으로 바꾼다. 오탭 한 번에 편성이 날아가는 문제를 없애고, 스톤 슬롯(탭=모드 진입)·컬렉션 셀(탭=선택)과 문법을 통일한다. 편성 해제는 상세 패널의 기존 [편성 해제] 버튼 경로로 일원화한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/SquadCharacterPageController.cs` — `OnUnitSlotTapped`
- `Assets/_Project/Scripts/UI/Outgame/SquadHeaderStrip.cs` — 클래스 헤더 코멘트("Tapping a unit slot removes it")만 갱신 (동작 코드 불변)
- `docs/spec/squad-character-page/README.md` — "편성 규칙" 계약 문구 갱신

## 구현

`OnUnitSlotTapped(int i)`:

- 찬 슬롯: `_selectedUnitId = squad.unitIds[i]` 후 상세 갱신. **편성 데이터 변경·Save 없음.** 상세 패널은 `inSquad=true`로 그려져 버튼이 자동으로 [편성 해제]가 된다(기존 `RefreshUnitMode` → `detailView.Show` 경로 그대로).
- 빈 슬롯: no-op(리프레시만) 유지.
- 스톤 모드 중 탭: 기존처럼 유닛 모드로 복귀하되, 찬 슬롯이면 그 유닛을 선택한 채 복귀(`EnterUnitMode`는 비-initial 호출 시 유효한 `_selectedUnitId`를 보존하므로 대입 후 호출).

제거 UI 신설(슬롯 X 버튼), 더블탭 토글, 헤더 슬롯 선택 하이라이트는 범위 밖(후속 후보). 브라우저 선택 하이라이트는 기존 `browser.SetSelected`가 자동으로 따라간다.

## 완료 기준

- compile 클린.
- Play: 편성된 유닛 슬롯 탭 → 덱 변화 없음 + 좌측 상세가 그 유닛으로 전환 + 버튼 라벨 [편성 해제].
- Play: [편성 해제] 버튼으로만 슬롯이 비워지고 저장됨. 빈 슬롯 탭은 아무 일 없음.
- Play: 스톤 모드에서 찬 유닛 슬롯 탭 → 유닛 모드 복귀 + 해당 유닛 상세 표시.

2026-07-19 사용자 Play 확인 · 커밋 ebfa923a
