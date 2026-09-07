# 9 — 「발견」 표식 저작 (unit 5 가 미룬 화면 몫)

## 목적

unit 5 는 채널·트레이스·전이 판정까지만 닫고 **화면을 미뤘다.** 사유는 저작물 부재였다 —
이 프로젝트의 머리 위 팝업은 `DamageNumberSpawner.Spawn(pos, **float**)` 하나뿐이라 **숫자만**
그리고, `Assets/_Project/VFX/` 에 경보·표식 계열 프리팹이 **0건**이었다. 그래서 `VfxSpawner` 에
슬롯(`detectionMarkPrefab`)만 파고 **미할당 = no-op** 으로 두었다.

이 unit 이 그 슬롯을 채운다. 「저 놈이 내 유닛을 봤다」를 0.5초 안에 읽히게 만든다.

## 사용자 결정 (2026-09-07)

**「!」 팝업 + 몸 플래시 링.** 관습 기호와 기존 오라 어휘를 **겹친다** — 밀집 전투에서 둘 중
하나는 읽히게 하려는 것이고, 어느 하나만으로는 각자의 약점이 있다:

- 몸 플래시만 → 상태이상 오라(출혈·화염·빙결·독) 4종과 **같은 자리**라 묻힌다.
- 「!」만 → 새 기호 하나에 전부를 건다.

「대상을 가리키는 선」은 **채택하지 않았다.** 무제한 감지(보스 3종 + 꿈조각)는 공용 사냥판을
타서 감지 대상과 이동 도착지가 갈리므로, 그 표기는 **거짓말이 된다**(unit 5 계약 6).

⚠ 색은 **경보 노랑~주황 `(1.00, 0.72, 0.10)`**. 스폰 예고 라인의 빨강 `(1, 0.16, 0.12)` 과
**분리한다** — 예고는 「올 것」이고 표식은 「이미 봤다」라서, 같은 색이면 두 사건이 섞인다.

### 무제한 감지도 표식을 낸다 — **그대로 둔다** (사용자 결정 2026-09-07)

무제한 감지(보스 3종 + `DreamShard`)는 반경 판정을 건너뛰므로, 판에 때릴 수 있는 방어유닛이
있고 갈 수만 있으면 **등장하자마자** 사냥 상태가 켜져 표식이 1회 뜬다. 「발견」이라기보다
**「사냥 개시」**로 읽히는 자리이고, 보스 등장 워닝과 겹칠 수 있다.

**표식을 유한 반경 전용으로 좁히지 않는다** — 사용자 결정이다(대안이었던 ⓑ「무제한 제외」는
`:443` 조건에 한 줄이면 되지만 채택하지 않았다). 다음 사람이 이걸 결함으로 보고 좁히지 말 것.
재검토하려면 근거는 **화면**이어야 한다(워닝과 실제로 겹쳐 읽히는가).

## 변경 대상

- `Assets/_Project/VFX/Textures/DetectionMark_Bang.png` (신규, 256², 무압축)
- `Assets/_Project/VFX/Textures/DetectionMark_Ring.png` (신규, 128², 무압축)
- `Assets/_Project/VFX/Materials/DetectionMark_Bang.mat` · `DetectionMark_Ring.mat` (신규)
- `Assets/_Project/VFX/DetectionMark_SKELETON.prefab` (신규)
- `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` — `DetectionMarkOrder` 대역 등록
- `Assets/_Project/Scripts/Presentation/VfxSpawner.cs` — 슬롯 null 을 `LogError` 로 승격
  (리뷰 HIGH-1: unit 5 의 「미할당 = 정상」 예외가 이 unit 으로 소멸) + 필드 주석 2곳
- `Assets/_Project/Tests/EditModeAssets/DetectionMarkVfxTests.cs` (신규 5건)
- `Assets/_Project/Scenes/BattleScene.unity` — `VfxSpawner.detectionMarkPrefab` 할당

## 구현

**2층 구조.** `BodyFlash`(가산 링, 몸통) + `Bang`(알파블렌드 「!」, 머리 위).

**⚠ `ConfigureOneShot` 을 견디게 저작한다.** `VfxSpawner.SpawnDetectionMark` 은 스폰 인스턴스에
`ConfigureOneShot` 을 태우고, 그것이 `emission` 을 **t0 버스트 1발로 덮어쓴다** — 개수는
`clamp(max(1,duration) × max(1,rate), 4, 24)` 라 **최소 4개**다. 실측 lifetime = **0.95s**
(Destroy 타이머), 버스트 4/4. 그래서 **자리**는 겹쳐도 흩어지지 않게 만든다 — shape 비활성
(산포 0) · 크기 산포 좁게(1.00~1.06).

