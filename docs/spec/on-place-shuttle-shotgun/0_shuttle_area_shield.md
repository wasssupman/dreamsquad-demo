# unit 0 — 실드셔틀: 배치 순간 주변 아군 전원에게 보호막

## 목적

실드셔틀을 놓는 순간, **반경 2 안 아군 전원**(자신 제외)에게 실드를 한 겹 씌운다.
평소 실드는 4초를 기다려 **체력 낮은 2명만** 고르므로, 만피 아군까지 동시에 덮는 이 사건은
평소 능력으로 만들 수 없다. 적이 0마리여도 유효한 최초의 배치 스킬이다.

**목표는 시뮬 코드 0줄** — `GrantShield`(19) 페이로드 arm 이 공용 트리거 시스템에 이미 있고
(`BossPeriodicTriggerSystem`), 방어유닛 host 분기·아군 풀·부여 VFX까지 배선돼 있다.

## 변경 대상

- **신규** `Assets/_Project/Data/Abilities/Ability_AreaShield_ShieldShuttle.asset` (`UnitSkillAbility`)
- `Assets/_Project/Data/Defenders/Defender_ShieldShuttle.asset` — `abilities` 리스트에 위 SO 추가
  (기존 `Ability_Shield_ShieldShuttle`(ShieldCastAbility)는 **그대로 둔다** — 능력 조회가 타입별이라 공존한다)

## 구현

능력 SO 의 `mechanics` 한 줄:

| 필드 | 값 | 의미 |
|---|---|---|
| `trigger.kind` | `9` (OnPlace) | 배치 1회성. 재무장은 재배치 때만 |
| `payload.kind` | `19` (GrantShield) | |
| `payload.magnitude` | `250` | 실드량. **상시 캐스트(150)보다 두꺼워야 한다** — 아래 참조 |
| `payload.tileRange` | `2` | 반경(체비셰프). **>0 이어야 반경 확산 arm 을 탄다** |
| `payload.duration` | `0` | 실드에는 시간 만료가 없다. >0 이면 bake 가 "무시된다" 경고 |

저작 시 알아야 할 것:

- **bake 화이트리스트에 걸리지 않는다.** `GrantShield` 의 미배선 조합 거절은
  `HealthThreshold × tileRange>0` 과 `PeriodicTimer × tileRange<=0` 둘뿐이라 `OnPlace × tileRange>0`
  은 그대로 통과하고, arm 은 트리거가 아니라 payload 로 분기한다.
- **배치 실드와 상시 실드는 한 슬롯을 공유한다**(README 계약 4). 그래서 **캐스트와 같은 150 을 주면
  배치 순간이 화면에서 사건으로 안 읽힌다** — 어차피 4초마다 채워지는 두께라 "놓은 순간 뭔가 생겼다"가
  아니라 "원래 있던 게 그대로"로 보인다. 250 으로 시작해 «놓는 순간 평소보다 두꺼운 막» 을 만들고,
  깎인 뒤에는 캐스트가 150 을 바닥으로 받쳐 준다(같은 출처 max 갱신).
- **재배치하면 다시 터진다.** 배치 스킬 전부의 성질이고(재배치는 코스트를 낸다 —
  `defender-relocation` unit 8) 이 spec 이 새로 만드는 것이 아니다. 실드는 같은 출처 max 병합이라
  **깎인 만큼만 다시 차서** 반복 발동으로 두께가 쌓이지 않는다 — 계약 4 가 펌프를 자동으로 막는다.
- arm 이 이미 걸러 주는 것: 죽은 아군 · 실드 버퍼 없는 대상 · **이미 이 출처로 만충인 대상**
  (헛 VFX 방지) · host 자신.

## 완료 기준

- [ ] 배치 시 콘솔에 `[BattleBridge]` 경고 0 (특히 `onPlaceEffect 와 UnitSkillAbility 동시 선언`,
      `GrantShield 미배선 조합`, `duration 은 무시된다`)
- [ ] Play: 아군 2기 이상이 붙어 있는 자리에 실드셔틀 배치 → **주변 아군 머리 위 실드 게이지가
      동시에 차고**, 대상 **각자의 위치에서** 부여 VFX 가 한 번씩 뜬다(host 에서 한 번이 아니다)
- [ ] Play: 실드셔틀 **자신에게는** 배치 실드가 붙지 않는다(계약 5)
- [ ] Play: **배치 직후 실드가 평소 캐스트보다 눈에 띄게 두껍다**(검증 질문 ② — 배치 순간이 사건으로
      읽히는지. 여기서 구분이 안 되면 `magnitude` 를 더 올린다)
- [ ] Play: 실드가 깎인 뒤 4초 캐스트가 돌면 같은 슬롯이 150 을 바닥으로 다시 채운다(계약 4 확인)
- [ ] Play: **반경 안 아군이 0명인 자리**에 배치 → 실드도 VFX 도 없다(계약 5 의 귀결). 코스트만
      나가는 것이 현재 사양임을 확인하고 기록한다
- [ ] **배치 페이즈(전투 시작 전) 배치**: 실드는 sim 이라 즉시 붙지만 부여 VFX 는 드레인이
      `_running` 아래라 **전투 시작에 몰려 터진다**(확정 사실 — 큐가 Persistent 다).
      이 unit 에서 고치지 않는다. 정리는 unit 2 ③.
- [ ] **시뮬 코드 변경 0** (검증 질문 ①). bake 게이트 주석 갱신은 unit 2 ④ 로 분리한다
- [ ] ⚠ **문안 계층은 예외다** — `UnitKitSummary.OnPlaceRuleClause` 에 `GrantShield` 절을 배선해야
      한다. 「배치 규칙이 있는데 문안이 비면 실패」하는 전수 테스트(`UnitKitCatalogTests
      .RuleDrivenOnPlaceUnits_HaveAClause`)가 있어서, 에셋만 넣으면 그 테스트가 빨개진다.
      조용히 비는 것을 막으려고 만들어 둔 가드다
