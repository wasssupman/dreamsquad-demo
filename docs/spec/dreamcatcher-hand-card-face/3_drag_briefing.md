# 3 — 드래그 툴팁 역할 전환: 카드 설명 → 조작 브리핑

## 목적

카드 면이 설명을 담게 됐으므로(unit 1), 상단 드래그 툴팁의 중복 설명을 걷어내고
**조작법(모드별 고정) + 조작 상태(실시간)** 브리핑으로 역할을 바꾼다 (2026-07-25 사용자 확정).

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — 툴팁 API 교체
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — 문안 결정 + 상태 갱신 호출

## 구현

- `ShowDragTooltip(slotIndex)`(이름+코스트+BodyCompact) → **`ShowDragBriefing(controls, status)`**
  + **`UpdateDragBriefingStatus(status)`**(동일 문자열 조기 반환 — OnDrag 매프레임 호출).
  위젯(상단 중앙 고정·bob·페이드)과 `CardPeeked` 튜토리얼 훅은 유지.
- **조작법(header, AimMode 별 고정)**: Defender 부착/시전 · ActiveTile · ActivePortal(2탭) ·
  EnemyMark. 문안은 드래그 슬롯의 `ControlsFor` — AimMode 분류를 아는 유일한 곳.
- **상태(body, 실시간, 색 코딩)**: 초록=커밋 가능(`놓으면 이 유닛에 부착`), 적색=불가/취소
  (`여기서 놓으면 취소`, `각성치가 부족합니다`, `이미 표식이 있는 적`), 무색=안내(`아군 유닛
  위로 끌어가세요`). 전이 지점: press(사용 가능/불가) → BeginDrag → OnDrag(호버·취소영역
  매프레임) → 포탈 1탭(`입구 지정됨 — 출구 타일을 탭하세요`).
- 카드 설명은 손패 카드 면과 lockon 콜아웃이 담당 — 툴팁에서 완전 제거. 덱빌더/상세 팝업 무변경.
- 문안 매핑은 자명 분기 + 호출처 1 (CLAUDE.md 제약 10 판정: 순수 함수 추출 대상 아님).

## 완료 기준

- compile 클린 + 기존 EditMode 무회귀.
- Play: press 시 조작법+상태 노출, 드래그 중 호버/취소영역 진입에 따라 상태 줄이 실시간 변경,
  포탈 1탭 후 "출구 타일을 탭하세요" 유지, 커밋/취소 시 기존 퇴장 페이드 정상.

확인 2026-07-25 — 사용자 Play 확인(적 표식 무호버 문안 "적에게만 쓸 수 있습니다" 수정 반영).
겸사 픽스: 적 지정 카드 태그 칩 "아군 부착" 오표기 → `DreamcatcherCard.HasBountyMark()` 단일
소스화로 "적 지정" 표기(EditMode 19/19).