⚠ **그러나 「겹쳐도 동일」은 아니다 — 겹침은 «색»을 바꾼다.** 두 층 모두 영향을 받는다:

- **가산 링**은 기여가 **선형 4배**다. `startColor.a = 0.62 × 4 = 2.48` 이라 피크에서
  `(1, .72, .10) × 2.48` → 클램프 후 **(1, 1, 0.25)** = 흰-노랑 **포화**. 즉 링의 가장 밝은 띠에는
  「경보색 축」이 사실상 남지 않는다(색은 텍스처 알파가 낮은 **가장자리**에만 남는다).
- **알파블렌드 「!」**의 커버리지는 `a` 가 아니라 **`1−(1−a)⁴`** 다. 꼬리 페이드가 저작값보다
  **단단하게 끊긴다**(저작 알파 0.2 지점의 실제 화면 ≈ 0.59).

**현재 값은 「4겹 상태의 렌더를 보고」 정했다** — 오프스크린 검증이 `ConfigureOneShot` 을 태운
실제 인스턴스를 찍기 때문이고, 링 알파를 0.45 → 0.62 로 **올린** 이력이 그 증거다.
따라서 **버스트가 4에서 벗어나면 이 값들은 전부 다시 잡아야 한다.** 그 4를
`DetectionMarkVfxTests.스포너가_강제하는_버스트는_최소값_4에_머문다` 가 지킨다.

**아웃라인은 텍스처에 굽는다.** 글리프 RGB 를 「흰 코어 → 어두운 림」으로 그려 두고 StartColor 로
**곱해서** 틴트한다. 곱셈이 명암 관계를 보존하므로 코어는 경보색, 림은 어두운 테두리가 된다 —
밝은 맵(사막·눈)에서도 기호가 배경에 녹지 않는다. 셰이더 신작 0.

**정렬 = 유닛 위.** 빔·궤적·브레스와 같은 판단이다(표식이 유닛에 잘리면 「누가 봤나」를 못 읽는다).
피격바(16000) 위 · 드래그 프리뷰(20000)·데미지 숫자(32000) 아래.

**링의 자리는 lift 와 짝이다.** 스포너가 루트를 `detectionMarkLift`(0.9) 만큼 올려 머리 위에
놓으므로, `BodyFlash` 는 `localPosition.y = -0.55` 로 되내려 몸통에 온다. **lift 를 바꾸면 이
값도 같이 본다.**

## 파이프라인 커버리지

`docs/reference/object-pipeline-map.md` 의 **`VFX (one-shot)`** 아키타입 대조. README 의 커버리지
절이 *「unit 5 의 표식만 연출 정거장을 새로 쓴다 — 그 문서에서 다룬다」* 로 위임했는데 unit 5 는
저작물이 없어 표를 못 썼다. **그 위임이 여기서 끝난다.**

| 정거장 | 이 unit 의 실현 | 확인 |
|---|---|---|
| 프리팹 소스 | `VfxSpawner.detectionMarkPrefab` SerializeField 슬롯 (SO 아님) | **슬롯 null → `LogError`, 코드 폴백 없음.** ⚠ unit 5 는 여기서 **조용한 리턴**이었다 — 저작이 끝나며 예외 근거가 소멸해 이 unit 이 규약으로 되돌렸다 |
| 트리거 | 큐 drain — `DetectionEventsSingleton` → `BattleBridge.DrainDetectionEvents` → `SpawnDetectionMark` | 단일 큐 경로(브리지 직접 호출 아님) |
| 공격 히트/캐스트 VFX | **N/A** — 표식은 공격 사건이 아니다(`ProjectileViewPool` 무관) | |
| View | 프리팹 내부 Shuriken PS 2개 | 풀링 없음 · `ConfigureOneShot` 반환 수명(0.95s)으로 `Destroy` |
| 씬 wiring | `BattleBridge.vfxSpawner`(기존) + `detectionMarkPrefab` 슬롯 할당(이 unit) | 슬롯 유실이 **유일한 실패 경로**라 위 `LogError` 가 그 자리의 그물이다 |

### 필수 오버라이드 (VFX 저작 스킬 4종)

| 시스템 | Duration | StartColor | MaxParticles | Loop |
|---|---|---|---|---|
| `Bang` | 0.2 | `(1.00, 0.72, 0.10, 1.00)` | 8 | false |
| `BodyFlash` | 0.2 | `(1.00, 0.72, 0.10, 0.62)` | 12 | false |

모바일 예산 「일반 50」 대비 합계 **20**. Overdraw = 작은 빌보드 쿼드 2종 × 4겹, 0.55s 단발 —
경고 수준 아님. Sub Emitter 0 · Texture Sheet Animation 0.

