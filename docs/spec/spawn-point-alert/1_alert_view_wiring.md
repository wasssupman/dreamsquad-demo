# 1. 경로 라인 트레일 뷰 + BattleBridge 예보 API + 씬 배선

> rev 2026-07-20: 비주얼을 스폰 타일 마커 → **스폰→골 경로 라인 트레일**(명일방주식)로
> 변경(사용자 결정). 표시 창 판정·예보 API 골격은 동일.

## 목적

unit 0 의 예보를 소비해, 다음 웨이브의 각 스폰 지점에서 골까지 적 이동 경로를 따라
흐르는 셰브론 라인 트레일을 첫 적 등장 `lead`(기본 2.5초) 전부터 등장 시각까지 띄운다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — read-only 예보 API + 경로 API (NextWave* 프로퍼티 군 옆)
- `Assets/_Project/Scripts/Presentation/SpawnAlertPresenter.cs` — 신규 (트레일 생성·표시 창 판정)
- `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` — `SpawnAlertOrder = 15000` 등록
- `Assets/_Project/Scenes/BattleScene.unity` — 프레젠터 GameObject + bridge 참조 배선

## 구현

**Bridge API** (읽기 전용, UI 폴링 — NextWaveDock 패턴):

```csharp
// 다음 예정 웨이브의 lane 별 첫 스폰 절대 시각 + 현재 battle 클럭.
// 반환 false = 예보 없음(생성 웨이브 미사용/마지막 웨이브 소진/맵 미생성).
public bool TryGetSpawnAlertForecast(out float battleClockSec, out float[] laneFirstSpawnSec)
// 스폰→골 대표 경로(sim 셀 중심 나열) — goal flow field 의 flow 를 셀 단위 추적.
public bool TryGetSpawnPathSim(int laneIndex, List<Vector3> outPath)
```

예보 배열은 `_nextWaveIndex` 가 바뀔 때만 재계산해 캐시한다(`FirstSpawnTimesPerLane`
호출, base = 해당 웨이브 `triggerTimeSec`). 경로는 유닛 이동과 **같은 flow field·같은
타이브레이크**라 표시 루트와 실제 진입 루트가 셀 수준에서 일치한다.

**캐시를 `_nextWaveIndex` 만으로 flip 하면 안 된다(실측으로 잡은 결함).** 웨이브가
큐잉되는 순간 `_nextWaveIndex` 가 넘어가지만 그 웨이브의 뒷 lane 들은 아직 안 나왔다
(레인 간 `intraWaveSpacing` 간격). 인덱스를 그대로 따르면 **전 lane 예고가 큐잉 시각에
동시 소멸**해 뒷 lane 은 자기 유닛보다 0.3~0.7초 먼저 사라진다. 따라서 캐시된 예보에
미래 스폰이 남아 있으면(`max(laneFirst) > clock`) 계속 서빙한다. 대신 `ForceNextWave`
는 예정 시각을 무효화하므로(스폰이 지금 일어남) 캐시를 명시적으로 비워 "예고 없이 즉시
스폰" 계약을 지킨다.

**프레젠터**: 매 프레임 폴링. lane L 트레일 표시 조건 =
`laneFirst[L] >= 0 && clock >= laneFirst[L] - leadSec && clock < laneFirst[L]`.
등장 시각 도달·강제 호출·전투 종료 시 즉시 숨김. 트레일은 lane 별 LineRenderer 를
생성해 재사용하고, 표시 시작 시마다 경로를 재조회한다(flow 변화 반영).

**비주얼 (rev 3 — 에너지 라인 VFX)**: lane 당 4개 레이어의 합. 단색 실선 하나로는
"그냥 빨간 선"이라 VFX 로 읽히지 않는다.

| 레이어 | 합성 | 폭 | 역할 |
|---|---|---|---|
| `glow` | 가산 | ×5 | 바깥 광휘(에너지 헤일로) |
| `streak` | 가산 | ×2.6 | 선을 타고 스폰→골로 훑는 에너지 |
| `core` | **알파** | ×1 | 흰-핫 중심 + 가장자리 채도 |
| `ring` | 가산 | — | 스폰 지점 반복 확산 맥동 |

- **코어만 알파 합성이다.** 가산으로 하면 이 게임의 밝은 배경(청록 잔디·회색 길) 위에서
  색이 바래 흐릿한 분홍이 된다. 채도를 지키려면 코어는 알파여야 하고, 광휘/스트릭만 가산.
