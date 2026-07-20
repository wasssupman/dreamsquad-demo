# 12 — 배틀 시작 각성 인트로 (3단계 중 0단계)

## 목적

두 번째 판부터, 전투가 시작되는 순간 각성 버튼을 한 번 포커스해 "여기를 열면 된다"를 알린다.
기존 2단계 힌트는 **그대로 유지**하고 그 앞에 한 단계를 더한다(사용자 결정 2026-07-21).

## 변경 대상

- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs`

## 구현

### 3단계 구성

| 단계 | 트리거 | 문구 | 상태 플래그 |
|---|---|---|---|
| **0 (신규)** | `PhaseChanged(Battle)` | `여기서 드림캐쳐 덱을 열어보세요` | `_awakeningIntroShownThisBattle` |
| A (기존) | 낼 수 있는 카드 생김 | `드림캐쳐 사용 준비 완료!` | `_awakeningOfferedThisBattle` |
| B (기존) | 손패 열림 + usable 슬롯 | `포커스된 카드를 원하는 캐릭터로 끌어보세요!` | 완료 저장 후 disarm |

**0단계는 `_awakeningOfferedThisBattle` 을 건드리지 않는다.** 건드리면 A 가 영영 안 뜬다.
전용 플래그 `_awakeningIntroShownThisBattle` 를 새로 두고 `Battle` 진입 시 다른 두 플래그와 함께 리셋한다.

**0단계는 arm 하지 않는다.** 초안은 "전투 시작 게이지 0" 을 근거로 arm 이 안전하다고 봤지만,
`Gauge` 초기값은 `AwakeningConfig.gaugeStart` 로 **SO·시트 튜너블**이다
(`DreamcatcherHandController.cs:124`, `DcSheetImportDto.gaugeStart`). 0 이 아니게 되는 순간
0단계 직후 손패를 열면 B 가 앞당겨 발화해 완료가 저장되고 A 문구가 그 계정에서 영영 안 뜬다.

대신 **B 단계 가드에 `_awakeningOfferedThisBattle` 을 추가한다** — 카드 사용법은 A 가 실제로 뜬
뒤에만 의미가 있다. 0단계만 본 상태로 손패를 열면 아무 문구도, 저장도 하지 않는다.

### 배치 — 한 프레임 미룬다

`OnPhaseChanged` 의 `Battle` 분기, `EvaluateAwakeningHint()` **앞**에서 코루틴을 시작한다.

**같은 프레임에 표시하면 안 된다.** `AwakeningGaugeView.OnPhaseChanged` 는 같은 `PhaseChanged`
이벤트의 다른 구독자이고 구독 순서가 보장되지 않는다. 튜토리얼이 먼저 돌면 패널이 아직 비활성이라
`Pulse()` 가 조용히 소실된다(`AwakeningGaugeView.cs:75` 가드). 포커스 링은 `Update` 가 다음 프레임에
복구하지만 **Pulse 는 복구되지 않는다.** `yield return null` 후 페이즈를 재확인하고 표시한다.

```csharp
_awakeningOfferedThisBattle = false;
_awakeningArmedThisBattle = false;
_awakeningIntroShownThisBattle = false;
CompleteCoreProgress();                                  // unit 10 — _coreActive 무관
if (_coreActive) EndCore(restoreNormalPlacement: false);
StartAwakeningIntro();                                   // 코루틴 시작만
EvaluateAwakeningHint();
```

```csharp
private IEnumerator AwakeningIntroRoutine()
{
    yield return null;                    // gaugeView 가 패널을 켤 기회를 준다
    if (_awakeningLockedThisMatch || _awakeningIntroShownThisBattle) yield break;
    if (gameManager == null || gameManager.CurrentPhase != GamePhase.Battle) yield break;
    if (guidance == null || gaugeView == null) yield break;
    if (!TutorialProgress.ShouldRunAwakeningHint(profileSO)) yield break;

    // 지연의 목적이 패널 활성화 대기이므로 실제로 활성인지 확인한다. 아직 비활성이면
    // Pulse 가 no-op 되고 링도 안 뜨는데 플래그만 소모돼 0단계가 조용히 사라진다.
    var hit = gaugeView.HitRect;
    if (hit == null || !hit.gameObject.activeInHierarchy) yield break;

    _awakeningIntroShownThisBattle = true;
    gaugeView.Pulse();
    guidance.ShowMessage("여기서 드림캐쳐 덱을 열어보세요", showSkip: false);
    guidance.FocusUi(gaugeView.HitRect);
    if (_awakeningRoutine != null) StopCoroutine(_awakeningRoutine);
    _awakeningRoutine = StartCoroutine(HideAwakeningPromptRoutine());
}
```

첫 판 억제는 unit 10 의 `_awakeningLockedThisMatch` 하나가 담당한다 — 별도 게이팅을 두지 않는다.

> **`ShouldRunAwakeningHint` 에 `!IsCorePending` 을 추가하는 방식은 쓰지 않는다.** `OnPhaseChanged` 가
> `CompleteCoreProgress()` 를 먼저 실행하므로 첫 판에도 그 시점엔 이미 pending 이 false 다. unit 10 참조.

### 단계 간 충돌

- 0단계 표시 중 게이지가 차서 A 가 발화하면 A 가 `_awakeningRoutine` 을 중단하고 자기 문구로 교체한다
  (기존 코드가 이미 그렇게 한다). 자연스러운 승계다.
- 0단계와 A 는 같은 대상(`gaugeView.HitRect`)을 포커스한다. 연속으로 뜨면 링이 그대로 유지된다.
- 완료 저장 시점은 **B 단계 그대로**다. 0단계만 보고 손패를 안 열면 다음 판에 다시 안내된다(의도).
- `_awakeningIntroShownThisBattle` 은 Battle 진입뿐 아니라 **`ResetAwakeningSession()` 에서도**
  다른 두 플래그와 함께 리셋한다(비대칭 방지).

## 완료 기준

- [ ] 컴파일 통과
- [ ] 첫 판에는 0단계가 뜨지 않는다(unit 10 봉인)
- [ ] 두 번째 판 전투 시작 즉시 각성 버튼이 포커스되고 `여기서 드림캐쳐 덱을 열어보세요` 가 뜬다
- [ ] 그 상태에서 손패를 열어도 **새 문구가 뜨지 않고** 0단계 문구가 잔여 시간만큼 유지된다
      (`HideAwakeningPromptRoutine` 3.5초). 저장도 일어나지 않는다
- [ ] 이후 게이지가 차면 `드림캐쳐 사용 준비 완료!` 가 이어서 뜬다 — **0단계 때문에 건너뛰지 않는다**
- [ ] 손패를 열면 `포커스된 카드를…` 가 뜨고 `awakeningHintVersion` 이 `1` 로 저장된다
- [ ] **두 번째 판에서 B 까지 도달해 저장된 경우**, 세 번째 판에서는 세 단계 모두 뜨지 않는다
      (B 에 도달하지 못했으면 다시 안내되는 것이 의도다)
- [ ] 판당 1회 — 같은 전투에서 재진입/재발화하지 않는다
