# 2 — 액션 슬롯 중립화 · 퇴근 버튼 · 쿨타임 · 카드 회수

## 목적

퇴근을 사람이 누를 수 있게 만들고, 퇴근한 유닛이 **쿨타임을 거쳐 트레이로 돌아오게** 한다.
같은 커밋에서 패널의 액션 슬롯을 **기능 이름에서 떼어낸다**.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectPanelView.cs` — 액션 슬롯 중립화
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — 진입 가드 + 콜백 + 라벨
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — `DefenderRetired` → 쿨타임 시작
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — 카드 회수
- `Assets/_Project/Tests/PlayMode/DefenderRetireTest.cs` — 이어서 작성

## 구현

**① 액션 슬롯 중립화** (rev 2, 리뷰 반영). 이동 버튼이 쓰던 1칸을 그대로 쓰되 **기능 이름을
뺀다**. 초안은 `SetMoveState` → `SetRetireState` 로 **재특화**했는데, 그러면 슬롯이 또 기능에
묶여 README 계약 10("상수 한 줄이면 이동이 부활한다")이 **거짓**이 된다 — 부활하려면 라벨·
시그니처·cost 파라미터를 전부 되돌려야 하기 때문이다.

```csharp
public void SetActionState(bool enabled, string label);          // was SetMoveState(bool, int cost)
public void Show(..., System.Action onAction = null);            // was onMove
```

라벨 소유가 컨트롤러로 넘어간다(`"퇴근"` / 부활 시 `$"이동  {cost}"`). 뷰 안에 하드코딩돼 있던
`$"이동  {cost}"` 가 사라지므로 **어느 쪽이든 뷰는 어차피 고쳐야 했다** — 개명 1회로 끝낸다.
래치도 `(bool enabled, int cost)?` → `(bool, string)?` 로 단순해진다(cost 가 label 에 흡수).

**② 진입 가드** — 이동 가드에서 코스트·이동모드·진입쿨다운을 뺀 형태(계약 9):

```csharp
private bool CanRetire(Entity e, Vector2Int cell)
{
    var gm = GameManager.Instance;
    if (bridge == null || gm == null || gm.CurrentPhase != GamePhase.Battle) return false;
    return bridge.TryGetDefenderAt(cell, out var found, out _, out bool busy) && !busy && found == e;
}
```

`TickSelectionAnchor` 의 매 프레임 피드에 태운다 — 이동 버튼 피드가 있던 자리 그대로.
비행 중(busy)이면 흐려졌다가 착지하면 풀린다.

> 참고: `TryGetDefenderAt(…, out busy)` 는 `BattleBridge.Relocation.cs:39` 에 있다. 퇴근이 재배치
> 파일의 헬퍼를 쓴다는 뜻이라 **"이동은 죽었다"는 절반만 참**이다(README 후속 후보에 기록).

**③ 쿨타임 시작.** `DefenderSelector` 가 이미 배치 확정에서
`StartCooldown(unit, unit.placementCooldown)` 을 부른다(`:201`). 퇴근도 **같은 줄을 같은 파일에서**
부른다 — 쿨타임 소유자를 하나로 유지한다.

```csharp
// PlacementCommitted / DefenderDied 구독 바로 옆
private void OnDefenderRetired(Entity _, DefenderUnitData data, Vector3 __)
{
    GameManager.Instance?.CooldownRuntime?.StartCooldown(data, data.placementCooldown);
    RefreshSlots();
}
```

⚠ **`DefenderDied` 핸들러와 합치지 않는다.** 트레이 리페인트는 같지만 **쿨타임 시작이 사망에는
없어야** 한다(README 열린 밸런스 항목). 합치면 그 결정이 코드에서 사라진다.

⚠ **`placementCooldown == 0` 이면 `StartCooldown` 이 no-op** 이라 즉시 재배치 가능하다
(런타임의 "0 = inert"). 그게 옳은 기본값이다 — **쿨타임을 켜는 것은 저작 행위**다.

**④ 카드 회수.** `DreamcatcherHandController.OnDefenderDied` 는 각성 지급 + 회수 둘을 한다.
퇴근은 **회수만** 한다(계약 5). 회수 절반을 `RecoverCardsHostedBy(Entity host)` 로 뽑아
**호출처 3개**가 공유한다(사망 / 적 소멸 / 퇴근).

⚠ **"같은 루프"는 절반만 사실이다** — `OnDefenderDied` 에만 `handle > 0 →
bridge.RevokeDreamcatcherEffects(handle)` 3줄이 있고, `OnEnemyGone` 주석은 "표식은 handle 0 이라
revoke 호출도 없다"고 말한다. 통합본은 revoke 분기를 포함하게 되고 `OnEnemyGone` 이 그걸
**물려받는 행동 확장**이다. 유일한 writer 인 `AttachAndSpend` 에서 **적 부착이 항상 handle 0 인지
한 줄 확인**하고 넘어간다. 참이면 확장은 무해(분기가 절대 안 탐), 거짓이면 회수 경로를 가른다.

> 이 추출은 CLAUDE.md 제약 8("나중을 위한 추상 레이어 금지")과 충돌하지 않는다 — 호출처가
> 실제로 3개이고, 기존 두 곳이 이미 같은 루프를 복제하고 있었다.

## 완료 기준

- 컴파일 통과.
- **PlayMode**: 퇴근 후 그 유닛 슬롯이 소진 해제되고 `RemainingFor(unit) == placementCooldown`.
  쿨타임 중 배치가 막히고, 0 이 되면 다시 놓인다.
- **PlayMode**: 부착 카드 2장을 얹은 유닛을 퇴근시키면 **2장 모두 큐 뒤로 복귀**한다.
- 육안: 선택 패널에 "퇴근" 버튼. 누르면 유닛이 사라지고 트레이 셀에 쿨타임 오버레이가 차오르며
  다 빠지면 배치 가능해진다.
- 육안: 비행 중 유닛을 선택하면 퇴근 버튼이 흐리고, 착지하면 활성화된다.
- **회귀**: `BoardLimit*` · 재배치 스위트 · 사망 경로 전부 통과.
- **회귀**: 액션 슬롯 중립화가 **레이아웃을 바꾸지 않는다** — 버튼 위치·크기 불변.
