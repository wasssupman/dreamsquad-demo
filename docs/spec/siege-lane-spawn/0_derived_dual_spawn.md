# 0. 스폰 파생 1→2 — 마음의 상/하단 타일

## 목적

공성 파생 스폰을 «마음 셀 1개»에서 «마음의 하단(y−1)·상단(y+1) 2개»로 바꾼다.
`GeneratorLaneCount = spawns.Length` 라 이 한 변경으로 laneCount 2 가 성립하고,
반 칸 어긋난 미러축이 만들던 «전 웨이브 한 줄» 이 스폰 수준에서 갈라진다
(Duel 실측: 하단 스폰 → 하단 다리 y3 / 상단 스폰 → 상단 다리 y8).

## 변경 대상

- `Assets/_Project/Scripts/Data/MapGrid/MapDocumentBuilder.cs` — 파생 블록 (마음당 2셀)
- `Assets/_Project/Scripts/Data/StructurePlacement.cs` — `ValidateStructures` 확장 (추가만)
- `Assets/_Project/Tests/EditMode/StructureAuthoringTests.cs` — 파생 pin 갱신 + 픽스처 복도 확장
- `Assets/_Project/Tests/EditModeAssets/MapDocumentPoolDevEntriesTests.cs` — 철학 ② 갱신 + 순서 pin
- `Assets/_Project/Tests/EditMode/StructureSpawnAndBreachTests.cs` — stale 메시지 절 삭제 (동작 무변경)

## 구현

1. **파생 (기계적 유지)**: `enemyCoreCount > 0` 분기에서 마음당 `[(x, y−1), (x, y+1)]` 순으로
   `enemyCoreCount * 2` 스폰을 만든다. **순서 계약: 하단 = lane 0, 상단 = lane 1.**
   Walk/경계 보장은 저작 검증 몫 — 파생은 검사하지 않는다(기존 원칙 유지).
2. **저작 검증 (추가만)**: 적 마음의 ⑴ y±1 격자 경계 검사(타일 무관 기하 규칙),
   ⑵ 마음·하단·상단 3셀 Walk 검사(기존 마음 검사를 셀별 메시지로 확장). 기존 검사 삭제 없음.
3. **테스트**: 파생 pin 을 2스폰+순서로 갱신, 픽스처 Walk 복도를 y=2..4 로 확장(상/하단이
   Walk 여야 연결성 통과), 비-Walk 검증 기대치를 3셀 규칙으로 갱신.

## 완료 기준

- compile (dotnet build, 영향 어셈블리)
- EditMode: `StructureAuthoringTests` · `MapDocumentPoolDevEntriesTests` 전량 초록 (Unity 가동 시)
- 실 맵 3장(Duel·Ford·Isle)의 파생 스폰 = (18,4)·(18,6), 둘 다 골 도달 (철학 ⑦ 단언이 검증)
- PlayMode Duel 참조 테스트 재실행은 spec «검증 주의» 에 따라 Unity 가동 시 일괄
