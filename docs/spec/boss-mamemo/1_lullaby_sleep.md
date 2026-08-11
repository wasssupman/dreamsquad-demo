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

### 제외는 «내가 때릴 대상» 하나뿐이다 — 링이 아니다

```
후보     = payload.tileRange 안 전부 (전 범위)
제외     = 거리 오름차순 앞에서 attackTargetCount 기, 단 **사거리 안일 때만**
```

마메모는 `attackTargetCount = 1` 이라 **최근접 1기**만 빠진다. 그 1기는 어차피 이번 평타가
깨울 대상이라 재워도 낭비다. 나머지는 사거리 안이어도 **재운다** — 한 번에 1기만 깨우므로.

> ### ⚠ 도넛은 폐기됐다 — 실측으로
>
> 초판은 «안쪽 반지름 = 사거리(+1)» 도넛이었다. 논리는 맞았지만 **능력이 죽었다.**
> 12초 조우 실측(방어유닛 4기):
>
> | | 도넛 | rank 제외 |
> |---|---|---|
> | 수면 시작 | **1회** | **2회** |
> | 누적 수면 | 2.3초 | **4.8초** |
> | 붙은 뒤(≤2타일) 발동 | **없음** | 있음 |
>
> 이유: 마메모는 방어유닛을 **사냥해서 붙는다.** 조우의 대부분을 사거리 안에서 보내고
> (268/720 프레임) 도넛은 **접근 중에만** 점유된다(85프레임). 붙은 뒤로는 후보가 **0명**이라
> 주기가 3.5초여도 안 터진다. 사용자 보고 *"재우는 효과가 발생하지 않는다"* 의 실체가 이것이다.
>
> **교훈**: 자기무효화 걱정(1/3 낭비)을 막으려다 발동 자체를 없앤 과잉이었다. 제외의 크기는
> **`attackTargetCount` 만큼**이지 사거리 링 전체가 아니다.
>
> 경계 산수 자체(`min` 이 inclusive 라 링을 빼려면 `+1` 이 필요)는
> `AuraPulseTests.Ring_MinIsInclusive_*` 가 계속 고정한다 — 누가 다시 링을 빼려 할 때를 위해.

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

`PeriodicTimer(3.5s)` × `AreaSleep(magnitude 3 / tileRange 4 / duration 2.5)`.
반경 4 전 범위(81칸)에서 가까운 3명 — 단 **사거리 안 최근접 1기는 제외**.

- **주기 3.5s** 근거: `boss-jjangssen` 계약 4 의 실측 **보스 생존 4~7초**. 한 조우에 최소 1~2회는
  터져야 "구현은 됐는데 게임에서 안 보인다"가 안 난다.
- **`duration(2.5) < period(3.5)`** — 매 주기 1초는 깨어 있다. 이게 없으면 "잠시 재운다"가
  거짓이 된다(리뷰 M7): 매 주기 같은 «가장 가까운 3명»을 다시 뽑아 갱신하므로 끊김이 없고,
  깨우는 유일한 수단이 마메모의 단일 대상 평타(cd 1.5)라 실질 회수가 1명뿐이다.
  bake 가 `duration >= period` 저작을 경고한다(whip 오라의 정반대 방향 함정).
- **`tileRange`** 이력: 5 → 3(사용자 체감) → **4**. 도넛 시절엔 사거리보다 최소 2 커야 했지만
  이제 전 범위라 그 제약은 없다(bake 는 `tileRange 0` 만 거절한다).

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
- [x] **리뷰 H1 회귀 가드** — `+1` 을 빼고 돌려 `BossLullabyTest` 가 «사거리 링은 안 잔다»
      단언에서 **실제로 빨개지는 것**을 확인했다(테스트가 구현을 미러링하지 않는다는 증거)
- [x] **Play 육안(사용자) 확인 완료 2026-08-11**
- [x] **README 계약 10 항목 이행 — «1회 조우에서 자장가 N회 이상 발동» (2026-08-11)**.
      `BossLullabyLiveTest` 가 실배치 + 보스 자율 이동 12초 조우를 프레임 단위로 계측하고
      **통과 여부와 무관하게 로그를 남긴다**(발동 횟수·수면 누적 초·거리 분포).
      실측: rank 제외 수정 후 **발동 2회 · 누적 수면 4.8초 · 보스가 붙은 뒤(≤2타일)에도 발동**.
      바로 이 계측이 도넛 설계의 "조우당 1회" 퇴화를 잡았다 — 이 축이 없으면
      "구현은 됐는데 게임에서 안 보인다" 가 재현된다는 경고가 실증됐다.

> **미검증(남김)** — 전체 PlayMode 회귀는 이 시점에 신뢰할 수 없었다. 병행 세션이 sim/data
> 9개 파일(`AttackUnitData`·`AggroStateSystem`·`AgentSeparationSystem` 등)을 **편집 중**이라
> 97개 중 17개가 실패했고, 그 범위가 인증·씬전환·드래그·스쿼드 흐름까지 걸쳐 이 unit 의 diff
> 로는 설명되지 않는다. **이 unit 이전의 PlayMode 기준선을 잡아두지 않았다** — 워크트리가
> 안정되면 재측정한다.
