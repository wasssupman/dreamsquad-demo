# 렌더링 · 에셋 · authoring

Spine, 타일맵 렌더, 프랍/VFX authoring, 카메라에서 겪은 함정.

## Spine 런타임은 3.8 고정 — 4.x 임포트 절대 금지

spine-unity 런타임은 **3.8 고정**(`Assets/Spine/version.txt` = `spine-unity-3.8-2021-11-10`). 게임 캐릭터(player-main·몬스터1·BellKnight·BellMage·DoubleWolf·FleshSwarmer·ForestWormBoss·HeartWolf·MutantShroom3·WolfLamb) skeleton 이 전부 3.8 export 이고 원본 `.spine` 소스는 repo·git 어디에도 없다(재-export 불가).

- **절대 금지**: Asset Store 등에서 최신 spine-unity(4.x) 임포트. `Assets/Spine` 을 in-place 덮어써 런타임이 4.3 이 되고 3.8 데이터를 못 읽어 캐릭터 전부 로드 실패. 3.8↔4.x 데이터 포맷 비호환.
- **잘못 임포트 시 복구**(검증됨): `git checkout HEAD -- Assets/Spine "Assets/Spine Examples"` → `git clean -nfd`(dry-run 범위 확인) → `-fd` → stale CS2001 남으면 `AssetDatabase.Refresh(ForceUpdate)` + asmdef `ImportAsset(ForceUpdate)` + `CompilationPipeline.RequestScriptCompilation()`.

## 한글명 Spine 에셋 macOS 임포트 깨짐 (3중 수정)

macOS 에서 한글명 Spine 에셋을 임포트하면 깨진다. 원인 3개 모두 고쳐야:

1. **NFC/NFD 정규화(핵심)**: Spine 이 쓴 이름은 NFC 인데 macOS 파일시스템은 파일명을 NFD 로 저장 → 텍스처 못 찾음("Material is missing texture"). 한글 파일명을 NFC 로 rename(`unicodedata.normalize('NFC', ...)`), 깨진 `_Atlas`/`_Material` 삭제 후 refresh 재생성.
2. **확장자**: `*.json.txt` 인식 안 됨 → `.json`(또는 `.skel.bytes`).
3. **버전 문자열**: 4.x→3.8 다운 export 시 `"spine":"3.8-from-4.0-..."` 가 3.8 파서를 죽임 → json 의 `"spine"` 필드를 `"3.8.99"` 로.

가능하면 파일명 영문으로 두면 NFC/NFD 자체 회피(정상 레퍼런스: `player-main`).

## 타일맵 격자선 원인 = 텍스처 압축

Tilemap(rect) 채움 타일이 큰 영역으로 반복될 때 셀 경계마다 격자선이 보이는 문제. **원인은 Compression**(압축 블록이 bilinear 샘플 시 경계 블리딩) — solid 단색 타일이어도 압축이면 격자가 남는다.

- **오답**: `FilterMode.Point`(격자는 사라지나 둥근 코너가 픽셀화).
- **정답**: `FilterMode.Bilinear` + `TextureImporterCompression.Uncompressed` + mipmap off.
- 1칸 폭 도로는 채움 반복이 없어 안 보여 오진하기 쉽다 — 큰 dirt/grass 에서만 드러남.

## dirt 오토타일 유기적 경계

박스형 원인 = 깨끗한 기하학적 타일(직선변/호가 격자 정렬). 자연스럽게:

- 모든 dirt 경계를 **타일링되는(주기=셀폭) 노이즈로 warp**(진폭 ~11px) → 인접 타일이 주기성 덕에 갭 없이 이어짐.
- inner/cross 케이스의 grass 노치는 **둥근 오목 곡선**(grass 1/4원)으로.
- 가장자리엔 선명한 cobble, 내부(mask 511)는 **flat 단색**(텍스처 fill 반복=격자 위험 회피).
- 분포: `ObstaclePlacer`(절차생성 = **실게임 맵에도 적용**됨, 주의). BFS 블롭 클러스터 + 시드 8-이웃 간격으로 작고 흩어진 패치. 사용자 선호 = 작은 유기적 패치(큰 연속 박스 ❌), zoom 스크린샷으로 검증.

## 타일맵 바닥은 tileSet 소관, 테마는 프랍만

전투 보드 두 렌더 경로가 테마를 다르게 쓴다:

- **Tilemap 모드(현 기본)**: 바닥을 `BattleBridge.tileSet`(`TileSetData` scene 필드)이 칠함(env/place/walk/deco/terrainTile + surroundFarColor). **`MapThemeData`/`SeasonData` 의 tile 텍스처/틴트는 여기서 inert** — 테마는 프랍만 구동.
- **레거시 MapView**: 여기서만 `MapThemeData` 의 envTileTexture/surfaceRules 가 바닥에 쓰임.
- 새 테마의 **바닥**을 바꾸려면 전용 `TileSetData` 필요. 테마별 선택은 **`MapThemeData.tileSet` 훅**(`theme.tileSet ?? scene tileSet`, 커밋 5ebe315). "테마만 바꾸면 바닥이 바뀐다"는 오답.

