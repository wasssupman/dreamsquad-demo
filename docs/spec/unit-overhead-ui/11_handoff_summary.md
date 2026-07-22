# 11. Handoff Summary — 스택 이상효과 아이콘 행 (확장)

> unit-overhead-ui 확장(unit 6~10). 최신 계약은 README 확장 섹션 + 번호 문서 우선.

## Commit

- `3142d377` unit 6 — 계약(OverheadStackKind/Entry·StackIconRegistry·스타일·StackRowBottom+테스트)
- `0b06f69b` unit 7 — `UnitOverheadView.ShowStacks`(아이콘 + TMP 카운트 배지)
- (이 커밋) unit 8 gather + unit 9 아이콘(Codex) + unit 10 registry/씬 배선

## Implemented

- 체력바 → 드림캐쳐 행 → **스택 이상효과 아이콘 행**(최상단). 아이콘=종류(피로도/열기), 배지=현재 스택 수.
- 소스=듀얼(A): `StackModifierSlot`(피로도) + `HeatAccrual`(열기), BattleBridge RO gather(재사용 버퍼).
- 아이콘=registry 구동(미매핑/미할당=생략, 아트↔코드 디커플링). 적/아군 공통(열기 전 유닛).
- 아이콘 sprite = Codex 생성(256² Sprite Single, mipmap off, 투명).

## Key Files

- `Scripts/Data/OverheadStackKind.cs`(+Entry) · `StackIconRegistry.cs` · `UnitOverheadUiStyle.cs`(스택 파라미터)
- `Scripts/Presentation/UnitOverheadView.cs`(ShowStacks) · `UnitOverheadLayout.cs`(StackRowBottom) · `UnitOverheadUiLayer.cs`(stackIcons+SetUnit)
- `Scripts/Bridge/BattleBridge.cs`(`GatherOverheadStacks`/`TryMapOverheadStackKind`)
- `Data/StackIconRegistry.asset` · `Art/StackIcons/icon_stack_{fatigue,heat}.png` · `Scenes/BattleScene.unity`(배선)
- `Tests/EditMode/UnitOverheadLayoutTests.cs`(StackRowBottom)

## Verified

- 컴파일 CS 에러 0(전 유닛). `UnitOverheadLayoutTests` 11/11 green.
- BattleScene diff = stackIcons 한 줄로 격리(타 세션 CostDisplay/tooltip 재직렬화 드리프트 HEAD 복원).

## Notes (되돌리면 안 되는 의도)

- Presentation 은 Battle 타입 미참조 — gather(Bridge)가 Battle.StackKind/HeatAccrual→OverheadStackKind 번역(계약).
- 열기는 전용 `HeatAccrual`(슬롯 아님)이라 gather 가 듀얼소스(A). Onsen 코드 불변.
- StatusFx 미사용(구조 상이, 사용자 결정) — 이 행은 오버헤드 UI 계층에서만.
- 재사용 gather 버퍼는 동프레임 동기 소비 전제(SetUnit→Show 즉시 슬롯 복사).

## Follow-up

- ✅ **Play 육안 검증 통과**(2026-07-22, 사용자) — 아이콘+배지 표시 확인.
- 표시 대상 확장(Bleed/Fire… registry 아이콘 추가 시 자동), 배지 스타일 튜닝.
- 스택행이 적에도 뜨는 클러터가 과하면 정책 재고(현재 적 표시 O).
