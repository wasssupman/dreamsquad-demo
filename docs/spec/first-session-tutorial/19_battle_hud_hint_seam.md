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
- `Assets/_Project/Data/Config/TutorialGuidanceStyle_Default.asset` — **편집하지 않는다.** YAML 에
  없는 키는 Unity 가 C# 필드 초기값을 그대로 유지하므로 초기값만 코드에 주면 된다. 굳이 재직렬화하면
  이 spec 과 무관한 orphan 키 churn 이 딸려온다(`forcereserialize-keeps-orphan-keys`).
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

- 앵커를 `HudHint` 로 바꿔 말풍선을 배지·링 아래로 내리고, **체인이 끝날 때까지 유지한다**
  (웨이브 스텝에서 되돌리면 방금 가르친 배지를 그 두 줄이 덮는다). 원복은 `StopBattleHudHint`.
- **기하는 이렇다**(초판 스펙의 `178~242` 는 `cornerPadding 36` 을 빠뜨린 오산이었다):
  - 배지는 **화면 우상단**이다 — `ScoreHudView` 패널이 anchor·pivot `(1,1)` · 폭 360 ·
    `cornerPadding 36` 인셋. 세로 구간 `36+20+148+10 = 214 ~ 278`, 포커스 링은 `focusPadding 14`
    + `pulseScale 1.10` 까지 먹여 **~297** 까지 내려온다.
  - 말풍선은 top-center 폭 880 이라 **와이드 화면에서는 수평으로 겹치지 않는다**
    (1920 기준 말풍선 520~1400 vs 배지 1524~1884). 겹치는 건 **4:3 급 좁은 화면**이다
    (1440 기준 116px). 즉 이 앵커는 4:3 방어이고, 값은 297 을 넘겨야 의미가 있다 → 기본 **310**.
  - `CanvasScaler` 는 `matchWidthOrHeight = 1`(높이 기준)이라 세로 좌표는 기기와 무관하게
    reference(1080) 기준으로 비교할 수 있다. 두 캔버스가 같은 `UiCanvasSetup` 을 쓴다.
- ① `악몽을 막아 스트레스 관리하세요!` — 사용자 작성본
- ② `스트레스가 {한계}이 되면 패배합니다.` — `{한계}` 는 위 seam 에서 읽는다. **하드코딩 금지**
  (제약 6). 현재 `WaveA.asset → defeatGoalReachedCount = 10` 이지만 덱마다 다르다.
  - **사용자 작성 원문은 `스트레스가 10이되면 패배합니다.`** (2026-08-01 요청). 수치만 데이터로
    빼고 조사 `이` 는 원문 그대로 두었으며, `이되면` → `이 되면` 띄어쓰기만 교정했다.
  - 한국어 이/가 는 수치의 읽음에 따라 갈린다(10=십 → `이`, 5=오 → `가`). 자릿수별 조사 계산은
    데모 범위에서 과잉이라 현 튜닝(한계 10)에 맞춘다 — 한계를 바꾸면 이 줄을 함께 본다.
- ②를 생략하는 조건은 **둘**이다: `ShowsStressLimit == false`(엔드리스 — 분모 미표기 · 유출로
  패배하지 않음) **또는** `StressLimit <= 0`. 후자가 필요한 이유는 `_leakShowLimit` 기본값이
  true 이고 `_leakLimit` 기본값이 0 이라, 스냅샷이 아직 안 왔거나 `ActiveDeck` 이 없으면
  `스트레스가 0이 되면 패배합니다.` 가 **경고 없이** 나가기 때문이다.
- 각 줄 `hudHintLineSeconds`(기본 3초, unscaled). **비차단** — `SetTapToContinue` 를 쓰지 않는다.
  전투 중이라 배치 입력을 막으면 안 된다(사용자 결정 2026-08-01).

### 기믹 배너는 건드리지 않는다

이 체인은 Battle 에서만 도는데 기믹 안내는 Battle 에 뜨지 않으므로 **억제할 대상이 없다.**
`SetTutorialSuppressed` 를 부르면 죽은 코드에 "억제 영구 고착" 위험만 딸려 온다(제약 8·10).

> 2026-08-01 갱신: 처음 근거는 `GimmickGuideView.RefreshVisibility` 의 `_phase == Placement`
> 게이트였고, 그래서 "core 안내(Placement)의 억제는 유지"라고 적었다. 그 뒤
> `gimmick-recognition-upgrade` unit 3 이 **배치 안내 카드를 은퇴**시켜 억제 대상이 통째로
> 사라졌고, 첫 판 리빌 생략은 `GimmickPhaseView` 가 `TutorialProgress.ShouldRunCore` 로 스스로
> 판정한다. 그 결과 `OnPlacementReady`·`EndCore` 쪽 억제 호출과 `gimmickGuide` 참조까지 전부
> 제거됐다 — 이 문서의 "core 쪽은 그대로"는 더 이상 성립하지 않는다.

### 정리 경로 3곳

`StopBattleHudHint()` = 코루틴 중단 + `guidance.Hide()` + 앵커 원복.
호출처: ① `OnPhaseChanged` 의 non-Battle 분기(Tally 포함) ② `OnDisable` ③ 체인 정상 종료.

- **`EndCore` 에 기대지 말 것.** 체인 구간은 core 가 이미 끝나 있어 `EndCore` 가
  `!_coreActive` 로 조기 return 한다 — 체인이 세운 상태(코루틴·말풍선·앵커)는 아무도 되돌리지
  않는다.
- **`_hudHintActive` 가드가 본체다.** 이 함수는 체인이 없을 때도 불린다(Placement 진입). 가드
  없이 `Hide()`·앵커 원복을 실행하면 core 안내가 막 세운 말풍선을 걷어버린다.

## 완료 기준

- 컴파일 오류 0 (Runtime · Tests.EditMode · Tests.PlayMode)
- EditMode 신규 4건 `ScoreHudStressSeamTests` — 한계·표기 플래그 왕복, 스냅샷 전 `0`/true 조합,
  링 대상이 `LeakPlate`(숫자 텍스트 아님)임. **EditMode 는 `AddComponent` 로 `Awake` 를 부르지
  않는다** — 앞 3건은 `BuildCanvas` 없이 성립하고, 링 대상 검증만 `BuildCanvas` 를 리플렉션으로
  강제한다. 그때 Unity 빌트인 리소스(`UI/Skin/Knob.psd`) 어서트가 나므로 그 테스트만
  `LogAssert.ignoreFailingMessages` 를 쓴다(문자열을 Expect 로 고정하면 Unity 버전에 깨진다)
- Play(첫 판 전투 시작): 배지에 링 + ①②가 3초씩 순차 → 자동 종료 · **말풍선이 배지를 가리지
  않음**(스크린샷 첨부) · 그 동안 배치·탭 입력이 계속 동작 · 두 번째 판엔 미노출(각성 인트로가 대신)
- 로비 왕복 후 기믹 안내가 정상 노출(억제 고착 회귀 확인)
- 콘솔 경고·에러 0

**완료 확인 2026-08-01** — 사용자 Play 확인 통과. 커밋 `45d35fea`(구현) · `04127844`(code-reviewer 반영) · `2e72a742`(critic 반영 — 배지 기하 재계산, 오프셋 310). 말풍선이 배지·링을 가리지 않음, 안내 중 배치·탭 입력 동작, 두 번째 판 미노출, 로비 왕복 후 기믹 안내 정상까지 확인. 스트레스 스텝은 unit 21 에서 **전투 정지 + 탭**으로 재설계됨 — 이 문서의 자동 순차 3초 서술은 그 이전 계약이다.
