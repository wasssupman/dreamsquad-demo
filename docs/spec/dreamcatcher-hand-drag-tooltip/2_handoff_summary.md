# 2 — Handoff Summary

## Commit

- `82c770ba` feat: 카드 본문 공용 포맷터 추출 (unit 0)
- `80910e3e` docs: unit 0 스탬프
- `fdbf12e6` feat: 드래그 성능 툴팁 (unit 1, rev 1~2)
- `6d523a1b` feat: press 기반 툴팁 (unit 1 rev 4 — 트리거 최종형)

## Implemented

- `DreamcatcherCardText.Body(card)` — 카드 성능 텍스트 단일 소스(덱빌더 팝업 + 인게임 툴팁 공유). exhaustive kind 매핑으로 `DamageVsCc` "Cost Rate" 오표기 수정, `ACTIVE` 라벨 신설(구 코드는 SQUAD 폴백).
- **트리거 최종형(rev 4)**: `OnPointerDown`(press-to-lift 동일 시점)에 선택 카드 우측에 표시, press 중 상시 유지 — usable/dim 무관. 해제(`OnPointerUp`) 시 숨김, 드래그로 이어지면 `EndInteraction` 깔때기·포탈은 조준 종료가 걷는다. `CanPeek` = CanStartDrag−usable(타 인터랙션 중 소유권 가드). 코스트는 `Controller.CostOf`.
- 숨김: `EndInteraction` 깔때기(커밋/취소/OnDisable) = 페이드, `Close`/`ForceClose`(토글·ESC·페이즈 이탈) = 즉시. 포탈 첫 탭은 종료가 아니라 조준 전환이라 유지됨.
- 불투명 다크 네이비 + 골드 보더 + 하단 그림자, 카드 idle bob 문법의 플로팅. 전 Graphic `raycastTarget=false`.
- 우측이 safe area 초과 시 좌측 플립(현재 4장 손패에선 미발동 — 로직만 존재).

## Key Files

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardText.cs`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` (BuildTooltip/ShowDragTooltip/HideDragTooltip/TickTooltip)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` (훅 2개)
- `Assets/_Project/Tests/EditMode/DreamcatcherCardTextTests.cs`

## Verified

- EditMode 760개 중 758 통과(스킵 2 = 기존 Ignored), 신규 5개 포함. 콘솔 에러 0.
- Play(테스트 모드 캐리 + TimeManager 배틀 동결 + 리플렉션 드래그 시뮬): 카드 4종 표시, 취소/커밋 페이드, 포탈 2탭 유지→취소 소멸, 게이지 토글 mid-drag 즉시 숨김, Result 페이즈 강제 닫힘 잔류 없음. 사용자 Play 육안 확인 완료.

## Notes (되돌리면 안 되는 것)

- **트리거를 스와이프-시작으로 되돌리지 말 것**: rev 3(스와이프 peek)은 실기에서 재시도 시 미표시 버그 + "스와이프가 트리거인 게 어색하다"는 사용자 판정으로 폐기됨(미커밋). press 모델이 최종.

- **불투명 배경**: 반투명은 카드 아트가 비쳐 시인성 저하로 사용자 기각(rev 2). 부양감은 bob+그림자 담당.
- **툴팁은 SafeAreaRoot 직속 + 패널 뒤 sibling(카드 위 렌더)**. Strip/HandPanel 자식 금지(X-flip 오염).
- `HideDragTooltip` 멱등 필수 — `EndInteraction` 은 `OnDisable` 상시 경로에서도 불린다.
- Active 툴팁 본문은 authored description 만 — `SkillData.cooldownSec` 은 각성 손패 경로에서 미소비(레거시 SkillRuntime 전용)라 자동 표기는 오정보.
- 씬 배선 없음(전부 런타임 구축). 신규 SerializeField 노브(tooltipWidth/Gap/Rise/BobY/BobX/BobFreq)는 코드 기본값이 소스(씬에 미베이크).

## Follow-up

- 롱프레스 press-peek(드래그 없이 정보 보기) — README 후속 후보.
- Defender 조준 중 호버 유닛 스탯 병기 — README 후속 후보.
- 좌측 플립 경로는 손패가 5장+ 로 넓어지면 실기 확인 필요.