- 코어 텍스처는 폭 방향 램프로 **RGB 를 구워 넣는다**(중심 흰색 → 가장자리 `lineColor`).
  렌더러 tint 는 흰색(밝기 변조만). `lineColor` 변경 시 자동 재굽기 → Play 중 실시간 튜닝.
- 표시 시작부터 `drawSec`(0.55초) 동안 스폰→골로 **그어지고**, 첫 적 등장 후에는
  `retractSec`(0.45초) 동안 **꼬리가 골로 수렴하며 사라진다**(즉시 소멸 금지 — 팝으로 읽힌다).
  둘 다 같은 arc-length 부분 폴리라인 함수로 구현한다: 그리기 = `[0, len·drawT]`,
  수렴 = `[len·smoothstep(retractT), len]`. 셀 단위 점프가 아니며 양 끝은 세그먼트 보간.
  수렴 꼬리는 smoothstep 으로 가속해 머리를 따라붙고, 마지막 15% 에서만 살짝 페이드한다
  (페이드가 주 연출이면 수렴이 안 보인다 — 페이드는 마지막 픽셀 팝 방지용).
  스폰점 링은 꼬리가 떠나므로 `(1-retractT)²` 로 빠르게 소멸.
- 그어지는 동안 선단이 강하게 발광(ignite), 완성 후엔 은은히 숨쉰다(breathe).
- **전투 종료/재시작(`has == false`)은 수렴 없이 즉시 정리**한다 — 클럭이 리셋되므로
  수렴 애니메이션을 태우면 잔상이 남는다.
- 스트릭은 `textureMode=Stretch` 라 U 0→1 이 선 전체 → `mainTextureOffset` 을 battle
  클럭으로 흘리면 에너지 하나가 스폰→골로 지나간다(정지 시 자연 동결). 머티리얼 1개 공유.

