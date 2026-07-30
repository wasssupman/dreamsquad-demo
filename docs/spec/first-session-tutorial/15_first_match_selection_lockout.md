# 15 — 첫 판 유닛 선택 봉인 (각성 봉인 누수 차단)

> 추가 2026-07-30. `selection-hand-attach` 도입으로 생긴 **회귀 수정**이다. unit 16 의 선행.

## 목적

unit 10 의 첫 판 각성 봉인은 이 전제에 기대고 있었다 — README 계약 인용:

> "손패를 여는 유일한 경로가 그 버튼이므로 카드 사용은 자연히 봉인된다"

`selection-hand-attach` unit 1 이 **두 번째 문**을 냈다. `DcInspectController.Select()` 는
`handView.OpenForSelection()` 을 무조건 부르고(`DcInspectController.cs:326`), `OpenForSelection`
에는 봉인 가드가 없다(`DreamcatcherHandView.cs:181`). `SetSuppressed` 는 항아리 패널만
`SetActive(false)` 할 뿐이라 **손패는 그대로 열린다**.

그리고 `AwakeningConfig` 은 `gaugeStart 20` · `costSquad/costUnit/costActive` **전부 20** 이다
(시트 `DcConfig` 기준값). 즉 매치 시작 게이지로 **어떤 카드든 정확히 1장**을 낼 수 있다.
첫 판에 배치한 유닛을 탭하면 숨겨둔 손패가 통째로 딜인되고 카드가 usable 로 뜨므로
**탭 즉발로 드림캐쳐를 실제로 써버린다.** 표시 수준에서도 기능 수준에서도 뚫려 있다.

> `gaugeStart` 는 오래 `100` 으로 드리프트해 있었다(디스크 SO 100 vs 시트 20). 2026-07-30 에
> 시트값 20 으로 맞췄다 — 이 unit 을 조사하며 발견했다. 1장이든 5장이든 **봉인이 뚫린다는
> 사실은 같다**(비용이 게이지보다 커야 막히는데 지금은 같다).

도달 경로: Placement 의 `Start` 스텝(`좋습니다! 더 배치해보세요`) 이후 — ClassHint 탭 캐처가
걷힌 뒤부터 첫 판 Battle 종료까지 계속 열려 있다.

