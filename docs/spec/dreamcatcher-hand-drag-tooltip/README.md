# dreamcatcher-hand-drag-tooltip — 손패 드래그 시작 시 카드 성능 툴팁

상태: 작성 완료, 구현 대기 (2026-07-14)

## 목표

인게임 각성 손패에서 카드 드래그(스와이프)를 시작하면, 손패 바로 위 영역에 해당 카드의
성능(버프 수치 / 설명 / 스킬 요약)을 보여주는 작은 툴팁 패널을 띄운다. 게임플레이를
가리지 않는 패시브 정보 표시이며, 드래그 상호작용이 끝나면 사라진다.

`docs/spec/dreamcatcher-card-description` 이 후속 후보로 남긴 "인게임 손패 peek" 의 구현이다.
단, 트리거는 롱프레스가 아니라 **드래그 시작**(사용자 결정 2026-07-14).

## 검증 질문

"플레이어가 카드를 쓰기 직전(드래그 중)에, 시선 이동 없이 그 카드가 뭘 하는지 읽을 수 있는가?"

## 작업 단위

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| `0_card_text_formatter.md` | 리팩터 + 테스트 | 덱빌더 `PopupBody()` 를 공용 static 포맷터로 추출, Active 카드 지원 추가, EditMode 테스트 |
| `1_drag_tooltip_panel.md` | UI + 배선 | `DreamcatcherHandView` 에 툴팁 패널 신설, 드래그 시작/종료 훅 연결, Play 검증 |

## Feature-wide 계약

- **트리거**: `DreamcatcherCardDragSlot.OnBeginDrag` 성공 경로(AimMode 확정 후)에서 표시.
  press-to-lift(OnPointerDown/SetFocus)만으로는 뜨지 않는다.
- **숨김**: 드래그 상호작용의 실질 종료 시점(`EndInteraction` 깔때기)에서 숨긴다.
  `EndInteraction` 은 모든 *종료*(커밋/취소/비활성)의 공통 teardown 이고, 포탈 첫 탭은
  종료가 아닌 조준 상태 전환이라 호출되지 않는다 — 조준이 이어지는 동안 툴팁 유지.
  커밋/취소는 페이드 아웃, **손패 닫힘(`Close`/`ForceClose`) / 페이즈 이탈은 즉시 숨김**
  (손패는 침강 애니메이션 후 비활성되므로 페이드만으로는 형제 툴팁이 잔류할 수 있음).
- **위치**: 손패 패널(HandPanel) 바로 위, 하단 중앙 고정. 카드/손가락을 따라다니지 않는다.
- **비간섭**: 툴팁 전체 `raycastTarget = false`. 터치/드래그 판정에 어떤 영향도 없다.
- **텍스트 소스**: 카드 성능 텍스트는 공용 포맷터(unit 0) 단일 소스. 덱빌더 팝업과
  인게임 툴팁이 같은 함수를 소비한다. Squad/Unit 출력은 기존 덱빌더와 동일해야 한다
  (회귀 금지 — 단 `DamageVsCc` 오표기 수정 1건은 의도된 변경, unit 0 참조).
- **코스트**: 카드에 없다. `DreamcatcherHandController.CostOf(card)` 로 조회해 헤더에 표시.
- **런타임 구축**: 기존 `DreamcatcherHandView.BuildCanvas()` 관례를 따른다(프리팹 없음,
  같은 캔버스 sortingOrder 5, `SafeAreaRoot` 하위). 새 캔버스/매니저 신설 금지.
- **스코프 밖**: 손패 상시 노출 변경, 롱프레스 peek, 유닛(방어수) 스탯 표시.

## 후속 후보

- 롱프레스 press-peek (드래그 없이 정보만 보기) — card-description spec 시절부터의 원안.
- Defender 조준 중 호버한 유닛의 스탯을 툴팁에 병기.

## 파이프라인 커버리지

N/A — 플레이 오브젝트 신설/생성→렌더 경로 변경 없음. 순수 UGUI 위젯.
