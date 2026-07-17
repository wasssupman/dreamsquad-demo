# 렌더링 · 에셋 · authoring

Spine, 타일맵 렌더, 프랍/VFX authoring, 카메라에서 겪은 함정.

## Spine 런타임은 4.2 고정 — export 는 Spine Editor 4.2.xx 만

spine-unity 런타임은 **4.2 고정**(2026-07-07 업그레이드, `Assets/Spine/package.json` = 4.2.120, spec: `docs/spec/spine-runtime-4-2-upgrade/`). 스켈레톤 데이터는 **major.minor 가 일치하는 런타임에서만 로드**된다 — 4.2 export ↔ 4.2.xx 런타임만 성립, 패치 버전만 상호 호환. 다른 버전 런타임 임포트로 덮어쓰지 말 것.

- 3.8 시절 리소스(player-main·몬스터1·BellKnight 등)는 원본 `.spine` 부재로 재-export 불가라 **전량 퇴역**했다(커밋 b758aa29). 신규 리소스는 반드시 원본 `.spine` 을 함께 보존한다 — 그래야 향후 4.3+ 업그레이드가 재제작이 아닌 재-export 로 끝난다.
- **잘못 임포트 시 복구**(3.8 시절 검증, 동일 원리): `git checkout HEAD -- Assets/Spine "Assets/Spine Examples"` → `git clean -nfd`(dry-run 범위 확인) → `-fd` → stale CS2001 남으면 `AssetDatabase.Refresh(ForceUpdate)` + asmdef `ImportAsset(ForceUpdate)` + `CompilationPipeline.RequestScriptCompilation()`.
- **배치 모드 `-importPackage` 는 컴파일 에러 상태에서 abort** 된다("Scripts have compiler errors"). 런타임 폴더를 지운 뒤 재임포트하는 업그레이드 경로는 배치로 불가 — unitypackage 를 tar.gz 로 직접 추출해 pathname 대로 배치하면 GUID/meta 보존 동일 결과 (4.2 업그레이드에서 실증).

## Spine 4.2 신규 리소스 수급 규약

신규 스켈레톤을 제작/외주/구매로 들일 때의 체크리스트 (spec: `docs/spec/spine-runtime-4-2-upgrade/3_new_asset_conventions.md`):

1. **Export**: Spine Editor **4.2.xx** 만. 바이너리(`.skel`) 권장, JSON 은 디버깅용. major.minor 불일치 데이터는 로드 불가.
2. **원본 `.spine` 보존(필수)**: `art/spine/{SkeletonName}.spine` 으로 repo 에 커밋. 외주/구매 시 원본 포함을 계약 조건에 넣는다. 3.8 리소스 전량 폐기의 근본 원인이 원본 부재.
3. **확장자 rename**: `.skel` → `.skel.bytes`, `.atlas` → `.atlas.txt` (임포터 인식 조건. 3.8 시절 8종이 rename 누락으로 임포트 실패 전례).
4. **파일명 ASCII 만**: 한글명은 위 NFC/NFD 함정 직행.
5. **텍스처/알파**: PMA export 기본, Unity 텍스처 설정(sRGB, Alpha Is Transparency 끔)과 일치. `SpineUnitView` 사망 페이드가 PMA 전제(`Skeleton.A` 직접 조작).
6. **rig 방향**: "ScaleX=+1 에서 -x(왼쪽) 바라봄" 관례. 어기는 rig 은 SkeletonData 의 `skeletonDataModifiers` 에 `Assets/_Project/Characters/SkeletonFlipX.asset` 부착.
7. **배치 위치**: `Assets/_Project/Characters/{SkeletonName}/` 폴더 단위.
8. **임포트 검증**: `_SkeletonData`/`_Atlas`/`_Material` 자동 생성 → 프리뷰 애니 재생 → 콘솔 경고 0.

## 한글명 Spine 에셋 macOS 임포트 깨짐 (3중 수정)

macOS 에서 한글명 Spine 에셋을 임포트하면 깨진다. 원인 3개 모두 고쳐야:

