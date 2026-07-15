# 8 — 인계 요약 (units 6~7, 전체 임포트 확장)

units 0~4 의 인계는 `5_handoff_summary.md`. 이 문서는 2026-07-15 확장분만 다룬다.

## Commit

- `69b7001a` feat(runtime-stat-refresh): unit 6 — IMPORT ALL 버튼 (전체 8탭 임포트)
- `3bd8a94e` feat(runtime-stat-refresh): unit 7 — 로그인 통과 시 전체 자동 임포트 1회
- 곁가지: `56cf7380` balance(dreamcatcher): 덱 사이즈 10→8 (시트 반영) · `ea46f3b0`/`7d2ddbcc` (dev 트레이 토글·DEFAULT LOADOUT → `outgame-login-gate` units 5~6)

## Implemented

- `AllRuntimeRefresher` — `IRuntimeRefresher` 를 fan-out 하는 composite. 두 자식(Unit/Dc)을 동시 실행하고 조인해 `onDone` 을 정확히 1회 부른다.
- 로비 3번째 버튼 `IMPORT ALL` — 8탭(Defenders/Enemies + Dc 6탭)을 한 번에.
- `LoginAutoImport` — `LoginPanelView.onSignedIn` 구독, 앱 세션당 1회 자동 임포트. 비블로킹.
- 로그 첫 줄 = 자식들의 첫 줄을 합친 요약(버튼 뷰가 `FirstLine` 만 라벨에 쓰기 때문).
- `StatRefreshButtonView` / `OutgameMenuController` / `LoginPanelView` 모두 diff 0.

## Key Files

- `Assets/_Project/Scripts/Core/AllRuntimeRefresher.cs`
- `Assets/_Project/Scripts/UI/Outgame/LoginAutoImport.cs`
- `Assets/_Project/Tests/EditMode/UnitStatImport/{AllRuntimeRefreshTests,LoginAutoImportTests}.cs`
- `Assets/_Project/Scenes/OutgameScene.unity` — `UnitStatRefresher` GO 에 composite + auto-import, `DevTrayContent/ImportAllButton`

## Verified

- EditMode 823 passed / 0 failed (units 6~7 신규 9개). 2 skip 은 기존 Ignored.
- Play: `IMPORT ALL` → `ALL: Matched 26 … | Matched 68 …` 양쪽 unmatched 0, 조인 1회.
- Play: 로그인 전 미발동 → SKIP 직후 발동 → `[LoginAutoImport]` 로그 1건 → 재발화해도 재임포트 없음.
- **적용 실증**: 시트 `deckSize=8` / 디스크 에셋 10 상태에서 SKIP 직후 메모리 `DeckRuleConfig.deckSize=8`.
- Play 종료 후 `Assets/_Project/Data/` 디스크 변경 0 — "런타임 적용은 메모리 한정" 계약 유지.

## Notes (되돌리지 말 것)

- **`remaining` 카운터는 루프 전에 시드**한다. 자식이 동기 콜백(자체 in-flight 가드)해도 조기 조인이 나면 안 된다.
- **자동 임포트는 앱 세션당 1회**. `onSignedIn` 은 중복 발화한다(자동 로그인 후 SKIP 이 `LoginPanelView:85` 의 already-signed-in 경로를 또 태움). 계정 리셋 후 재로그인도 재임포트하지 않는다 — 시트는 계정과 무관한 전역 값이고 재획득 수단은 `IMPORT ALL`. (초안 spec 이 이걸 `_done` 가드와 모순되게 요구했다 — `7_login_auto_import.md` 하단 참조.)
- **비블로킹은 의도**. 블로킹하면 저속·오프라인 망에서 `SheetFetcher` timeout(30s)만큼 로비가 잠긴다. 대가는 진입 직후 몇 초 내 전투 시작 시 그 판이 빌드값이라는 것.
- `LoginAutoImport` 는 `Wassup.UI` 소속. `LoginPanelView` 에 의존하는데 기존 방향이 UI→Core 라 Core 에 두면 역방향.
- **자동 임포트는 `StatRefreshResult` 라벨을 갱신하지 않는다**(라벨은 버튼 전용). 결과는 콘솔 로그로만 본다.

## 함정 (다음 세션이 반복하기 쉬움)

- **MCP `set_property` 로 컴포넌트 배열을 물리면 success 를 반환하고도 전부 NULL** 이 될 수 있다. `refresherSources` 가 그랬고, Play 검증이 아니었으면 `"no refreshers wired"` 만 뱉는 버튼이 커밋될 뻔했다. 리플렉션으로 물리고 YAML 로 되읽어 확인할 것.
- **에디터 임포터 사용 중에는 Unity 를 건드리지 말 것.** 도메인 리로드가 in-flight 웹요청 콜백을 죽인다(`UnitStatImportWindow:36` 의 hotfix 주석이 같은 사실을 기록). refresh/컴파일/테스트/Play 를 병행하면 임포트가 조용히 증발한다.
- 시트↔SO 대조는 importer 대신 `{baseUrl}/{탭}` GET 으로 읽기 전용 비교(재사용 스크립트: 세션 scratchpad `verify_sheet.py`). importer 는 dry-run 이 없어 돌리는 순간 디스크에 쓴다.

## Follow-up

- **실기기 Development Build 1회** — 미실시(unit 2 부터의 잔여).
- **릴리즈 빌드 실측** — 세 버튼 미노출 + 자동 임포트 미실행을 코드 경로 승계로만 확인했다. 실측 없음.
- 자동 임포트 착지 전 전투 시작 시 빌드값으로 도는 창(수 초) — 문제가 되면 로그인 화면 블로킹 또는 전투 시작 게이트를 검토.
- 릴리즈용 밸런스 배포 채널(버전 관리/서명) — README 후속 후보에 존치. 현 구성은 dev 빌드 전용이라 릴리즈는 여전히 빌드값 고정.
