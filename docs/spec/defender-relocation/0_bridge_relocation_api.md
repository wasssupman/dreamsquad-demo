# 0 — Bridge Relocation API (토대)

## 목적

relocate 의 시뮬 토대를 Bridge 창구에 만든다. 연출 없이 **즉시형(비행 0초)으로도 완결 동작**해야 하며,
unit 2~3 은 이 API 를 호출만 한다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.Relocation.cs` (신규 partial — `BattleBridge.Dreamcatcher.cs` 관례)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (기존 `ActivateDeployedDefender` 에 on-place 스킵 플래그 — 최소 접촉)
- EditMode 테스트 (기존 배치 판정 테스트 위치에 병치)

## 구현

1. **`bool TryBeginDefenderRelocation(Vector2Int from, Vector2Int to, out Entity entity, out PlacementRejectReason reason)`**
   - 검증: `_defenderByTile` 에 from 존재 · 대상이 `PendingDeployment` 아님(배치/이동 진행 중 차단) ·
     `from != to` · to 는 기존 `SpatialPlacementCheck`(L4119) 재사용 · 페이즈 게이트는
     `CanPlaceDefenderAt` 과 동일 규칙(`_running || _placementAllowed`).
   - 실행(확정 프레임 원자, README 계약 5): `_occupiedTiles` from 제거+to 추가 ·
     `_defenderByTile` 이관 · `_em.SetComponentData(entity, new DefenderTile { cell = to })` ·
     `_em.AddComponent<PendingDeployment>(entity)` · `RecomputeSynergyFor(from)` ·
     `RefreshPlacementHighlightIfShown()`.
   - `LocalTransform` 은 여기서 건드리지 않는다(착지 프레임 — Finish 담당).
2. **`void FinishDefenderRelocation(Vector2Int to, Entity entity)`**
   - `LocalTransform` position = `GridToWorldCenter(to, spawnHeight)` (스폰 L4662 와 동일 y 규칙, rotation/scale 유지).
3. **`ActivateDeployedDefender(cell, entity, facing, bool triggerOnPlace = true)`**
   - relocate 는 `triggerOnPlace: false` + `facing: zero` 로 호출 → `TriggerDeploymentOnPlaceSkill`(L4259) 스킵,
     기존 `DeployedFacing` 유지(zero 면 AddComponentData 하지 않는 기존 가드 그대로).
   - `RecomputeSynergyFor(to)` 는 기존 활성화 경로가 이미 수행(L4263) — 중복 호출 금지.
4. **판정 순수 함수 분리**: relocate 가부(존재/진행중/동일셀/공간) 판정을 plain 입력 순수 static
   `RelocationCheck` 로 → EditMode 테스트 대상 (CLAUDE.md 제약 10 — 분기 다단계라 추출 요건 충족).
5. **디버그 진입점**: 에디터 전용 컨텍스트 메뉴 또는 기존 디버그 메뉴에 "relocate (from,to)" 1개 —
   unit 1~3 없이 이 unit 단독 검증용.

## 완료 기준

- [ ] 컴파일 클린 (`dotnet build` 또는 Unity 콘솔 에러 0)
- [ ] EditMode: `RelocationCheck` 테스트 통과 (유효 / from 비어있음 / 진행중 / 동일셀 / to 무효·점유)
- [ ] 에디터 Play: 디버그 진입점으로 Begin→Finish→Activate 즉시 연쇄 실행 시 유닛이 새 타일에서
      정상 전투 재개, 시너지 배율이 양쪽 셀에서 갱신, 원래 타일에 새 유닛 배치 가능
- [ ] 이동 직후 유닛 사망 시 점유 해제가 to 셀에서 일어남 (`DrainDefenderDeathEvents` 셀 일관성)