### ⚠ 함정 — `velocityOverLifetime` 의 세 축은 «모드»가 같아야 한다

초판은 `Bang` 의 상승을 `y` 에만 커브로 주고 `x`·`z` 는 상수 0 으로 뒀다. **에디터에서는 멀쩡해
보이고** 오프스크린 렌더도 통과했지만, 재생 시 콘솔 에러가 쏟아진다. `ParticleCurveModeConsistencyTests`
(전역 가드)가 잡았다 — 저작 시점에 보이지 않는 결함이라 **그 테스트가 유일한 그물**이다.
해법: 상수 축도 **상수 0 «커브»** 로 승격(`AnimationCurve.Constant`). 시각은 완전히 동일하다.

새 파티클 프리팹을 저작하면 이 lane 을 반드시 통과시킨다.

## 완료 기준

- [x] 텍스처 2종 · 머티리얼 2종 · `_SKELETON.prefab` 저장, 카탈로그 draft 등재.
- [x] `BoardSortOrder.DetectionMarkOrder = 17000` 컴파일 확인(링 +0 / 「!」 +1).
- [x] **오프스크린 렌더 육안** — `ConfigureOneShot` 을 리플렉션으로 태운 실제 인스턴스를
      t=0.06/0.16/0.30/0.48 로 Simulate. **중간 톤·밝은 톤 배경 양쪽에서 기호가 읽힌다.**
      (1차 렌더에서 밝은 배경의 링이 조기 소실 → 링 alpha 0.45→0.62 · 유지 구간 연장으로 수정)
- [x] `VfxSpawner.detectionMarkPrefab` 할당 + 씬 저장. **⚠ Play 중에는 배선이 유실된다** —
      편집 모드에서만. 저장 전 씬 `isDirty=False` 를 확인해 남의 WIP 을 함께 굽지 않았다.
      ⚠ 씬 diff 가 **48+/73−** 로 커진다. **런타임 의미가 바뀐 줄은 `detectionMarkPrefab` 하나**이고
      나머지는 **은퇴 필드의 따라잡기 직렬화**다(전수 확인: `allyZoneColor` 참조 0 ·
      `blobShadowSize`/`liftShadowMinScale` unit 20 은퇴 · `enableAdjacencySynergy` 말소 ·
      `postVolume` 는 unit 18 에서 `[SerializeField]` 제거(값도 null) ·
      `+placementLiquidEnabled: 0` 은 C# 초기값 `= false` 와 동일 · `_MarkerProps` 는 블록 이동만).
      ⚠⚠ **예외 1건 — `TilemapPropScatter` 는 「직렬화」가 아니라 «편도 데이터 삭제»다**(리뷰 LOW-1).
      그 클래스는 `Assets/` 전체에 없어 런타임 영향은 0 이지만, 사라진 것은 **저작 데이터**다 —
      프롭 타일 GUID 8 + 가중치 8 + `density 0.45` + `seedSalt 12345`. 이제
      **`git show HEAD:Assets/_Project/Scenes/BattleScene.unity` 로만 복구된다.** 커밋 메시지에 남긴다.
      (GameObject `1627119923` 의 깨진 스크립트 컴포넌트 제거는 이 unit 범위 밖.)
- [x] **EditMode 2788건 중 실패 2건** — `boomerang`·`bomb_man` 문안 단언(**선행 실패**, 시트 소관).
      신규 `DetectionMarkVfxTests`(**5**) 통과 · Assets lane 단독 166/166 동일.
      총계가 움직였으므로 스테일 어셈블리가 아니다(+5 중 +1 이 이 테스트, +4 는 타 세션의
      `Data/Maps/MapDocument_MovementStress.asset` — 데이터 구동 테스트가 늘어난 몫).
- [x] **Play 육안** — 감지 적(선봉·탱커·스키머·드래곤)이 방어유닛을 처음 발견하는 순간에
      표식이 **한 번** 뜬다. 연속 사냥(죽이고 다음 놈)에서는 **다시 뜨지 않는다**(관성 중에는
      `hunting` 이 1로 유지돼 전이가 아니다 — unit 5 의 의도된 거동).
      **사용자 확인 2026-09-08 · 판 전체 콘솔 에러 0 · 커밋 `8cf313e1`.**

> ⚠ **표식은 «발견한 쪽»에만 붙는다.** `DetectionEvent.targetSimId` 는 트레이스·로그 전용이고
> 화면이 그 대상을 가리키지 않는다. 이유는 unit 5 계약 6 참조(무제한 감지에서 거짓이 된다).