**반복 아이콘(셰브론 스탬프) 금지** — 초기 구현이 그랬고 "그냥 아이콘 표기"로 읽혔다.
레퍼런스([Arknights Pathing](https://arknights.wiki.gg/wiki/Pathing))는 스폰→목적지로
뻗는 **연속 실선**(적=빨강/드론=노랑)이며, 정적 표기가 아니라 스폰 직전에 그어진다.

**z-fighting — `surfaceOffset` 은 보드 평면 법선 방향이어야 한다.** 경로 정점은 sim 셀
경로를 collinear 병합 후 `BoardSpace.ToView` 변환(코너만 유지). 여기서 view +Y 로 띄우면
(BoardSpace 는 평면 뷰라 +Y = 높이가 아닌 **화면 위쪽**) 가로 구간에서 선이 길 중앙을
벗어난다. 반대로 0 으로 두면 타일과 동일 평면이라 픽셀 겹침이 생긴다. 정답은
`BoardSpace.RaycastPlane().normal` 을 카메라 쪽으로 정렬해 그 축으로 띄우는 것 —
화면상 위치는 유지되고 깊이만 분리된다(기본 0.05).

**정렬 — 예고선은 유닛 위가 아니라 바닥에 깔린다.** `TilemapMapView` 의 "보드 레이어 <
유닛 레이어" 규칙(보드=음수, 유닛/VFX=양수)에 따라 `BoardSortOrder.SpawnAlertOrder = -9`,
레이어 4개가 `-9 ~ -6` 을 채운다. overlay 타일맵(−10) 위, 블롭 그림자(−5)·타일 게이지(−4)·
유닛(런타임 실측 +11~+75) 아래. 초기값 15000 은 유닛 위로 떠서 오답이었다.

**`surfaceOffset` 은 Ground 타일맵 전용이고 유닛 가림과 무관하다(실측 정정).**
큐를 실제로 찍어보면: `Ground` 만 queue 2000(불투명, `Wassup/Tile_ShadowReceive`)이라
깊이를 쓰고, `Overlay`/`Props`/`EffectTiles`/유닛/라인은 전부 **queue 3000(ZWrite Off)**
이라 `sortingOrder` 로만 갈린다. 라인(−9~−6)은 유닛(+11~+75)보다 먼저 그려지고 깊이를
안 쓰므로 이 값을 키워도 유닛을 덮을 수 없다.

따라서 z-fighting 은 **Ground 하나와의 깊이 경합**이며, 값을 아끼지 말고 정밀도를
넉넉히 이기게 준다(0.06). 너무 작으면(0.012) 보드 위치에 따라 깊이 정밀도가 달라
**일부 구간만** z-fighting 하는 형태로 남는다 — 실제로 그랬다.

> 이전 rev 에 "크면 유닛을 덮으니 최소치로" 라고 적었던 것은 **정렬이 양수(15000)이던
> 시절의 관찰**이었다. 정렬을 음수 대역으로 고친 순간 그 트레이드오프는 사라졌는데
> 서술만 남아, 오프셋을 과도하게 조인 채 z-fighting 을 방치하는 원인이 됐다.
> **선행 수정이 후행 제약을 무효화했는지 확인하지 않으면 이런 유령 제약이 남는다.**

색·폭·drawSec·retractSec·스트릭 속도·링 크기·leadSec 은 SerializeField.

**씬 배선**: `unity-feature-wiring` 스킬. dirty 씬 주의 — in-memory 배선 검증 후
저장 격리 절차(lessons 참조).

## 완료 기준

- 에디터 Play: 웨이브 2 이후 각 웨이브마다, 적이 나오는 스폰 지점→골 경로를 따라 등장 2.5초 전 에너지 라인이 그어지고 빛이 흐르며, 등장 후에는 꼬리가 골로 수렴하며 사라진다 (연속 스크린샷으로 수렴 확인).
- 선이 길(타일) 중앙에 정렬되고, 타일과 픽셀 겹침(z-fighting)이 없다.
- **선이 타일 위·유닛 아래에 깔린다** — 경로 위를 행진하는 적이 선에 덮이지 않는다(스크린샷 확인).
- **lane 별 ON/OFF 시각 실측**이 `첫 스폰 − leadSec` / `첫 스폰` 과 한 프레임 내로 일치한다.
  (검증 방법: 예보 3레인이 서로 다른 시각이어야 유의미하다. 배치 유닛 없이 Play 하면
  적 골 도달로 18초경 패배 종료돼 관측이 잘리므로, 프로브에서 `_goalReachedCount` 를
  프레임마다 0으로 눌러 억제한다 — SO 는 건드리지 않는다.)
- 트레일 경로가 실제 적 행진 루트와 일치한다 (같은 flow field 계약).
- ~~`Next Wave` 강제 호출 시 트레일 없이 즉시 스폰 (계약대로 스킵, 오류 로그 없음).~~
  → **2026-07-26 unit 3 에서 반전**: 강제 호출도 리드인만큼의 예고 창을 갖는다. 아래 스탬프는
  당시(unit 1) 계약에 대한 확인 기록으로 남긴다.
- 고정 시드(wave-pattern unit 6)로 2회 재진입 시 트레일 등장 타이밍·지점 동일.
- 마지막 웨이브 소진 후 트레일·경고 로그 없음. 콘솔 클린.

**확인 2026-07-20 · 커밋 `44f06965`** — 스크립트 e2e 로 전 항목 실측.

- lane 별 ON/OFF: 첫 스폰 `[18.00, 18.35, 18.70]` → ON `15.52 / 15.87 / 16.22`
  (각 −2.5초), OFF `18.49 / 18.84 / 19.17` (각 +약 0.47초 수렴 후). 한 프레임 내 일치.
- 정렬 실측: 예고선 `Glow −9 / Streak −8 / Core −7`, 유닛 `+11 ~ +75` → 타일 위·유닛
  아래 확정. 경로 위 적이 선에 덮이지 않음(스크린샷).
- 수렴: 스폰 직후 0.12초 간격 연속 캡처에서 꼬리가 스폰점→골로 전진하는 것 확인.
- `ForceNextWave`: 트레일 없이 즉시 스폰(`forced=True`), 예보는 다음 웨이브로 이동.
- 전투 종료·다음 웨이브(33.5초~) 재개 정상, 콘솔 에러/경고 0.

**사용자 Play 확인 2026-07-20** — 체감(굵기·색·수렴 속도·광휘 세기) 및 z-fighting
해소 통과. 최종 값: `lineWidth 0.14` · `retractSec 1` · `surfaceOffset 0.06`.

**미확인**: 실기기(Android) 성능. Follow-up 참조.
