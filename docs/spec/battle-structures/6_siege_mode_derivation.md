# unit 6 — 공성 모드 파생 (적 마음 → spawns[])

## 목적

**적 마음의 유무가 곧 모드다** — 저작 enum 도 런타임 분기도 없이(README §모드 판정), 투영 지점 한 곳에서 `spawns[]` 를 적 마음 셀로 채운다. `spawns[]` 소비처 8곳(웨이브 생성·레인·예고 라인·측면 분산·배치 차단·보드 시각·연결성·프랍)은 전부 «셀 좌표 목록» 만 보므로 **무변경**으로 공성이 성립한다.

이로써 unit 3 의 M-b 전제(«공성 문서는 unit 6 파생이 서기 전까지 런타임에서 돌지 않는다»)가 풀린다.

**행동 변화**: 적 마음이 저작된 맵에서만. 침략 맵(적 마음 0) = 0.

## 변경 대상

- `Assets/_Project/Scripts/Data/MapGrid/MapDocumentBuilder.cs` — `ToGeneratedMap` 파생 1블록
- `Assets/_Project/Scripts/Data/StructurePlacement.cs` — `ValidateStructures` 에 Walk 검사(선택 인자)
- `Assets/_Project/Editor/MapPainterWindow.cs` · `MapDocument.OnValidate` — tiles 전달
- 테스트 — 파생·연결성·침략 무변경

## 구현

### 1. 파생은 투영 지점 한 곳

`ToGeneratedMap` 에서 structures 투영 직후:

```
적 마음 셀 목록이 비어있지 않으면 → spawns = 그 셀들 (저작 spawns 무시)
비어있으면                       → spawns = 저작 spawns (현행 그대로)
```

- 여기가 유일한 파생 지점인 이유: 모든 소비처(8곳 + `MapConnectivity`)가 `GeneratedMap.spawns` 를 읽으므로, 투영에서 채우면 하류 전체가 자기가 무슨 모드인지 모른 채 공성으로 돈다.
- 공성 규칙(적 마음 정확히 1)은 저작 검증이 잡는다 — 파생은 저작이 뭘 들고 오든 기계적으로 처리한다(마음 2+ 여도 각 셀이 스폰이 될 뿐, 에러는 페인터·OnValidate 몫).
- «공성인데 spawns 저작» 은 검증 에러지만, 뚫고 와도 파생이 저작 spawns 를 **덮는다** — 표현 불가능해야 할 상태를 런타임에서도 화해시킨다.

### 2. 적 마음 셀은 Walk 여야 한다 (리뷰 A-LOW 선반영)

스폰이 된 셀이 Walk 가 아니면 연결성 BFS 가 도달 못 해 hard-fail 한다. 기존 «스폰이 Walk 아님» 검사는 `_spawns` 리스트만 봐서 파생 스폰을 못 잡는다 — `ValidateStructures` 에 tiles 를 선택 인자로 받아 **적 마음 셀의 Walk 검사**를 넣고 페인터·`OnValidate` 가 tiles 를 전달한다.

## 완료 기준

- [x] 컴파일 에러 0 · 콘솔 신규 에러 0
- [x] EditMode 전량 그린 — **2046개 / 실패 0 / 의도적 스킵 3**(기준선 2042 + 신규 4)
- [x] 공성 파생 — `SiegeDoc_EnemyCoreCell_BecomesTheSpawn_AndPassesConnectivity`. **공성 문서가 처음으로 연결성을 통과한다**(unit 3 M-b 전제 해소)
- [x] 침략 무회귀 — `InvasionDoc_NoEnemyCore_KeepsAuthoredSpawns` + 기존 왕복 테스트 전량
- [x] 검증을 뚫은 «공성 + spawns 저작» 은 파생이 덮는다 — `SiegeDoc_AuthoredSpawnsAreOverridden_ByDerivation`
- [x] 적 마음 non-Walk = 검증 에러 — `ValidateStructures_EnemyCoreOnNonWalk_IsError`(페인터·`OnValidate` 가 tiles 전달)
- [ ] 리뷰: 스펙 종료 시점 투트랙(4~6 묶음)에 합류

---

**확인 2026-08-09** — 구현 커밋: (아래 커밋 해시). `ToGeneratedMap` 의 spawns 투영이 `doc.Spawns` null 을 못 받던 잠복 NRE 도 함께 해소(공성 문서가 정확히 그 상태다).
