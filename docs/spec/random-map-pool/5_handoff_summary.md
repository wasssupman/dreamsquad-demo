# 5. Handoff Summary — random-map-pool

## Commit

- `f0a0b7ed` spec 초안
- `335bb2c3` unit 0 — MapDocumentPool SO + MapPoolSelect 순수함수 + 테스트
- `78346d23` unit 1 — BattleBridge 풀 resolve + ActiveDeck 라우팅
- `bf197fb7` unit 2 — MapDocument_TwinLane (스폰 2개)
- `0b390f51` unit 3 — WaveB 덱 (예산 동일·구성 차별화)
- `7942bfba` unit 4 wiring — 풀 asset + BattleBridge 배선 + Play 실증

## Implemented

- 단일 고정 맵 → **N장 (맵, 덱) 풀에서 매판 seed 로 랜덤 선택**. 이번 릴리스 = ArkFunnel/WaveA + TwinLane/WaveB.
- 선택 = `MapPoolSelect.SelectIndex((uint)seed % count)`, seed = `fixedMapSeed!=0 ? fixedMapSeed : DeriveMapSeed(matchSeed)`. 라이브 랜덤 스위치 = **`fixedMapSeed=0`**(과거 20260719 고정이 "매판 같은 맵" 원인).
- 맵·덱을 **같은 인덱스로 함께 선택** → 맵마다 그 맵의 적 패턴. BattleBridge 의 모든 덱 소비는 `ActiveDeck`(=`_resolvedDeck ?? deck`) 경유.
- **점수 예산 전 맵 동일**: 모든 덱 `defeatGoalReachedCount=10`·`timerDurationSec=180`·volume 범위 고정 → 시간·스트레스·킬 3원천 예산 동일(킬값 type-무관). 맵별 차이는 적 종류·레인·pacing 뿐.
- 레인 분배는 기존 런타임 로직이 `_generatedMap.spawns.Length` 로 자동(3레인/2레인) — 코드 무변경.
- Play 실증: `debugFixedMatchSeed=1`→ArkFunnel(3스폰)+WaveA, `=2`→TwinLane(2스폰)+WaveB. 렌더·reflection·콘솔 0에러 확인.

## Key Files

- `Assets/_Project/Scripts/Data/MapGrid/MapPoolSelect.cs` — 순수 선택함수
- `Assets/_Project/Scripts/Data/MapGrid/MapDocumentPool.cs` — (맵,덱) 엔트리 SO
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — resolve + `ActiveDeck`(라인 ~340, ~850)
- `Assets/_Project/Data/Maps/MapDocumentPool.asset` — 풀(엔트리 2)
- `Assets/_Project/Data/Maps/MapDocument_TwinLane.asset` — 신규 맵
- `Assets/_Project/Scripts/Data/Decks/WaveB.asset` — 신규 맵 덱
- `Assets/_Project/Tests/EditMode/MapPoolSelectTests.cs`

## Verified

- EditMode 1243 (unit0 5/5 신규 포함), 0 fail.
- 킬 예산 동일 확증(seed 3종 WaveA=WaveB waveCount·유닛·보스 일치).
- 선택 분포 984:1016 (~50/50).
- Play 두 맵 렌더 + 맵/덱 페어링 reflection 일치, 콘솔 에러 0.

## Notes (되돌리면 안 되는 의도)

- **`fixedMapSeed=0` 유지** — 비0으로 되돌리면 인덱스 핀돼 한 맵만 나온다(라이브 랜덤 죽음).
- **덱 예산 필드(leak/timer/volume) 는 전 덱 동일** 유지 — 어긋나면 맵별 점수 예산 불공정.
- 풀 비거나 엔트리 미완성 시 레거시 `mapDocument`/`deck` 폴백(무회귀) — 폴백 경로 삭제 금지.
- 맵 authoring 은 execute_code 경로(전용 툴 없음). MapDocument 는 `road bool[,]` → BFS·2×2 검증 → `MapDocumentBuilder.WriteToDocument`. `in` 파라미터라 CodeDom 에선 `ref` 로 호출.
- BattleScene 배선은 언로드 상태 disk YAML surgical edit 로 격리(로드 씬 WIP 베이크 회피).

## Follow-up

- **브리핑 스트립(`WavePatternStripView`) per-map 동기화** — 현재 draft 프리뷰가 정적 WaveA. TwinLane 선택 시 브리핑 불일치(시각만, 게임플레이 무관). draft-flow 플러밍 + 실전 wave seed 공유 필요.
- 맵 3종 추가(풀 5종 완성) + 각 맵 덱.
- 즉시-반복 방지, 시즌/테마별 풀, usable 엔트리 필터.
