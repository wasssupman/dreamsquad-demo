# 2. HeatAccrualSystem — 열기 누적/적용 [ECS]

## 목적

Part A 규칙을 지금 아키텍처에 바인딩한다. 모든 유닛(아군+적)에 주기적으로 열기를 쌓고, `HeatMath.Delta`(유닛 1)로 산출한 부호 있는 델타를 힐/피해 채널로 흘린다. `FatigueAccrualSystem` 구조 미러. `OnsenGimmickConfig` 부재 시 self-gate 로 완전 무동작.

## 변경 대상

- **신규**: `Assets/_Project/Scripts/Battle/Effects/HeatAccrual.cs` — `IComponentData { float elapsed; byte stacks; }` (Effects 소유 per-unit 타이머+카운터, `FatigueAccrual` 미러).
- **신규**: `Assets/_Project/Scripts/Battle/Effects/HeatAccrualSystem.cs` — ISystem, Burst, `[UpdateInGroup(BattleSimGroup)]` + `[UpdateBefore(DamageApplicationSystem)]`.

## 구현

**대상 = 유닛만**: `.WithAny<DefenderUnitTag, AttackUnitTag>()` — 하자드(Health 있지만 태그 없음)·투사체·픽업 배제. `.WithNone<DeadTag, PendingDeployment>()` — 죽은/배치대기 유닛 제외(DamageApplicationSystem 과 동일 제외 → 미드레인 버퍼 축적 방지).

**Pass 1 — lazy-attach** (신규 유닛): `WithNone<HeatAccrual>` 에 `HeatAccrual{0,0}` 부착 + `IncomingHeal` 버퍼 없으면 `AddBuffer<IncomingHeal>`(적은 IncomingHeal 미보유 → 여기서 부여). ECB playback 후 `_healLookup.Update` 재호출(이 프레임에서 append 하려면).

**Pass 2 — 주기 적용**: `elapsed += dt`; `while (elapsed >= heatInterval)`:
- `elapsed -= heatInterval`; `stacks++`(캡 `heatMaxStack`).
- `delta = HeatMath.Delta(stacks, flipThreshold, max, projectedHp, healPercent, lossPercent)`.
- `delta > 0` → `IncomingHeal.Add(delta)`; `delta < 0` → `IncomingDamage.Add(-delta, source=Null)`; `0` → 스킵.
- `projectedHp += delta` — **멀티-이터레이션(대형 dt) 시 HP1 바닥/오버힐 클램프가 프레임 내 누적에도 성립**하도록 로컬 투영값을 갱신(정상 단일-틱에선 currentHp 와 동일).

**self-gate/방어**: `RequireForUpdate<OnsenGimmickConfig>`; `heatInterval <= 0` 이면 early-return(무한 루프 방어).

**맥락 경계**: `Health`(Units) 읽기만. HP 변경은 `IncomingHeal`/`IncomingDamage`(canonical 채널) append 로만 — 직접 mutate 안 함. 손실 `source=Null`(미귀속 → 킬 크레딧 없음).

## 완료 기준

- Unity 재컴파일 CS 에러 0.
- (다음 유닛 3 Play 에서 실증) config 주입 매치에서 유닛들이 주기적으로 초록(회복)/빨강(손실) 숫자를 띄우고, 손실은 HP 1 에서 바닥.
- 적에게 `IncomingHeal` 이 lazy-add 되어 회복이 실제로 반영(초반 적 질겨짐).
- teardown 불필요: `HeatAccrual`·추가된 `IncomingHeal` 은 소속 엔티티(유닛) 파괴 시 함께 소멸.
