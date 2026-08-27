# 맵 스테이지 저작 가이드 — 프리팹 제약과 절차

> 디오라마 스테이지(`MapStage` 프리팹)를 직접 만들 때의 규칙 요약. bake 단계는 없다 —
> **프리팹이 곧 맵 정본이자 비주얼**이고, 배틀 진입 시 `MapStageScanner` → `DioramaMapBuilder` 가
> 프랍 위치를 셀로 양자화해 논리 맵(`GeneratedMap`)을 그 자리에서 파생한다.
> 설계 이력은 `docs/spec/map-diorama-stage/` (계약 목록은 README).

## 구성 요소

| 스크립트 | 역할 | 핵심 필드 |
|---|---|---|
| `MapStage` (루트, 필수) | 스테이지 선언 | `playAreaCells`(논리 격자 크기) · `gridOriginLocal`(셀 (0,0) 최소 모서리의 로컬 위치, Y=유닛 발바닥 평면) · `previewTileSize`(기즈모 전용 — 런타임 정본 `BattleBridge.tileSize`(1)와 같아야) · `suppressEffectTiles`(본편 false) |
| `SpawnMarker` (≥2) | 적 스폰 | `laneIndex`(0부터 연속·중복 금지 — 웨이브 결정론 키) · `routeIndex`(-1=골 직행 기본) · `visualRoot`(튜토리얼 포커스 앵커). **프랍은 저작하지 않는다** — 런타임에 공용 `MarkerPropStyle.spawnProp`(수직 빨간 포탈)이 붙는다. 맵 전용 연출만 `visualRoot` 를 직접 채운다(그쪽이 이김) |
| `GoalMarker` (≥1) | 골(방어 마음) | 셀만 준다 — 골 HP 는 `AttackDeck.goalStabilityMax` 단독 소유. `visualRoot` = 균열/붕괴/스트레스 틴트 대상(틴트는 머티리얼 저작 색에 **곱**). **프랍은 저작하지 않는다** — 공용 `MarkerPropStyle.goalProp`(수직 노란 포탈) |
| `PropFootprint` | 통행+배치 차단 | `size`(사각형만, 최소 1×1) · `anchorOffset`. **명시 선언이 정본(D6)** — 시각≠논리 저작 가능(가지가 3칸 드리워도 밑동 1칸만 차단) |
| `PlacementBlockZone` | 배치만 금지(통행 불변) | `size` — 앵커 셀부터 +x/+z. 옛 placeMask 브러시 후계, «전선» 저작 수단 |
| `RouteMarker` (선택) | 웨이포인트 경로 | `routeIndex`/`order` — 같은 route 를 order 오름차순으로 연결. `AttackUnitData.waypointPathIndex`/스폰 `routeIndex` 가 이 번호를 가리킴 |
| `BonusSpawnMarker` (선택) | 보너스 웨이브 포탈 칸 | 필드 없음 — 맵에 **0개 또는 정확히 2개**. 통행 가능하고 골에 닿는 서로 다른 칸. 없으면 그 맵엔 보너스 당기기 버튼이 뜨지 않는다(bonus-wave-pull 계약 8). 포탈 비주얼은 저작하지 않는다(런타임이 웨이브 수명으로 띄움) |
| `StructureMarker` (선택) | 거점 — **본능만** | `side`(Defender/Enemy) · `data`(`StructureData`, kind=Instinct). 3×3 footprint 는 **점유**(배치 배제·OccupiedCells)일 뿐 통행은 막지 않는다 — `PropFootprint` 를 겹치지 말 것. 프랍·체력·공격은 SO 소유, 브리지가 빌드 시 세운다(비주얼 자식 불필요). 마음(Core)은 계약 11 로 빌더가 거부 |

모든 마커는 **스테이지 프리팹 계층 안**에 있어야 스캔된다 (인스펙터가 밖이면 경고).

## 스폰/골 프랍 — 맵에 상관없이 공유

스폰/골 마커의 포탈 프랍은 스테이지 프리팹이 아니라 **`Assets/_Project/Data/Maps/MarkerPropStyle.asset`** 하나에서 온다. BattleScene 의 `_MarkerProps`(`MarkerPropInstaller`)가 스테이지가 켜질 때(`MapStage.Enabled`) `visualRoot` 가 빈 마커 밑에 프랍을 identity(수직)로 얹는다.

- 포탈의 색/모양을 바꾸려면 → `SpawnPortal_Red` / `GoalPortal_Yellow` 프리팹 또는 스타일 에셋의 슬롯을 바꾼다. 네 맵이 함께 바뀐다.
- 특정 맵만 다른 연출 → 그 프리팹에서 마커 밑에 프랍을 두고 `visualRoot` 를 채운다. 설치자는 채워진 마커를 건너뛴다.
- 라이브 풀 스테이지에 프랍을 내장하면 Assets lane(`MarkerPropStyleAssetTests.LivePoolStages_DoNotEmbedMarkerProps`)이 빨개진다 — 공유 구조를 반쪽으로 만들지 않기 위한 그물.
- 프리뷰(`RenderPrefabPreview`)도 같은 규칙으로 프랍을 얹어 «실제로 보일 그림»을 찍는다.

