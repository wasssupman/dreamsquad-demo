# 2. Handoff Summary — spawn-point-alert

> 2026-07-20 작성. 최신 계약은 README 와 번호 문서가 우선한다. 이 문서는 지도다.

## Commit

- `2d8c843e` feat(wave-pattern): 고정 웨이브 시드 — 테스트 버전 매판 동일 공격 패턴 (unit 6)
- `5d996eb0` feat(spawn-point-alert): lane 별 첫 스폰 시각 예보 순수 함수 (unit 0)
- `44f06965` feat(spawn-point-alert): 스폰→골 에너지 라인 예고 + 씬 배선 (unit 1)
- `2e951698` docs(spec): 완료 기록 + 파이프라인 맵에 예고 오버레이 아키타입 추가
- `086507e1` fix: Ground 타일맵 z-fighting 해소 (surfaceOffset 0.012 → 0.06)

## Implemented

- 각 스폰 지점의 **첫 적 등장 2.5초 전**, 그 지점에서 골까지 적 이동 경로를 따라 에너지 라인이 그어진다(0.55초).
- 등장 후에는 **꼬리가 골로 수렴하며** 사라진다(1초, 씬 값). 즉시 소멸은 팝으로 읽혀 폐기.
- 경로는 유닛 이동과 **같은 goal flow field** 를 셀 단위 추적 → 표시 루트 = 실제 진입 루트.
- 비주얼 4레이어: `glow`(가산) + `streak`(가산, 선을 타고 흐름) + `core`(알파, 흰-핫) + `ring`(스폰점 맥동). 전부 절차적 텍스처 — 외부 에셋 0.
- lane 산식 `EffectiveSpawnIndex` 를 `WavePatternGenerator` 로 이관해 예보와 실스폰이 같은 함수를 공유. `DeckIndexStride` 상수로 deckIndex 관례도 공유.
- `deck.waveSeed` 비0 = 라이브 고정 시드(매판 동일 패턴). 0 이면 기존 matchSeed 파생. 시작 로그에 출처(`deck-fixed|derived`) 표기.
- 고정 시드의 부수 효과로 **아웃게임 브리핑 스트립과 런타임이 같은 플랜**을 공유하게 됐다.
- Wave 1(0초 트리거)과 `Next Wave` 강제 호출은 표시 창이 성립하지 않아 자연 스킵.

## Key Files

- `Assets/_Project/Scripts/Presentation/SpawnAlertPresenter.cs` — 뷰 전부(레이어·트레이싱·수렴·절차 텍스처)
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — `EffectiveSpawnIndex` / `DeckIndexStride` / `FirstSpawnTimesPerLane`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `TryGetSpawnAlertForecast` / `TryGetSpawnPathSim` / 예보 캐시 / 시드 resolve
- `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` — `SpawnAlertOrder = -9`
- `Assets/_Project/Tests/EditMode/WaveSpawnForecastTests.cs`
- `Assets/_Project/Scenes/BattleScene.unity` — `SpawnAlertPresenter` GameObject + bridge 참조

## Verified

- EditMode 1016건 통과(실패 0, 스킵 2 = 기존 known-ignore).
- lane 별 ON/OFF 실측이 `첫 스폰 − lead` / `첫 스폰 + 수렴` 과 한 프레임 내 일치.
- 정렬 실측: 예고선 −9~−7, 유닛 +11~+75 → 타일 위·유닛 아래.
- 수렴·강제 호출·웨이브 재개·전투 종료 정리 전부 스크립트 e2e 확인. 콘솔 클린.
- **사용자 Play 확인 2026-07-20** — 체감·z-fighting 해소 통과. 최종 값 `lineWidth 0.14` / `retractSec 1` / `surfaceOffset 0.06`.

## Notes (되돌리면 안 되는 판단)

- **코어만 알파 합성.** 가산으로 하면 이 게임의 밝은 배경에서 채도가 죽어 흐릿한 분홍이 된다. 광휘·스트릭만 가산.
- **정렬은 음수 대역(−9~−6).** "보드 레이어 < 유닛 레이어" 규칙. 초기값 15000 은 유닛을 덮었다.
- **`surfaceOffset`(0.06)은 Ground 타일맵 전용이다 — 유닛 가림과 무관.** `Ground` 만 queue 2000(불투명)이라 깊이를 쓰고, 나머지 타일맵·유닛·라인은 queue 3000(ZWrite Off)이라 `sortingOrder` 로만 갈린다. 라인은 유닛보다 먼저 그려지므로 값을 키워도 유닛을 덮지 않는다. 아끼지 말고 깊이 정밀도를 넉넉히 이기게 준다 — 작으면 일부 구간만 z-fighting 한다.
- **예보 캐시를 `_nextWaveIndex` 만으로 flip 하지 않는다.** 큐잉 순간 인덱스가 넘어가 뒷 lane 예고가 자기 유닛보다 먼저 사라진다(실측 확인). 대신 `ForceNextWave` 는 캐시를 비워 "예고 없이 즉시 스폰" 계약을 지킨다.
- **전투 종료/재시작은 수렴 없이 즉시 정리.** 클럭이 리셋되므로 애니메이션을 태우면 잔상이 남는다.
- `Mathf.SmoothStep(a,b,t)` 는 GLSL edge 함수가 아니다 → `docs/reference/lessons/03-rendering-assets.md` 에 기록. 수렴이 "한 번에 사라짐"으로 보이던 실제 원인이었다.
- **유령 제약 주의**: 이 spec 에서 정렬을 음수로 고친 뒤에도 "오프셋을 키우면 유닛을 덮는다"는 옛 제약 서술이 남아, 오프셋을 과도하게 조인 채 z-fighting 을 방치했다. 선행 수정이 후행 제약을 무효화했는지 매번 되짚는다.
- 반복 아이콘(셰브론 스탬프) 금지. 레퍼런스는 연속 실선이다.

## Follow-up

- **실기기 성능** — lane 당 LineRenderer 3개 + SpriteRenderer 1개(3레인 = 12개), 매 프레임 폴리라인 재구축. Android 미측정. (유일한 미확인 항목)
- 상세 후속 후보는 README "후속 후보" 섹션 참조(보스 웨이브 얼럿 차별화 · Wave 1 사전 얼럿 · 얼럿 SFX).
