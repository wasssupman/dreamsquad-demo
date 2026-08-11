# 2 — 적 실드 개통 (`GrantShield` 페이로드)

## 목적

**적이 실드를 받을 수 있게 만든다.** 이 unit 은 배관만 깔고 능력은 안 붙인다 — 발동 arm 은 unit 3 이다.

쪼갠 이유는 컴파일 의존이다: enum·bake·버퍼가 없으면 arm 이 참조할 대상이 없다.
그리고 **버퍼 부착과 게이지를 같은 커밋에 둬야** "실드를 줬는데 화면에 안 보인다" 를 눈으로 가릴 수 있다.

## 변경 대상

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` | `DcPayloadKind.GrantShield = 19` append |
| `Assets/_Project/Scripts/Bridge/BattleBridge.cs` | 적 스폰에 버퍼 쌍 · bake 가드 2건 · `ShieldRatioOf` 헬퍼 |
| `Assets/_Project/Tests/PlayMode/EnemyShieldTest.cs` | **신규** — 배선 재현 |

## 구현

### 1. 페이로드 정의 (append-only)

`GrantShield = 19`. 필드 재사용:

- `magnitude` = 실드량
- `tileRange` **0 = 자신만 · >0 = 반경 내 같은 진영 유닛(host 제외)**
- `duration` = **쓰지 않는다**

`tileRange` 하나로 패턴 2·3 을 겸하는 것이 계약이다. host 를 포함하면 안 되는 이유는
`ShieldMath` 가 `source` 를 병합 키로 쓰기 때문 — 「경계마다 자기 실드」와 「주기마다 아군 실드」가
같은 host 에서 나오면 **한 슬롯을 공유**하고, 후자가 전자를 상시 재충전해 *경계에 생기는 벽* 이
*상시 실드* 로 붕괴한다.

### 2. 적 스폰에 버퍼 **쌍**

`SpawnUnit` 의 기존 사전 부착 자리(`IncomingDamage`·`CcEffect`·`DotEffect`) 옆에
`ShieldSlot` + `IncomingShield` 를 추가한다.

> **반드시 쌍이다.** `IncomingShield` 드레인이 `ShieldSlot` **존재로 게이팅**돼 있어
> (`DamageApplicationSystem`) 한쪽만 붙이면 부여가 영영 드레인되지 않고 버퍼가 **무한 성장**한다.

**보스만이 아니라 적 전원**이다 — 악몽의 가호(unit 3)의 수혜자가 호위 잡몹이고, 조건부 부착은
"누가 받을 수 있나" 를 스폰 시점에 못 박아 arm 의 대상 선정을 왜곡한다.
거점은 이 경로를 안 타므로 `battle-structures` 계약 8(거점은 실드 버퍼를 갖지 않는다)은 그대로다.

### 3. 오버헤드 실드 게이지 — 적 분기를 연다

초판 스펙은 이걸 "공짜" 라고 적었는데 **거짓이었다**(투트랙 리뷰가 잡음). 하위 레이어
(`UnitOverheadUiLayer` · `UnitOverheadView` · enemy skin 의 shield 색)는 이미 진영 무관인데,
**적 분기의 `shieldRatio` 인자가 리터럴 `0f`** 라 실드를 줘도 안 그려졌다.

방어유닛·순찰병 두 분기에 **복붙**돼 있던 폴링 3줄을 `ShieldRatioOf(entity, in Health)` 로 모으고
적 분기를 편입했다 — 세 호출처가 한 함수를 쓴다.

### 4. bake 가드 2건

| 저작 실수 | 처리 |
|---|---|
| `magnitude <= 0` | **loud 거절 + skip** — `ShieldMath.Merge` 가 `amount<=0` 을 그냥 return 해서 매 발동 조용한 no-op 이 된다 |
| `duration > 0` | **경고만** — 실드에 TTL 이 없어 무시된다. "몇 초 뒤 사라진다" 고 읽히는 저작을 저작 시점에 끊는다. 값이 무해하므로 skip 은 안 한다 |

### 5. `ShieldCastSystem` 을 손대지 않는다

그건 가디언 방어유닛 전용 생산자이고 caster·후보 양쪽에 `DefenderUnitTag` 하드 게이트가 있다.
이 spec 은 **`IncomingShield` append 라는 아래층**을 쓴다 — 병합(같은 출처 max · 교차 출처 합산)과
흡수는 `DamageApplicationSystem` 이 이미 진영 중립으로 한다.

## 완료 기준

- [x] 컴파일 에러 0 · `DcPayloadKind.GrantShield == 19`
- [x] **PlayMode `EnemyShieldTest`** — 실스폰한 적에 대해:
      ① 버퍼가 **쌍으로** 붙는다 ② 부여 버퍼가 드레인된다(무한 성장 아님) ③ 실드 50 적립
      ④ **`ShieldRatioOf` 헬퍼가 적에게도 0 아닌 값을 낸다** ⑤ 30 피해 = 완전 흡수, 체력 불변
      ⑥ 추가 30 피해 = 실드 소진 + 관통분 10 만 체력에서
      > ⚠ ④ 는 **호출처 회귀를 못 잡는다**(리뷰 M4). 이 unit 이 고친 결함은 「적 분기가
      > `SetUnit(..., 0f, ...)` 로 넘긴다」는 **호출처**인데, 테스트는 private 헬퍼를 리플렉션으로
      > 직접 부른다. 누가 그 인자를 다시 `0f` 로 되돌려도 초록이다. 진짜 가드는 **육안**이고
      > (unit 4 완료 기준), 헬퍼 추출이 세 호출처를 한 함수로 모은 것이 구조적 방어다.
- [x] **적 `OnShieldBreak` 는 아무것도 하지 않는다 — EditMode 로 고정**(README 「알려진 주의점」이
      약속했던 것, 리뷰 M3). 화이트리스트를 브리지 인라인에서 순수 술어
      `DcTrigger.EnemyTriggerArmed` 로 빼고 `DcTriggerTests` 2건이 고정한다(총체성 포함 —
      새 트리거 kind 를 추가하면 분류를 강제당한다).
      **왜 지금 필요해졌나**: 이 unit 이 적 전원에게 `ShieldSlot` 을 달면서
      `DamageApplicationSystem` 의 파열 감지(Sum>0→0)가 적에서도 참이 되기 시작했다. 지금
      `OnShieldBreak` 가 적에 안 붙는 유일한 이유가 그 화이트리스트이고, 완화하면 브리지의
      파열 드레인(대상 풀 `AttackUnitTag` 하드코딩)이 돌아 **보스의 파열 폭발이 자기 진영을 때린다.**
- [x] EditMode 전량 무회귀 — 2163 중 2160 통과 · 실패 0 · 스킵 3(전부 기존 `[Ignore]`).
      방어유닛·순찰병 게이지가 헬퍼 추출로 안 바뀐다
- [x] **신규 payload kind 는 `DcApplicability` 분류가 필수다** — 안 하면
      `DcApplicabilityTests.EvaluateMechanic_IsTotalOverAllKindAndArchetypePairs` 가
      "미분류 조합" 으로 빨개진다(실제로 빨개졌고 unit 3 에서 분류했다)
- [x] 콘솔 경고 0 — 다회 Play/PlayMode 세션에서 `GrantShield` 가드 경고 관측 없음
      (마메모 외 `GrantShield` 저작이 없어 침묵이 정상)

> **이 unit 에는 발동이 없다.** 적에게 실드를 주는 주체가 아직 없으므로 게임 화면은 무변화가 정상이다.
> 실드를 실제로 두르는 것은 unit 3(꿈의 장막 · 악몽의 가호)이다.