## 양자화 규칙

- 셀 = `floor((로컬위치 − gridOriginLocal) / tileSize)`. 셀 안 어디든 같은 셀이지만 **"셀 중심에 스냅" 버튼** 사용을 권장.
- footprint 점유 = 앵커 셀 + `anchorOffset` 부터 `size` 만큼. playArea 밖으로 걸친 부분은 무시(안쪽 셀만 차단).
- 열린 셀 = Walk + placeMask 7(Ground|Path|Air). 차단 셀 = Deco + 0. BlockZone 은 placeMask 만 0.
- 높이는 논리에 없다(D4) — 격자는 로컬 XZ 평면 하나.
- **좌표 관례 (2026-08-25)**: 프리팹 루트 = 원점·무회전·스케일 1(브리지가 런타임에 강제) **+ `gridOriginLocal.xz = 0`** — 격자는 항상 [0,w]×[0,h] 에 앉고 **아트를 격자에 맞춘다**(격자를 아트에 맞추지 않는다). "playArea 제안" 버튼은 크기만 제안하고 아트·마커를 함께 옮겨 이 관례를 유지한다. 결과: 전투 카메라 포즈가 `(playAreaCells, 화면비)` 만의 함수가 된다 — 격자 밖 남는 영역은 아트로 채운다.

## 형식 제약 (위반 = 배틀 진입 하드 실패, 오류 전수 목록 출력)

1. `playAreaCells` ≥ 1×1, `previewTileSize` == 런타임 tileSize(1)
2. 스폰 ≥ 2(멀티레인 계약), `laneIndex` 0..N−1 연속·중복 금지
3. 골 ≥ 1
4. 스폰·골·루트 셀은 **playArea 안 + 차단 위 금지** (강 위 공중 경유점도 현재는 거부 — 후속 후보)
5. `routeIndex` 0..P−1 연속(빈 번호 금지), (route, order) 유일, 스폰의 routeIndex 는 존재하는 루트만
6. **연결성: 모든 스폰 → 골 도달 가능** (`MapConnectivity`) — 차단 프랍으로 길을 막으면 실패
7. 보너스 포탈(`BonusSpawnMarker`)은 0개 또는 2개, 서로 다른 칸, 통행 가능, 골 도달 가능 — 규칙은 `BonusSpawnAuthoringRules`(bonus-wave-pull) 단일 소유
8. 거점(`StructureMarker`)은 `data` 필수 · kind=Instinct 만(Core 는 «계약 11» 오류) · 셀 중복 금지 · 중심 셀 playArea 안+차단 위 금지 · 3×3 footprint 전체가 playArea 안

## 절차 예시 — 12×8 마당 만들기

1. 빈 GameObject `MapStage_Courtyard` 생성 → `MapStage` 부착, `playAreaCells` = (12, 8).
2. Ground 비주얼(바닥 Plane + 머티리얼 등)을 자식으로 배치 → 인스펙터 **"자식 렌더러 바운즈에서 playArea 제안"** — 크기를 제안하고 아트를 격자 [0,w]×[0,h] 에 맞춰 옮긴다(원점 xz 는 0 유지). `gridOriginLocal.y` 만 바닥 윗면(유닛 발바닥) 높이로 저작.
3. 차단 프랍: 비주얼 배치 → `PropFootprint` 부착 → **"렌더러 바운즈에서 footprint 제안"** → **"셀 중심에 스냅"**. 기즈모 빨간 셀 = 차단 확인.
4. 전선(선택): 빈 오브젝트 + `PlacementBlockZone`, `size` 로 적 진영 금지 구역. 주황 셀 확인.
5. `SpawnMarker` 2개(laneIndex 0/1) + `GoalMarker` 1개 — 초록/골 셀이 열린 셀 위인지 확인.
6. (선택) `RouteMarker` 로 경유점 — R0.0, R0.1 … 순번 라벨 확인. (선택) `BonusSpawnMarker` 2개로 보너스 포탈 — 핑크 `B` 셀 확인.
7. 프리팹으로 저장 → 루트 인스펙터 **"Dev 엔트리로 등록 (MapStagePool)"**. dev 슬롯은 시드 선택에 안 잡히고 스테퍼(D 라벨)로만 진입.
8. 적 패턴은 풀 엔트리의 deck/plan 짝이 결정한다. "Dev 엔트리로 등록" 버튼은 **라이브 0번 엔트리의 덱**을 자동으로 물려주고, deck 을 비우면 BattleScene 의 BattleBridge `deck` 필드(현재 `Deck_Duel`)로 폴백한다 — 폴백은 기본 덱일 뿐이니 맵 전용 패턴이 필요하면 명시적으로 짝 지을 것.

## 검증 3단

