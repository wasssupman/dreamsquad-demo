# 3 — 실드 2패턴 (꿈의 장막 · 악몽의 가호)

## 목적

unit 2 가 깐 배관에 **발동**을 붙인다. 페이로드는 하나(`GrantShield`)인데 능력은 둘이다:

| 패턴 | 트리거 | `tileRange` | 하는 일 |
|---|---|---|---|
| ② 꿈의 장막 | `HealthThreshold` | **0 = 자기** | 경계마다 자기 실드 → 보스 처치가 늦어진다 |
| ③ 악몽의 가호 | `PeriodicTimer` | **>0 = 반경 아군** | 호위가 안 죽고 골에 눌러앉는다 → 전멸 지연 |

## 변경 대상

| 파일 | 내용 |
|---|---|
| `Battle/Combat/HealthThresholdSystem.cs` | `GrantShield` 분기 (self) |
| `Battle/Combat/BossPeriodicTriggerSystem.cs` | `GrantShield` 분기 (반경 아군, host 제외) |
| `Bridge/BattleBridge.cs` | **미배선 조합** loud 거절 |
| `Core/Dreamcatcher/DcApplicability.cs` | 신규 kind 분류 (아래 참조) |
| `Data/Enemies/Enemy_Boss_Mamemo.asset` | 슬롯 2개 |
| `Tests/PlayMode/BossShieldTest.cs` | **신규** — 증상 재현 2건 |

## 구현

### 배선은 트리거별로 갈라진다 — 그리고 그게 계약이다

`GrantShield` 는 kind 하나지만 **경계 arm 은 self 만, 주기 arm 은 반경만** 배선한다.
반대로 저작하면(경계×반경, 주기×self) 슬롯은 생기는데 **어느 arm 도 안 잡아 조용한 no-op** 이 된다.

그래서 bake 가 **미배선 조합을 loud 하게 거절**한다 — `dreamcatcher-trigger-gates` 의
"v1 배선 조합 외는 bake loud 거절" 선례. 미사용 라이브 경로를 만들지 않는다. 새 조합은 그걸
쓰는 능력이 생길 때 배선·테스트와 함께 연다.

> 왜 양쪽에 반경을 다 넣지 않았나: 지금 쓰는 능력이 없다. "나중을 위한" 경로는 제약 8 위반이고,
> 무엇보다 **미사용 분기는 테스트가 없어서 조용히 썩는다**(`SelfWarmupBuff` 유령 enum 전례).

### host 제외가 두 능력을 서로에게서 지킨다

`ShieldMath` 는 **`source` 를 병합 키**로 쓴다. 둘 다 마메모가 출처이므로 가호가 host 를
포함하면 **같은 슬롯**을 건드리고, 매 주기 `max` 로 장막의 잔량을 재충전한다
→ *경계에 생기는 벽* 이 *상시 실드* 로 붕괴한다.

`AuraPulse.SelectTargets` 는 host self-exclusion 을 하지 않는 것이 계약이므로(같은 셀 아군도
맞아야 한다), **arm 이 entity 비교로 제외**한다 — whip 오라와 같은 방식이다.

### 그 밖의 규칙

- **만충 스킵**: `ShieldMath.ValueFromSource(target, host) >= magnitude` 면 건너뛴다.
  `Merge` 가 `max` 라 어차피 no-op 인데 VFX 만 헛 터진다(가디언 unit 4 선례).
- **재부여 = 갱신**: 같은 출처 `max` 라 무한 누적이 아니라 **깎인 만큼만 다시 찬다.** 의도된 동작이다.
- **`DeadTag`·버퍼 부재 대상 스킵** — 시체에 실드를 주지 않는다.
- **1프레임 지연**(수용): 경계 arm 은 `[UpdateAfter(DamageApplicationSystem)]` 이라 append 가
  **다음 프레임** 드레인에서 슬롯이 된다. 경계를 관통한 그 히트는 이미 지나간 뒤다.
  60fps 기준 무시 가능하지만 "경계에서 즉시 무적" 으로 읽히지 않게 여기 적는다.

### `DcApplicability` 분류가 필수다

신규 payload kind 를 넣으면 `DcApplicabilityTests.EvaluateMechanic_IsTotalOverAllKindAndArchetypePairs`
가 **미분류 조합**으로 빨개진다(실제로 이 unit 에서 빨개졌다). `GrantShield` 는 self/반경 어느
쪽이든 host 의 공격 모델과 무관하므로 self·오라 계열에 넣는다 — 보스 전용인 `UltimateLeap` 이
거기 있는 것과 같은 이유다(어느 arm 이 잡느냐는 **authoring 사실**이지 적용성 판정이 아니다).

### 저작값 (초안 — 실플레이 튜닝 대상)

| 슬롯 | 값 |
|---|---|
| ② 꿈의 장막 | `HealthThreshold(fraction 0.34)` × `GrantShield(magnitude 350, tileRange 0)` → **66%·32% 2회** |
| | ⚠ **버스트 한 방이 두 경계를 관통하면 실드는 1회분만 나온다** — `HealthThresholdEval` 은 경계를 while 로 다 소비하고 fire 를 **1회**만 보고한다(그쪽의 의도된 설계). 실효 HP 가 1800 이 아니라 1450 이 되는 구간이 있다. 「순간 화력 집중」이 공식 대응책이므로 **의도에 부합**한다(리뷰 L3) |
| ③ 악몽의 가호 | `PeriodicTimer(2.5s)` × `GrantShield(magnitude 60, tileRange 4)` |

주기 **2.5s 와 자장가 3.5s 는 배수 관계가 아니다**(lcm 17.5s) — README 계약 10.
같은 프레임 동시 발동이 없어야 두 능력이 별개 사건으로 읽힌다.

경계 0.34 는 마메모 안에서 자장가(주기)·가호(주기)와 트리거 종류가 달라 충돌 자체가 없다.

## 완료 기준

- [x] 컴파일 에러 0
- [x] **PlayMode `BossShieldTest` 2/2**
      ① 꿈의 장막 — 경계 관통 후 자기 실드가 **저작된 양만큼** 붙는다
      ② 악몽의 가호 — 반경 안 호위가 받고 **마메모 자신은 못 받는다**(host 제외 = ②③ 슬롯 공유 방지).
      마메모를 만피로 유지해 ②를 재우지 않으므로 host 제외가 **고립 검증**된다
- [x] **PlayMode `EnemyShieldTest`·`BossLullabyTest` 3/3** — unit 1·2 무회귀
- [x] 전체 EditMode **2163 중 2160 통과 · 실패 0** · 스킵 3(전부 기존 `[Ignore]`)
- [x] **Play 육안(사용자) 확인 완료 2026-08-11** — 보스/호위 실드 게이지·흡수 동작 확인
