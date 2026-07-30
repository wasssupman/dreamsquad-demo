# 18 — Handoff Summary (units 15~17 · 선택 UX 연계)

> 2026-07-30. units 0~9 인계는 `5_handoff_summary.md`, 10~12 는 `13_handoff_summary.md`.
> 이 문서는 그 **이후**만 다룬다.

## 지금 상태 한 줄

units 15~17 구현·커밋 완료. **핵심 경로는 사용자 Play 확인을 받았고**(두 문 안내 · 양쪽 부착
안내 · 항아리→선택 전환), 경계 항목 몇 개가 남았다.

## Commit

| 해시 | 내용 |
|---|---|
| `69de248e` | `gaugeStart` 를 시트 기준값 20 으로 정렬(드리프트 수정) |
| `67d3350e` | units 15~16 스펙(critic 반영본) |
| `fbcac2db` | unit 15 — 첫 판 유닛 선택 봉인 |
| `652d2b4f` | unit 16 — 각성 안내를 두 개의 문으로 |
| `77e013b2` | unit 15 테스트 — 봉인 릴레이 배선 고정 |
| `48761e2e` | units 15~16 자동 검증 결과 |
| `4665fc1f` | unit 17 — 부착 안내 경로별 독립 |
| `dcd35ed5` | unit 17 검증 결과 |
| `4d87fa3f` | **unit 17 rev** — 이미 열린 손패 + 선택 경로 |
| `6313c8ec` | rev meta 회수 + 검증 결과 |

## Implemented

- **첫 판 각성 봉인 누수 차단** — 유닛 선택 자체를 봉인. 봉인 사실은 `AwakeningGaugeView`
  소유 → 손패 뷰 릴레이 → `DcInspectController` 가 **풀**(신규 씬 배선 0).
- **0단계를 두 문 안내로** — `항아리를 누르거나 캐릭터를 탭하면 / 드림캐쳐 덱이 열립니다`.
- **B단계를 오픈 경로로 분기** — 항아리 = 드래그 문구, 선택 = 탭 즉발 + 좌측 패널 문구.
- **두 안내를 경로별 플래그로 독립** — 먼저 뜬 쪽이 반대쪽을 삼키지 않는다.
- **인트로(0·A)는 파생** — `드래그 pending && 탭 pending`. 세 번째 필드 없음.
- **`SelectionTargetSet` 신설** — 이미 열린 손패에서 선택으로 전환되는 사건을 잡는다.
- `gaugeStart` 100 → 20(시트 정렬).

## Key Files

