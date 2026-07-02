# 4 — 다중 stat 확장 + 글래스캐논/재생 타일

## 목적

`EffectTileData` 를 **다중 stat 효과 배열**로 확장하고(이후 모든 타일 설계의 토대), 이를 이용한 글래스캐논 타일과 Tier-0 재생 타일을 추가한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/EffectTileData.cs` — `stat/op/magnitude` 3필드 → `EffectTileEntry[] effects` (`[Serializable] struct { StatKind stat; CombineOp op; float magnitude; }`)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ApplyEffectTileIfAny` 가 entries 루프 enqueue + 맵빌드 종류 배정을 round-robin → **seed rng per-cell pick** (종류 수 > count 여도 전 종류 등장 가능하게)
- `Assets/_Project/Tests/EditMode/EffectTileModifierTests.cs` — 다중 stat 테스트 추가
- authoring: 기존 3 에셋 effects 배열로 재저작 + 신규 2종

## 신규 타일

| id | 효과 | 비고 |
|---|---|---|
| effect_tile_glass_cannon | DamageMul ×1.4 **+** DmgTakenMul ×1.4 | 리스크/리워드. 보라 |
| effect_tile_regen | RegenPerSec **+1.0 (Additive)** | base 0 이라 Multiplicative 무효 — Additive 필수. `DamageApplicationSystem.cs:91` 소비 확인됨. 연두 |

## 구현 노트

- 한 타일 내 같은 (stat,op) 중복 entry 는 merge-key 동일로 마지막만 남는다 — 금지(저작 규칙).
- stackId 는 entry 전체 `EffectTileStackId=2` 공유 — stat 이 다르면 슬롯 분리라 충돌 없음.
- 기존 단일 stat 에셋 3종은 entry 1개짜리 배열로 마이그레이션(re-author).

## 완료 기준

- compile 클린 + EditMode 전체(기존 6 + 다중 stat 1) PASS.
- Play: 글래스캐논 셀 배치 → damageMul=1.4 **그리고** dmgTakenMul=1.4 동시 반영. 재생 셀 배치 → regenPerSec>0 + HP 회복 관찰. 콘솔 클린.
