# 25 — 인계 요약 (units 23~24: 기믹 리빌 홀드 안내)

`22_handoff_summary.md`(units 19~21) 이후분. 최신 계약은 README + 번호 문서 우선.

## Commit

| 해시 | 내용 |
|---|---|
| `1f365291` | docs — units 23~24 스펙 |
| `4c1d834e` | docs — 리뷰 반영(계약 6건 · 완료 기준 3건) |
| `5be4fa9b` | unit 23 — 진행 토큰 + 전용 말풍선 앵커 (동작 변화 0) |
| `2805f70f` | unit 24 — 홀드 seam + 문구 + 씬 배선 |
| `56c5c65d` | 코드 리뷰 반영 (주석만, 동작 변경 없음) |

## Implemented

- **두 번째 판 리빌이 요약에서 멈춘다.** 아이콘·룰 라벨·정서 카피·요약이 전부 뜬 자리에서
  무기한 홀드하고 `매 판마다 특수 룰이 하나 걸립니다. 이번 판은 이것!` 한 줄을 얹는다.
  탭하면 퇴장 → 배치. 계정당 1회.
- **첫 판은 무변화** — 리빌 자체가 `ShouldRunCore` 로 생략되므로 안내를 걸 자리가 없다.
- 게이트는 `gimmickRevealHintVersion` **자기 토큰 하나**. 선물·core 완료를 체인하지 않는다.
- 홀드는 `tutorialHoldFallbackSec`(기본 20초) 만료 폴백으로 스스로 풀린다.
- 홀드 전 탭은 무시한다(기존 탭 스킵이 튜토리얼 모드에서만 비활성).

## Key Files

- `UI/GimmickPhaseView.cs` — 홀드 seam(`TutorialHoldEntered`/`Released`) · 시퀀스 분할 · 폴백
- `UI/Tutorial/FirstSessionTutorialController.GimmickReveal.cs` — 문구 · 앵커 · 완료 저장 · 정리 창구
- `Core/Profile/TutorialProgress.cs` — `ShouldRunGimmickRevealHint` 외 3
- `UI/Tutorial/TutorialGuidanceStyle.cs` — `revealHintMessageTopOffset`
- `Data/GimmickRevealConfig.cs` — `tutorialHoldFallbackSec`
- `Scenes/BattleScene.unity` — `gimmickView → GimmickPhaseView` 컴포넌트(fileID 283596198)

## Verified

- 컴파일 0 (Runtime · Tests.EditMode · Tests.PlayMode).
- EditMode **1786 중 실패 0**(unit 24 시점). unit 23 이 신규 4건을 더했다.
- **홀드 상태머신에 EditMode 테스트가 없는 것은 구조적 한계다.** `BeginReveal` 이 `_tutorialMode`
  를 세우려면 `Play()` 에 진입해야 하고, 거기서 만든 시퀀스는 PrimeTween 이 틱해야
  `EnterTutorialHold` 까지 간다 — EditMode 에선 틱하지 않는다. 게이트만 1줄 static 으로 빼는 것은
  제약 10(자명한 로직 과잉 추출)에 걸린다. 선물 홀드(unit 7)와 같은 이유로 Play 검증 전용이다.
- **코드 리뷰 통과** — Codex 2차 리뷰 CRITICAL/HIGH/MEDIUM 0건, LOW 3건(도달 불가 분기 주석 ·
  테스트 공백 · 미확정 오프셋). 첫 항목은 `56c5c65d` 로 반영, 나머지 둘은 위/아래에 기재된 기지 항목.
- 씬 배선 실측(에디터 `SerializedObject`): `gimmickView` → `GimmickPhaseView` 컴포넌트,
  대상 GameObject active, `tutorialHoldFallbackSec = 20`.
- **씬은 열지 않고 디스크 YAML 을 편집했다** — BattleScene 이 로드돼 있지 않은 상태였고, 이렇게
  하면 씬 저장이 남의 미저장 WIP 를 함께 굽는 사고를 피한다. 검증만 additive open → close(무저장).

## Notes (되돌리면 안 되는 것)

- **`SetPhase(Gimmick)` 은 `Play()` 보다 먼저다.** `OnPhaseChanged` 가 `phase != Placement` 에서
  `ResetAwakeningSession(hide: true)` → `guidance.Hide()` 를 부르고 `Gimmick` 도 걸린다. 순서를
  뒤집으면 말풍선이 뜨자마자 지워진다. 이 안전은 **순서 하나**에만 의존한다.
- **구독자가 없으면 홀드하지 않는다**(`TutorialHoldEntered != null` 조건). 완료 저장의 주인이
  구독자라, 미배선 상태에서 홀드하면 문구 없는 정지가 **매 판** 반복된다(저장할 사람이 없어서
  영원히 pending). 선물(unit 7)의 "구독자 없어도 뷰 단독 진행" 을 복붙하면 이 결함이 산다.
- **`StopGimmickRevealHint` 는 완료를 저장하지 않는다.** 저장은 실제로 홀드를 통과한
  `OnGimmickHoldReleased` 에만 있다. 정리 창구가 저장까지 하면 조용히 끝난 판에서 안내가 소진돼
  플레이어가 문구를 영영 못 본다.
- **`OnGimmickHoldReleased` 의 `_gimmickHintActive` 가드를 빼지 말 것.** guidance 가 미배선이면
  `OnGimmickHoldEntered` 가 조기 return 해 **아무것도 못 보여준 채** 뷰만 홀드한다. 그 상태로
  저장하면 문구를 한 번도 못 본 판에서 안내가 소진된다. "구독자 없으면 홀드 안 함"과 같은 계열의
  구멍이고, 둘 다 **참조 누락은 안내 생략으로 떨어진다**는 fail-open 계약의 표현이다.
- **`Finish` 는 홀드를 조용히 내린다** — `TutorialHoldReleased` 를 발행하지 않는다. 발행하면
  재진입·이탈이 위와 같은 소진을 만든다. 대신 말풍선·앵커는 컨트롤러의 정리 창구가 걷는다.
- **홀드 해제는 `_holding` 가드 하나가 소유한다.** 탭과 폴백이 경쟁하므로 진입한 쪽이 즉시
  내린다. 없으면 퇴장 트윈이 같은 알파에 두 번 걸려 깜빡이고 이벤트가 두 번 나간다.
- **`guidance` 탭 캐처를 켜지 말 것.** 전체화면 `raycastTarget` 이 리빌의 `TapCatcher` 를 덮어
  홀드가 폴백 만료로만 풀린다. 이 구간은 `ContinueTapped` 를 쓰지 않는다.

## Follow-up

- **사용자 Play 확인 미완** — 두 번째 판 홀드 · 탭 진행 · 세 번째 판 미노출 · 말풍선이 리빌
  콘텐츠를 가리지 않는지. `revealHintMessageTopOffset 880` 은 계산값이라 실화면 확정이 필요하다.
- 말풍선은 `SafeAreaRoot`, 리빌은 `FullBleedRoot` 라 **좌표계가 다르다** — 하단 인셋이 큰 실기기
  에서 겹칠 수 있다. 모바일 QA 때 함께 볼 것.
- 두 번째 판 홀드가 2회 → 3회가 됐다. README 후속 후보의 **온보딩 총량 다이어트**가 그만큼
  급해진다.
