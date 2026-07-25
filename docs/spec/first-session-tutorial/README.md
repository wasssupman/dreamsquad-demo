# first-session-tutorial — 첫 판 행동형 온보딩

> 상태: **핵심(0~4) 완료 2026-07-19 · 선물 튜토리얼 확장(6~8) 커밋 `9e75c0ae` 2026-07-20 ·
> 아웃게임 튜토리얼 연계 개선(10~12) 완료 2026-07-21 (`7a704a20`~`649991bb`, 사용자 확인) ·
> UI 레이어 수정(unit 14) 진행 중 2026-07-25**
> 선행: `defender-tap-to-place` · `mobile-ui-safe-area` · `awakening-hud-resource-button` (완료)

## 검증 질문

신규 플레이어가 긴 설명이나 기능 투어 없이 첫 판에서 **유닛 1회 배치 → 전투 시작**을 직접 수행하고,
각성이 실제로 사용 가능해진 순간에만 한 줄 힌트를 받아 드림캐쳐 손패의 존재를 이해하는가?

확장(units 6~9): 두 번째 판에서 처음 노출되는 선물 단계를, 연출 홀드 2회 + 한 줄 문구만으로
**덱 10장 + 선물 2장 → 셔플로 순서 배정**이라는 구조로 이해하는가?

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

## Feature-wide 계약

- **강제 학습은 2행동뿐**: `유닛 배치 1회`와 `전투 시작`. 각성 카드 사용은 강제하지 않는다.
- 문구는 `적이 노란색 베이스에 닿기 전에 막아주세요` → `캐릭터를 배치하는 방법 두가지 방법!`과
  탭·드래그 두 방법 안내 → 탭이면 `하늘색으로 빛나는 곳을 터치해보세요!`, 드래그면
  `하늘색으로 빛나는 곳에 D&D 해보세요!` → `좋습니다! 더 배치해보세요. / 준비되면 전투 시작!`을 쓴다.
- `다음` 버튼 없이 실제 행동 성공 신호가 진행시킨다. 탭→탭과 드래그 앤 드롭을 동등한 배치 방법으로 안내한다.
- Gift·에너지·점수·타이머·기믹·Next Wave·결과·스쿼드·덱 편집은 설명하지 않는다.
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

## 파이프라인 커버리지

N/A — 신규 플레이 오브젝트나 생성→렌더 경로가 아니다. ScreenSpace 안내 UI와 기존 입력 성공 이벤트만 확장한다.

## 비목표 / 후속 후보

- Android 가로 실기기에서 탭 배치·D&D·Skip·각성 힌트 최종 터치 QA
- units 10~12 후속 후보 5건은 `docs/spec/README.md` Follow-up Backlog →
  **첫 판 튜토리얼 개선 (first-session-tutorial units 10~12 이관, 2026-07-21)** 로 이관
- 카드 타입별 종합 설명 이미지, 도움말 도감, 튜토리얼 다시 보기 메뉴
- 첫 판 전용 고정 맵·웨이브·난이도·보상 조정
- 로비 버튼 투어, 스쿼드/덱 편집 강제, 단계 해금
- Next Wave/기믹/메뉴 튜토리얼
