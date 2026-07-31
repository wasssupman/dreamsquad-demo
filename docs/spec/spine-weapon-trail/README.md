# spine-weapon-trail — Spine 유닛 공격 무기 궤적

> 상태: **spec 작성 · 사용자 승인 대기** (2026-07-31)

## 목표

Spine 유닛이 공격할 때 손에 든 무기가 지나간 자리에 **궤적 리본**을 남긴다.
소스는 이미 임포트된 벤더 패키지 `Assets/Hovl Studio/Epic Sword Slash Effects System`
(`HS_SwordMeshTrail` — 두 Transform 사이에 절차 리본 메시를 생성).

- **심 변경 0.** 전부 프레젠테이션. 신규 ECS 컴포넌트·시스템·이벤트 채널 없음.
- **구동원은 기존 공격 사건.** `UnitAttackVisualEventsSingleton` → `BattleBridge.DrainUnitAttackVisualEvents`
  → `SpineUnitPool.NotifyAttack` → `SpineUnitView.PlayAttack` 경로에 붙는다.
- **대상 지정은 SO 프리팹 유무.** 빔 유닛(`beamVfxPrefab`)과 같은 관례 — id/kind 분기 없음.
  원거리 유닛(활·총)에 참격이 뜨지 않게 하는 유일한 게이트다.

검증 질문: **"궤적이 '벤 자국'으로 읽히면서도, 실제 사거리(근접 1타일)보다 넓은 범위를 벤 것처럼
보이지 않는가?"** 스윙은 이미 충분히 크다(아래 실측) — 이 spec 의 난점은 궤적을 키우는 게 아니라
**형태를 초승달로 만들고 과장을 사거리 안에 가두는 것**이다.

## 작업 단위

| # | 구분 | 문서 | 상태 | 목적 |
|---|---|---|---|---|
| 0 | asset+code | `0_trail_rig_and_sorting.md` | 대기 | 본 추종 리그 프리팹 + 프로젝트 소유 프리셋(정렬 대역) + `BoardSortOrder` 상수 |
| 1 | code | `1_attack_driven_trail.md` | 대기 | SO opt-in 필드 + 스폰 시 부착 + `PlayAttack` 에서 Start/Stop |
| 2 | asset | `2_melee_roster_and_tuning.md` | 대기 | 근접 로스터 적용 + 오프셋/수명/색 튜닝 + **가독성 상한 판정** |
| 3 | docs | `3_handoff_summary.md` | 대기 | 인계 요약 |

### unit 1 로 이월된 검증 (unit 0 하네스가 답하지 못한 것)

unit 0 은 틸트 없는 정면 직교 뷰로 찍어 **형태·정렬·수명**만 입증했다. 아래는 **실전 시야에서만**
답이 나오므로 unit 1 의 완료 기준에 포함한다 — "메시가 생성된다"와 "화면에서 읽힌다"는 다른 질문이다.

- **가시성(최우선)**: 유닛은 `Billboard(Tilted, 45°)` 고정 평면 위의 빌보드 캐릭터다. 리본도 같은
  평면에 생기므로 스프라이트와 같은 방향을 보지만, **캐릭터 틸트는 45° 고정인데 카메라 pitch 는
  페이즈마다 다르다**(Draft↔Battle). 어긋난 만큼 평면의 세로축이 화면에서 단축되는데 `Attack3` 는
  세로 내려찍기라 호가 하필 그 축에 눕는다. 실제 배틀 카메라로 찍어 판정할 것.
- **카메라 이동 중 박제**: 리본 섹션은 방출 시점 월드 좌표로 굳고 **다시 빌보드하지 않는다**.
  스프라이트는 매 LateUpdate 재정렬된다. 수명 0.2초 안에 `CameraDirector` 가 카메라를 움직이면
  (비행·구두점·킥) 둘이 어긋난다.
- **실행 순서**: `BoneFollower.LateUpdate` ↔ `HS_SwordMeshTrail.LateUpdate` 순서가 미정의라 1프레임
  지연 가능. 눈에 띄면 Script Execution Order 로 고정.
- **레이어 회수**: 트레일 레이어는 씬 루트 오브젝트다. 유닛 사망·풀 반납·매치 종료 후 프레임을
  넘겨 잔존이 없는지 확인(unit 0 정리에서 같은 프레임엔 1개가 남아 보였는데, `Destroy` 가 지연
  파괴라 정상으로 보이나 확증 안 됨).

## 조사에서 실증된 것 (unit 0 진입 전 읽을 것)

벤더 문서(`Demo scene/Sword_Mesh_Trail_System_User_Guide.docx.pdf`, 14쪽)와 스크립트
(`HSFiles/Scripts/HS_SwordMeshTrail.cs`, 1726줄) · 스켈레톤 원본(`Casual Character.json`) 대조 결과.

