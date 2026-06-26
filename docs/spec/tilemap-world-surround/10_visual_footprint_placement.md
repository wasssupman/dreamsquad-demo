# 10 — 비주얼 풋프린트 기반 방향성 배치 (시인성)

## 목적

근경 프랍(특히 틸트된 나무)이 이동/배치 타일 위 유닛을 가리는 문제 해소. 프랍의 **비주얼 가림 범위를
명시적 데이터(`visualFootprint`, 예 1x4)로 선언**하고, 배치 시 틸트 누운 방향(`+y`, 실측 확정)으로
그 범위가 플레이 타일(Walk/Place)을 침범하면 거부한다. 결과적으로 큰 프랍은 플레이 타일의 `+y`(뒤)
에만 남아 "유닛 뒤로 숲, 앞은 트임"(산속 마을 + 시인성)이 된다.

검증 질문: *방어/적 유닛이 근경 프랍에 가려지지 않고, 큰 나무가 플레이 타일 뒤쪽으로 자연스럽게 빠지는가?*

## 변경 대상

- `Assets/_Project/Scripts/Data/PropData.cs` — `Vector2Int visualFootprint = (1,1)` (width × depth)
- `Assets/_Project/Scripts/Data/BackgroundPropPlacer.cs` — `VisualFootprintHitsPlay` 헬퍼 + `occlusionAware`
  파라미터 전파(Generate→TryPlaceNearestCandidate→CanPlaceAtCandidate / TryPlace)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — tilemap Generate 호출에 `occlusionAware: true`(legacy=false)
- 7종 PropData: `visualFootprint` 값 + 파일명에 `_WxD` 라벨(가독). source of truth=필드, 파일명은 라벨.

## 구현

- **명시 데이터(동적 X)**: `visualFootprint.x`=폭(X), `.y`=depth(틸트 `+y` 방향 셀 수). depth=1=가림 없음.
  값: flower×3 `1x1`, rock_s/m `1x1`, rock_l `1x2`, tree `1x4` (Play 보며 튜닝).
- **방향 `d=+y` 실측 확정**: +y 셀→월드 +Z, 틸트(50) 꼭대기→월드 +Z. 나무 비주얼은 발에서 `+y`로 깔림.
- **거부 규칙**: 발 셀(placement 중심) 기준 `{(cx+ix, cy+k) : ix∈[0,width), k∈[1,depth)}` 중
  하나라도 `zoneType ∈ {Walk, Place}` 면 배치 거부. 보드 밖 좌표=비-플레이. depth≤1=검사 skip(0 회귀).
- **tilemap 전용**: `occlusionAware`=tilemap true / Legacy3D false(기본) → Legacy 분포 불변(계약 보호).
- 기존 footprint(sim 발자국)·CanFit(Env)·minDistance·sameCategory 검사는 유지, occlusion 은 그 위에 추가.

## 완료 기준

- compile 0 에러.
- Play 스크린샷: 큰 나무가 플레이 타일 `-y`(앞)에서 사라지고 `+y`(뒤)/가장자리로 빠짐. 유닛 배치 시
  앞이 트여 가려지지 않음. 꽃/낮은 돌은 경로변 유지. 근경 전멸 아님(depth 1 다수).
- 객관: 플레이 타일 +y 인접에 depth>1 프랍 0. Legacy3D·원경·캐릭터 무영향.
