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
