# 1. MapDocument 수동 authoring (ArkFunnel) + 씬 배선

## 목적

map-grid spec 의 예약 슬롯이던 `MapDocument` 경로에 첫 실데이터를 채워, 명일방주 문법(가장자리 스폰 박스 → 합류 외길 → 골 박스)의 손 설계 맵을 실전 배선한다.

## 변경 대상

- `Assets/_Project/Data/Maps/MapDocument_ArkFunnel.asset` (신규)
- `Assets/_Project/Scenes/BattleScene.unity` — `BattleBridge.mapDocument` 슬롯

## 구현

authoring 은 에디터 execute_code 로 수행 (전용 툴 없음 — 후속 후보):

1. `road bool[,]` 로 레이아웃 정의
2. 자가 검증: spawn→goal BFS 연결성 + 2×2 walk 블록 금지 (Validator 미경유 경로이므로 authoring 시점 검증 필수)
3. `GeneratedMap` 조립 — tiles(Walk/Place), mergeDegree(4방향 인접 수), chokepoint(deg≥3), `authoringSeed=-1`
4. `MapDocumentBuilder.WriteToDocument(doc, ref gm)` + `SaveAssets` — 기존 asset 에 덮어쓰면 **GUID 유지로 씬 배선 불변**

씬 배선은 YAML 직접 수정(`mapDocument: {fileID: 11400000, guid: 1a25446142d844b5eb449719c8b67cee, ...}`). 이후 `MapGridBattleAdapter.Build` 가 document 를 감지하면 생성기 대신 `ToGeneratedMap` 으로 반환한다.

## 완료 기준

- [x] BFS 연결성·2×2 검증 통과
- [x] adapter 경유 로드 실측 (goal/spawns/walk 수 일치)
- [x] Play e2e — 적 위치 976/976 샘플 도로 준수·이탈 0, 유출 10 으로 골 완주 증명
- [x] 오버레이 없는 카메라 렌더로 맵 전경 육안 확인

확인 2026-07-19 — 커밋 `acff0abc`