- **검 메시도 Animator 도 필요 없다.** 시스템의 입력은 Trail Point A/B **두 Transform** 뿐이고,
  `StartTrail()` / `StopTrail()` / `ClearTrail()` 이 퍼블릭 메서드다. `HS_SwordTrailAnimationEvents`
  는 Animator 전용 편의 수신기라 Spine 경로에선 **쓰지 않는다**.
- **무기 본이 이미 있다.** 디펜더 전원 공용 스켈레톤(`Casual Character_SkeletonData`,
  guid `ee98f82138b60430f97c6863317c3a2f`)에 `Gear` 본(부모 `Hand_r`, length 16.78, rotation −90)
  이 있고 `gear_right` 슬롯(무기 스프라이트 153×43px)이 여기 붙는다. Bruiser 는 `gear_right/gear_right_c_25` 착용.
  두 점을 뽑는 도구도 있다 — `Assets/Spine/Runtime/spine-unity/Components/Following/BoneFollower.cs`.
- **바닥 평면 함정 해당 없음.** 리본 평면은 고정이 아니라 *칼 축 × 이동 방향*으로 결정된다.
  `Billboard`(Tilted 45°)로 기울어진 스켈레톤 평면 안에서 그대로 생성돼 카메라를 향한다.
  (벤더 VFX 의 XZ↔XY 회전 문제는 이 시스템엔 없다.)
- **셰이더 OK.** `HSFiles/Shaders/HS_Slash.shadergraph` 에 `UniversalTarget` 포함 → URP 17.4.
  Dissolve 는 MaterialPropertyBlock 이라 머티리얼 인스턴스가 안 생긴다(동시 다발 안전).
- **정렬은 반드시 손봐야 한다.** 벤더 프리셋 전부 `materialLayers[0].sortingOrder: 0` →
  유닛(`BoardSortOrder.Compute` = 수백대) 뒤에 깔린다(빔 유닛 때와 같은 증상).
- **스윙은 이미 크다 — 단 각도는 `Hand_r` 이 아니라 체인 합산으로 읽어야 한다.**
  디펜더 전원의 `attackAnimation` 은 `Attack3`. 주 스윙은 `Shoulder_r`(0 → −131.6°)이 담당하고
  `Hand_r`(0 → −36.6°)이 더해진다. **순 각도 0 → −168.2° / 0.27초** 의 내려찍기 뒤 −26° 로 복귀.
  (`Hand_r` 만 보면 −36.6° 로 보여 "스윙이 작다"는 오독이 나온다 — 2026-07-31 실측으로 정정.)
  `PlayAttack` 의 `attackAnimPeriod` 압축 재생(TimeScale ≥ 1)이 여기에 더 곱해진다.
- 반경·경로 실측: 어깨→칼끝 ≈ 167px × 스켈레톤 scale 0.01 × 오브젝트 스케일(visualScale 1.3 ×
  `CharacterVisualScale` 0.42 = 0.546) ≈ **0.9 월드 유닛**. 168°(2.93 rad) 스윙이면 칼끝 경로
  ≈ **2.7 유닛 = 2.7 타일**(`tileSize` 1). 근접 사거리 1 타일과의 격차가 unit 2 의 가독성 상한 근거다.
- 168°/0.27초는 벤더 문서 §8 의 **"Very fast weapons"** 구간이다 —
  `maximumSmoothedSectionDistance` ↓ · `maxIntermediateSectionsPerFrame` ↑ 없이는 바깥 호가 각진다.

## Feature-wide 계약

1. **심 변경 0.** 신규 큐·컴포넌트·시스템 없음. 궤적은 뷰가 기존 공격 사건을 해석한 결과다.
2. **opt-in 은 SO 프리팹 유무**가 결정한다. id/kind 분기 금지(빔 유닛 관례와 동일).
   원거리·투사체 유닛은 프리팹 미할당 = 무궤적.
3. **정렬의 유일한 소스는 프리셋 asset 이다.** `EnsureTrailObjects()` 가 매 `LateUpdate`
   끝에 `ApplyRendererSettings()` 를 호출해 `renderer.sortingOrder` 를 프리셋 값으로 되쓴다
   → 런타임 외부 쓰기는 무효. **벤더 스크립트 수정 금지**, 프로젝트 소유 프리셋 복사본으로 해결한다.
4. **`recalculatePointsOnAwake = false` 필수.** 켜두면 `GetComponentsInChildren<MeshFilter>` 가
   스켈레톤 메시 전체 바운드를 잡아 몸통만 한 리본이 나온다. Point A/B 는 프리팹 authored.
