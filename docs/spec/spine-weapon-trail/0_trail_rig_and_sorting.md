# 0 — 본 추종 리그 + 정렬 대역

## 목적

게임 코드를 건드리기 전에 **"Spine 본 2점만으로 리본이 나오고, 유닛 앞에 그려지고, 저절로 켜지지
않는다"** 를 격리 검증한다. 이 세 가지가 안 되면 unit 1 의 배선은 의미가 없다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` — 상수 1개 추가
- `Assets/_Project/VFX/WeaponTrailPreset_Slash.asset` — 신규 (`HS_SwordTrailPreset`)
- `Assets/_Project/VFX/WeaponTrail_Slash.prefab` — 신규 리그
- **벤더 파일 무수정** (README 계약 3)

## 구현

### 1. 정렬 상수

`BoardSortOrder.WeaponTrailOrder = 15500` — 빔(15000) 위 · 피격바(16000) 아래.
궤적은 공격자와 대상 **앞**에 떠야 참격으로 읽힌다. 실제 적용값은 프리셋의 layer sortingOrder 이고
이 상수는 대역 문서 겸 대조 기준이다(README 계약 3).

### 2. 프리셋

벤더 `Slash toon blue` 를 복사해 만든다(툰 계열이 프로젝트 아트 방향에 맞다). 세 필드가 load-bearing:

| 필드 | 값 | 이유 |
|---|---|---|
| `recalculatePointsOnAwake` | **false** | true 면 스켈레톤 메시 전체 바운드를 잡아 몸통만 한 리본 |
| `startActive` | **false** | 공격 사건이 유일한 구동원 |
| `materialLayers[0].sortingOrder` | `WeaponTrailOrder` 값 | 0 이면 유닛 뒤에 깔린다 |

**컴포넌트 인스펙터에서 고쳐도 소용없다** — `Awake` 가 `ApplyPresetValues()` 를 **먼저** 호출해
위 값들을 프리셋 값으로 덮는다(`HS_SwordMeshTrail.cs:180`). 프리셋이 유일한 소스다.

스무딩 초기값은 `maximumSmoothedSectionDistance 0.04` · `maxIntermediateSectionsPerFrame 16`.
168°/0.27초는 벤더 문서 §8 의 "Very fast weapons" 구간이라 기본값(0.08/8)이면 바깥 호가 각진다.

### 3. 리그 프리팹

루트 `WeaponTrail_Slash`:

- `Animator` — 컨트롤러 없음. **필수** (아래 함정)
- `BoneFollower` — `boneName = "Gear"`, `followBoneRotation` on, `followSkeletonFlip` on,
  `initializeOnAwake` **off** (`skeletonRenderer` 는 런타임 주입 — unit 1)
- `HS_SwordMeshTrail` — preset 할당, `pointA`/`pointB` 를 아래 자식으로 할당

자식 `Trail Point A` / `Trail Point B` — 로컬 오프셋 authored. A 를 손에서 바깥으로 빼 안쪽 호를
만드는 초승달 형태(README 계약 5).

**초기 오프셋** — 스켈레톤 원본에서 계산한 시작값이다(눈대중 대신 여기서 출발한다).
`gear_right` 어태치먼트는 `Gear` 본 로컬에서 중심 `(4.65, −41.58)` · 153×43 · rotation 90 이라,
**블레이드는 본 로컬 −Y 축을 따라 y −118 … +35 px** 로 눕는다. 스켈레톤 scale 0.01 이 이미
반영돼 있으므로 follower 로컬 1 유닛 = 100 px:

| 점 | follower 로컬 | 근거 |
|---|---|---|
| `Trail Point A` | `(0.047, −0.40, 0)` | 블레이드 40% 지점 — 피벗(손)에서 빼야 부채꼴이 안 된다 |
| `Trail Point B` | `(0.047, −1.25, 0)` | 칼끝(−1.18) 살짝 바깥 |

리본 폭 0.85 로컬 ≈ 0.46 월드, B 반경(어깨 기준) ≈ 0.95 월드 — README 실측과 일치한다.
**축 부호는 에디터에서 확인할 것**: BoneFollower 의 축 매핑과 `followSkeletonFlip`(ScaleX<0) 조합에
따라 −Y 가 +Y 로 뒤집혀 리본이 등 뒤로 날 수 있다. 뒤집히면 두 점의 y 부호만 반전한다.
무기 실측 길이는 시작점일 뿐 맞출 대상이 아니다(README 계약 5).

**함정 — `Animator` 가 없으면 궤적이 영구 방출된다.** Animator 부재 시 두 경로가 동시에 터진다:

1. `HS_SwordMeshTrail.Awake` → `EnsureAnimationEventsComponent()` 가 `transform.root` 에
   `HS_SwordTrailAnimationEvents` 를 **자동 추가**하고(풀링 유닛이면 남의 오브젝트에 붙는다)
   `SetWorkWithoutAnimation(true)` 를 호출한다.
2. 그 컴포넌트의 `Awake` 도 자기 GameObject 에 Animator 가 없으면 같은 플래그를 켜고,
   `Start()` 가 `StartSwordTrail()` 을 호출한다. `OnEnable` 마다 재시작한다.

