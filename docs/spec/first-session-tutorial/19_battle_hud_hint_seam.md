# 19 — 첫 판 전투 HUD 안내 ①: seam + 스트레스

## 목적

첫 판 전투 시작 직후는 **지금 안내가 하나도 없는 구간**이다(각성이 봉인돼 0단계가 억제된다).
그 자리에서 스트레스 배지의 의미와 패배 조건을 알린다. 첫 판에 플레이어가 관리해야 하는 자원은
스트레스 하나뿐이다.

## 변경 대상

- `Scripts/UI/ScoreHudView.cs` — 스트레스 배지 rect · 한계 · 한계 표기 여부 읽기 전용 노출
- `Scripts/UI/Tutorial/TutorialGuidanceStyle.cs` — `hudHintLineSeconds` · `hudHintTargetWaitSeconds`
  · `hudHintMessageTopOffset`
- `Scripts/UI/Tutorial/TutorialGuidanceView.cs` — 말풍선 앵커를 bool → 3값으로
- `Scripts/UI/Tutorial/FirstSessionTutorialController.cs` — 체인 골격 + 스트레스 2줄
- `Assets/_Project/Data/Config/TutorialGuidanceStyle_Default.asset` — 신규 필드 기본값
- `Assets/_Project/Scenes/BattleScene.unity` — 신규 SerializeField `scoreHud` 배선

## 구현

### 뷰 seam (읽기 전용)

- `ScoreHudView.StressBadgeRect` — 배지 **플레이트** RectTransform(`LeakPlate`). 현재 지역 변수라
  필드로 승격한다. `_leakValueRect`(숫자만)를 쓰면 링이 캡션 `스트레스` 를 감싸지 못한다.
- `ScoreHudView.StressLimit` · `ShowsStressLimit` — `SetLeakStatus` 가 저장한 스냅샷.
  선례: `AwakeningGaugeView.HitRect` · `PlacementPhaseView.StartButtonRect`.

> 한계값은 배지 분모(`EffectiveLeakLimit()` = 덱 원본 − 몽마의 계약 선불)다. **결과 화면의
> `StressLimit`(덱 원본)과 다른 값**이지만, 문구가 가리키는 것은 화면의 그 숫자이므로 배지와 같은
> 소스를 쓰는 것이 맞다(전투 시작 시점엔 선불 0 이라 둘이 일치한다).

### 게이트 — 신규 프로필 필드 없음

- `_awakeningLockedThisMatch`(= 첫 판 판정) 하나로 게이트한다. 첫 판 Battle 진입은 계정당 한 번이다
  (`CompleteCoreProgress()` 가 `_coreActive` 와 무관하게 Battle 진입에서 실행된다).
- **`IsCorePending` 으로 첫 판을 판정하려 하지 말 것** — 그 시점엔 이미 false 다(README 계약).
- 판당 래치 `_hudHintShownThisBattle` 를 두고 `ResetAwakeningSession` 에서 함께 되돌린다.
- 호출 위치는 `OnPhaseChanged(Battle)` 분기의 **말미**(`EvaluateAwakeningHint()` 다음)다. 첫 판이면
  그 앞의 두 각성 호출이 `_awakeningLockedThisMatch` 가드로 즉시 return 하므로 실질 배타가 되고,
  읽는 사람에게 "각성 안내 아니면 HUD 안내" 라는 관계가 드러난다.

### 전용 코루틴 핸들

- `_hudHintRoutine` 을 **새로 둔다. `_awakeningRoutine` 을 재사용하지 말 것.** 그 핸들은
  0·A·B 단계가 공유하고 `ResetAwakeningSession`·`OnCardPeeked` 가 임의로 중단시킨다. 첫 판엔
  각성 안내가 전부 억제돼 지금은 충돌하지 않지만, 한 핸들에 두 체인을 얹으면 누가 누구를 걷는지
  추적이 불가능해진다.

