# 3. Handoff — duel-route-tours

## Commit

- `2e88bc3b` feat: unit 1 — 웨이브 컨셉이 경로를 고르는 축
- `23538f43` feat: unit 2 — Duel 5웨이브에만 ㅗ/ㅜ 투어
- `71f25b3e` (병행 세션) — BattleBridge 배관이 여기 섞여 들어갔다. 아래 Notes 참조.

## Implemented

- `WaveConceptSlot.pathIndex` — 컨셉 슬롯이 맵 경로를 고른다. 기존 축(laneGroup ·
  classFilter · altitude) 옆의 네 번째 축이며 특례가 아니다.
- 우선순위 3축 `ResolvePathIndex(적 SO, 컨셉, 레인 기본)` — 좁은 쪽이 이긴다.
- `WaveSpawnGroup` · `ExpandedWaveSpawn` · `PendingSpawnEntry` 에 `pathIndex` 운반
  (전부 기본값 -1 이라 미저작 경로는 종전과 동일).
- 스폰 예고가 같은 함수를 타므로 자동으로 투어를 그린다.
- Duel `waypointPaths[1]·[2]` = ㅗ/ㅜ 투어 5경유점씩. `[0]` 공중 예약 불변.
- `Concept_Swarm_Duel`(사본)의 `variantSlots[0].pathIndex = 2` → 5웨이브에만 발동.
- `spawnRoutes` 는 **비운 채**다(unit 0 철회) — 채우면 전 웨이브에 걸린다.

## Key Files

- `Assets/_Project/Scripts/Battle/Movement/WaypointProgress.cs` — 우선순위 정본
- `Assets/_Project/Scripts/Data/WaveConceptData.cs` — 저작 표면
- `Assets/_Project/Data/Maps/MapDocument_Duel.asset` — 투어 좌표
- `Assets/_Project/Data/WaveConcepts/Concept_Swarm_Duel.asset` — 5웨이브 배선
- `Assets/_Project/Tests/EditModeAssets/DuelSnakeTourWaveTests.cs` — 회귀선 4건

## Verified

- EditMode 2,568개 · 이 spec 관련 실패 0. 콘솔 에러 0.
- 같은 시드 15웨이브를 코드 변경 전후 대조 — 컨셉·유닛·수량·lane·시각 전부 일치
  (편성 재추첨 없음).
- 거점 관통 0(본능 4기 + 적 마음). 사용자 Play 확인 2026-08-23.
- 무관한 사전 실패 1건: `UnitKitCatalogTests` malphite 2행 30자 > 28. 입력(에셋·테스트)
  둘 다 HEAD 그대로라 이 작업 이전부터 빨갛다. `docs/reference/test-procedure.md` 의
  「EditMode 기지 실패 없음」이 그만큼 stale 하다.

## Notes

- **경로를 «누가 고르는가»가 «어떤 그림이 나오는가»를 정한다.** 레인 축 = 항상 ·
  적 SO 축 = 그 적이 나올 때마다 · 컨셉 축 = 그 블록에만. unit 0 이 레인 축에
  얹었다가 「다 이렇게 온다」가 되어 철회됐다.
- **거점은 통행을 막지 않고 점유만 선언한다.** 그래서 흐름장이 건물을 관통하고,
  피하는 것은 저작의 몫이다. 경유점이 거점 밖인지만 보면 안 되고 **경유점 사이가
  무엇을 밟는지** 봐야 한다 — 이 판정을 열거식으로 되돌리지 말 것(1×1 마음을 놓친다).
- **직행 경로도 이미 거점을 관통한다**(lane0 6칸 · lane1 2칸). 이 spec 범위 밖이라
  두었다 — README 후속 후보.
- **길이와 변주는 맞바꿈이다.** 벽 없는 23×10 에서 면적을 덮는 수단이 왕복뿐이라,
  길이는 획 수에서 나오고 획이 늘면 반복으로 읽힌다. rev 3 은 길이를 포기했다.
- **「5웨이브」는 시드에 매인 결과다.** 시드·가중치·컨셉 풀이 바뀌면 옮겨간다.
  회귀선이 그때 빨개지므로, 옮겨간 것이 의도면 상수를 고치고 아니면 저작을 되돌린다.
- 병행 세션이 `71f25b3e`(bomb-barrel-on-place)에 이 spec 의 BattleBridge 배관을 함께
  커밋했다. 코드는 정상이고 HEAD 에 있으나 이력상 귀속이 어긋나 있다. 히스토리는
  건드리지 않았다.

## Follow-up

- **미푸시.** 푸시는 사용자 승인 후.
- PlayMode(`WaypointRoutingLiveTest` · `SpawnGuideMatchesWalkTest`) 미실행 — 8분이라
  사용자 확인 후.
- 투어 길이가 아쉬우면 손잡이는 **세로획의 끝점 y** 와 **획의 x 위치**다. 획을 늘리는
  것은 손잡이가 아니다(rev 2·2.5 가 그래서 버려졌다).
- 다른 컨셉·다른 맵으로의 확장은 저작만으로 된다(코드 0줄).
