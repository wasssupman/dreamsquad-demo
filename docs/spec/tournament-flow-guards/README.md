# tournament-flow-guards

> 상태: 구현 완료 (units 0~6 + 정리) — 2026-07-25
> 컴파일 클린 · EditMode 1275 pass / 0 fail. 라이브 e2e(로비 시작→완주)만 서버 락 해제 후 확인 대기.
> 커밋: 820de3c2·5d6c84a0·bedda40e·f496889f·98f2cd55·30989502·44d24c01

## 한 줄

토너먼트 서버 왕복(play / complete)이 **무응답인 채로 플레이가 진행되어 서버 상태가 꼬이는 것**을 막는다. `play` 응답으로 attemptId+seed 를 확보해야만 배틀에 입장하고, score(complete) 전송이 실패하면 알림 팝업으로 알린다. **메인 경로 방어이지, 모든 구멍의 방어가 아니다.**

## 배경 (왜)

데모 서버 모델: `play` 를 부르면 서버가 이 유저를 **현재 토너먼트에 참가중으로 락**한다. 락 시간 내 score 가 안 오면 서버가 **강제로 0점 처리**한다. 문제는 **play/score API 응답이 없는 채로 유저가 플레이를 진행**하면 클라가 attemptId 를 못 쥔 채 판을 끝내고, 서버는 락만 걸린 채 강제 0점 → **클라 상태와 서버 상태가 꼬인다.** 히스토리의 0-엔트리 상당수가 이 경로다.

핵심 처방: **play 가 정상 실행(attemptId+seed 확보)되고 세션이 살아있으면, score 는 현행 구현 그대로 보내는 것이 정답.** 그러니 이 spec 은 (1) play 응답을 확인하고서야 입장하고, (2) score 전송 실패를 사용자에게 드러내는 **두 지점만** 얹는다. score 전송 로직 자체는 손대지 않는다.

## 검증 질문

1. 로비 시작 → play 응답이 실패/무응답이면 배틀에 **안 들어가고** 알림이 뜨는가? 성공(attemptId+seed 확보)이면 정상 입장하는가?
2. 정상 완료 매치의 complete 전송이 실패하면 **점수/결과 화면/전환은 그대로 두고** 알림만 뜨는가?

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 공용 알림 팝업 | `0_notice_popup.md` | self-building `NoticePopup`. busy("매칭 중") + 알림(닫기/[선택]다시시도) 모드. 정적 `Show`, 부트스트랩 없으면 no-op degrade |
| 1 | play 게이팅 | `1_play_gated_entry.md` | 리포터에 await 가능한 로비 진입(성공=attemptId+seed) 추가 + `OnStartGame` 이 성공 시에만 `Go(Battle)`, 실패/타임아웃 시 알림. 진행 중 재진입 차단 |
| 2 | score 실패 알림 | `2_report_failure_alert.md` | complete 실패 콜백을 `NoticePopup` 으로 surfacing. **전송 로직 무변경, 재시도 없음, 논블로킹** |
| 3 | 배선 + Play 검증 | `3_wiring_and_verify.md` | NoticePopup 부트스트랩, 성공/실패(강제) 경로 재현, 완주 매치가 서버에 실점수로 남는지 확인, handoff |

## Feature-wide 계약

1. **입장은 play 성공에 게이트된다** (로그인 계정 한정). 성공의 정의 = 응답에서 **attemptId + seed 확보** (HTTP 200 만으론 불충분 — attemptId 빈 응답으로 입장하면 그 버그가 그대로 재발).
2. **게스트는 게이트 대상 아님.** `!UserSession.HasAccount` 면 play 자체가 없으므로 즉시 입장(현행 유지).
3. **`_lobbyIssued` 채택 유지.** await 성공 후에도 `_lobbyIssued=true` 로 배틀씬 `GameManager.OnEnable.BeginMatch` 가 **재발행 없이 adopt**. 한 판 = 엔트리 1개 불변.
4. **선발행(로비 play)의 목적 유지** — 응답의 `tournament.seed` 를 맵 빌드 전에 확보. await 로 시드는 오히려 입장 전 확정된다.
5. **score 전송 로직은 현행 `ReportResult` 그대로.** play 정상 + 세션 유지면 그대로 보내는 게 정답. 추가는 **complete 실패 시 알림뿐** — 논블로킹, **재시도 없음**. 결과 화면/로컬 점수/씬 전환에 무영향.
6. **await 무응답 방어가 목적.** 로비 await 는 **타임아웃(≤ play API timeout) 경과/실패 → 입장 취소 + 알림.** 진행 중 재진입(더블탭)은 차단(중복 play = 중복 락 방지).
7. **`NoticePopup` 은 단일 공용 뷰.** self-building 절차적(저작 아트 없음 — `PresetConfirmPopup` 패턴), DontDestroyOnLoad 정적 `Show`. busy 모드(무버튼) + 알림 모드(닫기/선택적 다시시도). 부트스트랩 부재/헤드리스에서 **NRE 없이 no-op degrade**(SceneTransition 하드컷 선례).
8. **메인 경로 방어에 한정한다.** epoch 드롭 후 stale 콜백, score 재시도, `reconcile`/`abandon` 의 `complete(0)` 덮어쓰기, TestMode 엔트리 생성(결함 A) 등은 **의도적으로 범위 밖**. "모든 구멍 방어" 아님.
9. **비파괴.** 기존 `BeginMatch`(GameManager 진입용)·`ReportResult` 시그니처 유지. 로비 await 는 별도 진입점/오버로드로 추가한다.

## 파이프라인 커버리지

N/A — 플레이 오브젝트(유닛/적/투사체/해저드/VFX) 신설·렌더경로 변경이 아니라 UI 팝업 + 씬 플로우/네트워크 게이팅 변경. `object-pipeline-map` 대조 대상 아님.

## 후속 후보 (범위 밖)

- **결함 A**: `GameManager.OnEnable` 이 배틀씬 진입마다 무조건 `BeginMatch`(play) → TestMode·에디터 직접 Play·재진입도 토너먼트 엔트리 생성. 개발/테스트 진입은 엔트리를 안 만들도록 게이트하는 별도 결정 필요.
- **결함 C/D**: `ReportResult` 스킵 시 pending 정리, `reconcile`/`abandon` 의 `complete(0)` 정책 재검토(서버 락+강제0점 모델과의 정합 포함).
- **히스토리 0-엔트리 정리**: 이미 쌓인 0 엔트리의 서버측 청소는 클라 범위 밖.
