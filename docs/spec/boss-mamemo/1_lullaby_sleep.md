# 1 — 자장가 (주기 × 수면)

## 목적

마메모의 첫 능력. **주기마다 주변 방어유닛 몇을 재운다.** 자는 유닛은 공격을 멈추고, 맞으면 깬다.

이 unit 이 붙는 순간 마메모는 비로소 **보스로 굴러간다** — `BakeNightmareMechanics` 가 mechanics
비면 early return 이라 unit 0 까지는 `BossTag`·꿈결 위기 배너·방어유닛 사냥 이동이 하나도 없었다.

## 변경 대상

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Battle/Combat/AuraPulse.cs` | `SelectRing`(도넛) 추가 · 기존 `SelectTargets` 는 위임 |
| `Assets/_Project/Scripts/Battle/Combat/BossPeriodicTriggerSystem.cs` | `AreaSleep` 분기 + 후보 풀 lazy-load 를 진영 중립으로 |
| `Assets/_Project/Scripts/Bridge/BattleBridge.cs` | bake — 저작 실수 loud 거절 + 연출 dataIndex 허용 |
| `Assets/_Project/Data/Enemies/Enemy_Boss_Mamemo.asset` | 자장가 슬롯 1개 |
| `Assets/_Project/Tests/EditMode/AuraPulseTests.cs` | 도넛 경계 5건 |
| `Assets/_Project/Tests/PlayMode/BossLullabyTest.cs` | **신규** — 증상 재현 2건 |

## 구현

### 신규 페이로드 0 · 신규 채널 0 · 신규 시스템 0

`AreaSleep` 페이로드는 이미 있었고(실드 파열용), 방어유닛은 이미 `CcEffect` 버퍼를 갖고,
`AttackSystem` 은 공격자의 `CcActionLock.IsLocked` 를 본다. `CcApplySystem` 에는 **대상 진영
게이트가 없다**(거점 skip · 버퍼 부재 skip · 보스 면역만). 그래서 arm 이 `EnemyCcEventsSingleton`
에 넣는 것으로 끝난다 — 채널 이름만 적 지향이지 소비는 진영 무관이다.

> **기존 `AreaSleep` 실행기를 재사용하지 않는다.** `BattleBridge.DrainShieldBreakEvents` →
> `CollectShieldBreakTargets` 는 대상 풀이 `AttackUnitTag` **하드코딩**이라, 거기에 방어유닛 쿼리를
> 섞으면 실드 파열 카드가 깨진다. **payload kind 만 공유하고 실행 경로는 별개다.**

### 자장가는 도넛이다 — 이게 핵심 규칙

```
안쪽 반지름 = 마메모의 사거리(타일)   ← 여기는 안 재운다
바깥 반지름 = payload.tileRange       ← 여기까지 재운다
```

**이유는 자기무효화다.** 마메모는 `BossTag` 이라 방어유닛을 사냥해 붙어서 때리고, 이 엔진의 수면은
**맞으면 풀린다**(`DamageApplicationSystem` 의 `CcClearRequest{Sleep}` — 진영 게이트 없음).
사거리 안을 재우면 자기 평타가 곧바로 깨워 슬롯 하나가 통째로 낭비된다.

사거리 밖만 재우면 **"앞은 때리고 뒤를 재운다"** 는 읽히는 모양이 되고, 그 사고가 규칙 하나로
구조적으로 사라진다.

안쪽 반지름은 `GridMath.RangeToTiles(AttackState.range)` 로 뽑는다 — **FSM 이 "때릴 수 있나"를
재는 것과 같은 변환**이라 두 판정이 갈리지 않는다.

> README 계약 4 의 초판은 "자기 **공격 대상**을 제외"였다. 구현에서 `AttackState.committedTarget`
> 이 START→RESOLVE 1회 수명이라(그 밖에서는 항상 비어 있다) 안정된 기준이 못 된다는 것이
> 드러나 **거리 기준으로 바꿨다.** 의도는 같고 기준만 안정적인 것으로 옮겼다.

### 대상 선별

1. `AuraPulse.SelectRing` — Chebyshev 도넛 (경계 양쪽 포함)
2. 자기 자신 · `DeadTag` · `PendingDeployment` 제외 — **cap 적용 전에** 걸러야 유령이 자리를 안 뺏는다
3. `AoeTargetCap.SelectNearest` — 월드 거리² 오름차순 `magnitude` 명
   (형제 경로인 실드 파열 `AreaSleep` 과 **같은 선별기**. 셀 거리는 동률이 흔해 쿼리 순서가
   결과를 가르므로 쓰지 않는다.)
4. `EnemyCcEvent{ Sleep, duration }` enqueue

진영 축은 **유닛 태그**(`AttackUnitTag`/`DefenderUnitTag`)다 — `FactionTag` 을 쓰면
`battle-structures` 이후 진영 비트가 거점을 포함하는데 거점엔 CC 버퍼가 없어 cap 자리를 유령이 먹는다.

### 저작값 (초안 — 실플레이 튜닝 대상)

`PeriodicTimer(3.5s)` × `AreaSleep(magnitude 3 / tileRange 5 / duration 4)`.
마메모 사거리 2 → 실효 도넛 = Chebyshev **2~5**.

주기 3.5s 근거: `boss-jjangssen` 계약 4 의 실측 **보스 생존 4~7초**. 한 조우에 최소 1~2회는 터져야
"구현은 됐는데 게임에서 안 보인다"가 안 난다.

## 완료 기준

- [x] EditMode `AuraPulseTests` 11/11 (도넛 5건 신규 — min 경계 포함 · min>max 는 무선택 ·
      min<=0 이 기존 동치인지 = whip 무회귀 근거)
- [x] 전체 EditMode 2146 중 2143 통과 · **실패 0** · 스킵 3(전부 기존 `[Ignore]`)
- [x] **PlayMode `BossLullabyTest` 2/2** — 실스폰 경로(`SpawnUnit` → bake)로 마메모를 세우고
      `CcEffect` 버퍼를 직접 읽는다:
      ① 사거리 밖 방어유닛이 **실제로 잔다** ② 사거리 안 방어유닛은 **안 잔다**
      ③ 마메모 자신은 자기 자장가에 안 잔다(보스 CC 면역)
      → 슬롯을 손으로 만들지 않고 에셋 저작을 통과시키므로 **저작이 틀리면 빨개진다**
- [x] 컴파일 에러 0
- [ ] **Play 육안(사용자)**: 마메모 주변 방어유닛 머리에 수면 표식이 뜨고 사격이 멈춘다.
      마메모가 때리는 유닛은 안 잔다. 표식 위치/크기는 unit 4 대상

> **미검증(남김)** — 전체 PlayMode 회귀는 이 시점에 신뢰할 수 없었다. 병행 세션이 sim/data
> 9개 파일(`AttackUnitData`·`AggroStateSystem`·`AgentSeparationSystem` 등)을 **편집 중**이라
> 97개 중 17개가 실패했고, 그 범위가 인증·씬전환·드래그·스쿼드 흐름까지 걸쳐 이 unit 의 diff
> 로는 설명되지 않는다. **이 unit 이전의 PlayMode 기준선을 잡아두지 않았다** — 워크트리가
> 안정되면 재측정한다.
