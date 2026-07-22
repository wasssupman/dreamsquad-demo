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

- [ ] compile 0 errors, 기존 EditMode green
- [ ] ArkFunnel Load → 즉시 "통과"(기존 유효 맵), 도로 한 칸 끊으면 해당 스폰 "미도달" 빨강 + Bake 비활성
- [ ] 2×2 walk 만들면 위반 표시
- [ ] 유효 편집 Bake → asset 갱신(디코드로 tiles/spawns/goal/파생값 일치), 기존 타깃은 GUID 유지
- [ ] 신규 Bake → SaveFilePanel 경로에 새 MapDocument 생성, adapter 로드 정상
