# 0 — `AreaCc`: 반경 안 적 전원에게 CC

## 목적

「반경 N 타일 안 적 **전원**에게 CC 를 L 초」라는 어휘를 연다. 지금 이 일은 브리지의 레거시
`StunNearby` 분기만 할 수 있고, 규칙 경로에는 CC 를 범위로 거는 payload 가 없다
(`AreaSleep` 은 「가까운 M명」cap 형태라 다른 선별기다).

이 unit 만으로는 어떤 스킬도 새로 생기지 않는다(소비자는 unit 2).

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcPayloadKind.AreaCc` **append(26)**
- `Assets/_Project/Scripts/Core/Dreamcatcher/DcApplicability.cs` — 분류 등록
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BakeUnitMechanics` 저작 검증 + ccKind bake
- `Assets/_Project/Scripts/Battle/Combat/BossPeriodicTriggerSystem.cs` — payload arm
- `Assets/_Project/Tests/EditMode/` — bake·거절·분류 테스트

## 구현

### enum append

`AreaCc = 26`. ⚠ append-only — 에셋이 int 로 직렬화한다.

주석에 **`AreaSleep`(16)과 갈리는 이유**를 적는다: 저쪽은 cap(가까운 M명) + 「내가 이번에 때릴
대상은 뺀다」는 자장가 전용 선별기를 갖는다. 이쪽은 **반경 안 전원**이고 cap 이 없다.
둘을 합치면 cap 0 = 전원이라는 매직값이 생기고, 자장가의 rank 제외가 전원 경로에도 딸려 온다.

### arm — `AreaTaunt` 를 그대로 베낀 자리

`BossPeriodicTriggerSystem` 의 `AreaTaunt` arm 과 **같은 후보 풀·같은 게이트**를 쓴다.
다른 것은 큐 하나뿐이다(`AggroAcquireEvent` → `EnemyCcEvent`).

```
if (slot.duration > 0f && slot.tileRange > 0 && hasCcQ && HasComponent<LocalTransform>(entity))
    BuildEnemyPool(...) → AuraPulse.SelectTargets(enemyCells, hostCell, slot.tileRange, ...)
    for each victim:
        skip DeadTag · UltimateLeapState                    ← 계약 9(합법 후보)
        skip !PlacementLayers.CanTarget(hostLayers, victimLayers)  ← 계약 4(층 게이트)
        ccQueue.Enqueue(EnemyCcEvent{ target=victim, effect={ kind=slot.ccKind, remainingTime=slot.duration } })
```

`hostLayers` = `AttackState.targetTraversalLayers`(없으면 0 = 무필터). **빼면 근접 말파이트가
하늘의 적을 스턴시킨다** — `AreaTaunt` 주석이 경고하는 것과 같은 구멍이다.

⚠ 이 시스템은 `[BurstCompile]` 이다. 저작 실수의 loud 경고는 **전부 bake(브리지)** 에서 낸다.

### 넉업 연출 — 「띄우는 길이」는 유닛이 갖는다

`CcKind.Stun` 은 심의 사실이고 「공중」은 뷰의 해석이다(`knockup-fighter-defender` unit 3).
브리지는 뷰에 직접 접근할 수 있어 `PlayKnockupHop` 을 바로 불렀지만, arm 은 ECS 안이므로
**기존 채널**을 쓴다 — `KnockupVisualEventsSingleton`(target · durationSec · height).

체공 길이·높이는 저작 필드를 새로 만들지 않고 host 의 `DefenderCcData` 에서 읽는다:

```
if (ccDataLookup.HasComponent(entity) && ccDataLookup[entity].knockupVisualHeight > 0f)
    hop = ccDataLookup[entity].knockupOnHitSec > 0f
        ? min(knockupOnHitSec, slot.duration) : slot.duration;
    knockupQueue.Enqueue(new KnockupVisualEvent{ target=victim, durationSec=hop, height=... });
```

`min` 인 이유는 지금과 같다 — 스턴보다 오래 떠 있으면 **땅에 닿기 전에 적이 다시 움직인다.**
`knockupVisualHeight` 가 0인 유닛(=적을 안 띄우는 유닛)은 연출 없이 CC 만 건다.

### bake — 저작 검증은 loud

`BakeUnitMechanics` 에서:

- `ccKind` 번역은 이미 있다(`ApplyCcToTarget` 경로) — 그 자리를 `AreaCc` 도 타게 한다.
- 거절 3종(경고 후 `continue`): `duration <= 0` · `tileRange <= 0` · `ccKind == None`.
  셋 다 「붙는데 영영 안 터진다」를 만드는 조합이다.

### `DcApplicability` 등록

`AreaTaunt` **바로 옆**에 같은 이유로 등록한다: host 의 공격 모델과 무관하다(대상은 반경이
정하고, 데미지 출력이 없으며, 진영은 CC 파이프라인이 고정한다). 빠뜨리면 `Unclassified`
fail-closed 로 전수 테스트가 빨개진다(설계된 안전망).

카드 경로는 추가 작업이 없다 — `OnPlace` 트리거는 이미 loud 거절이고, `AreaCc` 를 카드가
다른 트리거로 선언하는 것은 이 spec 이 만들지 않는다.

## 완료 기준

- [ ] compile 0 error
- [ ] **이 unit 만으로는 어떤 스킬도 바뀌지 않는다.** 기존 배치 스킬 10종 무회귀
- [ ] EditMode
  - `AreaCc` mechanic bake → `DcTriggerSlot` 에 슬롯 1개(ccKind·duration·tileRange 실림)
  - 저작 거절 3종이 각각 경고 + 슬롯 미생성
  - `DcApplicabilityMatrixTests` / `DcApplicabilityTests` 전수 green (`Unclassified` 0)
- [ ] PlayMode (arm 단독 검증 — 임시 능력 SO 로)
  - 반경 안 적 전원 `CcEffect{kind=Stun}` · 반경 밖 무영향
  - **층 게이트**: `attackTargetLayers` 가 지상만인 host 는 비행 적에 CC 를 안 건다
  - `knockupVisualHeight > 0` host 는 `KnockupVisualEvent` 가 대상 수만큼 큐에 들어간다