- `Scripts/Core/Profile/PlayerProfile.cs` · `TutorialProgress.cs` — 경로별 진행 + 파생 인트로
- `Scripts/UI/Tutorial/FirstSessionTutorialController.cs` — `EvaluateCardHint()` 로 두 신호 수렴
- `Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — `SelectionTargetSet` · `AwakeningSealedThisMatch`
- `Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs` — `IsSuppressed`
- `Scripts/UI/Dreamcatcher/DcInspectController.cs` — `SealedThisMatch()` 게이트
- `Tests/EditMode/` — `AwakeningSealRelayTests`(3) · `HandViewSelectionSignalTests`(4) ·
  `TutorialProgressTests`(경로 독립 4 추가)

## Verified

- 컴파일: Runtime · Tests.EditMode · Tests.PlayMode 3개 어셈블리 오류 0.
- **testrig 배치 EditMode 1615 / 통과 1613 / 실패 0 / 스킵 2.**
- 배선: 씬 YAML 로 `handView`·`gaugeView` non-zero fileID 확인(unit 15 가 no-op 아님).
- 사용자 Play: 0단계 두 문 문구 · 항아리 오픈 드래그 안내 · **항아리로 연 뒤 유닛 선택 →
  탭 즉발 안내**(rev 전에는 안 떴다).

> **워크트리 EditMode 는 실패 1건이 상시로 보인다** — `MultiGoalPoolSeparationTests`.
> 타 세션의 dirty `MapDocument_Zig` 탓이고, 깨끗한 HEAD 인 리그에서는 통과한다.
> PlayMode 전체 실패 9종도 전부 타 세션 영역(drag-cancel rev3 · `BattleBridge*`/`DcApplicability`)
> 이거나 기존 환경 실패(Auth·DeckCarryIn·Dreamstone·CardBuffs)다.

## Notes (되돌리면 안 되는 의도)

1. **`HandOpened` 만 듣지 말 것.** 닫힘→열림 전이에서만 발화한다. 이미 열린 손패에서 선택으로
   전환되면 `OpenForSelection` 이 no-op 이라 아무 신호도 안 난다 — `SelectionTargetSet` 과
   **둘 다** 들어야 한다. 안내가 조용히 안 뜨는 형태라 발견이 늦다.
2. **JSON 필드 `awakeningHintVersion` 을 리네임하지 말 것.** 바꾸면 기존 프로필 진행이 0 으로
   읽혀 튜토리얼이 통째로 되살아난다. 의미는 API 이름(`ShouldRunDragAttachHint`)이 나른다.
3. **인트로 파생은 `&&` 다.** `||` 로 바꾸면 한쪽 경로만 쓰는 플레이어에게 영원히 뜬다.
4. **unit 12 의 "B 는 A 선행 요구" 를 되살리지 말 것.** 인트로가 끝난 뒤
   `_awakeningOfferedThisBattle` 이 false 로 고정돼 못 배운 쪽이 영영 안 뜬다. 대신
   `EvaluateCardHint` 첫 줄의 `_awakeningLockedThisMatch` 가드가 첫 판을 직접 막는다.
5. **`ResetAll`/`ResetAllInJson` 의 `changed` 표현식에 모든 토큰을 넣을 것.** 빠지면 그 토큰만
   다를 때 리셋이 디스크에 안 닿는다. 테스트로 고정돼 있다.
6. **첫 판 봉인은 풀이지 푸시가 아니다.** 푸시로 바꾸려면 신규 씬 배선이 필요한데
   `BattleScene.unity` 는 타 세션 WIP 로 저장할 수 없다.
7. **A단계 문구에 새 정보를 넣지 말 것** — 진입 즉시 affordable 이라 한 프레임만 존재한다.

## Follow-up

1. **남은 Play 경계 항목** — ① 안내대로 탭했을 때 실제 즉발 부착 ② 저장 후 다음 판 미노출
   ③ 카드 press 시 배너 조기 해제 ④ **첫 판에 유닛을 탭해도 아무 일 없음**(unit 15 핵심,
   미확인) ⑤ 첫 판 Battle 재배치가 사라진 것이 체감상 문제없는지 ⑥ "선택 먼저 → 항아리"
   (항아리 탭 2회 필요 — 설계된 동작) ⑦ 둘 다 본 뒤 아무것도 안 뜸.
2. **A단계 가시성** — 현 튜닝에서 한 프레임짜리다. 3단계를 2단계로 접거나 0·A 순서 재설계.
   `docs/spec/README.md` Follow-up Backlog 에도 있다.
3. **부착 1회로 세션이 닫히는 연쇄** — `gaugeStart 20` · 비용 20 이라 카드 한 장 쓰면
   `UsableCardsExhausted` 로 손패·선택·패널이 한꺼번에 걷힌다. 체감 확인 필요.

## 환경 주의

- **같은 워크트리에 다른 세션이 동시 작업 중**이다. 스테이징은 **경로 명시**로만.
- `BattleScene.unity` 는 타 세션 WIP 로 dirty 하다. **저장 금지.**
- 에디터가 Play Mode 면 MCP `run_tests` 가 거부된다 → `wassup-testrig` 배치로 우회.
  **`-runTests` 에 `-quit` 를 같이 주면 결과 XML 이 안 나온다**(exit 0 이라 성공처럼 보인다).
  신규 `.cs` 의 `.meta` 는 리그가 생성한 것을 회수해 커밋한다.
