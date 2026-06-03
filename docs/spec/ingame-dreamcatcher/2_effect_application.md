# 2 — 효과 적용 (현재·미래 유닛)

## 목적

카드 1장을 조건에 맞는 현재+미래 아군 유닛에 매치 영구로 적용한다.

## 변경 대상

- 수정 `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — registry + apply + EnqueueDmgTakenMul

## 구현

`EnqueueDmgTakenMul(Entity, float multiplier, float duration, int stackId)` 추가 (기존 EnqueueDamageMul 패턴, StatKind.DmgTakenMul, op=Multiplicative, stackId 파라미터화).
- 기존 3개 Enqueue 도 dreamcatcher 적용 시 고유 stackId 가 필요 → 내부 공통 `EnqueueStat(target, stat, op, mult, duration, stackId)` 추출 후 래핑(기존 시그니처 유지, stackId=0 기본).

Registry:
```csharp
private struct ActiveDcEffect { public CardTargetAxis axis; public StatKind stat; public float mult; public int stackId; }
private readonly List<ActiveDcEffect> _activeDcEffects = new();
private int _dcStackCounter = 1;        // 선택마다 +N (효과 수만큼)
private const float DcDuration = 1e9f;  // 매치 영구
```

`public void ApplyDreamcatcherCard(DreamcatcherCard card)`:
1. card.effects 각각 → (StatKind, mult) 매핑(Unit 1 표).
2. 각 효과에 고유 stackId = `_dcStackCounter++`.
3. `_activeDcEffects.Add(...)`.
4. 현재 `_defenderByTile` 순회 → `Matches(data, axis)` 인 entity 에 `EnqueueStat(entity, stat, Mul, mult, DcDuration, stackId)`.

`Matches(DefenderUnitData data, CardTargetAxis axis)`:
- ClassRanger → data.role==Ranger / ClassGuardian → data.role==Guardian / Cost1 → data.cost==1.

미래 유닛: `PlaceDefenderAs`(및 deployment 활성화 경로)에서 entity 생성 직후 `ApplyActiveDcEffectsTo(entity, unitData)` 호출 → `_activeDcEffects` 중 매칭 효과 모두 enqueue.

리셋: `BeginPlacement` 에서 `_activeDcEffects.Clear()` + `_dcStackCounter=1` (새 매치).

검증 선행: `ModifierStats.dmgTakenMul` 이 실제 피해 적용에 반영되는지 확인(IncomingDamage 처리 경로). 미반영 시 HP 카드 무효 → 그 경우 spec 노트에 기록하고 사용 지점 수정.

## 완료 기준

- compile + read_console clean.
- 런타임: ranger 2기 배치 후 ranger_atk_10 적용 → 두 entity ModifierStats.damageMul ≈ 1.1. 적용 후 ranger 1기 추가 배치 → 그 entity 도 ≈1.1(미래 적용).
- guardian_hp_15 적용 → guardian dmgTakenMul ≈ 0.87.
- 중복 적용(ranger_atk_10 ×2) → damageMul ≈ 1.21(스택).
- 비매칭 유닛(예: caster)엔 미적용.

> 완료 확인 2026-06-02 — PlayMode `DreamcatcherEffectTest` 통과: ranger AS 스택 1.21, guardian dmgTaken 0.87, caster 미적용, **미래 ranger 상속 1.21**. EditMode 294(292+2skip)·PlayMode 5/5 회귀 없음.
> 발견·수정(framework): `ModifierApplySystem` 의 bufferless 경로가 `ecb.AddBuffer` 를 써서, 한 프레임에 여러 StatModifier 가 갓 배치된(버퍼 없는) 유닛에 오면 ECB playback 시 마지막 AddBuffer 가 이전 슬롯을 덮어써 1개만 남던 버그. `em.AddBuffer`(즉시) 로 변경(MarkDirty 가 em 직접 쓰는 것과 동일 근거). 미래 유닛이 활성 효과 전부를 상속하려면 필수. StackModifier 동일 경로도 함께 수정. (`Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierApplySystem.cs`)
> 검증 메모: synergy 가 DamageMul 을 건드리므로 테스트는 AttackSpeed 축으로 스택을 검증(오염 회피). HP 카드는 max-HP 가 아니라 DmgTakenMul(받는 피해↓) 프록시.

## 전투 적용 검증 (2026-06-03)

ModifierStats 4채널이 실제 전투 시스템에 소비되는지 확인:
- ATK%(damageMul) → `AttackSystem.cs:255,261` 나가는 데미지 ×
- AS%(attackSpeedMul) → `AttackSystem.cs:337` 쿨다운 ÷ (공속↑)
- HP%(dmgTakenMul) → `DamageApplicationSystem.cs:58` 받는 데미지 ×
- → 출하된 PowerSurge/RapidFire/시너지와 동일 채널.

라이브 전투 측정(MCP): 디펜더 7기 희소 배치 → baseline 누적 유출 10(~0.33/s). 생존 4기로 줄어든 상태에서 ×8 damageMul 적용 → 추가 유출 0(전 웨이브 클리어). 디펜더가 줄었는데도 유출 0 = 데미지 버프가 처치력을 키웠다는 직접 증거. (배치-가장자리 시 0킬·유출10, 커버리지 시 유출0 으로 데미지 경로 자체도 확인.)

**발견·수정**: `moveSpeedMul` 은 `MovementSystem` 이 이동 엔티티(적)에만 읽음 → 고정 디펜더에 무효. `guardian_fortress` 의 이동속도 -50% 페널티가 no-op 이었음 → **공속 -50%** 로 교정(커밋 `2185f92`). 4채널 중 Move 는 디펜더 대상 카드에선 사용 안 함이 원칙.
