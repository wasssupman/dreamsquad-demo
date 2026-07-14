# 1 — 드래그 툴팁 패널 + 훅 배선

## 목적

손패 카드 드래그 시작 시 손패 바로 위에 카드 성능 툴팁을 표시하고, 상호작용 종료 시
숨긴다. unit 0 의 `DreamcatcherCardText.Body()` 를 소비하는 첫 인게임 표면.

## 변경 대상

- 수정: `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs`
- 수정: `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs`

## 구현

### 패널 (DreamcatcherHandView)

- `BuildCanvas()` 에서 HandPanel 형제로 툴팁 루트 생성(같은 `SafeAreaRoot` 직속, 기본
  비활성. UnitStrip 의 X-회전 flip 대상이 아니도록 Strip/HandPanel 의 자식 금지):
  - **rev 1 (사용자 결정)**: 하단 중앙 고정이 아니라 **선택 카드 우측** — pivot (0,0),
    Show 시 슬롯 homePos 기준 x = 카드 우측 모서리(seated 1.08 확대 여유) + 간격,
    y = 카드 밑단 + rise. 우측이 safe area 를 넘으면 좌측 플립. 렌더는 패널 뒤
    sibling(이웃 카드 위). 카드 idle bob 문법의 플로팅 둥실거림(base 가산 sin,
    누적 금지). 폭 고정(≈ 480), 높이는 내용 기반(TMP preferred height).
  - **rev 2 (사용자 피드백)**: 배경은 **불투명** 다크 네이비 + 골드 보더 + 하단 그림자.
    카드 위에 렌더되는 패널이라 반투명은 카드 아트가 비쳐 시인성을 죽인다 — 부양감은
    알파가 아닌 bob + 그림자 담당(HS/StS 툴팁 관례). 헤더 TMP(카드명 + 코스트) +
    본문 TMP(`DreamcatcherCardText.Body`).
  - **모든 Graphic 의 `raycastTarget = false`** (배경 포함). CanvasGroup 으로 페이드.
- `public void ShowDragTooltip(int slotIndex)` — 슬롯의 `card` 로 헤더/본문 채움.
  코스트는 `Controller.CostOf(slot.card)` (기존 슬롯 코스트 렌더와 동일 정책, L661~667).
  짧은 스케일+페이드 인(기존 손패 spring 감성, `Update` 에서 lerp — 신규 코루틴/tween
  라이브러리 금지).
- `public void HideDragTooltip(bool immediate = false)` — 기본은 페이드 아웃 후 비활성,
  `immediate` 는 즉시 비활성. **멱등 필수**: `EndInteraction` 은 드래그 중이 아닐 때도
  호출된다(`OnDisable` L355 상시 경로) — 미표시 상태에서 불려도 no-op.
- 강제 숨김 경로: `Close()`/`ForceClose()`(게이지 토글·ESC·`OnPhaseChanged` 페이즈 이탈,
  L449~466)에서 `HideDragTooltip(immediate: true)`. 손패는 침강 애니메이션 완료 콜백
  (L606~611)에서야 비활성되므로, 형제인 툴팁을 페이드에 맡기면 침강 중 잔류한다 —
  닫힘 계열은 전부 즉시 숨김.

### 훅 (DreamcatcherCardDragSlot)

- `OnBeginDrag` 성공 경로(AimMode 확정 후, L88 이후)에서 `_view.ShowDragTooltip(_index)`.
- `EndInteraction()` 에서 `_view.HideDragTooltip()`. 검증됨(2026-07-14 코드 확인):
  커밋(`CommitNow` L234)/취소(`CancelDrag` L243)/패널 비활성(`OnDisable` L355)이 전부
  이 깔때기로 수렴하고, 포탈 첫 탭(L185-188)은 호출하지 않아 "포탈 조준 중 유지" 계약과
  일치한다. 별도 예외 처리 불필요.

### 비간섭 확인

- 툴팁이 떠 있는 동안 Defender 조준 화살표·Active 카드 팔로우·유닛 호버 판정이
  기존과 동일해야 한다(레이캐스트 차단 없음).

## 완료 기준

- compile 클린, console 에러 0.
- Play 검증 (스크립트 배틀 또는 수동):
  - Squad/Unit/Active(타일)/Active(포탈) 각 1장씩 드래그 시작 → 툴팁 표시, 내용 정확.
  - 드래그 취소(손패 안 드롭) / 커밋 성공 / ESC → 툴팁 소멸.
  - 포탈 2탭: 첫 탭 후에도 유지, 출구 확정/취소 시 소멸.
  - 툴팁 표시 중 유닛 호버 틴트·조준 화살표 정상.
  - 페이즈 전환(Battle 종료)으로 손패 강제 닫힘 시 툴팁 잔류 없음(즉시 숨김).
  - **드래그/포탈 조준 도중 게이지 토글로 손패 닫기** — 침강 중·후 툴팁 잔류 없음.
- 게임뷰 스크린샷으로 위치(손패 위, 보드 중앙 미침범) 육안 확인.
