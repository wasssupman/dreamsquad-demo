# Draft UX Upgrade — Design

게임 시작 시 공격 패턴 페이즈와 드래프트 페이즈를 한 흐름으로 통합하고, Slay-the-Spire 식 카드 fan + 폐기 인터랙션으로 카드 인터랙션을 재설계한다. 트윈 라이브러리는 PrimeTween.

## 목표

- Briefing / Draft 두 페이즈를 단일 Draft 페이즈 안의 sub-state 시퀀스로 통합한다.
- 공격 패턴 정보는 화면 상단 가로 strip 으로 unroll → 2초 dwell → roll, 좌측 중앙 토글 버튼으로 임의 재펼침/접기를 제공한다.
- 드래프트 카드 풀(10장)을 화면 하단 fan 으로 등장시키고, "7장 픽" 대신 **3장 폐기** 모델로 의미 반전 후 자동 confirm 한다.
- 폐기 트리거는 카드 클릭 또는 위 방향 스와이프(드래그-throw). 호버 효과는 없음.
- 모든 시각 트랜지션은 PrimeTween Sequence 로 구성한다.

## 아키텍처 요약

- 새 폴더 `Assets/_Project/Scripts/UI/Draft/` 에 `WavePatternStripView`, `DraftCardFanView`, `DraftCardView`, `MapSettingsPanelView` 와 슬림 오케스트레이터 `DraftView` 를 둔다.
- `GamePhase.Briefing` 제거. `TimelineBriefingView` 삭제, MAP SETTINGS UI 만 추출하여 좌상단 작은 토글로 이관(개발 옵션, 게임 영역과 비충돌).
- `DraftSession` 의 의미를 픽→폐기 모델로 반전: `DiscardCount = 3`, `PickedArray = Pool − Discarded`.
- 입력은 Unity UI EventSystems (`IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`, `IPointerClickHandler`) 만 사용. 임계값: 위 스와이프 = 누적 delta.y ≥ 120px(1080 ref) 와 duration ≤ 0.45s, 클릭 = 드래그 거리 < 30px.

## 비목표 (후속 후보)

- THIS ROUND SKILLS 패널의 카드형 비주얼 통합 (현 우측 박스 그대로)
- Battle / Placement 페이즈 중에도 공격 패턴 strip 토글 유지
- 카드 일러스트, 프레임 아트 (인터랙션과 레이아웃만 본 spec 범위)
- Undo / 폐기 취소
- Redraft 전용 단축 연출 (현 안: 매 BeginDraft 마다 동일 시퀀스)

## 스펙 분할

세부 구현 계약과 작업 단위는 `docs/spec/draft-ux-upgrade/` 에 분산한다.