1. **NFC/NFD 정규화(핵심)**: Spine 이 쓴 이름은 NFC 인데 macOS 파일시스템은 파일명을 NFD 로 저장 → 텍스처 못 찾음("Material is missing texture"). 한글 파일명을 NFC 로 rename(`unicodedata.normalize('NFC', ...)`), 깨진 `_Atlas`/`_Material` 삭제 후 refresh 재생성.
2. **확장자**: `*.json.txt` 인식 안 됨 → `.json`(또는 `.skel.bytes`).
3. **버전 문자열**(3.8 시절 이력): 4.x→3.8 다운 export 시 `"spine":"3.8-from-4.0-..."` 가 3.8 파서를 죽였음 → json 의 `"spine"` 필드를 수동 수정했던 사례. 4.2 체제에서는 다운 export 자체를 하지 않는다.

가능하면 파일명 영문으로 두면 NFC/NFD 자체 회피(정상 레퍼런스: `player-main`).

## 타일맵 격자선 원인 = 텍스처 압축

Tilemap(rect) 채움 타일이 큰 영역으로 반복될 때 셀 경계마다 격자선이 보이는 문제. **원인은 Compression**(압축 블록이 bilinear 샘플 시 경계 블리딩) — solid 단색 타일이어도 압축이면 격자가 남는다.

- **오답**: `FilterMode.Point`(격자는 사라지나 둥근 코너가 픽셀화).
- **정답**: `FilterMode.Bilinear` + `TextureImporterCompression.Uncompressed` + mipmap off.
- 1칸 폭 도로는 채움 반복이 없어 안 보여 오진하기 쉽다 — 큰 dirt/grass 에서만 드러남.

## `Mathf.SmoothStep(from, to, t)` 는 HLSL `smoothstep` 이 아니다 — 절차적 스프라이트가 유령이 된다

Unity `Mathf.SmoothStep(from, to, t)` 는 **결과를 `from..to` 로 보간**한다(t 를 0..1 로 clamp 후 smooth). HLSL `smoothstep(edge0, edge1, x)`(x 가 두 엣지 사이 어디냐로 0..1 반환)와 인자 의미가 정반대다.

절차적 텍스처에서 가장자리 페더로 `SmoothStep(0f, 0.06f, dist)` 를 쓰면 **알파가 0.06 에 캡핑**돼(두 축을 곱하면 ~0.004) 스프라이트가 거의 투명 — "유령"이 된다. Play 에서만 드러나고 EditMode/컴파일은 통과한다.

- **오답**: `Mathf.SmoothStep(0f, band, dist)` — band(페더 폭)를 출력 범위로 넣음.
- **정답**: `Mathf.SmoothStep(0f, 1f, dist / band)` — band 를 **입력** 정규화에 쓰고 출력은 0..1.
- 이 프로젝트에서 두 세션이 독립적으로 같은 실수(블롭·조준 화살표) — 절차적 스프라이트(`TilemapMapView` 의 Blob/Arrow/Pop 계열) 만들 때 반복되는 지뢰.
- **static 스프라이트 캐시 주의**: `_arrowSprite` 등이 static 이라 산식을 고쳐도 이전 텍스처가 남는다. 실측하려면 리플렉션으로 필드를 null 로 밀고 재생성.

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

## PixPlays 이펙트는 duration 을 무시한다 (지속형은 loop 오버라이드)

`LocationVfx.Play(VfxData)` 는 위치/스케일만 잡고 `ParticleSystem.Play()` 한 번 — **VfxData.Duration 을 소비하지 않는다**. PixPlays AOE 계열(WaterAOE 등)은 "t=0 버스트 + 수명 0.6~2s + 사이클 5s"로 저작돼 있어 지속 스킬(포탈 8s)에 붙이면 앞 1~2초만 보이고 공백. **처방**: 소비 프리팹의 **중첩 인스턴스에만 오버라이드**(공용 에셋 무접촉) — 연속 계열에 `loop=true` + `duration≈startLifetime`(버스트 연속 재발화), 버스트 개수는 모바일 예산으로 감축. Flash 류는 캐스트 액센트로 원샷 유지. 수명 정리는 루트 GO 의 `Destroy(duration)` 에 위임. 검증은 에디트 모드 `Simulate(사이클 중간 t)` 파티클 카운트(0=공백 증명).

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

## 머리 위 뱃지를 월드 +Y 로 띄우면 외곽 타일에서 바깥으로 밀린다

배틀 카메라는 **원근**(`CameraPreset_TilemapRect`: `orthographic: 0`, FOV 40)에 pitch 55°다. 이때 월드 up 은 카메라 공간에서 `(0, cosθ, -sinθ)` 로 분해된다 — 즉 뱃지를 `basePos + Vector3.up * h` 로 띄우면 **위로만 가는 게 아니라 카메라 쪽으로 당겨진다**. `view_z` 가 `h·sinθ` 만큼 줄고 `screen_x = f·view_x/view_z` 이므로 화면 x 가 그만큼 **확대**된다.

