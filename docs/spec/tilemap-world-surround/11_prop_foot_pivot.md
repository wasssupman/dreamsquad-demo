# 11 — 프랍 발 피벗 정착 (근본 수정)

## 목적

코드 리뷰(2026-06-26)가 식별한 근본 원인 해소. 프랍이 캐릭터처럼 **발 피벗**으로 서지 못하고
`visualOffset.y`(높이 리프트)에 의존했는데, tilemap 경로는 부모(BackgroundProps, XZ 바닥 90° 회전)가
이 `(0, h, 0)` 을 **월드 +z(깊이)** 로 회전시켜 스프라이트가 발 셀보다 뒤로 밀렸다. 그 결과:

- 정렬·occlusion 원점·blob 은 발 셀 기준인데 스프라이트 몸통은 +z 뒤 → 상시 어긋남.
- 부양 버그(14ad69c)·blob 2회 뒤집기(abfc7d8↔df3729d)가 전부 이 한 원인의 증상.

**수정**: 모든 프랍 sprite 가 하단 pivot(측정 `pivot.y_norm=0`)이므로, `visualOffset.y=0` 이면 visualRoot 가
발(instance) 위치에 오고 sprite 하단 = 발이 된다. visualOffset 이 `(0,0,0)` 이면 부모 90° 를 곱해도 0 →
좌표계 오버로드·부모 결합이 동시에 사라진다.

## 변경 대상

- 7종 PropData `visualOffset.y` → 0 (`Data/Theme/forest/prop_*.asset`). x/z 는 이미 0.
- 코드/prefab 구조 **무변경** (PropBillboard visualRoot 회전 유지 — localPos.y=0 이라 회전 피벗이 발).

## 구현

- `prop_flower_{p,w,y}` 0.22→0, `rock_s` 0.12→0, `rock_m` 0.29→0, `rock_l` 0.62→0, `tree` 1.43→0.
- PropBillboard 의 `ApplyData` 가 `visualRoot.localPosition = visualOffset` 를 적용하므로 SO 값만 바꾸면 됨.
- blob(df3729d, 발 피벗)·occlusion 원점(발 셀)이 sprite 하단과 자동 정렬된다.

## 검증 (시험 완료)

- tree `visualOffset.y` 1.43→0: sprite `center.z - foot.z` 2.52→1.09(남은 1.09=틸트 누운 몸통 투영, 정상),
  `sprite.min.y=-0.09` 접지 유지, blob.z=foot.z 일치.
- 7종 적용 후 Play(tilemap): onboard 43개 평균 `sprite.min.y=-0.03`, 부양(>0.3) 0개, tree blob.z=foot.z 정렬.
- **Legacy3D 무영향 확정**: `BattleScene_Legacy3D` 는 `mapSource=MapGrid(4)`. MapGrid 는 Legacy 프랍 경로
  (`BattleBridge` 696 `mapSource != MapGrid`)와 tilemap 경로(686 `UseTilemapView`) **둘 다 skip** →
  Legacy3D 는 keeper 프랍을 인스턴스화하지 않으므로 `visualOffset` 변경 무관. (MapView.InstantiateBackgroundProps
  는 Fixture/Legacy 등 비-MapGrid 옛 경로 전용.)

## 완료 기준 (충족)

- Play(tilemap) 스크린샷: 프랍 접지, blob 발밑 정렬, 부양 0. ✓
- Legacy3D = MapGrid 라 keeper 미사용 → 계약 무영향. CS 에러 0. ✓
