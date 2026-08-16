# 4 — 배스티온: 집단 도발 (`AreaTaunt` 페이로드)

## 목적

배스티온의 배치 스킬을 **반경 안 적 전원을 N초간 도발**로 바꾼다. 지금은 즉발 광역 50 +
밀쳐냄인데, 배스티온의 정체(유일한 가디언 · `aggroCapacity 2` · 체력 2070)와 정반대다 —
붙잡는 유닛이 배치 순간엔 적을 **밀어낸다**.

평소엔 **때린 적만 하나씩** 끌고 상한 2에 막힌다. 배치 순간엔 **상한을 무시하고 한꺼번에**
붙잡는 것이 이 유닛이 평소 못 하는 일이다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcPayloadKind.AreaTaunt` **append**(23)
- `Assets/_Project/Scripts/Battle/Combat/BossPeriodicTriggerSystem.cs` — `AreaTaunt` arm
- `Assets/_Project/Scripts/Core/Dreamcatcher/DcApplicability.cs` — 신규 kind 등록
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — bake 시점 저작 검증(loud warn)
- 신규 `Assets/_Project/Data/Abilities/Ability_Taunt_Bastion.asset`
- `Assets/_Project/Data/Defenders/Defender_Bastion.asset`
- 신규 `Assets/_Project/Tests/PlayMode/OnPlaceTauntNearbyTest.cs`

## 구현

### `DcPayloadKind.AreaTaunt` (신규 — 이번 spec 유일)

기존 어휘로 표현되지 않아서 만든다(README 계약 3). 확인한 후보들: `ApplyCcToTarget`(10)은
**맞은 대상 1기**라 범위가 아니고, `AreaSleep`(16)은 범위지만 수면 전용이며 도발은 CC 가 아니라
**타게팅 상태**다.

`payload.tileRange` = 반경(타일), `payload.duration` = 도발 지속(초). 기존 payload 필드 어휘를
그대로 쓰므로 `DcPayloadSpec` 에 신규 필드 0.

### arm (Combat 시스템, unit 0 의 `OnPlace` 소비 지점)

```
AreaTaunt:
    host 에 AggroCapacity 없으면 → skip (가디언만 도발한다)
    반경 안 적 = 「이번 프레임 합법 후보」 쿼리:
        WithAll<AttackUnitTag, LocalTransform>
        WithNone<DeadTag, UltimateLeapState>          ← 아래 ⚠ 참조
        + Chebyshev(hostCell, enemyCell) <= tileRange
        + 층 게이트(아래)
    각 적마다 _aggroAcquireQueue.Enqueue({ guardian = host, enemy = e,
                                          kind = Taunt, durationSec = duration })
```

- **게이트를 복제하지 않는다.** 보스 면역·도달 가능·공격 수단·`EnemyTargetFilter` 판정은 전부
  `AggroStateSystem`(Effects) 소유다. 여기서 미리 걸러도 같은 판정이 두 곳에 생기고, 둘이 갈리는
  순간 한쪽만 고쳐진다(`defender-on-place-skills` unit 4 의 후보 집합 결함과 같은 형태).
- **`affected` 는 "요청 수"지 "실제 도발된 수"가 아니다.** 로그 문구·주석에 명시한다.

⚠ **`UltimateLeapState` 는 이 쿼리가 직접 빼야 한다.** rev2 초안은 "드레인이 `DeadTag`·
`UltimateLeapState` 를 다 검사하므로 무해"라 적었는데 **거짓**이다 — `AggroStateSystem` 드레인에는
`DeadTag` 게이트만 있고 `UltimateLeapState` 게이트가 없다. 오늘은 `BossTag` 면역이 우연히 가려
주지만, 엘리트에 궁극기 도약이 열리면 그대로 구멍이 된다.

⚠ **층 게이트를 유지한다.** 브리지의 `CollectEnemiesInTileRange` 는 `CanDefenderTargetMover` 로
통행 층을 걸러 근접 가디언이 **비행 적을 끌어오지 않게** 한다(배스티온 `attackTargetLayers: 2`).
arm 이 시스템으로 가면 그 헬퍼를 못 쓰므로 **baked 마스크로 같은 판정을 한다.** 구현 시 방어유닛의
`attackTargetLayers` 가 ECS 에 baked 돼 있는지 먼저 확인하고, 없으면 unit 0 에서 굽는다.
게이트를 빼면 **하늘의 적이 근접 가디언에게 끌려온다.**

⚠ **`BossPeriodicTriggerSystem` 은 `[BurstCompile]`** — arm 안에서 `Debug.LogWarning` 불가.
저작 검증(`duration <= 0` · `tileRange <= 0` · host 가 가디언이 아님)은 **bake 시점 loud warn** 으로
낸다. 특히 `tileRange 0` 은 조용히 0명이 되므로 가드가 실제로 필요하다.

### `DcApplicability` 등록

새 `DcPayloadKind` 는 `EvaluateMechanic` 전수 검사 대상이다(등록 누락 시 `Unclassified`
fail-closed + 전수 테스트 실패 — 설계된 안전망). `AreaTaunt` 는 host 의 공격 모델과 직교하고
**host 가 가디언인가**에만 의존 → `None`(허용). 실제 유효성은 bake 가 loud 로 판정한다.

### `Ability_Taunt_Bastion` (`UnitSkillAbility`)

```
mechanics[0]:
    trigger.kind      = OnPlace
    payload.kind      = AreaTaunt
    payload.tileRange = 2
    payload.duration  = 5      ← 아래 밸런스 주의 참조