- **증상**: 유닛 머리 위 아이콘이 화면 중앙에서 멀수록 좌우로 밀려 보인다. 오프셋 2.6 · 보드 끝에서 **≈57px@1080w**(화면 폭의 5%). 중앙 유닛은 `view_x≈0` 이라 멀쩡해서 "UI 레이어 문제인가?" 로 오진하기 쉽다 — **레이어와 무관하다**(문제의 뷰들은 이미 월드 SpriteRenderer 였다). 오프셋에 비례하므로 작은 값(히트바 1.0)은 티가 안 나 수년 잠복 가능.
- **정답**: 오프셋을 **카메라 평면**에서 적용 — `HeadAnchor.Lift(basePos, offset, cam)`(`Scripts/Presentation/HeadAnchor.cs`). 카메라 up 은 시선축과 직교라 `view_z` 가 안 변해 어느 타일이든 같은 화면 거리를 유지하고, 페이즈별 pitch 변화(Draft 40°↔Battle 55°)에도 높이가 `cosθ` 로 안 흔들린다.
- **값 이전 시 등가식**: `k = h·cosθ·view_z/(view_z − h·sinθ)` (55°/23u 기준 ≈ **0.63배**). 월드 기준으로 눈 튜닝한 값을 그대로 옮기면 뱃지가 너무 높이 뜬다. 실적용: DcIconStrip 2.6→1.64 · StatusFx 1.5→0.91/2.2→1.37 · HitBar 1.0→0.60 · DmgNum 1.4→0.85/driftUp 0.7→0.41.
- **경계**: **billboard 여부가 기준**이다. 화면을 보는 뱃지 → 카메라 평면. 바닥에 눕힌 데칼(`TileHealthGaugeView`, Euler 90 BlobShadow 규약)의 z-fighting 리프트 → **월드 up 유지**(카메라 평면 적용하면 바닥에서 들림).
- **동반 함정**: 위치를 카메라 회전에 묶는 순간 **실행 순서가 정답의 일부가 된다**. `CameraDirector`(`[DefaultExecutionOrder(-90)]`)가 **LateUpdate** 에서 포즈를 확정하므로, `Update` 에서 위치를 잡으면 지난 프레임 회전을 읽어 **위치만 1프레임 뒤처진다**(회전은 LateUpdate 라 최신 → 카메라 이동 중 뱃지가 유닛에서 미끄러짐). 위치·회전 **둘 다 LateUpdate** 로.
- **검증법**: Play 없이 프리셋대로 카메라를 재구성(`Quaternion.Euler(55,0,0)`, `dist = radius/sin(fov/2)*1.12`)해 `WorldToScreenPoint` 로 발밑 대비 뱃지 dx 를 x=-5~+5 스윕하면 즉시 드러난다. 수정 후 전 구간 0.00px. (커밋 `d815bf59`)

## 런타임 `Shader.Find` 는 빌드에서 스트리핑된다 (에디터만 정상)

`Shader.Find` 는 **빌드에 포함된 셰이더만** 찾는다. 셰이더가 빌드에 들어가는 경로는 셋뿐이다:

1. **씬/프리팹의 머티리얼**이 참조 (전이 참조 포함 — SO 경유도 OK)
2. **`Assets/Resources/` 아래** 에셋이 참조 (무조건 포함)
3. **GraphicsSettings > Always Included Shaders** 등록

런타임에 `new Material(Shader.Find("..."))` 로만 쓰는 커스텀 셰이더는 **1·2 경로가 없어서 통째로 제거**된다.
에디터는 모든 셰이더가 살아있어 정상 → **"에디터는 되는데 빌드만 효과 없음"** 이 된다.