1. **기즈모** — 씬 뷰에서 셀 색(빨강=차단, 주황=배치금지, 초록=스폰, 보라=루트, 핑크=보너스 포탈, 파랑/빨강 3×3 `I`=방어/적 본능)이 의도와 맞는지.
2. **Assets lane** (5초) — `StagePoolBuildabilityTests` 가 풀의 전 스테이지를 스캔→조립→연결성까지 검사. 등록 직후 이것부터.
3. **스테퍼 Play** — 로비 dev 스테퍼로 진입해 이동·배치·전투 육안 확인.
4. **테스트 씬 카메라** — 스테이지를 놓은 씬에서 `Window/Wassup/Map Stage/Frame Scene Camera As Battle`: 루트를 원점으로 정규화(브리지 계약과 동일)하고 런타임 산식(`CameraFramingMath.SolveStatePose` + Battle 레시피)으로 카메라 포즈를 푼다. 포즈는 **격자와 화면비의 함수**라 저장값은 스냅샷이다 — 격자·레시피·화면비가 바뀌면 버튼을 다시 누른다. fov 는 런타임과 같이 `fovMin/fovMax` 로 클램프한 값을 쓴다(현재 Battle 레시피 25 → 화면엔 31 — main 의 화각 수정이 되돌려진 상태, 디렉터와 동일하게 미러). **스테이지를 고친 뒤엔 반드시 다시 누른다** — 안 그러면 씬 카메라는 고치기 전 격자를 본다. DoF·포스트·디렉터 동역학은 씬이 재현하지 못하므로 최종 확인은 스테퍼 Play.

## 증상 → 원인

| 증상 | 원인 |
|---|---|
| 스폰/골 포탈이 **모든 맵**에서 안 보인다 | BattleScene `_MarkerProps.style` 미배선 또는 `MarkerPropStyle.asset` 슬롯 비어 있음 — 콘솔 `[MarkerPropInstaller] style 미배선` 경고. 러너 `marker_prop_style` 로 슬롯 재채움 |
| 배틀 진입 즉시 실패 + 형식 오류 로그 | 위 제약 1~5 위반 — 로그가 전수 목록을 준다 |
| "스테이지 연결성 실패" 로그 | 차단 프랍이 스폰→골 길을 완전히 막음 |
| 기즈모 격자와 실제 판정이 어긋남 | `previewTileSize` ≠ 1, 또는 `gridOriginLocal` 이 바닥과 안 맞음 |
| 프랍이 안 막음 | `PropFootprint` 미부착(비주얼만 있음), 또는 footprint 가 playArea 밖 |
| 배치가 안 되는 이유를 모르겠음 | BlockZone 겹침 — 주황 기즈모 확인 |
| 스테퍼에 새 맵이 안 보임 | Dev 등록 버튼 미실행, 또는 로비 `DevMapOverridePanel.pool` 미배선 |
| 효과 타일이 안 나옴 | `suppressEffectTiles` 가 켜져 있음 (테스트 스테이지 전용 플래그) |

## 주의

- **이름 충돌 금지**: PlayMode 테스트가 스테이지를 이름으로 pin 한다 — `Duel`(라이브 0번·구조물 검증)·`Street`(기본판 `DefaultMap`) 재사용 금지. 은퇴 이름(`Fixture`·`Pilot`·`Serpent`·`Coil`·`Zig`·`Tutorial`·`MovementLab`·`Ford`·`Isle`·`DuelClassic`)도 Ignore 된 테스트가 기억하므로 피한다.
- 본편 승격(시드 로테이션 편입)은 dev 슬롯이 아니라 풀 `entries` 에 deck/plan 과 함께 추가 — 웨이브 밸런스 확인 필수(`docs/reference/map-wave-balancing.md`).
- 공성·적 마음은 기능 비가용(계약 11) — 자리는 `PlacementBlockZone`+장식으로만 표현(예: `MapStage_Duel` 의 `enemy_heart`). 본능은 `StructureMarker` 로 가용(unit 10).
- ⚠ 생성기(`Generate Duel Stage` 메뉴)는 **프리팹을 통째로 덮어쓴다** — 프리팹이 정본이므로 손으로 고친 뒤에는 다시 누르지 말 것(메뉴는 확인 대화상자를 띄운다). 레이아웃을 바꾸려면 생성기 코드를 고치고 재생성하거나, 프리팹만 편집하고 생성기를 버린다 — 둘을 섞지 않는다.
- 절차 조립 예시 코드는 `Assets/_Project/Editor/MapStageDuelGenerator.cs`(Street 제작방식의 Duel — 바닥 Plane·스프라이트 프랍·마커·볼륨). 사용자 프리팹에 마커만 심는 도구는 `MapStageAuthoringTools.cs`(`AuthorSpawnsAndGoal`·`AuthorBonusPortals`). 원격 육안 검증은 `MapStageCameraFraming.RenderPrefabPreview`(Battle 카메라 포즈 PNG + 논리 셀 오버레이).
