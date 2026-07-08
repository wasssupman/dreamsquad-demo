# 4 — Handoff Summary

## Commit

- (이 커밋) feat(ui): 결과 팝업 리더보드 비주얼 업그레이드 + RESTART 겹침 해소 — result-screen-visual-upgrade (코드 + 테스트 + 문서)

## Implemented

- `UiRoundedSprite`(신설, static): 절차적 라운드렉트(`Make`) + 원 배지(`MakeCircle`) 공용 헬퍼. `ScoreHudView` 사설 `MakeRoundedRectSprite` 를 이 헬퍼로 위임(동작 동일).
- `ResultScreen` 전면 리스킨: dim 오버레이(`UiOverlay.Dim`) 위 네이비 라운드 패널 + 골드 테두리 + VICTORY/DEFEAT 골드 탭 헤더 + `YOUR SCORE` 서브라인.
- 리더보드 = 행별 라운드 플레이트 + 순위 배지(1 골드/2 실버/3 브론즈/그 외 네이비칩), 본인 행 골드 틴트+테두리+골드 텍스트, 미배정 슬롯 `WAITING...` 회색.
- **RESTART 하단 고정 바** — 헤더/리스트/푸터 3영역 앵커 레이아웃으로 재구성. 기존 단일 `VerticalLayoutGroup`(버튼이 리스트 위로 뜨던 겹침 결함) 폐기.
- 순수 `BuildRows(entries, maxEntryCount, ownUserId)` → score 내림차순 rank(서버 rank>0 우선), maxEntryCount 까지 WAITING 채움, 본인 플래그. 봇 폴백·실 랭킹 공용 렌더 경로.
- tournament-play-report 배선 불변: `ShowVictory/ShowDefeat/UpdateLeaderboard/RestartRequested` 시그니처·BattleBridge 호출부 그대로. 내부 렌더링만 교체.

## Key Files

- `Assets/_Project/Scripts/UI/UiRoundedSprite.cs` — 공용 절차 스프라이트.
- `Assets/_Project/Scripts/UI/ResultScreen.cs` — 팝업 셸 + 행 렌더 + `BuildRows`.
- `Assets/_Project/Scripts/UI/ScoreHudView.cs` — 헬퍼 위임(1줄).
- `Assets/_Project/Tests/EditMode/ResultLeaderboardModelTests.cs` — `BuildRows` 6 테스트.

## Verified

- compile 0 에러. EditMode `ResultLeaderboardModelTests` 6/6.
- 인게임 1차(사용자 스크린샷): 패널·행·배지·본인 강조·WAITING·DEFEAT 붉은 탭·서버 랭킹 교체·RESTART 하단 고정 렌더 확인.
- dim 오버레이(배경 아트 제거) 버전: 프리뷰 하네스 재캡처 확인 + 사용자 마무리 승인.

## Notes

- **배경 번복(load-bearing)**: 최초 "시즌 배경 아트" 결정 → 인게임에서 풀스크린 아트가 배틀 보드까지 덮어 폐기. backdrop = `UiOverlay.Dim` 단색만. **되돌리지 말 것.**
- **직렬화 필드 0**: 팔레트/dim 은 `private static readonly` 코드 상수. `ResultScreen` 에 `[SerializeField]` 없음 → BattleScene diff 0(HEAD clean). 씬 와이어링/시즌 텍스처 임포트 변경 없음(cosmic 은 Texture2D 로 원복).
- **`??` 금지**: `GetComponent<T>() ?? AddComponent<T>()` 는 Unity fake-null 을 안 걸러 에디터 NRE. `if (== null)` 패턴 유지.
- 행 교체는 detach-then-Destroy(플레이 모드). 결과 팝업은 매치당 1회라 풀링 불필요.
- **최상위 모달 (nested canvas + dim 렌더)**: `ResultScreen` 은 씬에서 root `ResultCanvas`(overlay, order 0) 아래 **중첩**된다. 중첩 canvas 는 `overrideSorting=true` 없이는 `sortingOrder` 를 무시하므로 반드시 `overrideSorting=true` + `sortingOrder=2000` 을 함께 둔다(이게 없으면 order 0 로 취급돼 ScoreHud 6·MENU 1000 이 dim 위로 샌다). **되돌리지 말 것.**
- **dim 은 명시 스프라이트 필수**: 이 경로에서 null-sprite `Image` 는 렌더되지 않아(보드가 dim 안 먹고 밝게 보임) `UiRoundedSprite.Make` 흰색 스프라이트를 dim 에 부여한다. 빨강 배경 테스트로 dim 커버 검증.

## Follow-up

- 등장 애니메이션(패널 슬라이드/스코어 카운트업/배지 팝) — 정적 레이아웃 확정 후 별도.
- 리스트 10행 초과 시 ScrollRect(현재 maxEntryCount=10 고정이라 불필요).
- 한글 TMP 폰트(login-gate 백로그 공통).
