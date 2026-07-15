# 3 — 인계 요약

## Commit

- `986efa09` feat(game-start-loadout-gate): unit 0 — 순수 로드아웃 게이트 판정
- `650135f8` feat(game-start-loadout-gate): unit 1 — 신규 프로필 기본 덱 시딩
- unit 2 (팝업 + START 배선) — 같은 브랜치, 본 문서와 함께 커밋

## Implemented

- 로비 START 가 스쿼드(정확히 7)와 드림캐쳐 덱(`DeckRules`, 실사용 8장) 충족을 검사하고, 미충족이면 **씬 전환 없이** 팝업으로 안내 + 해당 편성 패널로 이동시킨다.
- `LoadoutGate.Check(profile, units, cards, shortfalls)` — 순수 판정. 규칙을 재정의하지 않고 `DeckRules.Validate` / `SquadDraw.Resolve` 에 위임한다.
- 스쿼드 판정이 stale 유닛 id(리네임 후)와 중복을 걸러낸다 — `FilledCount()` 로는 둘 다 "충족" 으로 통과했다.
- `ProfileStore` 가 기본 덱을 소유하고 신규(및 덱 없는 기존) 프로필에 시딩한다. `BuildDefaultDeck` 이 dev 전용 `DefaultLoadoutButton` 에서 이관됐다.
- `LoadoutGatePopup` — 미충족 항목만 `5/7` 형태로 나열, 해당 대상 이동 버튼 + 닫기. 스크림 탭 dismiss.
- 배선 참조 3개(`gatePopup`/`catalog`/`cardCatalog`) 미배선 시 LogError + 차단 (fail-loud).

## Key Files

- `Assets/_Project/Scripts/Core/Profile/LoadoutGate.cs` — 판정 (스쿼드 규칙의 소유자)
- `Assets/_Project/Scripts/Core/Profile/ProfileStore.cs` — `EnsureDefaultDeck` / `BuildDefaultDeck`
- `Assets/_Project/Scripts/UI/Outgame/LoadoutGatePopup.cs` — 뷰 (네비게이션 모름, 콜백만)
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` — `OnStartGame` 게이트 (유일한 게이트 지점)
- `Assets/_Project/Tests/EditMode/LoadoutGateTests.cs` · `ProfileStoreDefaultDeckTests.cs`
- `Assets/_Project/Scenes/OutgameScene.unity` — `LoadoutGatePopup` GO (MenuCanvas 직속) + 참조 3개

## Verified

- EditMode 854 중 852 passed / **0 failed** / 2 skipped(기존부터 문서화된 Ignore, 무관) — unit 2 최종 코드 상태에서 실행.
- fail-loud 실측: `gatePopup`/`catalog`/`cardCatalog` 를 각각 null 로 만들고 `OnStartGame()` → 세 경우 모두 씬 전환 차단. 참조 복원 일치 + 씬 파일 무오염 확인.
- 실제 에셋으로 신규 설치 재현(임시 경로, 라이브 `profile.json` 무접촉): squad 7 + deck 8 + `LoadoutGate.Check = True`. unit 1 이전이면 `deck=NULL, GATE=False`.
- 팝업 구조 프로브: `MenuCanvas` 마지막 sibling, 미충족 2건 → 2줄 + 버튼 3개, 한글 정상.
- 유효 로드아웃에서 START → BattleScene 정상 진입. Play 육안 확인 = 사용자 2026-07-16.
- 콘솔 에러 0. 씬 diff 는 이 spec 의 변경만 포함.

## Notes (되돌리면 안 되는 의도)

- **팝업 `_root` 는 루트 캔버스의 마지막 sibling.** 중첩 캔버스 + `overrideSorting` 은 렌더 순서만 이기고 `GraphicRaycaster` 우선순위는 못 이겨 탭이 아래 로비 버튼으로 샌다 (`SquadBuilderView.cs:271-277` 실증). 팝업 GO 는 **`MenuCanvas` 직속** — `menuRoot` 자식이면 패널 오픈 시 함께 사라진다.
- **패널의 no-op `Button`** 을 지우면 패널 안 클릭이 스크림의 `Hide` 로 버블링된다. 버튼 `transition = None` 을 지우면 ColorTint 가 `image.color` 를 덮는다.
- **게이트는 규칙을 복제하지 않는다.** 스쿼드 판정을 `SquadDraw.Resolve` 에 위임하는 것이 핵심 — 초안의 복제본은 "슬라이스 후 dedup" 이라 Resolve 의 "dedup 후 컷" 과 이미 어긋나 있었다. `OverlongListWithLeadingDuplicate_MatchesSquadDraw` 가 그 회귀를 잠근다.
- **카탈로그 null 은 shortfall 이 아니다.** `cardCatalog` 가 null 이면 `EffectiveDeckSize` 가 폴백 10 을 반환해 "덱 8/10" 을 요구하는데 빌더는 8 에서 막는다 = 영구 잠금. 반드시 호출자가 사전 차단한다.
- **시딩은 리셋이 아니다.** 선택된 덱이 있으면 무효여도 보존한다 — 규칙 변경으로 무효화되면 게이트가 알리고 플레이어가 빌더에서 고친다. 통째 리셋은 `DEFAULT LOADOUT` 의 역할.
- **기존 폴백을 제거하지 않았다.** `GameManager` draft 폴백과 `ResolveAttachDeck` 빈 목록은 그대로다 — 테스트 모드와 BattleScene 직접 Play 가 게이트를 우회하므로 방어선이 필요하다.
- 테스트 모드(`TestModePanelView`)는 **의도적으로 게이트 없음** (사용자 결정). `OnStartGame` 을 거치지 않는다.

## Follow-up

- **실측 안 한 것**: 테스트 모드 무게이트 통과 — `TestModePanelView.StartPlan` 이 `OnStartGame` 을 거치지 않고 직접 `SceneTransition.Go` 를 부르므로 구조적으로 보장되지만 실행하지는 않았다.
- `DEFAULT LOADOUT` 클릭 결과가 종전과 동일한지 실측하지 않았다 — 프로필을 통째로 덮는(스쿼드·드림스톤 포함) 버튼이라 사용자 프로필로 실행하지 않았다. 이관 후에도 `CreateDefault` 가 같은 정의를 쓰므로 동작은 같아야 한다. QA 가 한 번 눌러 확인하면 닫힌다.
- `UiOverlay.Dim`(alpha 0.92) 을 그대로 썼다. 안내 팝업치고 무겁게 읽히면 별도 결정 — 공유 상수라 이 spec 에서 바꾸지 않았다. README 후속 후보 참조.
- 나머지 후속 후보(스쿼드 저장 게이트, `PlayerProfile.cs:112` 낡은 주석, `DeckColumns=10` 불일치, 7 이중 하드코딩)는 README 하단 참조.
