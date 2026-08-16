# 2 — 마음: 게이지 제거 → 균열 연출

## 목적

**마음의 남은 수치를 바·숫자로 그리지 않는다.** 그 정보는 **금이 가는 상태**로만 보인다.
판정 권한이 0이 된 축(unit 0)을 화면에서도 판정처럼 안 보이게 만드는 마무리다.

곁들여 스트레스 배지의 **분모를 뗀다** — 한계가 아무것도 하지 않는데 `3 / 10` 이 떠 있으면
그건 거짓말이다. 누수 누적 수량만 남긴다(사용자 결정, "일단은").

## 마음은 게이지를 **두 개** 달고 있었다

조사에서 드러난 것 — 지우기 전에 둘 다 찾아야 한다.

| 어디 | 무엇 |
|---|---|
| `BattleBridge.SyncGoalStabilityBars()` | 골 셀마다 전용 안정도 바(수치 라벨 포함) |
| 구조물 공통 바 루프(`_structureRegistry` 순회) | **모든 구조물**에 오버헤드 바 — 마음도 포함 |

두 번째는 본능·적 마음도 함께 그린다. **그쪽은 건드리지 않는다** — 금지 대상은 「내 마음」
(`Faction.DefenderCore`) 하나다. 그래서 제거가 아니라 **그 진영만 스킵**이다.

## 변경 대상

- `Bridge/BattleBridge.cs` — `SyncGoalStabilityBars()`·`goalStabilityBarLift` 제거,
  구조물 바 루프에서 `DefenderCore` 스킵, 균열 단계 push, `RefreshLeakHud` 분모 off
- `Presentation/UnitOverheadUiLayer.cs` — `SetStability`/`HideStability`/`_stabilityViews` 제거
- `Data/UnitOverheadUiStyle.cs` — `OverheadBarSkin.GoalStability` enum 값 · `goalStability`
  BarSkin · `stabilityLabel*` 3종 제거
- `Presentation/UnitOverheadView.cs` — `valueLabel` 계열 제거(그 스킨 전용이었다)
- `Core/TilemapMapView.cs` — `SetGoalCrack(cell, stage)` 신설

## 구현

**1. 균열 = 프랍의 상태.** `MarkGoalCollapsed`(붕괴 = 그을림 + 주저앉음)의 형제로 둔다.
같은 제약(«프랍 교체 아트가 없으므로 코드만으로 읽히게 한다»)을 그대로 따라 틴트로 표현하고,
**단계로 양자화**한다(온전/1/2/3). 매 프레임 연속 변화는 눈에 안 읽히고, 단계가 있어야
「금이 하나 더 갔다」가 **사건**으로 읽힌다. 붕괴(0)는 기존 `MarkGoalCollapsed` 가 계속 소유.

**2. push 는 변화할 때만.** 브리지가 골 셀별 마지막 단계를 기억하고 바뀔 때만 부른다.
`SyncGoalStability` 의 **살아 있는 `DefenderCore` 분기**에 얹는다 — 그 순회가 이미 셀과
`Health` 를 둘 다 들고 있어서 새 기계가 필요 없다(적 마음 잔여를 같은 순회에 얹은 선례).

**3. `OverheadBarSkin.GoalStability` enum 값을 지워도 안전하다.** 이 enum 을 필드로 직렬화한
곳이 없다(전수 확인) — 런타임 switch 인자로만 쓰인다. `goalStabilityBarLift` 는
`[SerializeField]` 라 씬에 키가 남지만 Unity 가 다음 저장에 흘린다.

**4. 스트레스 분모.** `RefreshLeakHud` 가 `showLimit: false` 를 넘긴다. 한계 값 자체
(`EffectiveLeakLimit()`)는 계속 넘긴다 — 튜토리얼이 «스냅샷이 왔는가» 판정에 `_leakLimit` 을
읽는다. **부수 효과(의도된 것)**: 튜토리얼의 `스트레스가 N이 되면 패배합니다` 문구가
기존 가드(`ShowsStressLimit && StressLimit > 0`)에 걸려 **자동으로 빠진다** — 패배가
없어졌으니 그 문장은 거짓말이다.

## 완료 기준

- [x] 컴파일 통과(테스트 어셈블리 포함), 콘솔 에러 0
- [x] 코드베이스에 `SetStability`·`HideStability`·`GoalStability`·`goalStabilityBarLift`
      참조가 0건
- [x] EditMode **2435/2435 완주** — 신규 실패 0건(`ScoreHudStressSeamTests` 포함)
- [x] PlayMode `TallyFlowTest`(단독 7.1s)·`GoalStabilityTest` 초록
- [ ] Play: 마음 위에 **바도 숫자도 없다**. 본능·적 마음의 바는 그대로 있다
- [ ] Play: 마음이 맞을수록 프랍이 단계적으로 그을리고, 0 에서 주저앉는다
- [ ] Play: 스트레스 배지가 `3` 으로만 뜬다(`3 / 10` 아님)
