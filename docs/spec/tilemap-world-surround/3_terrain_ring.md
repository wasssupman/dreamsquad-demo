# 3 — 외곽 터레인 링 + 톤다운

## 목적

플레이 보드(N×M) 밖을 터레인 타일로 더 칠해 "더 큰 자연 환경"으로 감싼다. 플레이 영역 가독성은
주변 톤다운(채도/명도↓ + 바깥 페이드)으로 유지. sim 그리드는 N×M 그대로(순수 시각).

## 변경 대상

- `Assets/_Project/Scripts/Data/TileSetData.cs` — `terrainTile`, `ringRadius`, `surroundFarColor`, `surroundNoiseScale/Amount`
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `PaintSurroundRing` + `RingDistance`

## 구현

- `PaintGround` 끝에서 `PaintSurroundRing(w,h)` 호출. 플레이 셀 `[0,w)×[0,h)` 는 스킵(원색 유지).
- 링 셀 `[-R, w+R)×[-R, h+R)` 를 `TileSetData.TerrainTileOrFallback`(terrainTile 없으면 decoTile=grass)로 칠함
  → **원경도 플레이 영역과 같은 풀 타일**.
- 그라데이션: 셀마다 `SetTileFlags(None)` 후 `SetColor`. 보드 경계로부터 ring 거리(Chebyshev 1..R)로 `t`(0..1).
  `Color.Lerp(흰색, surroundFarColor, t)` → **안쪽(t=0)=플레이 영역 풀 타일 원색(흰색·매끄러운 연속)**, 바깥(t=1)=어두운 목표색.
  (rev: 기존 `surroundTint*factor` 곱셈 → 안쪽이 미리 톤다운돼 보드/링 단차가 생겼던 것을 Lerp 로 교체.)
- **banding 제거**: `Mathf.PerlinNoise((x+1000)*scale, (y+1000)*scale)` 로 `t` 를 교란(`surroundNoiseAmount`)
  → 동심 사각형 띠를 유기적으로 깬다. (프로젝트 "유기적 경계 = 노이즈 워프" 패턴)
- 센터링(`CenterBoardAtWorldOrigin`)은 플레이 보드 기준 유지 → 링은 대칭 확장, BoardSpace/sim 무영향.

## 데이터 (forest/AutoTileTest 적용값)

- `ringRadius=6`, `surroundFarColor=(0.06,0.07,0.06)`, `surroundNoiseScale=0.25`, `surroundNoiseAmount=0.5`.
- `terrainTile` 미지정 → decoTile(grass) 폴백 = 플레이 영역과 동일 풀 타일.

## 완료 기준 (검증 2026-06-26)

- compile 0. 페인트 영역 20×10 → 32×22. 보드 경계에서 원색 풀 타일이 매끄럽게 이어지고 바깥으로 갈수록 그라데이션 어두워짐(banding 없음).
- sim/배치/이동 무영향(센터 권위 불변). 스크린샷 육안 확인 완료.
