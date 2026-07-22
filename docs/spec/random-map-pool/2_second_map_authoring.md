# 2. 신규 맵 1종 authoring (스폰 2개)

## 목적

풀에 넣을 두 번째 손설계 맵을 만든다. **ArkFunnel(스폰 3개)과 대비되게 스폰 2개**로 잡아, 스폰 개수↔웨이브 분배 결합을 실증하고 `EffectiveSpawnIndex` 의 laneCount≤2 경로(3+ 는 ArkFunnel 이 커버)도 함께 검증한다.

## 변경 대상

- `Assets/_Project/Data/Maps/MapDocument_TwinLane.asset` (신규 — 이름은 작업용 placeholder, 테마 확정 시 리네임 가능하나 GUID 유지)

## 구현

ArkFunnel 과 동일 프로세스(에디터 execute_code authoring, `docs/spec/manual-map-authoring/1_mapdocument_authoring.md` 참조):

1. `road bool[,]` 로 레이아웃 정의. 스폰 2개(예: 좌상·우상) → 합류/우회 → 골 1개. non-road 셀은 전부 `Place`.
2. **자가 검증**(Validator 미경유 경로라 authoring 시점 필수): 각 spawn→goal BFS 연결성 + 2×2 walk 블록 금지.
3. `GeneratedMap` 조립: `tiles`(Walk/Place), `mergeDegree`(4방향 인접 path 수), `chokepoint`(deg≥3), `spawns`(2개), `goal`, `authoringSeed = -1`, `generatorVersion = 0`.
4. `MapDocumentBuilder.WriteToDocument(doc, in gm)` + `SaveAssets`.

- grid 크기는 문서별 독립(다운스트림이 per-map gridSize 처리) — ArkFunnel(15×10)과 같게 두거나 달리해도 됨. 시각 대비를 위해 레이아웃 성격을 ArkFunnel(3갈래 깔때기)과 다르게(예: 2갈래 크로스/S자) 잡는다.
- `spawns.Length` 는 1~4 만 허용(`MapDocument.OnValidate`). 여기선 2.

## 완료 기준

- [x] BFS 연결성(스폰 2개 모두 골 도달)·2×2 walk 금지 검증 통과 — execute_code authoring 시 valid=True, 2x2=0, spawnA·B conn=True
- [x] adapter(`ToGeneratedMap`) 경유 로드 실측 — goal(7,0)/spawns(2)/walk 29 일치 (asset 디코드 확인)
- [x] ASCII 레이아웃 육안 확인(도로 형태·스폰/골 위치) — 사용자 승인 2026-07-22. 실제 타일맵 렌더는 unit 4 Play 로 이월
- [ ] Play 진입 시 적이 도로 준수·스폰 2지점 진입·골 완주 → **unit 4 에서 실증**

확인 2026-07-22 (unit 2 — MapDocument_TwinLane 베이크, GUID 51855b55…, 스폰 2개 15×10). Play 실증 unit 4 이월.
