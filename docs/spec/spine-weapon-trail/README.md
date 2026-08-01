# spine-weapon-trail — Spine 유닛 공격 무기 궤적

> 상태: **units 0~3 구현 완료 · 코드 리뷰 반영 완료 · 보스 크기 결정 1건 남음** (2026-08-01)
> `d37e3196` → `314c0033` → `4aab5bc7`·`71117b42`·`a340ed48`·`851ee392` → `bd6f079a`·`dd573654`

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
| 0 | asset+code | `0_trail_rig_and_sorting.md` | **완료** `d37e3196` | 본 추종 리그 프리팹 + 프로젝트 소유 프리셋(정렬 대역) + `BoardSortOrder` 상수 |
| 1 | code | `1_attack_driven_trail.md` | **완료** `314c0033` | SO opt-in 필드 + 스폰 시 부착 + `PlayAttack` Start/Stop + 가시성 판정 게이트 |
| 2 | asset | `2_legibility_and_roster.md` | **완료** `4aab5bc7`+3 | 룩 세트 · 크기/수명 튜닝 · role 기준 로스터 · 유닛별 룩 배분 |
| 3 | code | `3_any_host_generalization.md` | **완료** `bd6f079a` | 디펜더 종속 해제(`ISpineUnitVisualData` + `WeaponTrailRig`) + 보스 적용 |
| 4 | docs | `4_handoff_summary.md` | 대기 | 인계 요약 |

### 검증 결과 — 전부 해소 (2026-08-01 코드 리뷰 동시 진행)

| 항목 | 결과 |
|---|---|
| 가시성 | 틸트 45° vs 카메라 pitch 60° → 어긋남 15°, 단축률 0.966 로 무시 가능. 색·파티클로 해결 |
| 드로우콜 | 트레일당 레이어 1 = **유닛당 1** |
| `LateUpdate` 순서 | **지연 0.** 최신 섹션 좌표 = 현재 Point 좌표(`dA=dB=0.0000`). 리그 컴포넌트 순서가 `BoneFollower → HS_SwordMeshTrail` 이고 **같은 GameObject** 라 이 순서로 돈다. Script Execution Order 불요 |
| 레이어 회수 | **누수 없음.** Kill 직후 같은 프레임 1 → 다음 프레임 0(지연 파괴) |
| 카메라 이동 중 박제 | **성립하지 않는 기우였다.** `BillboardRotation.Compute(Facing.Tilted, …)` = `Quaternion.Euler(tilt,0,0)` — **카메라를 보지 않는다.** 스프라이트도 리본도 같은 고정 월드 평면이라 카메라가 움직여도 어긋날 수 없다. ⚠ `BillboardMode.Full`/`YAxis` 로 바꾸면 우려가 되살아난다 |

### 결정 대기

- **보스 궤적 크기** — 보스는 `spineVisualScale` 이 커서 리그가 그대로 스케일된다. 나이트메어의
  호가 약 4타일인데 사거리는 2. 그대로 둘지, 보스 전용 Variant 에서 Point A/B 만 좁힐지.

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
6-a. **정렬 대역은 리그가 소유하고, 호스트는 리그 하위를 건너뛴다** (리뷰 지적 반영).
   리본 메시는 씬 루트라 안전하지만 프리셋의 pointA 파티클은 **리그의 자식**이라
   `SpineUnitView.UpdateSortingOrder` 의 `GetComponentsInChildren<Renderer>` 스윕에 걸려
   유닛 대역으로 끌려갔다(실측 파티클 111 vs 리본 15500 — 파티클만 앞 유닛에 가림).
   `WeaponTrailRig` 가 파티클을 `WeaponTrailOrder + 1` 로 못 박고, 호스트는 `IsChildOf(rigRoot)`
   로 건너뛴다. **둘은 한 쌍이다** — 한쪽만 있으면 매 프레임 다시 덮인다.
7. **시간 제어**: 궤적은 `Time.time` 기반이고 `Time.timeScale` 은 1 고정(TimeManager 원칙)이라
   슬로우모/정지 중에는 두 점이 얼어 새 섹션이 안 생기고 기존 섹션만 수명대로 증발한다.
   이 동작을 사양으로 받아들인다 — 별도 시간 배선을 만들지 않는다.
   **단 방출 창은 예외다**(리뷰 지적 반영): 창은 `Duration × norm ÷ (entry.TimeScale × _skeleton.timeScale)`
   로 **스켈레톤 배속까지 나눠야** 한다. 슬로우모는 `_skeleton.timeScale` 로 들어오는데 이 항을
   빼면 스윙이 느려진 만큼 창이 모자라 방출이 도중에 끊긴다(0.25× 실측: 창 0.269s 대 스윙 1.075s,
   4배 짧음 → 수정 후 1.00 배). `_skeleton.timeScale == 0`(정지)이면 스윙이 진행되지 않으므로 방출을 걸지 않는다.
8. **수치는 전부 authoring 소유.** 리그 오프셋 = 프리팹, 수명·색·두께·정렬 = 프리셋 SO.
   코드에는 정렬 대역 상수(`BoardSortOrder`) 하나만 둔다.
9. **호스트를 가리지 않는다** (unit 3 에서 계약 반전 — 아래 "설계 오판" 참조).
   궤적 필드는 `ISpineUnitVisualData` 에 있어 디펜더·적·보스가 모두 대상이고, 실제 범위는
   **프리팹 할당 여부**가 정한다. `WeaponTrailRig.Bind(null)` 은 본 없는 호스트(구조물)용 경로다.

## 설계 오판 — 스코프를 타입에 새기지 말 것

unit 1 에서 궤적 필드를 `IDefenderSpineExtras`(방어 유닛 전용)에 뒀다가 unit 3 에서 되돌렸다.
같은 자리에서 미끄러지지 않도록 근거와 오류를 남긴다.