**사용자 결정(2026-07-30): 선택 자체를 봉인한다.** 첫 판엔 패널·리티클·줌·슬로모도 뜨지 않는다.
부착이 0장이고 드림캐쳐 개념이 아직 도입 전이라 선택이 보여줄 것이 없고, unit 10 의
"첫 판은 배치만으로 승부를 본다"에 가장 가깝다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs` — 읽기 getter
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — 릴레이 getter
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — 봉인 게이트

## 구현

### A. 봉인 사실을 읽을 수 있게 한다 — 신규 씬 배선 0

**`BattleScene.unity` 는 타 세션 WIP 로 저장 불가**다. 튜토리얼 컨트롤러에 `DcInspectController`
참조를 새로 다는 방식(푸시)은 씬 배선을 요구하므로 쓰지 않는다. 이미 배선된 참조 사슬을 탄다:

```
FirstSessionTutorialController ──기존──▶ AwakeningGaugeView   ← _suppressed 소유
DreamcatcherHandView           ──기존──▶ AwakeningGaugeView
DcInspectController            ──기존──▶ DreamcatcherHandView
```

- `AwakeningGaugeView`: `public bool IsSuppressed => _suppressed;` — 소유자가 사실을 **공개만**
  한다. `SetSuppressed`/`ApplyPanelVisibility` 는 손대지 않는다(unit 10 의 검증된 경로 보존).
- `DreamcatcherHandView`: `public bool AwakeningSealedThisMatch => gaugeView != null && gaugeView.IsSuppressed;`

**릴레이 이름을 `AwakeningSuppressed` 로 두지 말 것.** 소비자가 둘이고 하는 일이 다르다 —
항아리는 "표시를 끈다", 인스펙트는 "선택을 막는다". `Suppressed` 는 전자의 어휘라 후자에서
읽으면 왜 선택이 막히는지가 안 보인다. 사실의 이름(`이 판은 각성이 봉인됐다`)으로 올리고,
소비처 주석에 **인과**를 남긴다 — "선택이 손패를 열기 때문에 함께 막는다".

> 사실의 소유자가 `AwakeningGaugeView` 인 것은 unit 10 이 표시만 막으면 됐던 우연이다.
> 세 번째 소비자가 생기면 그때는 소유를 옮길 신호로 본다(지금 옮기면 검증된 경로를 흔든다).

푸시가 아니라 **풀**이라 구독자 순서 의존이 없다. 튜토리얼이 `OnPlacementReady` 에서 세운 값을
인스펙트가 다음 `Update` 에 읽는다.

### B. 게이트 — 정리는 전환 시 1회

`Update()` 의 `MustClose()` **바로 앞**에 둔다.

```csharp
// unit 15 — 첫 판 봉인. MustClose 와 자리는 같지만 이유가 다르다:
// MustClose 는 "보드 입력의 주인이 바뀜"(일시적), 이쪽은 "이 판엔 기능 자체가 없음"(판 전체).
if (SealedThisMatch())
{
    _pendingTap = false;
    if (_selected != Entity.Null) Close(); // 전환 시 1회 — 아래 참조
    return;
}
if (MustClose()) { _pendingTap = false; Close(); return; }
```

**`MustClose` 처럼 매 프레임 `Close()` 를 부르지 않는다.** 그 규약이 성립하는 이유는 배치 드래그가
**수 프레임짜리 일시 상태**이기 때문이다. 봉인은 **첫 판 내내** 참이라 같은 규약을 쓰면
`panel.Hide()` 와 `_slomoLease.Dispose()`(stale id 로 `TimeManager.Release`)가 수천 프레임 동안
헛돌게 된다. 기능적으로 무해하지만 의도가 흐려진다 — 선택이 없으면 정리할 것도 없으므로
`_selected` 가 살아 있을 때만 걷는다.

`OnBoardTapped` 에도 같은 가드를 앞세운다. 첫 판엔 손패가 안 열려 dismiss 캐처가 존재하지
않지만, 폴백 경로로도 선택이 되살아나지 않게 방어적으로 막는다.

### C. 함께 사라지는 것 — 첫 판 Battle 재배치

선택을 봉인하면 **첫 판의 재배치도 도달 불가**가 된다. 이동 버튼이 상세 패널 안에 있고
(`selection-hand-attach` unit 15) 패널은 선택에서만 뜨기 때문이다.

손실 범위는 좁다 — `DefenderRelocationController.BeginMoveModeFor` 는
`CurrentPhase == GamePhase.Battle` 게이트라(`DefenderRelocationController.cs:98`) **Placement
재배치는 애초에 없다.** 즉 없어지는 것은 "첫 판 Battle 중 재배치" 하나이고, 튜토리얼은 재배치를
가르치지 않으므로 첫 판에서 빠지는 것이 의도에 부합한다.

다만 **오늘은 되는 것이 안 되게 되는 변경**이므로 여기 남긴다. 첫 판 재배치가 필요하다는 판단이
서면 봉인 범위를 "손패만"으로 좁히는 재설계가 필요하다(선택은 살리고 `OpenForSelection` 만
막는 방향 — 그 경우 `selection-hand-attach` 계약 1 에 첫 판 예외가 생긴다).

### D. Fail-open

`handView` 미배선 · 튜토리얼 컴포넌트 부재 · 참조 누락이면 `_suppressed` 가 false 라 선택이
정상 동작한다. 봉인은 `OnPlacementReady` 가 매 판 재판정하므로(unit 10 그대로) 둘째 판부터
자동으로 풀린다 — 이 unit 은 해제 로직을 새로 만들지 않는다.

첫 판 **도중** 튜토리얼 컴포넌트가 비활성화되면 `OnDisable` 이 Battle 중에는
`SetSuppressed(false)` 를 부르지 않으므로(unit 10 의 왕복 방지) 그 판은 봉인이 유지된다.
첫 판이라는 사실은 변하지 않으므로 의미상 맞고, 다음 판은 씬 재로드로 초기화된다.

## 검증 준비 (unit 16 과 공유)

튜토리얼 진행은 **로비 메뉴의 `OnResetTutorial()`** 로만 되돌린다
(`OutgameMenuController.cs:140` — 백업 파일을 남기고 `profile.json` 을 패치한다).

**이 리셋은 5개 플래그를 통째로 되돌린다** — `firstBattleTutorial` · `awakeningHint` ·
`giftTutorial` · `lobbyIntro` · `lobbyLoadoutHint`. 즉 한 번 검증할 때마다 로비 챕터 A →
첫 판 → 챕터 B → 선물 홀드 2회 → 둘째 판까지 온보딩 전체를 다시 타야 한다.

→ **units 15·16 은 함께 구현한 뒤 리셋 1회로 관통 검증한다.** 따로 검증하면 같은 비용을
두 번 낸다. 첫 판 항목(15)과 둘째 판 항목(16)이 한 번의 플레이에 순서대로 나온다.

## 완료 기준

- [x] compile 클린 · Unity 콘솔 error 0 (2026-07-30 — `dotnet build` 오류 0)
- [x] **배선 확인**: 씬 YAML 에서 `DcInspectController.handView` · `DreamcatcherHandView.gaugeView`
      가 둘 다 non-zero fileID — 풀 체인이 온전해 이 unit 이 no-op 이 아니다
- [x] EditMode: 릴레이 getter 가 소유자 값을 그대로 옮긴다(이름 오타·부호 역전 방지) —
      `AwakeningSealRelayTests` 3건
- [x] EditMode 전체 1607 / 실패 1 — `MultiGoalPoolSeparationTests`(타 세션의 dirty
      `MapDocument_Zig`)만 남는다. 무관
- [x] PlayMode: 튜토리얼·인스펙트·손패·재배치 테스트 실패 0. 전체 실패 9종은 전부 타 세션
      영역(`drag-cancel-affordance rev3` · `62260b82` 의 `BattleBridge*`/`DcApplicability`)
      이거나 기존 환경 실패(Auth·DeckCarryIn·Dreamstone·CardBuffs)
- [ ] Play 첫 판: 배치한 유닛을 탭해도 **아무 일도 일어나지 않는다** — 패널·리티클·줌·슬로모·
      손패 전부. 특히 `Start` 스텝(`좋습니다! 더 배치해보세요`) 중에 확인한다
- [ ] Play 첫 판: 항아리는 여전히 숨겨져 있고 배치·전투 시작은 정상(unit 10 회귀 0)
- [ ] Play 둘째 판: 유닛 탭 → 선택·패널·리티클·손패 전부 정상(봉인이 확실히 풀린다)
- [ ] Play 둘째 판: 패널의 **이동 버튼 → 재배치**가 정상 동작(첫 판에만 사라지는지 확인 — C절)
- [ ] Play: 튜토리얼 컴포넌트를 비활성화해도 선택이 정상 동작(fail-open)

구현 `fbcac2db` · 테스트 `77e013b2` (2026-07-30). **Play 항목은 사용자 확인 대기** —
로비 `OnResetTutorial()` 로 리셋 1회 후 unit 16 과 함께 관통 검증한다(위 검증 준비 절).

> **자동 테스트 한계**: 게이트 본체(`DcInspectController`)는 `BattleBridge`·Entity·EventSystem
> 의존이라 EditMode 로 못 덮고, PlayMode 로 덮으려면 배틀 하네스가 필요하다. handoff 13 이
> 이미 지적한 커버리지 gap(`SetSuppressed` 회귀 미보호) 위에 얹히는 구조다 — 위 EditMode
> 항목은 릴레이 배선만 고정하고, **회귀 방지는 Play 체크리스트에 의존한다.** 숨기지 말 것.
