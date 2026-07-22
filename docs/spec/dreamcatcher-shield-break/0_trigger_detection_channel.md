# 0. OnShieldBreak 트리거 + 탐지 + 이벤트 채널 [ECS]

## 목적

실드가 **피격으로 완전 소진**되는 순간을 감지하는 드림캐쳐 트리거의 토대. 정의 계층에 트리거 kind 추가 + Units→Bridge 이벤트 채널 + `DamageApplicationSystem` 탐지/emit. 페이로드 실행(유닛 2) 전에도 로그로 독립 검증 가능.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcTriggerKind` 에 `OnShieldBreak` **append**(마지막).
- **신규** `Assets/_Project/Scripts/Battle/Units/ShieldBreakEvent.cs` — `ShieldBreakEvent`(host·position·payload·magnitude·tileRange·duration·aoeDataIndex) + `ShieldBreakEventsSingleton { NativeQueue<...> }`.
- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs` — 실드 Absorb 전후 Sum 비교로 피격 파열 감지 + host `DcTriggerSlot`(RO) OnShieldBreak 슬롯 읽어 emit.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 채널 lifecycle(field/create/dispose/teardown) + `DrainShieldBreakEvents`(유닛 0 은 로그 stub, 유닛 2 가 실행 채움) + 드레인 호출.

## 구현

1. **트리거 enum**: `DcTriggerKind { …, OnKill, OnShieldBreak }`. append-only(직렬화 안전). bake 는 `trigger = m.trigger.kind` 제네릭 복사(BattleBridge.Dreamcatcher:388)라 enum 추가만으로 슬롯에 실림 — 별도 bake 분기 불필요.
2. **탐지**(`DamageApplicationSystem`): Absorb 직전 `preSum = ShieldMath.Sum(slots)`, 직후 `Sum(slots)`. `preSum > 0 && post <= 0` → `shieldBrokeByHit`. **Absorb 경로 전용이라 시간만료는 구조적 배제**.
3. **emit**: `shieldBrokeByHit` 시 host `_dcTriggerSlotLookup`(기존, RO) 순회 → `trigger == OnShieldBreak` 슬롯마다 `ShieldBreakEvent` enqueue(host·`_transformLookup` position·payload·magnitude·tileRange·duration·aoeDataIndex=SelfTileAoe면 projectileDataIndex 아니면 -1). OnKill emit 선례 동형. 사망 프레임에도 발동(death 분기와 독립).
4. **채널**: `_defenderDeathQueue` 6개 사이트 미러 — field(282)/DestroyEntitiesByType(523)/Dispose(558)/create(1240-43)/drain call(2149). `DrainShieldBreakEvents` 는 유닛 0 에선 `TryDequeue` → `Debug.Log`(검증용), 유닛 2 가 payload 실행으로 교체.

## 완료 기준

- Unity 재컴파일 CS 에러 0.
- (유닛 0 자체 검증) OnShieldBreak+SelfTileAoe 임시 카드로, 실드 부여 유닛이 **피격으로 실드가 깨질 때** 콘솔에 `[ShieldBreak]` 로그 1회. 부분 흡수/무피격에는 미발동. (정식 카드·실행은 유닛 1~3.)
- teardown 대칭(재진입 orphan/leak 없음).
