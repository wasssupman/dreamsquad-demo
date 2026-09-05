# 4 — 놓아주기와 경계 진동 방지

## 목적

감지가 **언제 풀리는지**를 정한다. 규칙이 없으면 둘 중 하나가 난다: 대상을 죽인 적이 그 자리에서
뚝 돌아서 골로 걸어가거나(연출이 끊긴다), 경계선에서 감지가 깜빡여 제자리 진동한다.

사용자 확정: **대상 사망 + 관성 1초.** 죽이면 1초간 다음 대상을 찾아보고, 없으면 경로 복귀.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/DetectedTarget.cs` — `graceRemaining` 추가
- `Assets/_Project/Scripts/Battle/Combat/DetectionSystem.cs` — 유지 판정 + 관성
- `Assets/_Project/Tests/EditMode/…/DetectionSystemTests.cs` — 케이스 추가

## 구현

**두 임계를 가른다** — 이 프로젝트가 사거리에서 이미 쓰는 형태 그대로다:

- **획득**: `gap ≤ detectionRange` — `AttackReach.InReach`
- **유지**: `gap ≤ detectionRange + h` — **`TargetPersistence.KeepsLock` 을 그대로 재사용**한다.
  `h = TargetPersistence.HysteresisTiles = 0.1칸`이고, 그 값은 「멈춘 유닛이 프레임마다 실제로
  얼마나 흔들리는지」의 실측(0.047·0.051칸)에서 나왔다. 새 상수를 만들지 않는다 — 같은 종류의
  진동을 막는 데 두 개의 자를 두면 그게 다음 드리프트다.

**관성(grace)**:

```csharp
public struct DetectedTarget : IComponentData
{
    public Entity target;
    public byte hunting;
    public float graceRemaining;   // > 0 = 대상을 잃었지만 아직 사냥을 유지하는 중
}
```

프레임 절차(unit 2 의 절차에 이어붙는다):

1. 현재 `target` 이 **살아 있고 유지 임계 안**이면 그대로 유지 — `hunting = 1`, `grace = 0`.
   (매 프레임 최근접을 다시 고르지 않는다. 그러면 방어유닛 둘 사이에서 대상이 튄다.)
2. 아니면 새로 스캔한다. 후보가 있으면 채택 — `hunting = 1`, `grace = 0`.
3. 후보가 없는데 **직전에 사냥 중이었으면** `grace = GraceSeconds`(1초)를 켠다.
   `hunting` 은 **1로 유지**하고 `target` 만 비운다 — 적은 계속 사냥판을 따르며 다음 대상을 찾는다.
4. grace 중이면 매 프레임 감소(`BattleSimGroup` dt = TimeManager Battle 도메인 — CC·도발과 같은
   시계라 슬로모에서 갈리지 않는다). 그 사이 후보가 잡히면 2 로 돌아가 grace 를 끈다.
5. grace 가 만료되면 `hunting = 0` — 골 경로 복귀.

**대상 사망은 별도 분기가 아니다.** 1의 「살아 있고」가 거짓이 되어 2 → 3 으로 흐른다. 사망·소멸·
반경 이탈이 **같은 경로**를 지나므로 「죽었을 때만 관성이 붙는」 비대칭이 생기지 않는다.

**막힘 해제(stuck release)** — 관성과 별개의 두 번째 해제 사유다.

감지는 legal 필터를 지나지만(계약 4) **이동을 만드는 소스 수집은 안 지난다** —
`DefenderFieldSystem` 은 faction 필터 하나뿐이고, `MovementSystem.cs:81~85` 가 이미 그것을
선재 결함으로 기록해 뒀다(「최근접이 **못 때리는** 방어유닛이면 그쪽으로 다가가 눌러붙을 수
있다」). 오늘 무해한 이유는 사냥 적이 4건이고 보스 마스크가 넓기 때문이다. `Enemy_Kindler` 의
`targetClassMask`(레인저 전용)가 이 결함이 실재한다는 증거이고, unit 6 이 잡몹에 감지를 켜면
결함을 **상속한다.** 갇힌 적은 웨이브 회전(「필드에 적 0기」)까지 막으므로 결함이 곱해진다.

```csharp
if (hunting && ai == Marching && holdingGround != 0 && !actionLocked) stuckSeconds += dt;
else stuckSeconds = 0f;
if (stuckSeconds >= StuckReleaseSeconds) { hunting = 0; suppressRemaining = SuppressSeconds; }
```

`holdingGround`(Movement 소유, RO)는 「자기주도 변위가 실제로 있었나」의 정본이다.

⚠⚠ **그 값은 「CC 잠금」도 함께 접는다**(ECS 리뷰 H1 — 그 필드 문서가 접는 것을 직접 열거한다:
`Standoff / Engaging-Halt / Pulse-타격중 / CC 잠금 / 순찰 dir 0 / 고립 셀`). 그것만 보면
**자장가 한 번에 감지가 풀린다** — `Card_ShieldLull` 지속이 2.5초로 임계 2초를 넘는다. 결과는
**플레이어가 CC 를 쓸수록 적이 사냥을 그만두는** 정반대 방향이다. 그래서 `!lockedNow` 를 더한다
(`CcActionLock.IsLocked || LeapFlight` — `MovementSystem:162` 와 **같은 술어**, 자를 새로 안 만든다).

⚠ **무제한 사냥(보스·보너스)은 막힘 해제에서 통째로 면제한다.** 「전멸시켜야 골에 간다」는 저작된
성질이고, 타이머가 그것을 취소할 권한을 가지면 감지가 패배 통로의 조절기가 된다(계약 9와 충돌).
해제 뒤 `suppressRemaining` 동안 재감지를 막지 않으면 다음 프레임에 같은 대상을 다시 물어
제자리에서 깜빡인다.

`StuckReleaseSeconds = 2f` · `SuppressSeconds = 5f`. 근거: 감지 전 기준선의 정체가
**최장 3프레임(0.05초)** 이므로(보고서 §6) 2초는 정상 통행과 구조적으로 겹치지 않는다.

**`GraceSeconds = 1f` 는 코드 상수이고, 근거는 실측이 아니라 사용자 결정이다.**
`HysteresisTiles = 0.1` 은 「멈춘 유닛이 실제로 얼마나 흔들리는가」의 실측(0.047·0.051칸)에서 나왔지만
이 1초는 그런 뒷받침이 없다 — 그 차이를 숨기지 않는다. 값이 틀렸다는 신호는 unit 6 의 Play 육안
(「대상을 죽인 적이 즉시 돌아서지 않고 잠깐 주변을 노린다」)에서 온다. 유닛 스탯이 아니라 술어의 폭이라 `HysteresisTiles` 와 같은
성격이며(제약 6 이 겨냥하는 「유닛별 밸런스 값」이 아니다), `sceneKnobs` 에 등재하지 않는다 —
등재하면 `configHash` 가 움직여 골든 red 가 「조건 드리프트」로 읽힌다. 적마다 다른 관성이
필요해지면 그때 `AttackUnitData` 로 올린다.

**전방향 원의 대가는 여기서 완화된다.** 감지는 뒤쪽 방어유닛도 잡으므로(사용자 결정 1) 적이
되돌아간다 — 실측 R=3 에서 감지 시간의 18%. 관성이 없으면 되돌아가다 반경을 벗어나 다시 앞으로
가는 왕복이 생길 수 있고, grace 1초가 그 구간을 덮는다. **grace 를 0 으로 줄이려면 「앞쪽만 감지」를
같이 켜야 한다**(README 후속 후보) — 둘은 같은 문제의 두 손잡이다.

## 완료 기준

- compile 통과 · EditMode 전체 초록(선행 실패 2건 제외).
- EditMode 신규:
  - 대상이 죽으면 1초 동안 `hunting == 1` 이 유지되고, 그 뒤 `0` 이 된다.
  - grace 중에 새 후보가 반경에 들어오면 즉시 채택되고 `graceRemaining == 0` 이 된다.
  - 반경 경계 바로 밖(`detectionRange + 0.05칸`)에서는 이미 문 대상을 **놓지 않는다**(히스테리시스).
  - 반경 + 0.2칸에서는 놓는다.
  - 대상이 살아 있고 유지 임계 안이면 더 가까운 방어유닛이 새로 배치돼도 **대상이 안 바뀐다**.
  - **막힘 해제**: `hunting` 인 채 `holdingGround != 0` 이 2초 지속되면 `hunting == 0` 이 되고,
    그 뒤 5초 동안은 같은 조건에서도 다시 안 잡힌다.
  - 막힘 해제는 **행동정지 CC 중에는 누적되지 않는다**(못 움직이는 게 규칙이지 막힌 게 아니다).
- **거동 무변** — ⚠ 골든 `Verify` 는 이 판정에 못 쓴다. 코퍼스가 이 spec 이전부터 stale 이고
  (unit 1 완료 기준 참조) `configHash` 도 스키마 변경으로 이미 움직였다. 대신 **이 unit 의
  변경 한 줄만 임시로 끄고 verify 를 돌려 켠 실행과 이벤트/킬을 대조한다** — 같으면 무변이다.
  감지 저작은 아직 무제한 4건뿐이고, 무제한은 후보가 늘 있어 grace 에 들어가지 않는다.
- Play 육안(unit 6 과 함께): 대상을 죽인 적이 즉시 돌아서지 않고 잠깐 주변을 노린다.
