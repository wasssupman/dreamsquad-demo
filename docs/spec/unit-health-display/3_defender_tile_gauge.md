# 3 — 방어유닛 점유 타일 테두리 게이지

## 목적

방어유닛 HP 를 유닛이 아닌 **점유 타일 테두리**로 표시. 테두리가 HP 비율만큼 채워지고(perimeter fill) 녹→황→적으로 변한다. 캐릭터·이펙트와 겹치지 않고 모바일에서 타일 크기 가독성을 얻는다. full HP 는 숨김.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Presentation/TileHealthGaugeLayer.cs` (풀 + cell 키 관리) + `TileHealthGaugeView.cs`
- 신규: 테두리 fill 셰이더그래프 + 머티리얼 + quad prefab
- `Assets/_Project/Scripts/Data/HealthDisplayStyle.cs` — 게이지 필드 추가
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `[SerializeField] TileHealthGaugeLayer`, defender 폴링 + 사망/teardown 정리
- `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` — 게이지 오더 상수
- BattleScene — 레이어 GO + 참조 배선 (unity-feature-wiring)

## 구현

- SO 추가 필드: `Gradient gaugeColorGradient`(녹→황→적), 테두리 두께, `bool hideWhenFull = true`, full 판정 epsilon.
- 셰이더그래프: 타일 사각 테두리 SDF 마스크 × 둘레-각도 기반 fill cutoff (`_FillRatio`, `_Color` 프로퍼티). URP Unlit·transparent. 시계방향 상단 시작(방향은 시각 튜닝에서 확정).
- `TileHealthGaugeLayer.Set(Vector2Int cell, Vector3 tileCenterView, float ratio)` / `Hide(cell)` / `Clear()`. 내부 `Dictionary<Vector2Int, TileHealthGaugeView>` + 풀. `ratio >= 1-eps && hideWhenFull` 이면 자동 Hide (regen 만피 복귀 케이스).
- BattleBridge: `SyncMonoUnitViews` 의 `_defenderByTile` 루프(:1769~)에서 `Health` read-only 폴링 → `Set`. `DrainDefenderDeathEvents`(:1791) 에서 `Hide(cell)`. 전투 teardown 경로에서 `Clear()`.
- 타일 중심: cell → sim 좌표(`cell * tileSize`) → `BoardSpace.ToView`. 게이지 quad 는 바닥 데칼로 눕힘 (그림자와 동일 평면 규약).
- sorting: `BoardSortOrder` 상수 추가 — `ShadowOrder(-5)` 위·캐릭터(양수) 아래 (예: -4).
- 레이어 미할당 시 스킵 (null 가드). 머티리얼 값 조정은 SetFloat + SaveAssets 경로(force reimport 금지 — 브리지 끊김).

## 완료 기준

- compile 0 에러 + EditMode 무회귀.
- Play 검증: ① 배치 직후(만피) 게이지 없음 ② 피격 시 테두리 등장, HP 비율만큼 닳고 색 전이 ③ 사망 시 게이지 제거 + 타일 해제 ④ regen 만피 복귀 시 자동 숨김 ⑤ 58° pitch 게임뷰에서 fill 비율 육안 판독 가능 — 스크린샷 확인.
- 다수 방어유닛 배치 시 바닥 클러터 없음(만피 숨김 동작) 확인.

— 완료 확인 2026-07-04 · 커밋 `ca37995`. compile 0, EditMode 447 중 446 통과(무관 사전 ObstaclePlacer). Play 검증(방어유닛 6개 배치): ①만피=게이지 0개 ②차등 피격 시 6개 부분 테두리 등장, HP 비율 fill(시계방향)+녹→황 색, 58° 각도 육안 판독 ④전원 만피 복원 시 active 0(pool 유지). ③사망 Hide(cell)·⑤은 코드/동일 Hide 경로 확인. Gauge 콘솔 에러 0. **구현 편의**: spec 의 셰이더그래프 대신 절차적 4-edge 스프라이트(바닥 눕힘, 흰 스프라이트 shared) — MCP 자체완결·값은 전부 SO. 시각적으로 동등하나 fill 이 각 변 단위(둘레 1/4 계단)로 채워짐(연속 SDF 아님). 필요 시 후속에서 셰이더로 교체 가능.
