# 1 — 카메라 틸트 + framing 보정

## 목적

ortho 카메라를 X축으로 θ 기울여 보드를 "비스듬히 누운 지면"으로 만든다. 현재 framing
(`ApplyTilemapCameraPreset`)은 **틸트=0 가정**(`b.extents.y`, position offset `(0,0,−20)`)이라
틸트하면 보드가 화면 밖/치우침 → framing 을 틸트 보정으로 교체한다.

## 변경 대상

- 수정: `Assets/_Project/Scripts/Data/BoardCameraPreset.cs` (이미 `rotationEuler` 존재 — 값만 활성)
- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` `ApplyTilemapCameraPreset` (라인 ~2620)
- 수정(에셋): `Assets/_Project/Data/Camera/CameraPreset_TilemapRect.asset`, `CameraPreset_TilemapIso.asset`
- 수정: `Assets/_Project/Scripts/Core/TilemapMapView.cs` (보드 중앙을 월드 (0,0) 으로 — `CenterBoardAtWorldOrigin`)

## 구현

프리셋 에셋: `rotationEuler = (θ, 0, 0)`, θ 시작값 **50**. (Rect/Iso 둘 다. Iso 는 추후 별도 튜닝 가능.)

`ApplyTilemapCameraPreset` framing 교체 (틸트 무관하게 보드를 화면에 꽉/중앙):

1. `cam.transform.rotation = Quaternion.Euler(preset.rotationEuler)` **먼저** 적용.
2. 보드 중심 `center` 산출(기존 bounds/gridSize 경로 유지).
3. **카메라 위치 = `center − cam.transform.forward * dist`** (ortho 라 dist 는 클립 안이면 무방, `positionOffset.magnitude` 또는 고정 20 사용). 기존 `center + (0,0,−20)` 대신.
4. **orthographicSize 틸트 보정**: 보드 4코너(또는 bounds 8점)를 **카메라 view 공간으로 투영**(`cam.transform.InverseTransformPoint`)해 `maxAbsY`, `maxAbsX/aspect` 의 최댓값 + padding.
   - 이렇게 하면 틸트로 세로 압축된 실제 화면 extent 를 정확히 반영. iso 마름모·틸트 동시 처리.
5. 나머지(near/far/clear/transparencySort)는 기존 유지.

보드 중앙 정렬 (`TilemapMapView.CenterBoardAtWorldOrigin`, Initialize 페인트 직후):
- Tilemap 모드는 sim origin=0, 월드 배치는 `grid.transform` 권위 → grid 를 `−보드중심.xy` 로 이동해 보드 중앙을 (0,0) 에.
- 보드 양 끝 셀 `CellToWorld` 중점으로 중심 산출(rect/iso 공통). 맵 크기 달라져도 재계산·idempotent. sim 무영향(ToView/ToSim 모두 grid live 기준).

> 핵심: ortho + 코너 투영 방식이라 θ 가 바뀌어도 framing 이 자동 추종. 하드코딩된 `−20`/`extents.y` 의존 제거.

## 완료 기준

- compile 통과.
- Tilemap Play: 보드 전체가 화면 안에 비스듬히, 중앙 정렬. 잘림/치우침 없음.
- θ 를 40/50/60 으로 바꿔 재빌드해도 보드가 항상 화면에 맞음(framing 자동 추종 확인).
- 이 단위에서 캐릭터는 아직 0° 틸트(누운 카드처럼 보일 수 있음) — 정상. unit 2 에서 세움.
- 스크린샷 검증(메모리: Play 게임뷰 흰화면 퀴크 주의 — 좌표/씬뷰로 확인).
