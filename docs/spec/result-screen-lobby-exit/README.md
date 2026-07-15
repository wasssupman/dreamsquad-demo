# result-screen-lobby-exit — 결과창 "다시하기" → "로비로"

> 상태: **구현 2026-07-16** (units 0~1 — 사용자 Play 확인 대기)
> 선행: `result-screen-visual-upgrade`(완료) · `tournament-play-report`(완료) · `game-start-loadout-gate`(완료)
> 사용자 결정 2026-07-16: 재시작 코드는 **삭제하지 않고 배선만 끊는다** · 씬 레거시 자식 제거를 같이 한다
> **철회 2026-07-16**: 초안의 "`ResultCanvas` 정렬 버그" 는 **오진**이었다. 실측으로 반증돼 unit 1 에서 제외했다 — `1_scene_legacy_cleanup.md` 하단 "오진 기록" 참조

## 검증 질문

전투가 끝나 결과창이 떴을 때, 하단 버튼이 **"로비로"** 이고 누르면 **OutgameScene 으로 나가는가?**

## 상위 목표

결과창의 종착 동선을 "같은 판 재시작" 에서 "로비 복귀" 로 바꾼다. 로비에는 `game-start-loadout-gate` 가 붙어 있으므로, 다음 판은 스쿼드/덱을 다시 갖춘 뒤 START 로 들어온다 — 결과창에서 곧장 재시작하던 경로는 그 게이트를 우회했다.

스코프는 **버튼 교체 + 재시작 배선 해제 + 씬 레거시 제거** 셋이다. 재시작 로직 자체의 삭제, 결과창 레이아웃/팔레트/정렬 변경, 로비 쪽 변경은 범위 밖.

## 작업 단위 목록

| 번호 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 구현 | `0_lobby_button_and_restart_unwire.md` | `ResultScreen` 버튼 → "로비로" + OutgameScene 전환, `BattleBridge` 재시작 배선 해제 |
| 1 | wiring | `1_scene_legacy_cleanup.md` | 씬 레거시 자식 3개 제거 (+ 정렬 오진 기록) |
| 2 | 인계 | `2_handoff_summary.md` | handoff (구현 종료 시) |

## Feature-wide 계약

- **결과창이 직접 씬 전환한다.** `MenuPopup.OnExit()`(`MenuPopup.cs:81-87`)이 같은 씬의 이웃 UI 로서 이미 `SceneTransition.Go(SceneNames.Outgame)` 를 직접 부른다. 결과창도 같은 idiom 을 쓴다 — 새 이벤트를 만들어 `BattleBridge` 를 경유시키지 않는다. `BattleBridge` 는 ECS 게이트웨이지 씬 네비게이션 소유자가 아니다.
  - `game-start-loadout-gate` 의 "팝업은 네비게이션을 모른다" 계약과 충돌하지 않는다. 그건 **패널 가시성**(컨트롤러가 소유하는 씬 내 상태)에 대한 규칙이고, 여기는 **씬 전환**(전역 종착 동작)이다. 후자의 선례는 `MenuPopup`.
- **재시작 로직은 남기고 배선만 끊는다** (사용자 결정). `BattleBridge.OnRestartRequested` / `ReLogSkillLoadoutForNewSession` / `EnterPlacementOrGift` 는 그대로 두고 이벤트 구독만 제거한다. 호출처가 사라진 이유를 코드에 적어 다음 사람이 "버그로 끊긴 배선" 으로 오해하지 않게 한다.
- **`ResultScreen.RestartRequested` 이벤트는 제거한다.** 그게 곧 "배선" 이다. 발화처(버튼)가 없어진 이벤트를 남기면 죽은 API 가 공개 표면에 남는다. 남기는 쪽은 `BattleBridge` 의 private 메서드들.
- **`TournamentMatchReporter.BeginMatch()` 누락 없음.** 재시작 경로가 부르던 `BeginMatch`(`BattleBridge.cs:366`)가 죽어도 `GameManager.cs:144` 가 **전투 진입 시** 부른다. 로비→START→전투가 유일 진입이 되므로 토너먼트 시도 집계는 그대로다. `GameManager.cs:143` 의 "restarts issue their own via BattleBridge.OnRestartRequested" 주석은 사실이 아니게 되므로 갱신한다.
- **정렬은 건드리지 않는다 — 이미 맞다.** 중첩 캔버스의 `overrideSorting=true` 는 그 캔버스를 **전역 오버레이 정렬에 자기 `sortingOrder` 로 참여**시킨다(루트의 order 에 갇히지 않는다). 루트 `ResultCanvas` 가 0 이어도 결과창(nested, 2000)은 이미 `MenuReturnCanvas`(1000) 위, `SceneTransition`(10000) 아래에 정확히 있다. `ResultScreen.cs:296-300` 의 기존 주석이 옳다. 초안이 이를 버그로 본 것은 **오진**이며 실측으로 반증됐다 — 상세는 `1_scene_legacy_cleanup.md` 하단.
- **씬 레거시 자식 3개를 제거한다.** `BattleScene` 의 `ResultScreen` 밑 `ResultLabel` / `RestartButton`("다시 시작") / `RedraftButton`("REDRAFT") 는 코드가 참조하지 않는데 `BuildCanvas()` 가 기존 자식을 지우지 않아 살아난다(`childCount=5`). 근거는 **참조 없는 죽은 오브젝트**이지 보이는 결함이 아니다 — 시각 영향은 패널 알파 0.98 뒤로 새는 1~2/255 수준이고 클릭도 뺏지 않는다(실측). 위생 작업으로 기록한다.
- **`BuildCanvas()` 에 자식 청소 로직을 넣지 않는다.** 씬에서 오브젝트를 지우는 것으로 끝낸다 — 런타임 방어 코드는 재발 방지가 아니라 원인 은폐다(제약 8: 지금 안 쓰는 구조 금지).

## 파이프라인 커버리지

**N/A** — 플레이 오브젝트(유닛/적/투사체/해저드/VFX)를 신설하거나 생성→렌더 경로를 바꾸지 않는다. UI 캔버스 정렬과 MonoBehaviour View 변경만 있다.

## 후속 후보 (본 spec 범위 밖)

- **`ResultCanvas` 에 베이크된 빈 `FullBleedRoot`/`SafeAreaRoot`** — 씬 파일에 커밋돼 있으나(HEAD 동일) 어떤 코드도 만들지 않는다. `UiCanvasSetup.Ensure` 가 `ResultCanvas` 대상으로 불렸던 시절의 잔재로 보인다. 빈 오브젝트라 무해하지만 `UiSafeAreaFitter` 가 붙어 있으면 매 프레임 일한다. 확인 후 제거 여부 결정.
- **`MENU` 와 `MAP SETTINGS` 텍스트가 좌상단에서 겹침** — 결과창과 무관한 기존 레이아웃 문제(2026-07-16 스크린샷 확인).
- **`BattleLogger.StartReplacementSession`** — 재시작 배선 해제로 호출처 0. 재시작을 영구 폐기하면 같이 정리 대상.
- **결과창에서 로비로 나갈 때 진행 중이던 전투가 계속 돈다** — 결과창이 떠도 시뮬은 멈추지 않는다(실측: 결과창 표시 중 전투가 진행돼 패배까지 감). 씬 언로드로 정리되므로 무해하나, 결과 확정 후 시뮬을 멈추는 게 맞는지는 별도 판단.