5. **Point A/B 는 무기 실측 지오메트리와 무관하다** (2026-07-31 사용자 결정). 시스템은 두 Transform
   만 보고 무기의 존재조차 모르므로, 오프셋은 **궤적 흐름이 맞는 값**으로 과장하거나 축소한다.
   칼날 반사가 아니라 참격 이펙트로 읽히는 게 목적이다. 다만 두 방향의 성격이 다르다 —
   - **Point A 를 손에서 바깥으로 빼는 것이 형태 레버.** A 가 회전 피벗(손) 근처면 A 가 거의
     안 움직여 손에서 퍼지는 **부채꼴**이 된다. 안쪽 호를 만들어야 **초승달**이 나온다.
   - **Point B 를 늘리는 것은 가독성 부채.** 호 길이 = 반경 × 각도인데 각도가 이미 168° 라,
     B 를 2배로 빼면 경로가 5 타일을 훑는다. 사거리(근접 1타일)를 넘는 과장은 "안 맞는 걸
     벤 것처럼" 보이게 한다. 확대 방향은 unit 2 의 가독성 상한 판정을 거친다.
   - 오프셋은 **스켈레톤 평면 안**에 둔다. 평면 밖(로컬 깊이축)으로 빼면 틸트 빌보드에서 리본이 눕는다.
6. **생성 레이어는 씬 루트 오브젝트**(월드 공간 메시, 부모 없음). 두 가지가 따라온다 —
   (a) `SpineUnitView.UpdateSortingOrder` 의 `GetComponentsInChildren<Renderer>` 가 안 건드린다,
   (b) 회수는 `OnDisable→ClearTrail` / `OnDestroy→DestroyRuntimeLayers` 가 한다.
   매치 종료·씬 전환 시 잔존 여부는 unit 1 에서 확인한다.
7. **시간 제어**: 궤적은 `Time.time` 기반이고 `Time.timeScale` 은 1 고정(TimeManager 원칙)이라
   슬로우모/정지 중에는 두 점이 얼어 새 섹션이 안 생기고 기존 섹션만 수명대로 증발한다.
   이 동작을 사양으로 받아들인다 — 별도 시간 배선을 만들지 않는다.
8. **수치는 전부 authoring 소유.** 리그 오프셋 = 프리팹, 수명·색·두께·정렬 = 프리셋 SO.
   코드에는 정렬 대역 상수(`BoardSortOrder`) 하나만 둔다.
9. **스코프는 방어 유닛.** 적/보스는 이 spec 밖(후속 후보).

## 파이프라인 커버리지 (VFX one-shot × Defender 대조)

궤적은 one-shot VFX 와 달리 **본에 부착돼 유닛 수명을 따르는** 계열이라 두 표를 섞어 대조한다.

| 정거장 | 이 spec 에서 |
|---|---|
| 데이터 SO | `DefenderUnitData.weaponTrailPrefab` 신규 필드(unit 1) + 프로젝트 소유 `HS_SwordTrailPreset` 복사본(unit 0) |
| 프리팹 소스 | `Assets/_Project/VFX/WeaponTrail_*.prefab` — BoneFollower + `HS_SwordMeshTrail` + Point A/B 자식 |
| ECS | **N/A — 시뮬 무관 순수 프레젠테이션.** 궤적은 판정에 기여하지 않는다 |
| 트리거 | 기존 `UnitAttackVisualEventsSingleton` drain → `SpineUnitView.PlayAttack`. **신규 큐 0** |
| View | `SpineUnitView` 가 소유(스폰 시 부착) + 소형 부착 컴포넌트 1개(unit 1) |
| Pool | **N/A — 유닛당 1개 부착, 유닛 수명과 동일.** 별도 풀 불요 |
| 정렬 | `BoardSortOrder` 신규 상수 + **프리셋 layer sortingOrder 가 실제 적용값**(계약 3) |
| 씬 wiring | **N/A — 씬 오브젝트 신설 없음.** 프리팹 참조는 디펜더 SO 가 들고 있다 |

## 후속 후보

- **절차 스윙(본 비의존)** [M] · unit 2 에서 "애니 궤적 자체가 원하는 형태가 아니다"로 판정될 때의
  탈출구. 두 점을 본이 아니라 스크립트 아크로 구동하면 애니와 무관한 참격 형태를 만들 수 있다.
  코드가 늘고 애니와 어긋날 위험이 있어 기본안이 아니다.
- **`Attack1` 과의 관계 정리** [S] · `Attack1`(순 57.4°)은 `Attack3`(168°)보다 작은 스윙인데
  현재 `dragAnimation` 전용이다. 궤적을 붙일 애니를 유닛별로 고를 필요가 생기면 그때 다룬다.
- **적/보스 궤적** [S] · 같은 리그를 적 스켈레톤에 재사용. 보스 도약 공격이 후보.
- **무기 종류별 프리셋 분기** [S] · 도끼/둔기/마법무기에 다른 색·수명. 지금은 유닛 SO 가 직접 지정.
- **타격 순간 강조** [S] · `hitDelaySec` 시점에 궤적 밝기 펄스 — 기존 `attackVfxPrefab` 히트 연출과 역할 분담 필요.
- **모바일 실기기 프로파일** [S] · 동시 근접 유닛 최대치에서 LateUpdate CPU(샘플링+Catmull–Rom+메시 리빌드) 측정.