```

### `Defender_Bastion.asset`

| 필드 | 현재 | 변경 | 근거 |
|---|---|---|---|
| `abilities` | `[]` | `[Ability_Taunt_Bastion]` | |
| `onPlaceEffect` | 4 (`MeleeBurst`) | **0 (`None`)** | 레거시 경로 해제 |
| `onPlaceRange` / `onPlaceMagnitude` | 1 / 50 | 0 / 0 | 피해 없음 — 사건은 붙잡기 하나다 |
| `onPlacePushDistance/Duration/Radius` | 2 / 0.2 / 3 | **0 / 0 / 0** | 밀쳐냄과 도발은 정반대 힘(계약 10) |

`aggroCapacity 2` 는 **그대로** — 평시 히트 어그로의 상한이고, 도발은 그 상한을 우회하는 별개
경로다(unit 3).

### ⚠ 밸런스 주의 — Play 에서 확인할 실패 모드

`TauntAttackGrantSystem` 은 **평소 공격 안 하는 러너/스위프트에도 도발 공격 프로필을 부여**한다.
즉 반경 2 안 웨이브 전원이 5초간 배스티온 하나를 때린다. 2070 이 못 버티면 가디언 사망 →
`ShouldRelease` 로 전원 즉시 해제 → **적이 이미 한 덩어리로 뭉친 채 골로 진격**한다. 도발이 오히려
유출을 앞당기는 형태다.

또 세 유닛 반경이 전부 2 라 **도발(5) → 스턴(5) → 폭격(4) 콤보**의 겹침이 100% 다.
`duration 5` 와 `tileRange 2` 는 **첫 값이며 Play 실측 대상**이다 — 반경을 어긋나게 하거나
지속을 줄이는 조정이 필요할 수 있다.

⚠ **도발 만료 시 archetype 이동 스파이크**: `previousTargetMask == 0` 인 적은 만료 시
`AttackState`+`AttackOutputElement` 가 통째로 제거된다. 5마리 동시 만료 = 한 틱 구조 변경 5회.
기능 문제는 아니나 육안 검증에서 한 프레임 튐이 보일 수 있다.

## ⚠ 테스트 더미는 실제 적의 부품 셋을 갖춰야 한다 (실측 2026-08-16)

도발은 **sim 게이트를 여럿 통과해야** 성립하는데, 합성 더미가 그 게이트가 읽는 부품을 빠뜨리면
제품이 멀쩡해도 테스트가 빨개진다. 이번에 셋에 연달아 걸렸다:

| 빠진 부품 | 증상 |
|---|---|
| `PathFollowState` | 통행 층이 0 = 무제한으로 조기 통과 → **층 게이트가 죽어도 초록** |
| Walk 아닌 칸에 배치 | 도달 가능 게이트가 거절 → 5기 중 1기만 걸림 |
| `EnemyAiState` | `MovementSystem` 이 `Marching` 으로 떨어뜨려 **골로 걸어간다**(실측 2.46 → 5.39) |

`defender-on-place-skills` 후속 후보의 「테스트 더미가 통행 층을 안 가진다」가 가리키던 공백이
실은 더 넓다. 이 unit 의 테스트는 셋을 다 갖춘 더미를 쓰고, 적을 놓을 칸은
`FlowFieldSingleton.walkMask` 로 **실제 Walk 칸**을 찾아 고른다(맵이 바뀌어도 안 깨진다).

## 완료 기준

- [x] compile 0 error · `DcApplicability` 전수 테스트 green (2026-08-16)
- [x] PlayMode `OnPlaceTauntNearbyTest`
  - 반경 2 안 적 5마리(상한 2 초과) → **전원** `Aggroed`, `guardian == 배스티온`
  - 반경 밖 적 → `Aggroed` 없음
  - **비행 적은 도발되지 않는다** (층 게이트 핀)
  - **판 밖(`UltimateLeapState`) 적은 후보가 아니다** (rev2 의 거짓 주장 자리)
  - `duration` 경과 → 전원 해제
  - **도발된 적이 배스티온 쪽으로 이동한다**(N프레임 뒤 거리 감소) — 상태만 붙고 안 움직이면
    도발이 아니다. ⚠ 이 단언이 이 unit 의 핵심 회귀 핀이다
  - 배치 직후 적이 배스티온을 공격한다(`AggroAttackProfile` 경로 회귀)
  - 가디언 아닌 유닛에 같은 능력 → bake 경고 + 무동작 (조용한 통과 금지)
- [x] 기존 어그로 PlayMode/EditMode 무회귀
- [ ] Play 육안: 적 무리 근처에 배스티온 배치 → **반경 안 적이 전원 동시에 방향을 틀어** 몰려온다.
      1~2기만 끌려오면 실패다. 5초 뒤 다시 흩어져 골로 향한다
- [ ] Play 육안: **밀집 웨이브에서 배스티온이 도발 중 죽는 시나리오** — 뭉친 적이 그대로 진격하는
      실패 모드를 실제로 보고 `duration`/`tileRange` 를 조정한다
- [ ] Play 육안: **세 유닛(배스티온·말파이트·캐논) 동시 배치** 콤보의 파괴력 확인