### 대상 활성 대기 (필수)

- `ScoreHudView` 는 자기 `Update` 에서 lazily 구독하고 `OnPhaseChanged(Battle)` 에서 패널을 켠다.
  `PhaseChanged` 구독자 순서는 보장되지 않는다.
- `FocusUi` 는 대상이 `activeInHierarchy` 가 아니면 **링을 조용히 끈다**(0단계가 같은 함정에
  빠졌던 자리). 한 프레임 양보 후 `hudHintTargetWaitSeconds`(기본 1초, unscaled) 동안 폴링하고,
  그래도 비활성이면 **그 스텝만 생략**한다(fail-open — 플레이를 잠그지 않는다).

### 스트레스 2줄

- 앵커를 `HudHint` 로 바꿔 말풍선을 배지 아래로 내린다. **기본값으로는 배지를 덮는다**:
  점수판 top inset 20 + plate 148 + gap 10 → 배지 구간 **178~242**, 말풍선(`messageTopOffset 184`,
  높이 116) 구간 **184~300**. `hudHintMessageTopOffset` 기본값은 280 에서 시작해 **스크린샷 실측으로
  확정**한다(링은 `focusPadding 14` + `focusBorder 5` 만큼 더 나온다).
- ① `악몽을 막아 스트레스 관리하세요!` — 사용자 작성본
- ② `스트레스가 {한계}가 되면 패배합니다.` — `{한계}` 는 위 seam 에서 읽는다. **하드코딩 금지**
  (제약 6). 현재 `WaveA.asset → defeatGoalReachedCount = 10` 이지만 덱마다 다르다.
- `ShowsStressLimit == false`(엔드리스 — 분모 미표기 · 유출로 패배하지 않음)면 ②를 생략한다.
- 각 줄 `hudHintLineSeconds`(기본 3초, unscaled). **비차단** — `SetTapToContinue` 를 쓰지 않는다.
  전투 중이라 배치 입력을 막으면 안 된다(사용자 결정 2026-08-01).

### 기믹 배너

- 체인 시작에서 `gimmickGuide?.SetTutorialSuppressed(true)`, 종료 **모든** 경로에서 `false`.
- **`EndCore` 에 기대지 말 것.** `EndCore` 는 `if (!_coreActive) return` **뒤에서** 억제를 푼다.
  체인 구간은 core 가 이미 끝난 상태라 `OnDisable → EndCore` 가 조기 return 하고
  **억제가 영구 고착된다**(로비로 나갔다 와도 기믹 안내가 다시 안 뜬다).

### 정리 경로 3곳

`StopBattleHudHint()` = 코루틴 중단 + `guidance.Hide()` + 앵커 원복 + 기믹 억제 해제.
호출처: ① `OnPhaseChanged` 의 non-Battle 분기(Tally 포함) ② `OnDisable` ③ 체인 정상 종료.

## 완료 기준

- 컴파일 오류 0 (Runtime · Tests.EditMode · Tests.PlayMode)
- EditMode 신규 1건: `ScoreHudView` seam — `SetLeakStatus(2, 7)` 뒤 `StressLimit == 7` ·
  `ShowsStressLimit` · `StressBadgeRect != null`. 뷰 빌드가 EditMode 에서 성립하지 않으면(절차적
  스프라이트·폰트) 테스트를 억지로 만들지 말고 **그 사유를 이 완료 기준에 적고** Play 검증으로 갈음한다
- Play(첫 판 전투 시작): 배지에 링 + ①②가 3초씩 순차 → 자동 종료 · **말풍선이 배지를 가리지
  않음**(스크린샷 첨부) · 그 동안 배치·탭 입력이 계속 동작 · 두 번째 판엔 미노출(각성 인트로가 대신)
- 로비 왕복 후 기믹 안내가 정상 노출(억제 고착 회귀 확인)
- 콘솔 경고·에러 0
