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
3. **활성화 = 기존 `ActivateDeployedDefender(cell, entity)` 그대로 재사용 (플래그 불필요 — 구현서 정정)**
   - `_onPlaceTriggeredEntities` 가드 셋이 이미 on-place·effect-tile 을 **entity 당 exactly-once** 로
     만들므로(양 배치 경로 모두 등록 확인), relocate 재활성화는 자동으로 재발화하지 않는다.
     BattleBridge.cs 시그니처 변경 0 — 공유 파일 접촉 최소화.
   - 2-인자 오버로드 사용 → 기존 `DeployedFacing` 컴포넌트 유지(계약 3).
   - `RecomputeSynergyFor(to)` 는 기존 활성화 경로가 이미 수행(L4263) — 중복 호출 금지.
4. **판정 순수 함수 분리**: relocate 가부(존재/진행중/동일셀/공간) 판정을 plain 입력 순수 static
   `RelocationCheck` 로 → EditMode 테스트 대상 (CLAUDE.md 제약 10 — 분기 다단계라 추출 요건 충족).
5. **검증 진입점**: `RelocationDebugMenu`(에디터 메뉴, 사람용) + **PlayMode 스모크 테스트**
   `RelocationSmokeTest`(자동 검증용 — 원격/unfocused 에디터에서 Play 중 메뉴 실행이 불가한 환경 제약).

## 완료 기준

- [x] 컴파일 클린 (Unity 콘솔 에러 0)
- [x] EditMode: `RelocationCheck` 테스트 통과 (유효 / from 비어있음 / 진행중 / 동일셀 / to 무효·점유·경계밖) — 7/7
- [x] Play 검증(PlayMode `RelocationSmokeTest` 2/2): Begin→Finish→Activate 연쇄에서 점유·바인딩·
      `DefenderTile` 스왑, `PendingDeployment` 부착/해제, busy 재이동 `SourceBusy` 거부,
      `LocalTransform` 타이밍(Begin 불변·Finish 이동), 시너지 양쪽 재계산(1.1↔1.0), 원 타일 재배치 성공
- [x] 이동 직후 유닛 사망 시 점유 해제가 to 셀에서 일어남 — 코드 경로 확인(`DefenderTile`=to 를
      `UnitLifecycleSystem` 이 death 이벤트에 적재, drain 이 그 셀로 해제). 실전 발화는 unit 3 Play 에서 재확인

2026-07-23 자동 검증 통과 (EditMode 7 + PlayMode 2, 에디터 실행). **관측 노트**: 라이브 BattleScene 은
`enableAdjacencySynergy: 0`(시너지 비활성) — relocate 의 재계산 호출은 플래그 무관하게 유지되며,
스모크 테스트는 플래그를 테스트 스코프에서만 켜서 계약을 검증한다.