## 배틀 카메라는 페이즈마다 pitch 가 바뀐다

BattleScene Tilemap 모드 Main Camera 는 **런타임 정적이 아니다**. 실측: **Draft pitch 40° / z=−10.44**, **Battle pitch 58° / z=−7.85**. `ApplyTilemapCameraPreset()` 호출이 주석 처리돼 있어도 페이즈 전환이 카메라를 다시 움직인다.

- **처방**: 카메라 pitch/거리 의존 값(빌보드 틸트·그림자·framing)은 **스폰 시 1회 bake 금지, 라이브 재계산**(또는 최소 페이즈 전환 시 재계산). 코드만 믿지 말고 Play 에서 여러 페이즈에 걸쳐 측정.

## 프랍이 눕거나 묻히면 = authoring 값, 배치 로직 아님

프랍이 게임뷰에서 눕거나 바닥에 깔리면 원인은 billboardMode(FullCamera→`Tilted`)·sprite pivot(Center→`BottomCenter`) — 상세와 강제 도구는 `.claude/skills/unity-prop-tile-authoring/SKILL.md`. 여기 남길 진단 지식 하나: **visualOffset 은 접지 수단이 아니다** — 프랍은 90°X 회전 root 아래라 local +Y 가 월드 +Z(깊이)로 새서 수직으로 안 올라간다. 피벗을 고치면 visualOffset=0 이 정답.

## 프랍/타일 authoring 은 스킬 먼저 로드

코드베이스에 구세대(PPU 545, 프랍별 `_cast` mat)와 신세대(공용 mat) 패턴이 공존해 **"기존 애셋 미러링"이 그럴듯하지만 틀린 경로**다. 정식 파이프라인·강제 값은 `.claude/skills/unity-prop-tile-authoring/SKILL.md`.

## 벤더 투사체 VFX 를 ECS 파이프라인에 넣을 때

벤더 VFX(예: GabrielAguiar)를 `ProjectileViewPool`(ECS SyncTransforms 가 transform 구동)에 넣을 때 view-only 로 스트립, 3가지 필수:

1. **제거**: 무버 스크립트 + `Rigidbody` + `Collider`(안 떼면 물리가 SyncTransforms 를 이겨 제멋대로 날아감).
2. **`TrailRenderer.autodestruct = false`**(풀링 재사용 GO 가 트레일 만료 시 자가파괴 → **정적 분석 안 보이고 Play 에서만 드러남**).
3. **`ParticleSystem.emitterVelocityMode = Transform`**(RB 없이 velocity 시각 유지).
- 색 보존은 `ProjectileData.preserveVfxColors=true`. 스트립 툴 `GaProjectileStripper.cs`.
- **교훈**: 시각 통합은 예측 수정을 쌓기 전에 **Play 스크린샷 테스트 먼저**(진짜 필수였던 autodestruct 는 런타임만 잡음).

## 드래그 배치 프리뷰 튜닝 위치

- sway 튜닝값 = **`Assets/_Project/Data/Config/DragSwaySettings.asset`**(SO, Play 중 실시간 반영). 코드 아님.
- **왜 SO 인가**: `DefenderDragPlacementController` 가 런타임 `AddComponent` 로 붙어 씬 인스펙터 인스턴스가 없음 → SerializeField 튜닝 불가 → SO 주입. 이런 런타임-부착 MonoBehaviour 는 전부 같은 패턴.
- sway 모델 = velocity-lean(목표각 ∝ 포인터 속도), 피벗 = 머리 위. 프리뷰 정렬 `BoardSortOrder.DragPreviewOrder=20000`.

## 평면 보드에서 sim-Y 는 화면 높이가 아니다

`BoardSpace.ToView`(`Scripts/Core/BoardSpace.cs`)는 sim 의 XZ 만 셀로 매핑하고 **`simWorld.y` 를 완전히 버린다**(평면 tilemap 정책).

- **함정**: 투사체 arc·유닛 높이를 sim-Y(`LocalTransform.Position.y`)에 실으면 화면 반영 0(곡사포가 arc 없이 미끄러짐).
- **정답**: 높이는 **presentation 층**에서 — `ProjectileViewPool.SyncTransforms` 가 `view.y` 에 `heightOffset`/arc 를 더하는 패턴. sim(ArcPosition)/AOE(셀 XZ)/타이밍은 sim-Y 무관이라 그대로. (프랍 "90°root라 +Y가 깊이로 샘"과 같은 뿌리.)
