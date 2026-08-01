# first-session-tutorial — 첫 판 행동형 온보딩

> 상태: **핵심(0~4) 완료 2026-07-19 · 선물 튜토리얼 확장(6~8) 커밋 `9e75c0ae` 2026-07-20 ·
> 아웃게임 튜토리얼 연계 개선(10~12) 완료 2026-07-21 (`7a704a20`~`649991bb`, 사용자 확인) ·
> UI 레이어 수정(unit 14) 완료 2026-07-25 (`8138996b`) ·
> 선택 UX 연계(15~17) 구현·커밋 2026-07-30 — **핵심 경로 사용자 Play 확인 완료**,
> 경계 항목 잔여. 인계는 `18_handoff_summary.md` ·
> 첫 판 전투 HUD 안내(19~20) + 스트레스 정지+탭 rev(unit 21) **완료 2026-08-01**
> (`45d35fea`·`34cf2a8d`·`65a4fb74`, 리뷰 반영 포함) — **사용자 Play 확인 통과**.
> 컨트롤러는 관심사별 partial 4개로 분할(`3ebe1568`). 인계는 `22_handoff_summary.md` ·
> 기믹 리빌 안내(23~24) **설계 승인 2026-08-01 · 구현 대기**
> 선행: `defender-tap-to-place` · `mobile-ui-safe-area` · `awakening-hud-resource-button` (완료)

## 검증 질문

신규 플레이어가 긴 설명이나 기능 투어 없이 첫 판에서 **유닛 1회 배치 → 전투 시작**을 직접 수행하고,
각성이 실제로 사용 가능해진 순간에만 한 줄 힌트를 받아 드림캐쳐 손패의 존재를 이해하는가?

확장(units 6~9): 두 번째 판에서 처음 노출되는 선물 단계를, 연출 홀드 2회 + 한 줄 문구만으로
**덱 10장 + 선물 2장 → 셔플로 순서 배정**이라는 구조로 이해하는가?

확장(units 19~20): 각성이 봉인된 첫 판 전투에서, 비차단 4줄만으로 **스트레스 = 패배 조건**과
**웨이브 당기기 = 점수 선택지**를 인지하는가?

확장(units 23~24): 두 번째 판에서 리빌을 **멈춰 세운 한 줄**로, 이번 판의 룰이 무엇인지가 아니라
**특수 룰이 매 판 바뀐다는 구조**를 인지하는가?

## 상위 목표

튜토리얼은 시스템을 열거하지 않고 `행동 → 반응 → 이해` 순서로 최소 정신 모델만 만든다.

