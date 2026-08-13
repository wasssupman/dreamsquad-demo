# 1 — 퇴근 경로 (정리 절차 추출 · `RetireDefender` · `DefenderRetired`)

## 목적

유닛을 판에서 내리는 **사망과 완전히 별개인 경로**를 만든다. 사망 코드는 한 글자도 건드리지 않고,
공유되는 판 정리 절차 1벌만 두 호출처가 나눠 쓴다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ReleaseDefenderTile` 추출 + `RetireDefender`
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — `DefenderRetired` 이벤트 선언
- `Assets/_Project/Tests/PlayMode/DefenderRetireTest.cs` — **신규**

**ECS 는 건드리지 않는다** — 신규 컴포넌트 0 · 신규 채널 0 · 시스템 수정 0 (README 계약 1).

## 구현

**① 정리 절차 추출.** `DrainDefenderDeathEvents`(약 3566~3611줄)의 정리 블록은 연속 구간이고,
그 뒤 사망 고유(작별선물·`DefenderDied`)는 `evt`/`binding` 만 쓴다 — 깨끗이 갈린다.

```csharp
// 방어 유닛이 판에서 내려왔다 — 원인(사망/퇴근)과 무관한 결과. 호출처 2개가 공유한다.
// 갈라두면 한쪽만 고치는 버그가 난다(유령 게이지 · 안 풀리는 점유).
private bool ReleaseDefenderTile(Vector2Int cell, out (Entity entity, DefenderUnitData data) binding)
```

내용: 빔 종료 · `_defenderByTile.Remove` · `_occupiedTiles.Remove` ·
`RefreshPlacementHighlightIfShown()` · `tileHealthGaugeLayer.Hide(cell)` · `RecomputeSynergyFor(cell)`.

⚠ **뷰 반납은 이 함수에 넣지 않는다** (rev 2, 리뷰 반영). 사망은 `spineUnitPool.NotifyDeath` →
`SpineUnitView.Kill()` → `deathAnimation` 재생인데 **그 기본값이 `"die"`**
(`DefenderUnitData.cs:215`) 라, 퇴근이 같은 함수를 타면 **사망 애니가 나온다**. 넣었다면
`bool playDeathAnim` 같은 플래그 파라미터를 부르게 된다. 뷰 반납은 처음부터 **호출처 소유**다:
사망은 `NotifyDeath`, 퇴근은 `Despawn`(unit 3 에서 아치 연출로 확장).

⚠ **바인딩을 제거 전에 out 으로 넘긴다** — 기존 드레인이 이미 그렇게 한다(엔티티가 카드 회수
키라서). 이 순서를 뒤집으면 두 호출처가 동시에 깨진다.

⚠ **이 함수는 엔티티를 파괴하지 않는다.** 사망 경로에서는 `UnitLifecycleSystem` 이 이미 파괴한
뒤고, 퇴근 경로에서는 호출자가 파괴한다. **파괴 주체를 호출처가 갖는다**는 것이 계약이다.

**② 퇴근 진입점.**

```csharp
// 판 위 유닛을 자발적으로 내린다. 사망이 아니므로 사직서·작별선물·각성이 일어나지 않는다.
public bool RetireDefender(Vector2Int cell)
```

가드: 바인딩 존재 · `_em.Exists(entity)` · `PendingDeployment` 없음(비행 중 퇴근 금지) ·
`DeadTag` 없음(이미 죽는 중이면 사망 경로에 양보). 통과하면:

```csharp
ReleaseDefenderTile(cell, out var binding);
spineUnitPool?.Despawn(binding.entity);       // 사망 애니 배제. unit 3 이 여기를 아치로 확장한다.
defenderFallbackViewPool?.Despawn(binding.entity);
_em.DestroyEntity(binding.entity);
DefenderRetired?.Invoke(binding.entity, binding.data, GridCellToViewCenter(cell));
```

> ⚠ **defender 를 브리지가 파괴하는 것은 이 리포의 첫 사례다.** 주석으로 명시할 것.
> 브리지의 `DestroyEntity` 9건 중 유닛은 적 2건뿐이고(`BattleBridge.cs:5565`·`5805`) 나머지는
> 캐리어·필드·구조물이다. 그래도 성립하는 근거: 퇴근은 UI 기원 행위이고, 브리지가 유일한
> 게이트웨이이며, **브리지가 배치한 것을 브리지가 수거하는 대칭**이다. ECS 경계 리뷰가
> dangling 없음을 확인했다 — 참조 보유자(`FocusTarget`·`SummonerState`·`Aggroed`·투사체 target)가
> **전부 매 프레임 `Exists`/`HasComponent` 를 첫 관문으로 쓴다.**

**엔티티 파괴 자체가 나머지 신호를 대신한다** — `PatrolLifecycleSystem:48` 이 `Exists(owner)` 를
첫 검사로 쓰므로 순찰병이 다음 sim 틱에 따라 내려간다(1 틱 지연은 무해).

**③ 이벤트.** `DefenderDied` 의 형제로 **바로 옆에** 선언한다(같은 시그니처).

```csharp
// 자발적 퇴근. DefenderDied 와 갈라 둔 이유: 각성 지급·사직서·작별 선물은 죽음의 결과이지
// 퇴장의 결과가 아니다. 한 이벤트에 플래그로 실으면 구독자마다 거르는 것을 기억해야 한다.
// Vector3(셀 월드좌표)는 unit 3 의 아치 출발점이 소비한다.
public event System.Action<Entity, DefenderUnitData, Vector3> DefenderRetired;
```

## 완료 기준

> rev 2 (리뷰 반영): 초안의 PlayMode 단정 11개를 **핵심 5개로 줄였다.** 사직서 0장·작별선물 0·각성
> 0 대조군은 비싼 셋업(기믹 매치 부팅 / OnDeath 카드 부착)으로 "부르지 않은 코드가 안 돌았다"를
> 확인하는데, **`DeadTag` 미부착 + `DefenderDied` 0회 단정 하나가 그 가족 전체를 덮는다** —
> 사직서도 작별선물도 각성도 전부 그 둘에서 파생되기 때문이다. CLAUDE.md: "커버리지는 목표가
> 아니다. 회귀 방지 수준이면 충분하다."

- 컴파일 통과.
- **PlayMode(신규)**: 배치 → `RetireDefender(cell)` → `_defenderByTile` 에서 사라지고 타일 점유가
  풀린다. **`DefenderRetired` 1회 · `DefenderDied` 0회 · 엔티티에 `DeadTag` 미부착** —
  이 세 단정이 사망 결과 가족 전체(사직서·작별선물·각성)의 가드다.
- **PlayMode(신규)**: 소환사를 퇴근시키면 순찰병도 사라진다. **코드로 자명하지 않은 유일한
  cross-system 주장**이라 반드시 남긴다(`Exists` 기반 자동 회수).
- **PlayMode(신규)**: `PendingDeployment` 중(비행 중) 유닛은 `RetireDefender` 가 false 이고 판에
  남는다.
- **회귀**: 사망 경로 전체 불변. 추출이 순수 이동임을 기존 스위트가 증명한다 —
  **사망 관련 테스트가 한 건도 수정되지 않아야 한다.**
- 육안: 퇴근 시 유닛이 사라지고 **사망 애니메이션이 나오지 않는다**.

> **자동 검증 2026-08-13** — 컴파일 통과(에러 0).
> 신규 `DefenderRetireTest` **2/2** · `PatrolDefenderPlayTest` **3/3**(사망 판본 + 신규 퇴근 판본) ·
> `PlacementAuraTest`·`SlimeSplitE2ETest` 통과(사망 드레인·OnDeath 분열을 지나는 회귀 축) ·
> unit 0 스위트 재확인(재배치 4/4 · 재배치+BoardLimit 9/9 · EditMode 8/8).
> **사망 관련 테스트는 한 건도 수정하지 않았다** — 추출이 순수 이동임을 기존 스위트가 증명한다.
> 첫 테스트가 우연히 `G3_ClockOut` 기믹 매치에서 돌았다(사직서 기믹 활성 상태에서 `DefenderDied` 0회).
>
> **깨끗한 baseline 을 못 떴다 — 대신 도달 불가를 증명했다.** 전체 PlayMode(129건)를 돌렸으나
> ⑴ `editor_unfocused` 로 라이브 전투 테스트에서 고착(72/129에서 중단), ⑵ 남은 실패 중
> `DropDismountTest`·`DreamcatcherCursedRelicTest` 2건이 포커스 복구 후에도 재현됐다.
> `BattleBridge.cs` 에 **다른 세션의 미커밋 타이머 HUD 작업**(`SetTopBar`·`TimerDuration`)이
> 섞여 있어 stash 로 baseline 을 뜨면 그쪽 작업을 가져가므로, 호출처 전수 조사로 대신했다:
> `ReleaseDefenderTile` 호출처는 **정확히 2개**(사망 드레인 · `RetireDefender`)이고
> `RetireDefender` 의 프로덕션 호출처는 **아직 0개**(unit 2 가 버튼을 붙인다).
> 두 실패 테스트는 방어유닛을 죽이지 않으므로 **바뀐 코드에 도달할 수 없다.** 런타임 예외도 0건.
