# 3. Handoff — siege-lane-spawn

## Commit

- `f825a86d` feat: unit 0 — 공성 파생 스폰 1→2 (마음 하단·상단 = lane 0·1)
- `bfbed4c3` feat: unit 1 — spawnRoutes 공성 부활 (버리기 → 재구축)
- `a74b7e67` feat: unit 2 — Ford·Isle 레인 경로 저작 + 경로 분리 술어

## Implemented

- 공성 파생 스폰 = 마음 (x,y−1)·(x,y+1), 순서 = 레인 번호(하단 = lane 0). 단일 소스
  `StructurePlacements.SiegeSpawnOffsets`.
- `GeneratorLaneCount` 2 성립 → 「원거리」 컨셉이 공성 맵 후보로 부활.
- `ValidateStructures` 확장(추가만): 마음 y±1 경계 + 스폰 클러스터 3셀 Walk.
- 빌더 공성 분기: `spawnRoutes` 저작 길이 = 파생 스폰 수일 때만 채택, 어긋나면 폐기+경고.
- OnValidate 레인 검증이 파생 스폰 목록(`CollectDerivedSiegeSpawns`)을 받는다.
- Ford (10,4)/(10,7)·Isle (10,3)/(10,8) 상·하단 경유 저작, routes [1,2]. Duel 은 비움(지형).

## Key Files

- `Assets/_Project/Scripts/Data/MapGrid/MapDocumentBuilder.cs` — 파생·재구축
- `Assets/_Project/Scripts/Data/StructurePlacement.cs` — 오프셋 상수·검증·파생 목록
- `Assets/_Project/Tests/EditModeAssets/MapDocumentPoolDevEntriesTests.cs` — 철학 pin·분리 술어

## Verified

- EditMode 전량 2459 / 실패 0 (skip 3 = 기존 Ignored). 실 맵 3장 파생 (18,4)·(18,6) 연결성.
- 분리 술어: Duel 플로우 추적 하단 다리/상단 다리, Ford·Isle 경유 상·하단.
- **PlayMode 미실행** — Duel 참조 테스트(`SpawnGuideMatchesWalkTest` 등)와 사용자 Play 체감
  (두 줄 스폰) 확인이 남아 있다.

## Notes

- path 0 = 공중 예약(`Enemy_Skimmer.waypointPathIndex 0`) — 레인 경로는 1부터. 되돌리지 말 것.
- 파생 순서 [하단, 상단]을 뒤집으면 Ford·Isle 의 spawnRoutes 가 서로 바뀐다 — 오프셋 상수와
  테스트 pin 이 지킨다.
- laneCount 1→2 로 공성 3덱 컨셉 시퀀스가 재추첨된 **의도된 중간 상태** — 새 baseline 은
  `wave-ramp-two-phase` unit 3(시드 재선정)이 박는다.

## Follow-up

- `wave-ramp-two-phase` spec 진행 (선행 조건 충족됨).
- 페인터의 공성 레인 경로 저작 지원 / 본능 프랍 진영 구분 / 강 시각 표현 / 라이브 풀 편입
  — README 후속 후보 참조.
