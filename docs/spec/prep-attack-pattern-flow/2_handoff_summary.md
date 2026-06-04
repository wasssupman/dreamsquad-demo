# 2 — Handoff Summary

## Commit

- (이 커밋) `feat(prep-attack-pattern-flow): Squad 준비단계 공격패턴 자동 인트로 + 자동 진행`
- 선행: `e60cc56` (START-게이트 초기 버전 — 본 커밋이 대체)

## Implemented

- Squad 모드 진입 시 공격패턴(`WavePatternStripView`)이 자동으로 펼쳐짐 → ~1초 유지 → 위로 사라짐(Roll).
- 사라진 뒤 **자동으로** `RequestPlacement()` 호출 → 드캐 3중1 → 배치. START 버튼/대기 화면 제거.
- 공격패턴이 완전히 사라진 뒤 드캐가 뜨도록 인트로 코루틴이 Unroll 완료 → dwell → Roll 완료를 순서대로 대기.
- 공격패턴("!") + 맵 설정("MAP SETTINGS") 토글이 배치·전투 내내 생존 — 언제든 열람/조정 가능.
- 드캐 모달(`sortingOrder=50`)이 토글(`8`) 위를 덮어 드캐 선택 중에는 자연 차단.

## Key Files

- `Assets/_Project/Scripts/UI/Outgame/SquadPrepView.cs` — 전면 재작성(화면 chrome 제거 → Canvas 호스트 + 자동 인트로 + 자동 진행).
- `Assets/_Project/Scripts/UI/Draft/WavePatternStripView.cs` — 변경 없음(공개 API 조합만 사용: `Unroll/Roll/SnapHidden/RebuildFromDeck/SetToggleEnabled/CurrentState`).
- `Assets/_Project/Scripts/UI/Draft/MapSettingsPanelView.cs` — 변경 없음(자체 토글 보유, active 유지만).
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherController.cs` — 변경 없음(첫 픽은 `SetPhase(Placement)` 트리거).

## Verified

- compile: CS 에러 0 (UnityMCP refresh + read_console).
- Play (사용자, 2026-06-04): 펼침 → ~1초 유지 → 사라짐 → 드캐 → 배치 순서 확인. 배치/전투 중 토글 동작 확인.

## Notes

- **인트로 타이밍은 load-bearing**: `Unroll()` 은 카드 stagger 로 ~1.3s 후에야 `Shown` 이 된다. dwell 을 Unroll 완료 전부터 세면 Roll 을 건너뛰고 즉시 진행 → 공격패턴 위에 드캐가 겹쳐 뜬다. 코루틴의 `Unrolling`/`Rolling` 폴링 대기를 제거하지 말 것.
- realtime 대기 사용: `RequestPlacement()` 전까지 `timeScale==1` 이라 Roll 퇴장이 끝난 뒤 드캐(timeScale=0)가 뜬다.
- `SquadPrepView` 는 더 이상 START/타이틀 chrome 을 만들지 않는다. 씬에서 strip/맵설정은 `SquadPrepView`(Canvas 호스트, `order 8`)의 하위여야 렌더된다.
- 적용 범위는 Squad 모드만. Draft 모드(`DraftView`)는 기존 Unroll→dwell→Roll 유지.

## Follow-up

- README "후속 후보" 참조 — 특히 **배치/전투 중 맵 설정 변경 시 맵 재생성 안전성**(토글로 노출된 `MapSettingsPanelView` 가 배치 이후 맵 재빌드 시 충돌 가능)은 미검증.