- **더 나쁜 건 조용하다는 것**: 대개 `if (sh == null) return;` 식 graceful 폴백이라 **에러도 로그도 없이 연출만 사라진다**. 실제로 이 상태로 출시됐다(2026-07-15: 배치 컷신 뎁스 패럴랙스). 카드 구김(`CardCrumple`)·포일(`DraftCardFoil`)도 같은 이유로 죽어 있었고 아무도 몰랐다. **Always Included 등록 후 모바일 재빌드로 해결 확인됨.**
- **`Tile_Unlit`**: `RuntimeMaterialFactory` 의 폴백 체인(`?? URP/Unlit ?? ...`)이 있어 맵은 그려졌지만 **의도한 셰이더가 아닌 내장으로 대체** — 에디터와 모바일이 다르게 렌더되고 있었다. 폴백이 있으면 더 안 들킨다.
- **정답**: 런타임 `Shader.Find` 대상은 **Always Included Shaders 에 등록**. 고아 머티리얼을 씬에 매달아두는 방식은 누가 떼면 재발해서 취약하다.
- **폴백엔 반드시 경고를 남길 것**. 조용한 폴백이 이 버그를 출시까지 보낸 원인이다.
- **진단 팁**: 컷신 프레임은 나오는데 셰이더 효과만 없다 → 같은 SO 가 참조하는 텍스처/프레임은 빌드에 있다는 뜻 → **셰이더만 없는 것** = 스트리핑 확정.
- **감사 방법**(오진 주의): 셰이더 GUID 를 `Assets` **전 타입**에서 grep. `.unity`/`.prefab` 만 보면 오진한다 — `Solid_Unlit` 은 `Assets/Resources/RuntimeMaterials/SolidOpaque.mat`(Resources 경유)로 이미 안전한데 씬만 보면 위험으로 잘못 잡힌다.

## 비활성 계층에서 만든 TMP 는 Awake 가 안 돌아 줄바꿈·측정이 깨진다

UGUI 위젯을 **비활성 루트 밑에** lazy 생성하면(`root.SetActive(false)` 상태에서 `AddComponent<TextMeshProUGUI>()`), 비활성 오브젝트는 **Awake 가 호출되지 않는다**. TMP 는 Awake 에서 `TMP_Settings` 기본값을 로드하므로, 그 전 상태의 TMP 는 **두 가지가 동시에 깨진다**:

1. **`textWrappingMode` 가 enum 기본값 `0`(=`NoWrap`) 으로 남는다.** 정상 기본값은 `Normal`(TMP_Settings 로부터). → 본문이 rect 폭을 무시하고 **패널 밖으로 흘러나간다**.
2. **폰트 스케일이 서기 전이라 `GetPreferredValues` 가 정답의 ≈1/10 을 답한다.** 실측(2026-07-15): 헤더 `2.21` vs 정답 `22.0`, 본문 `6.96` vs `69.5`, `14.56` vs `145.6`. → 그 높이로 다음 요소를 배치하면 **텍스트가 서로 겹치고**, 행 높이가 내용에 반응하지 않고 고정값처럼 보인다.

- **왜 안 걸렸나**: 손패 드래그 툴팁(`DreamcatcherHandView.BuildTooltip`)은 `BuildCanvas()` 에서 **활성 상태로 미리** 만들어 두고 `ShowDragTooltip` 에서 재사용하므로 이 함정을 우회한다. **lazy 생성으로 바꾸는 순간 밟는다.**
- **처방 2점**: (a) `textWrappingMode = TextWrappingModes.Normal` 을 **명시**한다(기본값에 의존 금지). (b) 측정 전에 루트를 `SetActive(true)` 하고, 텍스트 대입 후 `ForceMeshUpdate()` 로 레이아웃을 확정한 뒤 측정한다.
- `enableWordWrapping` 은 **Obsolete** — `textWrappingMode`(`NoWrap`/`Normal`/…) 를 쓴다.
- 활성화를 앞당기면 `if (!_root.activeSelf)` 로 "처음 뜨는가"를 판정하던 코드가 무너진다 → `wasHidden` 같은 플래그로 분리.

## `EventSystem.IsPointerOverGameObject()` 는 지난 프레임 상태다 — 실행 순서가 −면 터치에서 무너진다

`InputSystemUIInputModule` 의 이 API 는 **`EventSystem.Update` 가 세운 pointer 상태**를 읽는다(음수 id → `m_PointerStates[..].eventData.pointerEnter`). 패키지 주석이 명시한다: *"calling this method earlier than that in the frame will make it poll state from **last frame**"*.

`EventSystem` 은 `[DefaultExecutionOrder]` 가 **없어 순서 0** 이다(`ProjectSettings/MonoManager.asset` 에 커스텀 오버라이드도 없음). 따라서 **음수 실행 순서의 입력 핸들러는 항상 EventSystem 보다 먼저 돌아 지난 프레임 상태를 본다.**

