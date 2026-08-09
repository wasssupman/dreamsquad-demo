# units 1·2 — 락 유지 술어 단일화 + 범위 이탈 해제 (B2)

두 unit 을 한 커밋으로 묶는다. **unit 1 없이 unit 2 만 넣으면 미러가 갈려 데드락이 재발**하고, unit 1 만 넣으면 아무 동작도 바뀌지 않아 검증할 것이 없다. 절단선이 커밋 경계로 성립하지 않는다.

## 목적

**B2 제거** — `FocusUntilDead` 적이 사거리를 벗어난 대상의 락을 붙든 채 발사를 보류하고, FSM 이 `Marching` 으로 떨어져 **바로 옆 방어유닛을 두고 골로 걸어가던** 결함.

```csharp
else bestTarget = Entity.Null;                                   // :653 out of range → hold fire
focusLookup[attackerEntity] = new FocusTarget { current = cur };  // 락은 재저장 ← 여기
```

## 결함이 «둘 반쪽»이었다는 점이 핵심

같은 규칙이 **두 시스템에 복제**돼 있었다.

| 시스템 | 무엇을 결정 | 옛 규칙 |
|---|---|---|
| `AttackSystem:653` | 누구를 때릴까 | 이탈 → `bestTarget = Null`, 락은 재저장 |
| `EnemyAiStateSystem:133` | 움직일까 멈출까 | 이탈 → `false` → `Marching` |

둘이 합쳐져야 «발사도 안 하고 골로 걸어감»이 된다. 그리고 후자의 주석이 이미 경고하고 있었다 — *"⚠ AttackSystem fire 조건 미러. 타겟 선정 로직 변경 시 동기화 필요."*

**말로 된 계약은 이미 실패한 상태였다.** 그래서 한쪽만 고치면 안 되고, 구조로 묶어야 한다.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Battle/Combat/TargetPersistence.cs`
- 수정: `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — Focus 블록 재구성 + 거짓이 된 헤더 주석 정정
- 수정: `Assets/_Project/Scripts/Battle/Combat/EnemyAiStateSystem.cs` — 미러가 같은 함수를 호출
- 수정: `Assets/_Project/Scripts/Data/EnemyBehaviorEnums.cs` — `FocusUntilDead` 의미 변경 명시
- 신규: `Assets/_Project/Tests/EditMode/TargetPersistenceTests.cs`
- 수정: `EnemyAiStateSystemTests.cs` · `EnemyBehaviorTests.cs` — **옛 계약을 고정하던 테스트 2건** (아래)

## 구현

### unit 1 — 술어 하나

```csharp
public static bool KeepsLock(bool targetAlive, int tileDistance, int tileRange)
    => targetAlive && tileDistance <= tileRange;
```

**이 함수의 가치는 산술이 아니라 «두 시스템이 같은 규칙을 본다»이다.** 한 줄짜리를 파일로 뺀 근거는 제약 10 의 (b) 실제 재사용 2 호출처 + (c) sim-critical 회귀 가치이며, `AggroPolicy.CanAcquire/ShouldRelease` 와 같은 형태다. 엔티티 룩업은 호출자에 남긴다(순수 유지).

### unit 2 — 이탈 시 해제 (D2)

`AttackSystem` 의 Focus 블록을 «유지/해제» 두 갈래로 재구성한다. 해제 갈래는 **이미 있던 else 분기를 그대로 재사용**한다 — 거기에 골 락 금지 가드(`goalPointLookup`)가 이미 들어 있어 새로 쓸 필요가 없다.

`EnemyAiStateSystem` 은 이탈 시 `return false` 대신 **fall through** 시켜 nearest/filter 경로로 넘긴다.

## 옛 계약을 고정하던 테스트 2건 (의도적 갱신)

**일괄 갱신하지 않았다.** 각각 왜 뒤집히는지 판단하고 이름과 주석까지 고쳤다.

| 테스트 | 옛 기대 | 왜 뒤집히나 |
|---|---|---|
| `EnemyAiStateSystemTests.Focus_LockOutOfRange_OtherNear_Marching` → `..._ReleasesAndEngages` | `Marching` | 옛 근거가 *"락 때문에 발사를 못 하니 영구 정지를 막으려면 걸어가야 한다"* 였다. **그 조합이 정확히 B2 다.** 락을 놓지 않는 것이 전제였고 그 전제가 사라졌다. 이제 `Engaging` 이며 옛 근거(영구 정지 방지)도 함께 만족된다 |
| `EnemyBehaviorTests.FocusUntilDead_OutOfRange_HoldsFire_KeepsLock` → `..._ReleasesLock_NoFireWhenNoOtherTarget` | 락 유지 | 이름 자체가 옛 계약이다. 이제 락이 비고, 대체 후보가 없으므로 발사도 없다 |

**테스트가 버그를 고정하고 있었다는 것이 이 unit 의 부수적 발견이다.**

## 완료 기준

- [x] compile 에러 0 · EditMode **2001 중 1998 통과 · 실패 0** (나머지 3은 기존 `[Ignore]`)
- [x] 신규 `TargetPersistenceTests` 7건 — 술어 3(유지/이탈해제/사망해제) + 통합 4:
  ① 락 대상이 이탈하면 **사거리 안의 다른 방어유닛으로 넘어간다**
  ② 그때 FSM 이 `Engaging` 이다 (`Marching` = B2)
  ③ 사거리 안에 아무도 없으면 `Marching` 이 **맞다**(버그 아님)
  ④ 사거리 안이면 더 가까운 대상이 와도 락을 유지한다(유지 계약 불변)
- [x] 옛 계약 고정 테스트 2건을 이유와 함께 갱신
- [ ] **Play 육안**: Focus 적(Needler·Rootcaster·Vanguard)이 방어유닛을 지나친 뒤 **되돌아 교전하거나 다음 방어유닛과 교전**하는가 — 예전엔 그냥 골로 걸어갔다

## 남은 것

**unit 3(`Nearest` 모드에도 락)** 은 하지 않았다. `Nearest` 4종(Tanker·Debuffer·**보스 2종**)은 여전히 매 프레임 재선정한다 — 원칙 2 의 나머지 절반이다.

---

**완료 기준 확인**: (Play 육안 미확인)