**그때의 근거** — (a) 첫 스펙에서 적·보스까지 끌어안는 건 스코프 확장이다, (b) `SpineUnitView` 는
적 스폰 시 `_defenderExtras` 가 null 이라 **적 제외가 코드 분기 없이 성립**한다, (c) 선례로
`SpineCastAnchorBone` 이 이미 그 인터페이스에 살고 있다.

**무엇이 틀렸나**

- **스코프 결정을 타입 경계로 굳혔다.** "지금 누구에게 켜나"(정책)와 "누가 켤 수 있나"(능력)는
  다른 질문인데 전자를 후자에 새겼다. 정책을 바꾸려니 타입을 바꿔야 했다.
- **그 "자동 게이트"는 중복이었다.** 게이트는 이미 `weaponTrailPrefab == null` 이 하고 있었다.
  인터페이스 배치로 얻은 안전은 0 이고, 대신 구조적 제약을 샀다 — 지우기 어려운 쪽을 남긴 셈.
- **근거 (b) 자체가 사실 오해였다.** `DrainUnitAttackVisualEvents` 는 `NotifyAttack` 을
  **모든 공격자**(적 포함)에게 부른다. 그 아래 `FindDefenderData == null → continue` 는 그 뒤
  디펜더 전용 VFX 에만 걸린다. 공격 경로는 원래 적도 태우고 있었고 **배제는 내가 넣은 것**이다.

**놓친 신호**: unit 0 의 리그 프리팹은 이미 호스트를 가리지 않았다(BoneFollower + 트레일 + 점 둘).
**에셋이 범용인데 배선만 좁으면** 그게 신호다.

**규칙**: opt-in 데이터는 메커니즘이 자연히 지원하는 **가장 넓은 인터페이스**에 두고,
범위는 **에셋 할당**으로 표현한다. 범위는 타입이 아니라 데이터에 산다.

## 파이프라인 커버리지 (VFX one-shot × Defender 대조)

궤적은 one-shot VFX 와 달리 **본에 부착돼 유닛 수명을 따르는** 계열이라 두 표를 섞어 대조한다.

| 정거장 | 이 spec 에서 |
|---|---|
| 데이터 SO | `ISpineUnitVisualData.SpineWeaponTrailPrefab` / `…EndNormalized` — `DefenderUnitData`·`AttackUnitData` 공용(unit 3) + 프로젝트 소유 `HS_SwordTrailPreset` 복사본 7종 |
| 프리팹 소스 | `WeaponTrail_Slash.prefab`(base) + 룩별 **Prefab Variant** 7종. 리그 = Animator(빈) + BoneFollower + `HS_SwordMeshTrail` + `WeaponTrailRig` + Point A/B 자식 |
| ECS | **N/A — 시뮬 무관 순수 프레젠테이션.** 궤적은 판정에 기여하지 않는다 |
| 트리거 | 기존 `UnitAttackVisualEventsSingleton` drain → `SpineUnitView.PlayAttack`. **신규 큐 0** |
| View | `SpineUnitView` 는 Instantiate + `Bind`/`Play` 위임만. 부착·타이머는 `WeaponTrailRig` 소유 |
| Pool | **N/A — 유닛당 1개 부착, 유닛 수명과 동일.** 별도 풀 불요 |
| 정렬 | `BoardSortOrder.WeaponTrailOrder` + **프리셋 layer sortingOrder 가 실제 적용값**(계약 3) |
| 씬 wiring | **N/A — 씬 오브젝트 신설 없음.** 프리팹 참조는 유닛 SO 가 들고 있다 |

> `docs/reference/object-pipeline-map.md` 갱신 대상이다 — "본 부착 · 유닛 수명 추종" 계열이
> 기존 VFX(one-shot) 아키타입에 없다. handoff(unit 4) 작성 시 반영한다.

## 후속 후보

- **절차 스윙(본 비의존)** [M] · 애니 궤적 자체가 원하는 형태가 아닐 때의 탈출구. 두 점을 본이
  아니라 스크립트 아크로 구동한다. 지금은 불필요 — Attack3 통일로 해결됐다.
- **공격 애니 다양성** [S] · 궤적이 읽히는 건 `Attack3`(순 178.8°) 뿐이라 디펜더 7종 + 보스 2종이
  전부 같은 모션을 쓴다. 다른 모션을 쓰려면 그 모션의 스윙 폭부터 키워야 한다(스켈레톤 저작).
- **구조물 호스트** [M] · `WeaponTrailRig.Bind(null)` 경로와 BoneFollower 없는 Variant 로 길은
  열려 있다. 실제 소비자(회전 포탑·해저드 등)가 생기면 만든다.
- **보스 전용 크기** [S] · 보스는 `spineVisualScale` 이 커서 호가 ~4타일이다. 줄이려면 보스 전용
  Variant 에서 Point A/B 만 좁힌다(Variant 가 자식 트랜스폼 오버라이드 가능, 코드 0).
- **무기 종류별 프리셋 분기** [S] · 도끼/둔기/마법무기에 다른 색·수명. 지금은 유닛 SO 가 직접 지정.
- **Lightning 룩 활용처** [S] · 짙은 남색이라 현 보드에서 죽어 배분에서 뺐다. 어두운 배경 맵이
  생기면 후보. 프리셋·Variant 는 남아 있다.
- **타격 순간 강조** [S] · `hitDelaySec` 시점에 궤적 밝기 펄스 — 기존 `attackVfxPrefab` 히트 연출과 역할 분담 필요.
- **모바일 실기기 프로파일** [S] · 동시 근접 유닛 최대치에서 LateUpdate CPU(샘플링+Catmull–Rom+메시 리빌드) 측정.