결과는 공격과 무관한 상시 방출이다. 컨트롤러 없는 빈 `Animator` 를 리그 루트에 두면 두 경로가 모두
막히고(수신기도 리그 루트에 갇힌다), 벤더 스크립트를 고칠 필요가 없다.

### 저작 시 함정 — `AddComponent` 가 Point 위치를 덮는다

에디터에서 `AddComponent<HS_SwordMeshTrail>()` 를 하면 Unity 가 `Reset()` 을 호출하고, 그 안의
`RecalculateTrailPoints()` 가 **이미 배치해 둔 Point A/B 의 localPosition 을 기본값 ±0.5 X 로
덮어쓴다**(메시가 없으면 "Default point positions were used" 경로). 컴포넌트의
`recalculatePointsOnAwake` 를 false 로 둬도 막히지 않는다 — 그 플래그는 Awake 전용이다.

→ **Point 위치는 컴포넌트를 붙인 뒤에 쓴다.** 이미 만든 프리팹을 고칠 땐
`PrefabUtility.LoadPrefabContents` → 위치 수정 → `SaveAsPrefabAsset` 으로 되돌린다.
같은 `Reset()` 이 `HS_SwordTrailAnimationEvents` 를 루트에 붙여 프리팹에 baked 되는데, 이건 정상이다
(Animator 가 있으므로 `workWithoutAnimation = False` 로 굳는다).

## 완료 기준

- compile 통과, 콘솔 에러/경고 0 (특히 `No material assigned`)
- Play 하네스(`execute_code`): 빈 씬에 `Casual Character` 스켈레톤 + `gear_right` 스킨을 세우고,
  리그를 Instantiate → `BoneFollower.skeletonRenderer` 주입 + `Initialize()` → `Attack3` 루프 재생
- 스크린샷 3장으로 판정
  1. `StartTrail()` 중 — 리본이 스윙 궤적을 따라간다
  2. `StopTrail()` 후 — `trailLifetime` 안에 사라진다
  3. **`StartTrail()` 호출 전 — 아무것도 안 보인다** (영구 방출 함정 회귀 검출)
- 배틀 씬 대조: 궤적이 유닛 스프라이트 **앞**에 그려진다 (뒤에 깔리면 프리셋 sortingOrder 미반영)

## 검증 실측 (2026-08-01, BattleScene Play 하네스)

4컷 전부 통과. 하네스는 `DontDestroyOnLoad` 필수 — 안 걸면 씬 전환에 쓸려간다.

| 관측 | 값 |
|---|---|
| 레이어 sortingOrder | **15500** (프리셋에서 적용 — 계약 3 성립) |
| 레이어 parent | `(root)` — 씬 루트 오브젝트 (계약 6 성립) |
| 레이어 수 / 머티리얼 | 1개 / `Path14Slash` — 드로우콜 1 |
| 방출 중 메시 | `verts=288` |
| `StopTrail` + 수명 후 | `verts=0` |
| `StartTrail` 전 | `isEmitting=False`, 화면에 아무것도 없음 |
| `pointA→pointB` | 0.464 월드 (예측 0.46) |

**하네스 함정**: 스킨 적용 직후 프레임이 한 번도 안 돌면 `SkeletonAnimation` 메시가 재생성되지
않아 **조합 스킨이 화면에 반영되지 않는다**(무기가 없는 것처럼 보인다). 정적 캡처로 외형을
판정하지 말 것 — 프레임을 한 번 이상 돌린 뒤 찍는다.

**튜닝 방향(unit 2 인계)**: 현재 오프셋에서 리본 안쪽 가장자리가 **캐릭터 얼굴 위를 지난다**.
스윙 시작 자세에서 손이 머리 옆에 있고 Point A 가 손에서 0.40 밖에 안 떨어져 있기 때문.
A 를 더 바깥으로(−0.40 → −0.7 부근) 빼 띠를 얇게 만들고 몸에서 떨어뜨리는 쪽이 1순위 후보다.

**이 검증의 한계 — 시야 조건이 실전과 다르다.** 하네스는 틸트 없는 **정면 직교** 카메라에
스케일 `0.42`(코드 기본값)를 썼다. 실전은 `Billboard(Tilted, 45°)` 고정 평면 + 배틀 카메라
pitch + 스케일 **`0.504`**(BattleScene 직렬화값, 즉 실제 유닛은 여기서 본 것보다 20% 크다).
따라서 이 4컷은 **형태·정렬·수명·라이프사이클만** 입증하고 **"화면에서 잘 보이는가"는 입증하지
않는다**. 가시성 검증은 unit 1 로 이월(README 참조).

## 커밋 범위 주의

`Assets/Hovl Studio/` 전체가 아직 미추적이다. 프리셋이 참조하는 머티리얼
(`HSFiles/Materials/Path*Slash.mat`)과 셰이더(`HSFiles/Shaders/HS_Slash.shadergraph`)가 같이 들어가야
참조가 깨지지 않는다. 반면 `Marta chan/` · `Procedural fire/` 는 이 spec 과 무관하다 —
**경로 명시 스테이징**으로 필요한 것만 넣는다(`git add -u` 스윕 금지).
