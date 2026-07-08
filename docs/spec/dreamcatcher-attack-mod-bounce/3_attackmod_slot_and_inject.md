# 3 — DcAttackModSlot + 부착 확장 + 스폰 주입

## 목적

카드 → 유닛 부착 → 기본 공격 request 주입 경로를 잇는다. unit-trigger 의 부착 인프라(가드·instanceId·베이크) 재사용.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Battle/Combat/DcAttackModSlot.cs`
- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ApplyDreamcatcherCardToUnit` 확장
- 수정: `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — Homing request 생성 지점 주입

## 구현

`DcAttackModSlot : IBufferElementData` (Combat): `{ int instanceId; DcAttackModKind kind; int count; int tileRange; float damageMul; }`

BattleBridge `ApplyDreamcatcherCardToUnit`:

- 기존 mechanics 루프와 나란히 `attackMods` 루프 추가. 기존 가드(binding=Unit, defender, ECS ready) 공유. instanceId 는 같은 `_dcInstanceCounter` 에서 발급.
- 개조형 가드: `kind == None` / `count <= 0` / `damageMul <= 0` → warn+skip. **`ProjectileRef` 없는(근접) 유닛 → warn+skip** (계약 4 호환표 — 카드 전체 거절이 아니라 해당 mod 만 skip; mechanics 는 근접에도 유효할 수 있으므로).
- `mechanics 도 attackMods 도 전부 skip` 이면 false 반환 (기존 `attached > 0` 집계에 합산).

AttackSystem RESOLVE — 기존 Homing `ProjectileSpawnRequest` 생성 직전에 슬롯 집계:

```csharp
// DcAttackModSlot 집계 (defender + 슬롯 보유 시): count 합산, damageMul 곱, tileRange max (계약 5)
```

- 집계 결과를 request 의 bounce 3필드에 기입. **Ballistic arm 에는 주입하지 않는다** (계약 4). dc 트리거 캐리어 투사체에도 주입하지 않음 (기본 공격 개조라는 정의 — 콕콕 바늘 화살은 튕기지 않음).
- BufferLookup<DcAttackModSlot> RO 추가.

## 완료 기준

- [ ] 컴파일 + 무회귀
- [ ] execute_code: 부착 → 슬롯 필드 확인, 근접 유닛 부착 시 warn+skip, 같은 카드 2장 = count 합산 동작 (request 필드 관측)
