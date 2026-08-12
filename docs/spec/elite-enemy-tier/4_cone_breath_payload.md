# 4 — `AreaBreath` 페이로드 (화염 브레스)

## 목적

드래곤의 3타 브레스를 성립시킨다. `AttackN(3)`(unit 3)이 발동하면 **대상 방향 부채꼴** 안의 후보
전원에게 즉발 피해를 준다. unit 1 의 `TileAoe.IsInCone` 의 유일한 소비자다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcPayloadKind.AreaBreath` +
  `coneHalfAngleDeg` 필드 (**둘 다 append-only**)
- `Assets/_Project/Scripts/Battle/Combat/DcTriggerSlot.cs` — 반각 필드 1개
- `Assets/_Project/Scripts/Battle/Combat/UnitAttackVisualEvent.cs` — 브레스 연출 필드 append
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — bake 분기 + `DrainUnitAttackVisualEvents` 에
  콘 VFX 스폰
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — arm (콘 적용)

## 구현

### 페이로드 저작 축

```
DcPayloadKind.AreaBreath = 21        // append-only. 20 은 unit 5 의 SplitOnDeath 가 선점
```

★ **`DcApplicability.EvaluateMechanic` 에 분류를 추가해야 한다.** 그 함수의 전수 테스트
(`DcApplicabilityTests.EvaluateMechanic_IsTotalOverAllKindAndArchetypePairs`)가 (payload ×
trigger × archetype) 전 조합을 돌며 `Unclassified` 를 잡는다 — 잊으면 EditMode 가 빨개진다
(unit 5 구현 중 실제로 걸렸다). 브레스는 host 의 공격 모델과 무관하므로 `SelfBlink`·
`UltimateLeap` 과 같은 자리에 두고 `DcRejectReason.None` 을 반환한다.

필드 재사용 규약을 따른다: `magnitude` = 피해 · `tileRange` = 사거리(타일) · **신규
`coneHalfAngleDeg`** = 반각(도).

반각을 `duration` 에 겸직시키지 않는 이유: `slamDamage` 선례와 같다 — 도형 파라미터는 이름으로
grep 돼야 하고, `duration` 은 «시간» 이라는 의미를 이미 갖고 있어 겸직하면 «시간인 줄 알고» 읽는
코드가 생긴다. 필드 1개 추가는 append-only 라 기존 카드 전부 무손상(0 = 반각 0 = 직선 1칸,
저작 누락이 조용히 전방위가 되지 않는다).

### 적용 지점 = `AttackSystem` arm (신규 시스템 0)

**투사체 캐리어를 만들지 않는다.** 브레스는 즉발이고(계약 9), `AttackSystem` 은 그 프레임에
이미 후보 배열(`targetEntities` · `targetTransforms` · `targetFactions` · `targetTraversalLayers`)을
손에 들고 있다. 그 자리에서 순회한다:

```
발동 시:
  dir  = normalize((bestTargetPos - attackerPos).xz)   // 대상 방향(월드)
  for 후보 i:
      if ((targetFactions[i].value & atk.targetMask) == 0) continue   // ① 진영
      if (통행층 교집합 == 0) continue                                  // ② Air/Path
      if (targetEntities[i] == attackerEntity) continue                // ③ 자기
      if (!InCone(attackerPos.xz, targetTransforms[i].Position.xz,
                  dir, cosSq, rangeWorld)) continue
      ecb.AppendToBuffer(targetEntities[i], new IncomingDamage { amount = magnitude, source = attacker })