- **증상**: **마우스는 멀쩡, 터치만 깨진다.** 마우스는 hover 로 pointer 상태가 상시 유지돼 지난 프레임 값이 맞지만, **터치는 hover 가 없어 press 프레임에 pointer 상태 자체가 없다** → `stateIndex = -1` → `false`. 즉 손가락이 버튼/트레이 위에 있어도 **가드가 통과해 그 뒤 보드가 눌린다**. 에디터에선 **절대 재현되지 않는 Android 전용 결함**이다.
- **`PlacementInput.cs:63~65` 를 선례로 삼지 말 것**: 같은 패턴을 쓰지만 클릭 배치가 은퇴(`clickPlacementEnabled=false`)해 실전 검증된 적이 없다. "기존 코드가 그러니 괜찮다"가 성립하지 않는 자리다.
- **처방**: 실행 순서와 무관한 **즉석 UI 레이캐스트**로 대체한다. press 때만 도는 경로라 비용도 무시할 만하다.
  ```csharp
  var es = EventSystem.current;
  _uiHits.Clear();
  es.RaycastAll(new PointerEventData(es) { position = screenPos }, _uiHits);
  bool overUi = _uiHits.Count > 0;
  ```
- **딸림 효과**: `raycastTarget=false` 인 위젯은 이 레이캐스트에 안 걸린다 → 그 위젯 위 탭은 "빈 보드"로 취급된다. 패널을 탭해서 닫는 동작을 원하면 의도대로지만, 원치 않으면 배경만 `raycastTarget=true` 로 둬야 한다.
- **음수 순서를 포기할 수 없는 이유가 보통 있다**(예: aim-mode race 를 이기려고 −50). 그러니 "순서를 0으로 되돌린다"가 아니라 **가드를 순서-무관하게 만드는 게** 정답이다.

## `CanvasGroup.alpha = 0` 은 숨기지 않는다 — 보이지 않는 채로 입력을 계속 먹는다

`alpha = 0` 은 **화면에서만** 지운다. `blocksRaycasts` 는 그대로라 그 위젯이 덮는 화면 영역이 통째로 **투명한 입력 벽**이 된다. 눈에 안 보이므로 "왜 여기만 안 눌리지?" 로만 나타난다.

- **실제 사례(2026-07-15)**: `WavePatternStripView.SnapHidden` 이 header/cardGrid 를 알파로만 숨겨, Draft 가 끝난 뒤에도 카드 그리드가 화면 **x[24~1624] y[430~590]** 를 계속 막았다. 증상은 "배치 유닛을 탭할 때 **머리는 눌리는데 몸통은 실패**" — 유닛이 띠 경계(y=590)에 걸치면 위는 통과, 아래는 차단이기 때문. **유닛 픽킹 버그로 오진하기 딱 좋다**(픽킹은 정상이었다: 렉트 161x101px 가 스프라이트를 정확히 덮었다).
- **왜 오래 안 걸렸나**: 보드 raw 탭 소비자가 없었다(클릭 배치 은퇴, 카드 드래그는 자체 픽킹). 첫 소비자(`DcInspectController`)가 생기고서야 드러났다. **입력 소비자가 없는 동안 이런 벽은 조용히 쌓인다.**
- **처방**: 숨김/표시와 raycast 를 **한 메서드에 묶는다**. 알파만 되돌리고 플래그를 잊으면 반대 방향 버그(끈 채 표시 → 안 눌림)가 난다. 또는 `SetActive(false)`(같은 파일의 `_overlayGroup` 이 이미 그렇게 한다).
- **진단법**: 의심 지점에서 `EventSystem.RaycastAll` 을 찍어 무엇이 잡히는지 본다. 화면을 격자로 훑어 차단 지도를 그리면 띠가 즉시 보인다.
  ```csharp
  // 알파 0 인데 입력 먹는 그래픽 전수 조사
  foreach (var g in FindObjectsByType<Graphic>(FindObjectsSortMode.None))
      if (g.raycastTarget && g.gameObject.activeInHierarchy && g.color.a < 0.05f) Debug.Log(g);
  // CanvasGroup 쪽
  foreach (var cg in FindObjectsByType<CanvasGroup>(FindObjectsSortMode.None))
      if (cg.alpha < 0.05f && cg.blocksRaycasts) Debug.Log(cg);
  ```
- **주의**: 알파 0 + raycastTarget 이 **의도적인** 경우도 있다(투명 히트 영역). `DefenderSelector` 의 `Slot_*` 이 그렇다 — 일괄로 끄지 말 것.
