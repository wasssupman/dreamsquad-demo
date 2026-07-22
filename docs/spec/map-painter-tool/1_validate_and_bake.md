# 1. 실시간 검증 + Bake

## 목적

편집 버퍼의 유효성을 실시간으로 보여주고, 유효할 때 `MapDocument` asset 으로 굽는다.

## 변경 대상

- `Assets/_Project/Editor/MapPainterWindow.cs` (검증 패널 + Bake)

## 구현

**검증(매 OnGUI):**
- 스폰 수 1~4, 골·모든 스폰이 Walk 셀인지.
- 각 스폰 → 골 **BFS 연결성**(Walk 셀만 이동). 미도달 스폰 목록 표시.
- **2×2 walk 블록 금지**: 어떤 (x,y) 도 (x,y)(x+1,y)(x,y+1)(x+1,y+1) 전부 Walk 이면 위반(위치 표시).
- 결과를 색 박스로: 통과=녹색, 실패=빨강+사유. 실패면 Bake 버튼 비활성.

**Bake:**
- 파생값 계산: `mergeDegree[i]` = 셀이 Walk 일 때 4방향 인접 Walk 수(아니면 0), `chokepoint[i]` = mergeDegree≥3, `propLayerId[i]=0`.
- `GeneratedMap` 조립(tiles/mergeDegree/chokepoint/propLayerId/gridSize/spawns/goal, `seed=-1`, `generatorVersion=0`) → `MapDocumentBuilder.WriteToDocument(target, ref gm)` → `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets`. gm 은 Temp allocator, Bake 후 Dispose.
- target 이 null(신규)면 `EditorUtility.SaveFilePanelInProject` 로 경로 받아 `CreateInstance<MapDocument>()`+`CreateAsset` 후 WriteToDocument.
- Bake 성공 시 짧은 로그 + target 을 방금 구운 doc 으로 세팅(연속 편집).

## 완료 기준

- [x] compile 0 errors
- [x] ArkFunnel Load → Validate errors=0(통과). 스폰 진짜 고립 시 "→ 골 미도달", 골 Place 시 "Walk 아님", 스폰0 시 "1~4 필요", 2×2 walk 시 "블록 (x,y)" — 4케이스 다 검출
- [x] Bake 왕복: ArkFunnel Load→Bake→asset tileDiff=0, goal/spawns 일치, authoringSeed=-1, chokepoint=(deg≥3) 일관
- [x] 기존 타깃 Bake = WriteToDocument(GUID 유지). 신규는 SaveFilePanelInProject 경로에 CreateAsset
- [ ] (사용자) 창에서 직접 페인팅→Bake→인게임 로드 육안 확인

확인 2026-07-23 (unit 1 — Validate 4케이스 + Bake 왕복 reflection 실증). Play 육안만 남음.
