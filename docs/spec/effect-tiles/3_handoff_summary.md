# 3 — handoff summary (effect-tiles units 0~2)

## Commit
- unit 0 `f07f1a6` — EffectTileData SO + EffectTilePlacer.SelectCells + EditMode 4종.
- unit 1 `b1c191d` — 런타임 효과 타일맵(−15) + AddEffectTile + 맵빌드 배선 + 에셋/테마.
- unit 2 `c8ee5c2` — 배치 훅(ApplyEffectTileIfAny, stackId=2) + EditMode 2종.
- unit 4 `41d4361` — EffectTileData.effects[] 다중 stat + 글래스캐논/재생 + 종류 배정 rng.
- 비주얼 `a987c82` — 구분 심볼 글리프 5종(절차 생성, 형태+색). `faf2f00` — 효과 타일맵 펄스 발광 셰이더(Wassup/EffectTilePulse, theme.effectTileMaterial).

## Implemented
- 맵 빌드 시 Place 셀 seed 결정론 선정(`EffectTilePlacer.SelectCells`, `seed^0x51F15EED|1u`) → `AddEffectTile` 로 dict 등록+페인트.
- 효과 타일맵은 grid 하위 런타임 생성(sorting −15) — `overlayTilemap` 의 hover/reject SetTile/null 과 분리(hover 왕복 생존 Play 실증).
- 배치 두 경로(즉시 `TriggerOnPlaceAndSynergy`/드래그 `TriggerDeploymentOnPlaceSkill`) 가드 뒤 exactly-once 로 `StatModifierApplyEvents` enqueue — source=배치유닛, duration=∞, `EffectTileStackId=2`(on-place 0·시너지 1·드림캐쳐 100+ 규약).
- `AddEffectTile` 점유 셀 즉시 적용(순서 무관 불변식) — 현재는 후속 런타임 생성 루트 전용, merge refresh 멱등.
- 효과 3종(공격력 ×1.25/공속 ×1.2/받는피해 ×1.25) `Data/EffectTiles/` + forest 테마 배선(`effectTiles[]`/`effectTileCount` 는 **MapThemeData** — 씬 수정 회피).

## Key Files
- `Assets/_Project/Scripts/Data/{EffectTileData,EffectTilePlacer}.cs`
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` (SetEffectTile/EnsureEffectTilemap)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (AddEffectTile/ApplyEffectTileIfAny/맵빌드 배선/훅 2곳)
- `Assets/_Project/Tests/EditMode/{EffectTilePlacerTests,EffectTileModifierTests}.cs`
- `Assets/_Project/Data/EffectTiles/` · `Assets/_Project/Map/Theme/forest/forest.asset`

## Verified
- EditMode 6/6 (결정론/필터/상한 + stackId 3중 스택 ×1.65/멱등+영구).
- Play: 효과 셀 배치 → slot[×1.25,sid=2,∞] + ModifierStats.damageMul=1.250 / 대조군 무효과 / 표시 데미지 11→14(13.75 반올림) 체감. 콘솔 클린.
- 2-렌즈 스펙 리뷰 GO-WITH-CHANGES(M1 stackId 규약·M2 unit 분할) 반영 완료.

## Notes (되돌리면 안 됨)
- 효과 타일을 `overlayTilemap` 에 넣지 말 것(hover 가 지움). `EnqueueStatMul` 은 op 파라미터가 없어 `EffectTileData.op` 존중 위해 직접 enqueue 유지.
- placement 는 draft 후 `BeginPlacement` 이후에만 가능(`NotRunningOrPlacementClosed`) — Play 검증 시 참고.
- sim(GeneratedMap/ECS) 무변경 — 효과 타일 상태는 bridge dict 뿐.

## Follow-up
- unit 4(진행): 다중 stat 확장 + 글래스캐논/재생 타일. 그 외 README 후속 후보 참조(적 경로 타일·사거리 StatKind·환급/직군/미스터리 타일·revocation 등).
