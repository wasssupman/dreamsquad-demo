# 8. 레인이 기본 경로를 갖는다 (저작 축)

## 목적

**적이 전부 골 최단거리로 온다는 사실 자체가 전략을 지운다.** 최단거리 지식은 전 맵 공통이라 한 번 배우면 6맵에 다 통하고, 그래서 맵이 서로 달라지지 않는다. 경로 저작은 그 지식을 **맵 지식**으로 바꾼다 — Serpent 를 외워도 Coil 엔 안 통한다.

unit 3 이 만든 축은 **적 SO 가 경로를 고른다**(`AttackUnitData.waypointPathIndex`)라서 「이 종은 늘 옆길로 온다」는 되지만 「이 레인은 저 복도로 온다」가 안 된다. 그 결과 라이브에서 경로를 타는 적이 **Skimmer 하나뿐**이고 지상 12종은 전부 최단거리다.

이 unit 은 **스폰 지점이 기본 경로를 소유**하게 한다. D3 이 예고한 「index 기반 결정론(seeded RNG 아님)」의 답이고, `map-rework` D2 가 *「경로 개수는 1 유지 — 늘리려면 배정 규칙 필요」* 로 미룬 그 배정 규칙이다.

## 변경 대상

- `Assets/_Project/Scripts/Data/MapGrid/MapDocument.cs` — `spawnRoutes` 저작 배열
- `Assets/_Project/Scripts/Data/MapGrid/WaypointPath.cs` — `WaypointAuthoringRules` 에 술어 추가
- `Assets/_Project/Scripts/Data/MapGrid/MapDocumentBuilder.cs` — 런타임 투영 + `WriteToDocument`
- `Assets/_Project/Scripts/Data/GeneratedMap.cs` — 런타임 배열 + 조회
- `Assets/_Project/Editor/MapPainterWindow.cs` — 스폰별 경로 지정 UI
- `Assets/_Project/Tests/EditMode/MapGrid/WaypointPathAuthoringTests.cs` · `WaypointPathBakeTests.cs`

## 구현

### 저작 형태 — 스폰당 경로 인덱스 하나

```
MapDocument.spawnRoutes : int[]     // spawns 와 같은 순서, -1 = 최단거리(현행)
```

**병렬 배열인 이유**: `spawns` 는 이미 `Vector2Int[]` 평탄 배열이고 그 **순서가 곧 레인 번호**다 — `WavePatternGenerator.EffectiveSpawnIndex` · `PendingSpawnEntry.laneIndex` · 페인터 오버레이가 전부 같은 순서를 읽는다. 스폰을 구조체로 승격하면 그 세 곳이 함께 바뀌는데, 지금 필요한 값은 `int` 하나다(제약 8).

**길이 불일치는 에러가 아니라 폴백이다.** 배열이 짧거나 비면 그 레인은 `-1`. 이 필드가 없는 기존 맵 문서 11장이 그대로 서야 한다(계약 3 — 미저작 폴백 = 현행).

### 검증 — 순수 함수에 술어 2개

`ValidatePaths` 옆에 `ValidateSpawnRoutes(spawnRoutes, paths, spawns, errors, warnings)` — 경로 개수는 `paths.Count` 로 내부 계산한다(호출자가 두 값을 따로 넘기면 어긋날 수 있다):

| 판정 | 등급 | 이유 |
|---|---|---|
| 범위 밖 인덱스 | **에러** | 계약 9 — 조용한 골 직행 폴백이 「저작했는데 안 먹는」 상태를 만든다 |
| 두 레인이 같은 경로 | 경고 | 합류 저작일 수 있다. 막지 않는다 |
| 경로 첫 지점이 **다른 스폰에 더 가깝다** | 경고 | 가로지르기 징후 — 레인 1 적이 맵을 건너 레인 0 복도로 간다 |

세 번째가 이 unit 의 실질 가드다. 체비셰프 거리 비교라 plain 값 계산으로 끝나고, `ValidatePaths` 와 같은 «에러/경고 분리» 규약을 따른다.

**경계 3개는 «한 저작 실수 = 한 진단»으로 정한다** (구현 시 확정):

- **동률은 경고 아님** — `<` 로만 판정한다. 그리고 나보다 가까운 스폰이 여럿이어도 **가장 가까운 하나만** 지목한다. 저작자가 고칠 곳은 어차피 그 경로의 첫 지점 하나다.
- **범위 밖 인덱스는 거기서 멈춘다** — 에러를 낸 레인은 공유·가로지르기 검사를 건너뛴다. 두 레인이 똑같이 범위 밖이어도 «공유» 경고를 겹쳐 내지 않는다.
- **`spawns` 보다 긴 초과분은 「레인 없음」 경고만** — 대응 스폰이 없어 가로지르기를 계산할 수 없고, 인덱스 유효성까지 겹쳐 내면 실제 문제(배열 길이)가 묻힌다.

### 런타임 투영

`GeneratedMap.spawnRoutes : NativeArray<int>` + `int RouteForSpawn(int laneIndex)` — 미생성·범위 밖이면 `-1`. `goals`·`placeMask` 와 같은 이유로 `IsCreated` 불변식에는 넣지 않는다(직접 구성 픽스처 보호).

### 페인터

경로 브러시 바 하단에 **레인당 드롭다운 하나** (`레인 0 → [최단거리 / 경로 0 / 경로 1 …]`). 스폰 개수만큼만 그리고, `Bake` 가 `WriteToDocument` 로 넘긴다.

## 완료 기준

- **EditMode**
  - 필드 없는 기존 문서 11장이 전 레인 `-1` 로 읽힌다
  - 범위 밖 인덱스가 **에러**, 공유·가로지르기가 **경고**로 갈린다
  - 페인터 왕복(`Bake` 후 값 보존) — `WaypointPathBakeTests` 와 같은 형태
  - `RouteForSpawn` 이 미생성/범위 밖에서 `-1`
- **행동 변화 0** — 이 값을 읽는 런타임 코드가 아직 없다. 기존 EditMode 전량 초록이 증거다.
