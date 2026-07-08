# 4 — 포트레이트 크기 조정 + 부수 UI 수정

## 목적

사용자 육안 피드백(2026-07-08) 반영. 포트레이트를 더 크게 보이게 하고, 겸사겸사
드림캐쳐 덱 화면의 타이틀 가림 문제를 함께 고친다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/SquadBuilderView.cs` — 유닛 피커 셀 확대.
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — 배치 스트립 패널 확대.
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs` — 타이틀 가림
  수정 (이번 spec 범위 밖이지만 UI 수정으로 함께 처리).

## 구현

1. **스쿼드 피커 아이템 50% 확대**: 유닛 선택 모달 셀 `150→225`, 아이콘 `96→150`,
   라벨 폰트 `17→22`. 그리드는 폭에 맞춰 열 수가 줄고 스크롤로 흐른다.
   - 참고: 상단 편성 슬롯 바(SlotsRow, 1000×140, 7슬롯 고정, childForceExpand=false)는
     슬롯을 50% 키우면 폭이 넘쳐 유지. "유닛 포트레이트 아이템"을 브라우즈용 피커
     컬렉션으로 해석. 편성 슬롯도 키우려면 SlotsRow(scene) 확대 필요 — 별도 후속.

2. **인게임 선택 UI 20% 확대**: `DefenderSelector` 런타임 패널 `760×100 → 912×120`.
   슬롯은 HorizontalLayoutGroup childForceExpand 로 패널을 채우므로 패널만 키우면
   균등 확대.

3. **드림캐쳐 타이틀 가림 수정**: PanelTitle(RectTransform y −60..−180)과 겹치던
   MY DECK 프레임을 `DeckFrameTopY −128 → −196` 으로 내려 타이틀 아래로 비킴.
   내린 만큼 컬렉션 높이 `430 → 400` 으로 줄여 하단 상태텍스트(y 180..236)/버튼과
   안 겹치게 함. (모두 코드 내 상수, 씬 편집 없음.)

## 2차 조정 (2026-07-08 육안 피드백 rev)

4. **스쿼드 편성 슬롯 확대**: 상단 7슬롯 `120→165`, 아이콘 `96→130`. 이에 맞춰
   씬 `SquadPanel/SlotsRow` RectTransform `sizeDelta 1000x140 → 1260x190`
   (7×165 + 6×12 = 1227 < 1260). 스톤 행은 slotsContainer 기준 런타임 배치라 자동 추종.
   → **씬 편집 포함**(OutgameScene 저장 필요).

5. **SELECT UNIT 그리드 상향**: 피커 스크롤 밴드 `anchoredPosition (0,-170)→(0,-34)`,
   `sizeDelta (1400,440)→(1400,700)`. 타이틀(y 380, 하단 340) 기준 그리드 top
   `-34+350 = 316 = 340−24` → 타이틀과 24px 간격. 밴드가 커져 더 많은 카드가 보임.
   (스톤 피커도 같은 밴드 공유 → 동일 상향.)

6. **인게임 선택 UI 딤/테두리 제거**: `DefenderSelector` 포트레이트 슬롯의 상시
   per-class 색 테두리와 비선택 딤 제거. 배경을 기본 `Color.clear`, 포트레이트는 항상
   풀 밝기. 선택된 슬롯만 골드 프레임(`SelectionFrameColor`). 폴백(포트레이트 없음)은
   기존 단색 유지.

## 완료 기준

- 컴파일/콘솔 클린. ✅
- (육안) 스쿼드 피커의 유닛 아이템이 확연히 커지고 그리드가 재배치됨.
- (육안) 인게임 배치 스트립의 유닛이 약 20% 커짐.
- (육안) 드림캐쳐 덱 화면에서 "DREAMCATCHER DECK" 타이틀이 MY DECK 에 가리지 않고,
  컬렉션이 하단 버튼/상태텍스트와 겹치지 않음.

---
완료 확인: 2026-07-08 · 커밋 95e1099b
