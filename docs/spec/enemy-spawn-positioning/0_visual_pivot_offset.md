# 0 — 비주얼 피봇 오프셋 (유닛 타입별)

## 목적

목표 1. 적 유닛 비주얼 피봇이 이동타일 중심(=sim 좌표)에 정렬됨을 보장하고, **유닛 타입별** 미세조정
오프셋 노브를 제공한다. 기본 `(0,0,0)` = 현재 동작(피봇이 sim 좌표=셀 중심 추적) 그대로 — 회귀 없음.

## 변경 대상

- `Assets/_Project/Scripts/Data/ISpineUnitVisualData.cs` — `SpineVisualOffset` getter 추가(`SpineVisualScale` 형제).
- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — `visualOffset` 필드 + 구현.
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — 구현(현 spec 범위 밖 → `Vector3.zero`).
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` — 렌더 위치에 offset 적용. `_simWorld` 은 순수 sim 유지.

## 구현

- `ISpineUnitVisualData` 에 `Vector3 SpineVisualOffset { get; }` — 모든 Spine 유닛의 시각 변환 계약.
  **타입 분기(`is AttackUnitData`) 금지**, 공유 계약으로 통일(땜빵 회피).
- `AttackUnitData`: `public Vector3 visualOffset;` + `=> visualOffset`.
- `DefenderUnitData`: `=> Vector3.zero` (방어 유닛은 본 spec 범위 밖, 계약 기본값).
- `SpineUnitView.ApplyRenderPosition(world)` 단일 지점: `_simWorld = world`(정렬/셀 역산은 순수 sim),
  `transform.position = (Vector3)ToView(world) + SpineVisualOffset`. `Spawn`·`UpdatePosition` 양쪽이 이 지점 사용.
- 오프셋은 **view-space**(post-ToView) — Legacy3D/Tilemap 양 모드에서 화면 기준 미세보정.
  `_simWorld` 불오염 → sorting/cell 역산 무영향(목표 2 의 sim 위치와 직교).

## 완료 기준

- compile 0 에러.
- 기본값 `(0,0,0)` 에서 적 위치·정렬 기존과 동일(회귀 없음).
- `AttackUnitData.visualOffset` 에 (예: `y=+0.2`) 주면 해당 유닛 비주얼만 그만큼 이동(Play 육안).
- `_simWorld` 기반 sorting/cell 역산 무변화.

완료 확인 2026-06-26 — compile 0 에러 / 기본값 `(0,0,0)` 무회귀(시각 변화 없음) / 사용자 진행 승인.
육안(`visualOffset` 실제 적용)은 unit 1 스폰 분산과 통합 Play 검증 예정. Quad 폴백 미배선(적=Spine 이라 무영향).