```

⚠⚠ **위 세 술어는 생략 불가다 — 후보 배열은 진영 필터가 되어 있지 않다.**
`targetCandidatesQuery`(`AttackSystem.cs:44`)는 `FactionTag, Health, LocalTransform` 의 **전 진영
통합 풀**이고, 진영 판정은 배열이 아니라 공격자 루프 안의 `int mask = attack.ValueRO.targetMask`
(≈464행)가 한다. 이 세 줄이 없으면 **드래곤이 같은 웨이브 동료와 적 마음(`EnemyCore`)을 태운다.**
② 는 지상 전용 공격이 `Air` 로 번지는 것을 막는다(`waypoint-routing` 계약 7).

- **`bestTarget` 이 없으면 발동하지 않는다.** 방향을 만들 수 없다. 카운트는 이미 소비된 상태로
  둔다(기존 계약 5 «반경 안에 적이 없어도 카운트는 소비» 와 동형).
- **순회 본문을 private static 으로 뺀다**(리뷰 L12). `SpawnNeedleCarrier`(≈`:1865`) 선례대로
  plain 배열·plain 값만 받게 하면 1974줄 시스템을 키우지 않고 단위 테스트가 가능해진다.
- **`AoeTargetCap` 을 쓰지 않는다.** 부채꼴에 든 전원이 맞는 것이 이 능력의 요점이다(cap 0 =
  무제한과 동치이므로 호출 자체를 생략).
- **위협(`ThreatHitEvent`) 귀속을 하지 않는다.** 위협 테이블은 보스 전용 부속물이고 엘리트는
  갖지 않는다(unit 0 계약).

### bake 가 지는 두 가지 의무

술어 자체의 계약은 **unit 1 이 소유**한다(정의역·부호 가드·같은 자리·월드 좌표 근거). 여기서는
그 계약이 요구하는 저작·bake 쪽 의무만 진다:

1. **`coneHalfAngleDeg >= 90` 을 loud 거절한다.** 제곱 비교의 정의역이 반각 < 90° 이고,
   `cos²θ = cos²(180−θ)` 라 저작 120° 가 **조용히 60° 콘으로 동작**한다. `<= 0` 은 warning.
2. **각도 → `cosSq` 변환을 bake 에서 1회** 한다(저작은 도, 슬롯은 코사인²). sim 이 삼각함수를
   부르지 않고, 저작값 하나가 두 표현으로 갈리지 않는다. 사거리도 같은 자리에서
   `tileRange × tileSize` 로 환산하거나 arm 이 hoist 한 `tileSize` 를 쓴다.

**저작 초기값은 50° 다 — 45° 가 아니다.** 대각 방향의 내적이 `cos 45°` 와 수학적으로 같은 값이라
부동소수 비교가 동전 던지기가 되고, 이 프로젝트는 «비동기 토너먼트 양측 동일 시뮬»
(`AoeTargetCap` 주석)을 결정론 요건으로 두고 Android·iOS·에디터를 동시에 타깃한다.

### 연출 — 채널을 신설하지 않되 필드를 append 한다

⚠ **`AttackSystem` 은 `[BurstCompile] ISystem` 이라 managed `VfxSpawner` 를 부를 수 없다**
(리뷰 M3 — 초판이 «`VfxSpawner` 직접 호출» 이라고만 써서 이 모순을 놓쳤다).

기존 **`UnitAttackVisualEvent`** 에 필드를 append 한다(`EnemyKilledEvent.hasKillBurst` 선례).
이 채널이 맞는 이유: ① 브레스는 공격 사건과 **같은 프레임**이고 이 이벤트는 이미 그 프레임에
발행된다 ② 이미 `attacker`(Entity) + `targetWorld`(float3) 를 실어서 **브리지가 콘 방향을 만들 수
있다** ③ 소비자가 이미 브리지 드레인(`DrainUnitAttackVisualEvents`)이다.

`DcTriggerFiredEvent` 는 쓰지 않는다 — `host` 하나만 싣고 소비자가 머리 위 아이콘 펄스다.

프리팹·스케일·수명은 unit 7 이 저작한다.

## 완료 기준

- [ ] compile 통과
- [ ] unit 1 의 콘 단언이 그대로 통과 (이 단위는 술어를 바꾸지 않는다)
- [ ] EditMode 신규: bake 가 `coneHalfAngleDeg <= 0` 을 **loud warning**, **`>= 90` 을 loud 거절**
      한다(정의역)
- [ ] EditMode 신규: bake 가 각도 → `cosSq` 를 굽고, 저작 도(degree)와 런타임 값이 한 지점에서만
      변환된다
- [ ] EditMode 신규: 콘 적용 루프(private static 추출분)가 **진영 마스크·통행층·자기 제외** 세
      술어를 실제로 적용한다 — plain 배열 입력으로 단위 테스트
- [ ] PlayMode 신규 e2e: 드래곤 1기 + 부채꼴 안 방어유닛 2기 + **부채꼴 밖(옆·뒤)** 방어유닛 2기 →
      3번째 공격에서 **안쪽 2기만** HP 가 줄고 밖의 2기는 무피해
- [ ] **PlayMode 신규 (아군 오사 회귀 방지)**: 드래곤 콘 안에 **다른 적 유닛과 적 마음**을 두고
      발동시켜도 **둘 다 무피해**다. ★초판 스펙의 거짓 전제가 만들려던 버그를 이 단언이 막는다
- [ ] PlayMode: 대상이 없는 프레임에 발동해도 예외·오사가 없다
- [ ] PlayMode 무회귀 — baseline 대비 실패 집합 동일
- [ ] 신규 ECS 시스템 0 · **신규 이벤트 채널 0**(`UnitAttackVisualEvent` 필드 append) ·
      신규 컴포넌트 0 (슬롯 필드만)

---

**확인 2026-08-13** — 구현 `903936d0`, 연출 정정 `c3f29e3d`·`78e3931f`·`5909a090`·`ef78f937`,
소유권 이관 `b7750a4b`, 술어 커버리지 `60339fa7`. 사용자 Play 확인 완료.

세 술어(진영·통행층·자기 제외)는 `ConeBreathPredicateTests`(EditMode 4)가 `ApplyConeBreath`
를 리플렉션으로 직접 불러 plain 배열로 고정한다. **부정 단언마다 진공 방지 짝을 붙였다** —
「동료 적이 안 맞았다」는 같은 좌표를 마스크에 넣으면 맞는 테스트가, 「시전자가 안 맞았다」는
같은 호출에서 이웃이 맞는 단언이 각각 뒷받침한다(초판은 시전자가 EnemyUnit 이라 진영 마스크가
먼저 걸러, 자기 제외 술어를 지워도 통과하는 상태였다).

**미이행 2항목 — PlayMode e2e 「부채꼴 안 2기만 피해」·「동료·적 마음 무피해」.**
부채꼴 방향은 런타임 타게팅이 고른 대상으로 정해져 씬 안에 「안/밖」을 결정적으로 배치할 수
없다. 위 EditMode 단위 테스트가 같은 계약을 **더 강하게** 덮는다고 판단해 대체했다.
아군 오사는 사용자 Play 에서도 육안 확인됐다.
