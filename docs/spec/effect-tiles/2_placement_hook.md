# 2 — 배치 훅 + 검증

## 목적

효과 타일 셀에 방어 유닛 배치 시 해당 효과를 기존 modifier 파이프라인으로 정확히 1회 부여한다. 두 배치 경로(즉시/드래그) 모두.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Tests/EditMode/EffectTileModifierTests.cs` (신규)

## 구현

- 상수 `EffectTileStackId = 2` (규약: on-place=0 · 시너지=1 · 드림캐쳐=100+).
- `ApplyEffectTileIfAny(Vector2Int cell, Entity entity)`: `_effectTilesByCell` lookup → 있으면 기존 `EnqueueStatMul(entity, data.stat, data.magnitude, float.PositiveInfinity, EffectTileStackId)` 헬퍼(`:2362`) 재사용. **`source = 배치 유닛(target)`, `Entity.Null` 금지**(M1 — merge-key 규약 + revocation 대비).
- 호출 지점 2곳: `TriggerOnPlaceAndSynergy` · `TriggerDeploymentOnPlaceSkill` — 둘 다 `_onPlaceTriggeredEntities` 가드 뒤(exactly-once, 리뷰 확인).
- `AddEffectTile` 즉시 적용 완성: 셀 점유 시(`_defenderByTile`) `ApplyEffectTileIfAny` 호출(순서 무관 불변식).

## 완료 기준

- compile 클린.
- EditMode 통합(`EffectIntegrationTests` 패턴): World + `ModifierApplySystem` + aggregate 로 배치 시뮬 후 `ModifierStats.damageMul == 1.25f` 단언 — stackId=2 가 on-place(0)/시너지(1)와 무충돌로 스택되는지 포함.
- Play: 효과 타일 셀 배치 → ModifierStats 반영(execute_code), 일반 셀 = 무효과, 드래그 경로 동일, 기존 on-place/시너지 회귀 없음. 콘솔 클린.