1. 적이 목표에 닿기 전에 막는다.
2. 하단 유닛을 탭한 뒤 밝은 타일을 탭하거나, 유닛을 타일로 끌어 놓는다.
3. 전투를 시작하고 배치 결과를 직접 본다.
4. 각성은 준비된 순간에만 비차단 힌트로 알린다.

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_progress_state.md` | 프로필 상태 | 첫 판/각성 힌트 버전 저장과 회귀 테스트 |
| 1 | `1_guidance_view.md` | 공통 UI | Safe Area 말풍선·대상 펄스·건너뛰기 |
| 2 | `2_first_placement_flow.md` | 핵심 플로우 | 목표 제시 → 탭 배치 1회 → 전투 시작 |
| 3 | `3_awakening_context_hint.md` | 상황별 힌트 | 사용 가능한 손패가 생긴 순간에만 각성 안내 |
| 4 | `4_scene_wiring_and_qa.md` | 통합/검증 | BattleScene 배선과 모바일 가로화면 QA |
| 5 | `5_handoff_summary.md` | 인계 | 핵심(0~4) 구현 종료 인계 |
| 6 | `6_gift_tutorial_progress.md` | 프로필 상태 | 선물 튜토리얼 버전 + 두 번째 판 판정 |
| 7 | `7_gift_phase_holds.md` | 연출 seam | GiftPhaseView 홀드 2지점 분할 + 첫 판 연출 억제 |
| 8 | `8_gift_tutorial_orchestration.md` | 오케스트레이션 | 홀드 문구 표시 + guidance elevated + 완료 저장 |
| 9 | `9_gift_wiring_and_qa.md` | 통합/검증 | 씬 배선 + 판 전이 smoke/Play QA |
| 10 | `10_first_battle_awakening_lockout.md` | 첫 판 각성 봉인 | 버튼 숨김 seam + 힌트 억제. 자리는 빈 채로 |
| 11 | `11_class_hint_step.md` | 클래스 안내 | 첫 배치 후 클래스 5종 설명, 탭으로 넘김 |
| 12 | `12_awakening_intro_on_battle_start.md` | 각성 인트로 | 두 번째 판 전투 시작 시 버튼 포커스(3단계 중 0단계) |
| 13 | `13_handoff_summary.md` | 인계 | units 10~12 커밋·검증·되돌림 금지 항목 |
| 14 | `14_tutorial_ui_layering.md` | UI 레이어 | Canvas 우선순위와 첫 배치 안내의 화면 겹침 방지 |
| 15 | `15_first_match_selection_lockout.md` | 회귀 수정 | 첫 판 유닛 선택 봉인 — 각성 봉인 누수 차단 |
| 16 | `16_selection_aware_awakening_hint.md` | 문구/분기 | 손패 여는 문 2개 안내 + 오픈 경로별 부착 방식 |
| 17 | `17_independent_attach_hints.md` | 진행 상태 | 두 부착 안내를 경로별 플래그로 분리(서로 삼키지 않게) |
| 18 | `18_handoff_summary.md` | 인계 | units 15~17 커밋·되돌림 금지 7건·남은 Play 경계 항목 |
| 19 | `19_battle_hud_hint_seam.md` | 전투 HUD 안내 ① | 뷰 seam 2개 + 체인 골격 + 스트레스 2줄 |
| 20 | `20_next_wave_hint.md` | 전투 HUD 안내 ② | 다음 웨이브 2줄 + 계약 갱신 |
| 21 | `21_stress_hint_pause_rev.md` | unit 19 rev | 스트레스를 전투 정지 + 탭으로 (비차단 계약 뒤집기) |
| 22 | `22_handoff_summary.md` | 인계 | units 19~21 + partial 4분할 |
| 23 | `23_gimmick_reveal_progress_state.md` | 토대 | 리빌 안내 진행 토큰 + 말풍선 앵커·폴백 필드 |
| 24 | `24_gimmick_reveal_hold_hint.md` | 기믹 리빌 안내 | 리빌 요약에서 홀드 + 구조 한 줄 |

15 → 16 → 17 순서 필수(첫 판 경계가 흐리면 16 의 검증이 성립하지 않고, 17 의 분기는 16 이 만든다).
19 → 20 순서 필수(20 은 19 가 만든 체인·활성 대기·앵커에 스텝을 잇는다).
21 은 19 의 스트레스 스텝을 대체한다 — 19 를 읽을 때 **비차단 서술은 21 이 뒤집었다**고 보라.
23 → 24 순서 필수. 뒤집으면 완료 토큰이 없어 **매 판 홀드**가 된다.

## Feature-wide 계약

- **강제 학습은 2행동뿐**: `유닛 배치 1회`와 `전투 시작`. 각성 카드 사용은 강제하지 않는다.
- 문구는 `적이 노란색 베이스에 닿기 전에 막아주세요` → `캐릭터를 배치하는 방법 두가지 방법!`과
  탭·드래그 두 방법 안내 → 탭이면 `하늘색으로 빛나는 곳을 터치해보세요!`, 드래그면
  `하늘색으로 빛나는 곳에 D&D 해보세요!` → `좋습니다! 더 배치해보세요. / 준비되면 전투 시작!`을 쓴다.
- `다음` 버튼 없이 실제 행동 성공 신호가 진행시킨다. 탭→탭과 드래그 앤 드롭을 동등한 배치 방법으로 안내한다.
- Gift 카드 운용·에너지·타이머·기믹·결과·스쿼드·덱 편집은 설명하지 않는다.
  **`스트레스`와 `Next Wave` 는 units 19~20 이 해제한다**(사용자 결정 2026-08-01) — 첫 판
  전투 구간에 한해 설명한다.
- 목표는 말풍선과 출발/목표 지점 지속 마커로 보인다. spawn을 먼저, goal을 다음에 열고 실제 구조물의
  렌더 중심을 가리킨다. 마커는 5초 Goal beat 동안 유지하되 조기 arm/드래그 시 즉시 닫고,
  다단 배치 방법 안내로 넘어가기 전에도 정리한다.
- 핵심 안내 동안 카운트다운은 계속 hold한다. 첫 배치 전 Start는 조용히 숨기고, 배치 성공 후에만 표시·활성화한다. 실제 Start 탭 또는 Skip·이탈에서 hold를 원복한다.
- 각성은 비용을 낼 수 있는 카드가 생긴 순간에만 판당 1회, 3~4초 안내하고 실제 손패가 열린 뒤 한 줄 후 자동 종료한다.
- UI는 `UiCanvasSetup`/`SafeAreaRoot`를 따르며, 상태 저장 불가 시 플레이를 잠그지 않고 안내를 생략한다.
- ECS 변경 없이 UI·입력·프로필 MonoBehaviour 계층의 기존 성공 신호만 관찰한다.
- 핵심 안내 중 기존 `GimmickGuideView`는 숨겨 한 화면에 한 지시만 남기며, 종료·Skip·이탈에서 즉시 원복한다.
- **선물 튜토리얼(units 6~9)**: 첫 판(core pending)엔 선물 **연출만** 억제한다 — 덱 구성은 동일
  (12장, `BuildGiftDeck` 불변). 두 번째 판(core 완료 · gift pending · loaded 세션)에만 리빌
  포커스·셔플 직전 2회 무기한 홀드 + 탭 진행으로 안내하고, 그 판에선 기존 탭 스킵을 비활성한다.
- 선물 튜토리얼 문구의 kind(루시드/림)·카드 수는 하드코딩이 아니라 실제 구성 덱에서 읽는다.
  완료 저장은 셔플 홀드 통과(셔플 연출 시작) 시점. 말풍선은 elevated sortingOrder(40)로 선물
  패널(30) 위에 표시하고 종료 경로에서 원복한다.
- **첫 판 각성 봉인(units 10~12)**: 첫 판은 각성 버튼을 숨겨 **배치만으로** 승부를 본다. 버튼은
  절대 위치라 자리가 빈 채로 남고 다른 HUD 는 움직이지 않는다. 게이지 충전·덱 회수 로직은 그대로
  두고 **표시만** 막는다(손패를 여는 유일한 경로가 그 버튼이므로 카드 사용은 자연히 봉인된다).
- 봉인 판정은 `_awakeningLockedThisMatch` **하나**가 버튼 숨김과 힌트 억제를 함께 구동한다.
  Placement 진입 시 `ShouldRunCore(profileSO)` 로 결정하고, 해제는 `OnDisable` 에서 **Battle 중이
  아닐 때만** 적용한다(Battle 중 해제는 패널이 켜졌다 꺼지는 왕복을 만든다). 다음 매치는
  `OnPlacementReady` 가 매번 재판정한다. `EndCore` 에서 풀지 않는다 — Skip 해도 첫 판은 첫 판이다.
- **`ShouldRunAwakeningHint` 에 `!IsCorePending` 을 걸어 첫 판을 막으려 하지 말 것.**
  `OnPhaseChanged(Battle)` 가 `CompleteCoreProgress()` 를 먼저 실행하므로 그 시점엔 이미 pending 이
  false 다. 첫 판 억제는 위 `_awakeningLockedThisMatch` 로만 한다.
- **`CompleteCoreProgress()` 는 `_coreActive` 와 무관하게 Battle 진입에서 호출한다.** 예전에는
  `_coreActive` 뒤에만 있어서, 참조 누락·affordable 슬롯 부재로 안내가 fail-open 된 계정은
  `firstBattleTutorialVersion` 이 영원히 0 이었다. unit 10 의 lock 이 그 위에 얹히면 각성 버튼이
  **매 판 영구 봉인**된다. 같은 결함으로 선물 튜토리얼과 로비 챕터 B 도 영영 발동하지 못했으므로
  이 수정이 셋을 함께 고친다. **되돌리지 말 것.**
- **클래스 안내 문구는 사용자 작성본이다. 임의로 고치지 않는다**(2026-07-21). 리뷰가 제기한
  표현 정합성 지적(배지 글리프 앵커·캐스터 설명 범위·`어그로`/`서포터` 어휘)은 후속 후보로 둔다.
- 클래스 안내 스텝은 **만료 안전장치**를 함께 건다. 이 스텝이 `BeginStart()` 의 유일한 호출처가
  되므로, 탭이 유실되면 Start 잠금이 안 풀려 첫 판이 Skip 외 탈출 불가가 된다.
- **units 10~12 가 추가한 수치는 전부 SO/SerializeField 로 뺀다.** 튜토리얼 타이밍·색은
  `TutorialGuidanceStyle`(`classHintFallbackSeconds`·`tapCatcherDimAlpha`), 로비 오버레이 값은
  `OutgameTutorialOverlay`/`OutgameTutorialController` 의 SerializeField. 코드 const 로 두지 말 것.
- **각성 0단계는 arm 하지 않는다.** `AwakeningConfig.gaugeStart` 는 SO·시트 튜너블이라 "전투 시작
  게이지 0" 은 불변식이 아니다. B 단계는 `_awakeningOfferedThisBattle`(=A 가 실제로 떴다)를 요구한다.
- **0단계는 한 프레임 미뤄 표시한다.** `AwakeningGaugeView` 가 같은 `PhaseChanged` 의 다른 구독자라
  패널 활성화 순서가 보장되지 않고, `Pulse()` 는 비활성 패널에서 조용히 소실된다(링과 달리 복구 안 됨).
- 각성 안내는 **3단계**다: 전투 시작(`여기서 드림캐쳐 덱을 열어보세요`) → 낼 수 있는 카드 생김
  (`드림캐쳐 사용 준비 완료!`) → 손패 열림(`포커스된 카드를…`, 여기서 완료 저장). 0단계는
  `_awakeningOfferedThisBattle` 을 건드리지 않는다 — 건드리면 A 단계가 영영 안 뜬다.
- 클래스 안내 스텝은 **탭으로 넘긴다.** 이 구간만 풀스크린 투명 캐처로 배치 입력을 막으며,
  "입력은 항상 열려있다" 계약에서 의도적으로 벗어나는 유일한 구간이다. Skip 은 노출한다.
- **튜토리얼 Canvas order는 `TutorialGuidanceStyle`이 소유한다.** guidance와 탭 캐처는 일반
  HUD·메뉴보다 위, 결과·중요 알림·씬 전환보다 아래다. 아웃게임 dim은 같은 Style의 별도
  order로 guidance 바로 아래에 두어 통과구멍 입력 계약을 유지한다.
- **첫 판 봉인은 문이 둘이다(units 15·16).** unit 10 의 "손패를 여는 유일한 경로가 항아리
  버튼" 전제는 `selection-hand-attach` unit 1 이 깼다 — 유닛 탭도 손패를 연다. 게다가
  `AwakeningConfig` 은 `gaugeStart 20` · 카드 비용 전부 20 이라 첫 판에도 **1장을 실제로 쓸 수
  있다**. 그래서 첫 판엔 **선택 자체를 봉인**한다(사용자 결정 2026-07-30). 봉인 사실은
  `AwakeningGaugeView._suppressed` 가 소유하고 `DreamcatcherHandView` 릴레이로
  `DcInspectController` 가 **풀**한다 — 푸시로 만들면 신규 씬 배선이 필요한데
  `BattleScene.unity` 를 저장할 수 없다. 릴레이 이름은 `AwakeningSealedThisMatch`(사실의 이름)로
  둔다 — `Suppressed` 는 항아리 표시의 어휘라 선택 봉인 쪽에서 읽으면 인과가 안 보인다.
  **첫 판엔 재배치도 함께 사라진다**(이동 버튼이 패널 안에 있다). Placement 재배치는 원래
  없으므로(`BeginMoveModeFor` 가 Battle 게이트) 손실은 첫 판 Battle 하나이고, 튜토리얼이
  재배치를 가르치지 않으므로 의도에 부합한다.
- **각성 안내는 오픈 경로를 구분한다.** `HandOpened` 는 경로를 구분하지 않고 발화하므로
  분기는 `DreamcatcherHandView.InSelectionMode` 로 한다(신규 이벤트 금지 — 이미 public 이다).
  일반 오픈 = 드래그 문구(기존), 선택 오픈 = 탭 즉발 + 좌측 패널 문구. **포커스는 두 경우 모두
  usable 슬롯**이다.
- **두 부착 안내는 서로를 소비하지 않는다(unit 17).** 완료 저장이 경로별이다 —
  `awakeningHintVersion`(= 드래그) · `awakeningTapAttachHintVersion`(= 탭 즉발). JSON 필드명을
  좁히지 않는 이유는 호환이다(바꾸면 기존 진행이 0 으로 읽힌다) — 의미는 API 이름이 나른다
  (`ShouldRunDragAttachHint`/`ShouldRunTapAttachHint`).
  **인트로(0·A단계)는 파생**: `ShouldRunAwakeningIntro = 드래그 pending && 탭 pending`.
  `||` 로 쓰면 한쪽만 쓰는 플레이어에게 영원히 떠서 잔소리가 된다. 신규 토큰은
  `ResetAll`/`ResetAllInJson` 의 **`changed` 표현식에 반드시** 넣는다(빠지면 그 토큰만 다를 때
  디스크에 영영 안 닿는다).
- **unit 12 의 "B 는 A 선행 요구" 가드는 unit 17 이 걷어냈다.** 그 목적("B 가 A 의 완료 저장을
  훔치는 것" 방지)은 저장이 경로별이 되면서 사라졌고, 남겨두면 인트로가 끝난 뒤
  `_awakeningOfferedThisBattle` 이 false 로 고정돼 **못 배운 나머지 한쪽이 영영 발화하지 못한다**.
  "낼 수 있는 카드가 있을 때만" 은 usable 슬롯 탐색이 계속 강제한다.
- **첫 판 전투 HUD 안내(units 19~20)는 신규 프로필 필드가 없다.** 게이트는
  `_awakeningLockedThisMatch`(= 첫 판) 하나다 — 첫 판 Battle 진입은 계정당 한 번이므로
  별도 버전 토큰이 같은 사실을 두 곳에 들게 만들 뿐이다. `IsCorePending` 으로 판정하려 하지 말 것
  (Battle 시점엔 이미 false 다 — 위 계약과 같은 함정).
- **HUD 안내 체인은 `_awakeningRoutine` 을 공유하지 않는다.** 그 핸들은 0·A·B 단계가 공유하고
  `ResetAwakeningSession`·`OnCardPeeked` 가 임의로 중단시킨다. 전용 `_hudHintRoutine` 을 둔다.
- **튜토리얼은 기믹 안내를 억제하지 않는다.** Battle 전용 HUD 체인에는 억제할 대상이 없고,
  `gimmick-recognition-upgrade` unit 3 이 배치 안내 카드를 은퇴시킨 뒤로는 **core 안내 쪽
  억제도 제거됐다**(`gimmickGuide` 참조 자체가 없다). 첫 판 리빌 생략은 `GimmickPhaseView` 가
  `TutorialProgress.ShouldRunCore` 로 스스로 판정한다 — 튜토리얼이 밀어 넣지 않는다.
- **체인 정리는 `EndCore` 에 기대지 말 것.** `EndCore` 는 `!_coreActive` 로 조기 return 하므로
  체인이 세운 상태(코루틴·말풍선·앵커)를 되돌리지 않는다. 반대로 정리 함수는 체인이 없을 때도
  불리므로(Placement 진입) `_hudHintActive` 가드가 없으면 core 안내의 말풍선을 걷어버린다.
- **포커스 대상은 활성을 기다린 뒤 건다.** `ScoreHudView`·`NextWaveDock` 은 모두 자기 `Update`
  에서 lazily 켜지고, 특히 웨이브 버튼은 `bridge.NextWaveAvailable` 폴링 결과라 Battle 진입
  프레임엔 꺼져 있다. `FocusUi` 는 비활성 대상에서 링을 조용히 끄므로(0단계와 같은 함정)
  짧게 폴링하고 그래도 없으면 **그 스텝만** 생략한다.
- **HUD 안내 두 스텝의 성격이 다르다(unit 21).** 스트레스는 **전투를 정지하고 탭으로** 넘기고
  (사용자 결정 2026-08-01 — 비차단으로는 읽기 전에 지나간다), 웨이브는 **비차단·시간 경과**를
  유지한다. ④ `단, 준비가 되었을때!` 가 "지금 누르지 마"라는 뜻이라 행동 요구와 모순이고,
  첫 판에 웨이브를 겹치면 안내가 패배를 유도한다. 한 판에 정지 구간은 하나만 둔다.
- **정지는 `TimeManager` 의 Battle 도메인 lease 다.** 글로벌 `Time.timeScale` 을 건드리지 않는다.
  시뮬·타이머·웨이브 스폰이 함께 멈추고(셋 다 `_battleClock` 기반이라 **시간점수가 깎이지 않는다**)
  안내 자신은 unscaled 라 계속 흐른다. 손패 슬로모(0.3x)와 겹쳐도 `0` 이 이기고 해제 시 복귀한다.
- **lease 는 필드 하나가 소유하고 `StopBattleHudHint` 만 해제한다.** 누수되면 `ResetAll`(매치 경계)
  까지 **그 판이 영구 정지**한다. 만료 폴백(`stressHintFallbackSeconds`)을 반드시 함께 건다 —
  클래스 안내가 같은 모양의 위험을 막은 것과 같은 이유다. 이탈 경로를 늘릴 때 그 함수를 타는지
  확인할 것.
- **`ContinueTapped` 의 소비자가 둘이다**(클래스 안내 · 스트레스 정지 안내). `OnContinueTapped`
  첫 줄의 대기 분기가 우선순위를 명시한다 — 순서를 흐리면 한쪽이 다른 쪽 탭을 삼킨다.
- **스트레스 한계 수치는 화면과 같은 소스에서 읽는다.** `ScoreHudView` 가 들고 있는 배지 분모
  (`EffectiveLeakLimit`)를 쓴다 — 결과 화면의 덱 원본값과는 다른 값이지만 문구가 가리키는 것은
  배지의 그 숫자다. 코드에 `10` 을 박지 말 것(제약 6).
- **기믹 리빌 안내(units 23~24)는 자기 토큰 하나로만 게이트한다.** 선물·core 완료를 **체인하지
  않는다** — `ShouldRunGiftTutorial`/`ShouldRunLobbyLoadoutHint` 가 쓰는 그 형태는 백로그가 이미
  결함으로 지적했다(선행 안내가 fail-open 경로를 타면 뒤 안내가 영영 발화 못 한다). 첫 판 배제는
  리빌 자신의 `ShouldRunCore` 게이트(`GimmickPhaseView.cs`)가 이미 한다.
- **리빌 홀드에는 만료 폴백을 반드시 건다.** 리빌엔 Skip 버튼이 **없고** 홀드가 안 풀리면
  `ProceedToPlacement` 가 영영 안 불려 **그 판이 죽는다**. 선물 홀드는 무기한이지만
  (`7_gift_phase_holds.md`) 새로 여는 구간에는 unit 21 이 세운 폴백 계약을 적용한다.
- **`SetElevated` 는 이제 아무 데서도 필요 없다.** guidance 기본 order 1500 이 리빌(20)·선물(30)을
  모두 압도한다. `Gift.cs` 의 호출은 order 가 `10 ↔ 40` 이던 시절(unit 8)의 잔재로 unit 14 재번호
  이후 사실상 no-op 이다 — **선례로 복붙하지 말 것**.
- **리빌 구간의 탭 주인은 리빌 패널이다.** guidance 탭 캐처(`SetTapCatcher`)를 켜면 전체화면
  `raycastTarget` 이 리빌의 `TapCatcher` 를 덮어 홀드가 **폴백 만료로만** 풀린다. `ContinueTapped`
  는 쓰지 않는다 — 클래스 안내·스트레스 정지와 소비자를 다투지 않는다.
- **`SetPhase(Gimmick)` 은 홀드보다 먼저여야 한다.** `OnPhaseChanged` 가 `phase != Placement` 에서
  `ResetAwakeningSession(hide: true)` → `guidance?.Hide()` 를 부르고 `Gimmick` 도 여기 걸린다.
  현재는 `BeginReveal` 이 `Play()` 앞에서 동기로 페이즈를 바꿔 그 `Hide()` 가 말풍선보다 ~2초
  먼저 지나가서 안전하다. **순서를 뒤집으면 말풍선이 뜨자마자 지워진다.**
- **리빌 말풍선은 기본 앵커를 쓸 수 없고, 리빌과 좌표계도 다르다.** 리빌 콘텐츠가 y `+390`
  (아이콘 상단) ~ `-290`(탭힌트)을 점유해 기본 앵커(y `356`~`240`)가 아이콘과 겹친다. 전용 앵커로
  탭힌트 아래에 두되, **말풍선은 `SafeAreaRoot`(인셋만큼 위로 클램프) · 리빌은 `FullBleedRoot`
  (인셋 무관 고정)** 이라 에디터에서 안 겹쳐도 하단 인셋이 큰 실기기에서는 겹칠 수 있다.
- **리빌 홀드 해제는 정확히 한 번.** 탭과 폴백 만료가 경쟁하므로 진입한 쪽이 즉시 가드를 내리고
  폴백 핸들을 명시 취소한다. 없으면 퇴장 트윈이 같은 알파에 두 번 걸리고 이벤트가 두 번 나간다.
- **A단계 문구에 새 정보를 넣지 말 것.** 현 튜닝(`gaugeStart 20` · 비용 20)에서 Battle 진입
  즉시 `20 >= 20` 이라 A 가 `OnPhaseChanged` 안에서 동기로 뜨고, 다음 프레임 0단계 코루틴이
  덮어써 **한 프레임만 존재한다**. 읽을 수 없는 자리다. 이 성립 조건은 **여유가 0** 이다 —
  비용이 21 이 되거나 `gaugeStart` 가 19 가 되면 A 가 진입에 안 뜬다(unit 16 알려진 한계).

## 파이프라인 커버리지

N/A — 신규 플레이 오브젝트나 생성→렌더 경로가 아니다. ScreenSpace 안내 UI와 기존 입력 성공 이벤트만 확장한다.

## 비목표 / 후속 후보

- Android 가로 실기기에서 탭 배치·D&D·Skip·각성 힌트 최종 터치 QA
- **A단계가 현 튜닝에서 안 보인다** — 진입 즉시 affordable 이라 0단계 코루틴이 다음 프레임에
  덮어쓴다. 3단계를 2단계로 접거나 0/A 순서를 재설계하는 두 방향. (unit 16 조사)
- **부착 안내를 판당 경로별 1회로** — 지금은 첫 B 에서 저장하고 끝나 한쪽 문만 배운다.
  `_awakeningArmedThisBattle` 래치 + `ShouldRunAwakeningHint` 가드 재설계가 필요하다. (unit 16)
- **온보딩 총량 다이어트** — units 23~24 로 두 번째 판 홀드가 2회 → **3회**가 된다(선물 ①② +
  리빌). 백로그의 "온보딩 총량 [M]" 지적이 그만큼 커지므로, 이 확장이 끝나면 뺄 것을 정한다
  (후보: 클래스 안내 · 각성 0단계 · 선물 2번째 홀드).
- units 10~12 후속 후보 5건은 `docs/spec/README.md` Follow-up Backlog →
  **첫 판 튜토리얼 개선 (first-session-tutorial units 10~12 이관, 2026-07-21)** 로 이관
- 카드 타입별 종합 설명 이미지, 도움말 도감, 튜토리얼 다시 보기 메뉴
- 첫 판 전용 고정 맵·웨이브·난이도·보상 조정
- 로비 버튼 투어, 스쿼드/덱 편집 강제, 단계 해금
- 기믹/메뉴 튜토리얼 (Next Wave 는 units 19~20 이 첫 판 전투 구간에 한해 다룬다)
