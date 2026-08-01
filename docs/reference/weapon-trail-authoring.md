# 무기 궤적 — 붙이고, 바꾸고, 늘리는 법

> 공격할 때 무기가 지나간 자리에 남는 리본. **켜고 끄고 바꾸는 일은 전부 authoring 이고 코드는 안 건드린다.**
> 설계 이력·시행착오·기각된 대안은 `docs/spec/spine-weapon-trail/`.
> 파이프라인 정거장 대조표는 `docs/reference/object-pipeline-map.md` "본 부착 VFX — 무기 궤적".

## 세 줄 요약

1. 궤적을 켜는 건 **유닛 SO 필드 하나** — `weaponTrailPrefab` 에 리그 Variant 를 끼우면 끝이다.
2. **룩을 바꾸는 것도 같은 필드.** 7종 중 고른다. 유닛마다 다른 색이어도 된다.
3. 나머지 전부(본·모양·크기·수명·색·정렬)는 **리그 프리팹과 프리셋 SO** 가 소유한다. 코드에 있는 건 정렬 대역 상수 하나뿐이다.

**게이트는 프리팹 유무다.** 미할당 = 무궤적. id/kind 분기는 없고, 만들지도 않는다.

## 이 시스템이 실제로 하는 일 — 선분 AB 의 자취

입력은 **Transform 두 개**뿐이다. 매 `LateUpdate` 마다 두 점의 월드 위치를 찍어 **섹션 하나**로 쌓고,
쌓인 섹션들을 이어 리본 메시를 만든다.

```
TrailSection { pointA, pointB, spawnTime, distance }   ← 한 프레임의 스냅샷
리본 = 섹션 히스토리를 이어 붙인 것 = 선분 AB 가 시간 위로 쓸고 간 자취
```

| 화면의 무엇 | 실제로는 |
|---|---|
| 리본 **폭** | 그 순간의 \|AB\| |
| 리본 **길이** | 그동안 선분이 이동한 거리 |
| 꼬리가 사라짐 | 섹션이 `trailLifetime`(0.28초) 지나면 앞에서부터 만료 |
| 안 움직이면 안 나옴 | 이동이 `minimumSectionDistance`(0.015) 미만이면 샘플을 아예 안 쌓는다 |

**"슬래시"는 시스템이 아는 개념이 아니다.** 선분이 호를 그리며 지나가서 슬래시로 보일 뿐이다. 여기서 따라 나오는 것들:

- **무기도 검 메시도 Animator 도 스켈레톤도 필요 없다.** 두 점이 움직이기만 하면 된다 — 레시피 D·E 가 성립하는 이유다. 직선으로 움직이면 띠, 호를 그리면 참격, 원을 그리면 링이 된다.
- **Point A 가 회전 피벗(손) 근처면 A 가 거의 제자리라 부채꼴**이 나온다. 빼야 안쪽 호가 생겨 초승달이 된다 — 레시피 C 의 근거.
- **리본 평면은 고정이 아니라 *선분 축 × 이동 방향*으로 매 순간 결정된다.** 그래서 벤더 VFX 의 XZ↔XY 바닥 평면 함정이 이 시스템엔 없다.
- 프레임당 이동이 크면 호가 각진다. 저장은 **실 샘플만** 하고, 메시를 빌드할 때 이웃 샘플로 접선을 잡아 **Catmull–Rom 중간 섹션**을 끼워 넣는다(`maximumSmoothedSectionDistance` / `maxIntermediateSectionsPerFrame`).

## 지금 붙어 있는 것

| 유닛 | 역할 | 룩 | | 유닛 | 역할 | 룩 |
|---|---|---|---|---|---|---|
| 파이터 (`Defender_Bruiser`) | Fighter | ToonFire | | 가디언 (`Defender_Guardian`) | Guardian | ToonBlue |
| 말파이트 (`Defender_Malphite`) | Fighter | ToonGreen | | 배스티온 (`Defender_Bastion`) | Guardian | ToonFire |
| 이쑤시개 (`Defender_Slasher`) | Fighter | Simple | | 실드셔틀 (`Defender_ShieldShuttle`) | Guardian | Cyan |
| 투머치토커 (`Defender_TooMuchTalker`) | Fighter | ToonWater | | 짱쎈놈 (`Enemy_Boss_Jjangssen`) | Boss | ToonFire |
| | | | | 나이트메어 (`Enemy_Boss_Nightmare`) | Boss | ToonWater |

**총 9종.** 기준은 `DefenderUnitData.role` 이 `Guardian`/`Fighter` 인 전원 + 보스 2종.
나머지(원거리·투사체·잡몹)는 전부 미할당이다. 9종 모두 `weaponTrailEndNormalized = 0.31`.

## 전제 조건 — 아무 유닛에나 켜지지는 않는다

| 조건 | 왜 | 현재 |
|---|---|---|
| 스켈레톤에 **`Gear` 본** | `BoneFollower.boneName = Gear` 로 추종한다 | `Casual Character_SkeletonData` **하나뿐**. 궤적 켜진 9종 전부 이걸 쓴다 |
| **무기 스킨** (`gear_right/*`) | 없어도 궤적은 나오지만 "빈손이 베는" 그림이 된다 | 나이트메어는 무기가 없어서 `gear_right_c_40` 을 새로 줬다 |
| **스윙이 큰 공격 애니** | 각도가 작으면 리본이 얼룩으로 보인다 | 순 스윙 폭 실측: `Attack3` **178.8°** / `Attack1` 57.4° / `Attack2` 24.7° → 9종 전부 `Attack3` |

> 각도는 `Hand_r` 만 보면 −36.6° 로 읽혀 "스윙이 작다"고 오독한다. **`Shoulder_r` 까지 체인 합산**해야 178.8° 가 나온다.

**원거리·투사체 유닛에는 켜지 말 것.** 사거리가 5인데 참격이 뜨면 무엇으로 맞혔는지가 흐려진다.

---

## 레시피 A — 기존 유닛에 궤적을 켠다

코드 0. 위 전제 조건 3개를 만족하면 SO 인스펙터에서 끝난다.

1. 유닛 SO(`DefenderUnitData` 또는 `AttackUnitData`) 인스펙터 → **Weapon Trail** 섹션
2. `weaponTrailPrefab` ← `Assets/_Project/VFX/WeaponTrail_Slash_{Look}.prefab` 중 하나
3. `weaponTrailEndNormalized` ← **스윙이 끝나는 시각 ÷ 애니 전체 길이**

세 번째 값이 방출 창을 정한다. 애니 끝까지 방출하면 **칼을 되돌리는 자국**이 남는다.
`Attack3` 는 0.867초 중 0~0.267초만 스윙이라 `0.267 / 0.867 ≈ 0.31`.
다른 애니를 쓰면 그 애니의 값을 다시 재야 한다 — 상수로 박지 않는 이유다(제약 6).

실제 방출 시간은 코드가 배속을 나눠 계산한다:

```
창 = Duration × endNormalized ÷ (entry.TimeScale × _skeleton.timeScale)
```

`entry.TimeScale` 은 공격 주기 압축, `_skeleton.timeScale` 은 슬로우모다. **둘 다 나눠야** 느려진 스윙에서 방출이 도중에 끊기지 않는다.

## 레시피 B — 새 룩을 만든다

코드 0. 벤더 프리셋을 **직접 참조할 수 없다** — 반드시 복사본을 만든다.

1. `Assets/Hovl Studio/Epic Sword Slash Effects System/Sword slash presets/` 에서 골라 복사
   → `Assets/_Project/VFX/WeaponTrailPreset_{Look}.asset`
2. **반드시 덮을 3개** — 취향이 아니라 **동작 결함**이다:

   | 필드 | 벤더 | 우리 | 안 덮으면 |
   |---|---|---|---|
   | `materialLayers[].sortingOrder` | 0 | **15500** | 유닛(수백대) 뒤에 깔려 안 보인다 |
   | `recalculatePointsOnAwake` | true | **false** | 스켈레톤 메시 전체 바운드를 잡아 몸통만 한 리본이 나온다 |
   | `startActive` | true | **false** | 공격과 무관하게 상시 방출 |

3. 스케일 맞춤 3개 — 이 프로젝트 값에 맞춘다:
   `trailLifetime 0.28` · `maximumSmoothedSectionDistance 0.04` · `maxIntermediateSectionsPerFrame 16`
   (뒤 둘은 178°/0.27초가 벤더 문서 §8 **"Very fast weapons"** 구간이라 필요하다. 없으면 바깥 호가 각진다.)
4. `WeaponTrail_Slash.prefab` 우클릭 → **Create > Prefab Variant** → 새 Variant 에서 **preset 참조만** 오버라이드

**색 고를 때 1순위 필터: 어두운 머티리얼은 이 보드에서 죽는다.** 청록·갈색 바닥 위에서 검은 얼룩으로 읽힌다.
Red(`Path18Slash`)와 Lightning(짙은 남색)이 이 이유로 탈락했다 — Lightning 프리셋·Variant 는 선택지로 남겨뒀지만 어디에도 안 쓴다. **따뜻한 색이 분리된다.**

### 현재 룩 7종

| 룩 | 머티리얼 | maxDissolve | 칼밑 파티클 |
|---|---|---|---|
| ToonFire | `Path7Slash` | 0.55 | `SlashToonFire` |
| ToonWater | `Path6Slash` | 0.55 | `SlashToonWater` |
| ToonBlue | `Path14Slash` | 0.41 | — |
| ToonGreen | `Path14Slash2` | 0.41 | — |
| Cyan | `Path15Slash` | 0.41 | — |
| Simple | `Path0Slash` | 0 | — |
| Lightning (미사용) | `Path4Slash` | 0.7 | `SlashLightningTrails` |

파티클은 벤더 `Slash*.prefab` 을 **그대로** 참조한다(중첩 트레일 없는 순수 ParticleSystem 확인).

## 레시피 C — 모양·크기를 바꾼다

**`WeaponTrail_Slash.prefab`(base) 한 곳.** Variant 7종이 전부 상속하므로 여기서 한 번 고치면 전 룩에 적용된다.

| 자식 | 현재 로컬 위치 | 성격 |
|---|---|---|
| `Trail Point A` | `(0.047, −1.00, 0)` | **형태 레버** |
| `Trail Point B` | `(0.047, −2.30, 0)` | **가독성 부채** |

- **Point A 를 손에서 바깥으로 뺀다 = 초승달.** A 가 회전 피벗(손) 근처면 A 가 거의 안 움직여 손에서 퍼지는 **부채꼴**이 된다. 안쪽 호를 만들어야 참격으로 읽힌다.
- **Point B 를 늘리는 건 비싸다.** 호 길이 = 반경 × 각도인데 각도가 이미 178° 다. B 를 2배로 빼면 경로가 5타일을 훑어 "안 맞는 걸 벤 것처럼" 보인다.
- **A·B 를 함께 바깥으로 미는 것이 정답 방향이다.** 폭이 아니라 **반경**이 커져 호가 커지면서도 캐릭터를 덮지 않는다. 폭만 키웠다가 리본이 유닛을 가리고 인접 유닛 궤적이 한 덩어리로 뭉쳐 되돌린 이력이 있다.
- **z 를 건드리지 말 것.** 오프셋은 스켈레톤 평면 안에 둔다. 평면 밖(로컬 깊이축)으로 빼면 틸트 빌보드에서 리본이 눕는다.

> **Point A/B 는 무기 실측 지오메트리와 무관하다**(2026-07-31 사용자 결정). 시스템은 두 Transform 만 보고 무기의 존재조차 모른다. 무기 스킨이 유닛마다 달라도 클래스 전체가 **같은 참격 형태**를 갖는 게 의도다 — 무기별로 맞추면 클래스 정체성이 흩어진다.

**특정 유닛만 다른 크기**로 하려면 그 룩 Variant 에서 자식 트랜스폼을 오버라이드한다(Variant 는 자식 트랜스폼 오버라이드 가능, 코드 0). **보스 궤적 축소가 이 경로다** — 보스는 `spineVisualScale` 이 2.6~3.2 라 리그가 그대로 스케일돼 호가 ~4타일인데 사거리는 2다.

## 레시피 D — 본 없는 호스트(구조물·포탑)에 붙인다

길만 열려 있고 **아직 소비자가 없다.** 만들 때는:

1. base 프리팹의 **`BoneFollower` 를 뺀 Variant** 를 만든다
2. `Bind` 를 부르지 않거나 `Bind(null)` — 오류가 아니라 **구조물 경로**다
3. Point A/B 가 부모 트랜스폼을 그대로 따라간다. `HS_SwordMeshTrail` 은 두 Transform 만 보므로 스켈레톤 유무를 모른다
4. 호스트가 사건에 맞춰 `Play(seconds)` 를 직접 부른다

## 레시피 E — 공격이 아닌 사건으로 구동한다

`WeaponTrailRig` 가 아는 것은 이 셋뿐이다. 호스트 타입에 대한 지식이 0이라 어디에 붙여도 된다.

| API | 동작 |
|---|---|
| `Bind(SkeletonRenderer)` | `BoneFollower` 에 스켈레톤 주입 + `Initialize`. **null 허용**(레시피 D) |
| `Play(float seconds)` | 방출 시작 + `seconds` 뒤 자동 정지. 연속 호출은 정지 시각을 **밀 뿐** 코루틴을 겹쳐 만들지 않는다 |
| `StopNow()` | 즉시 정지 |

현재 유일한 호출처는 `SpineUnitView.AttachWeaponTrail`(스폰 시 1회) / `PlayWeaponTrail`(`PlayAttack` 안). 다른 사건(스킬 시전·돌진·사망 연출)에 물리려면 그 자리에서 `Play` 를 부르면 된다 — **심(ECS)은 건드릴 일이 없다.** 궤적은 판정에 기여하지 않는다.

---

## 되돌리면 안 되는 것

1. **리그 루트의 빈 `Animator`.** 없으면 벤더가 `transform.root` 에 이벤트 수신기를 붙이고 `workWithoutAnimation = true` 로 켜서 **공격과 무관하게 상시 방출**한다.
2. **정렬의 유일한 소스는 프리셋 asset 이다.** HS 가 매 `LateUpdate` 끝에 `renderer.sortingOrder` 를 프리셋 값으로 되쓴다 → 런타임 외부 쓰기는 무효. **벤더 스크립트 수정 금지**, 프리셋 복사본으로 해결한다.
3. **파티클 정렬은 리그 소유 + 호스트 스윕 제외가 한 쌍이다.** 리본 메시는 씬 루트라 안전하지만 파티클은 리그의 **자식**이라 `SpineUnitView.UpdateSortingOrder` 의 `GetComponentsInChildren<Renderer>` 에 걸려 유닛 대역으로 끌려간다(실측 111 vs 리본 15500). `WeaponTrailRig.ApplyEffectSorting` 과 `IsChildOf(rigRoot) continue` 중 **한쪽만 있으면 매 프레임 다시 덮인다.**
4. **방출 창은 `_skeleton.timeScale` 까지 나눈다.** 안 나누면 0.25× 슬로우모에서 창 0.269s 대 스윙 1.075s 로 4배 모자라 방출이 끊긴다.
5. **`Billboard` 는 `Tilted` 를 유지한다.** `BillboardRotation.Compute(Tilted, …)` = `Quaternion.Euler(tilt,0,0)` 로 **카메라를 보지 않아서** 스프라이트와 리본이 같은 고정 월드 평면에 산다. `Full`/`YAxis` 로 바꾸면 카메라 이동 중 어긋남 우려가 되살아난다.
6. **시간 제어는 `Time.time` 그대로.** 슬로우모/정지 중에는 두 점이 얼어 새 섹션이 안 생기고 기존 섹션만 수명대로 증발한다 — **이건 사양이다.** 별도 시간 배선을 만들지 않는다(TimeManager 원칙).
7. **범위는 타입이 아니라 데이터에 산다.** 궤적 필드는 `ISpineUnitVisualData`(디펜더·적·보스 공용)에 있다. "지금 누구에게 켜나"를 타입 경계로 굳혔다가 되돌린 이력이 있다 — 상세는 spec README "설계 오판".

## 증상 → 원인

| 증상 | 원인 | 고칠 곳 |
|---|---|---|
| 궤적이 아예 안 보인다 | `weaponTrailPrefab` 미할당 | 유닛 SO |
| 유닛 뒤에 깔린다 | 프리셋 `sortingOrder` 가 0 | 프리셋 asset (15500) |
| **파티클만** 앞 유닛에 가린다 | 리그/호스트 정렬 한 쌍 중 한쪽 누락 | `WeaponTrailRig` + `SpineUnitView.UpdateSortingOrder` |
| 몸통만 한 거대 리본 | `recalculatePointsOnAwake` true | 프리셋 asset |
| 공격 안 해도 계속 방출 | `startActive` true **또는** 리그 루트 Animator 누락 | 프리셋 asset / base 프리팹 |
| 칼을 되돌리는 자국이 남는다 | `endNormalized` 가 크다 | 유닛 SO |
| 슬로우모에서 방출이 끊긴다 | 창 계산에서 `_skeleton.timeScale` 누락 | `SpineUnitView.PlayWeaponTrail` |
| 리본이 바닥에 눕는다 | Point 오프셋에 z 성분 | base 프리팹 |
| 손에서 퍼지는 부채꼴 | Point A 가 회전 피벗(손)에 너무 가깝다 | base 프리팹 |
| 얼룩으로 보인다 / 안 읽힌다 | 어두운 머티리얼 · 스윙 각도 부족 | 프리셋 룩 교체 · 공격 애니 확인 |
| 인접 유닛 궤적이 한 덩어리 | 리본 폭 과다 | base 프리팹 (반경↑ 폭↓) |
| 무기가 안 보인다(하네스에서) | 스킨 적용 후 프레임이 안 돌아 메시 미재생성 | 하네스 문제. 프레임 넘기고 재촬영 |
| 화면에 어두운 헤이즈(하네스에서) | 맨 `Camera` 로 촬영 | `CopyFrom(main)` 으로 실보드 카메라 복제 |

## 파일 지도

```
Assets/_Project/Scripts/
  Presentation/WeaponTrailRig.cs         리그 자립 컴포넌트 — Bind / Play / StopNow + 파티클 정렬
  Presentation/SpineUnitView.cs          AttachWeaponTrail · PlayWeaponTrail · UpdateSortingOrder 제외
  Presentation/BoardSortOrder.cs         WeaponTrailOrder = 15500 (Beam 15000 < 여기 < HitBar 16000)
  Data/ISpineUnitVisualData.cs           SpineWeaponTrailPrefab / SpineWeaponTrailEndNormalized
  Data/DefenderUnitData.cs               weaponTrailPrefab / weaponTrailEndNormalized (직렬화 호환 위해 맨 뒤)
  Data/AttackUnitData.cs                 동일 (적·보스)

Assets/_Project/VFX/
  WeaponTrail_Slash.prefab               base 리그 — Animator(빈) + BoneFollower(Gear) + HS_SwordMeshTrail
                                          + WeaponTrailRig + Trail Point A/B
  WeaponTrail_Slash_{Look}.prefab        룩별 Variant 7종 — preset 참조만 오버라이드
  WeaponTrailPreset_{Look}.asset         프로젝트 소유 프리셋 7종 (벤더 복사본 + 오버라이드)

Assets/Hovl Studio/                      벤더. HSFiles(184) + Epic Sword Slash(77) 전량 커밋됨
  HSFiles/Scripts/HS_SwordMeshTrail.cs   절차 리본 생성. 수정 금지
  HSFiles/Scripts/Hovl.HSFiles.asmdef    ★없으면 Wassup.Runtime 이 벤더 타입을 못 쓴다
  .../Demo scene/Sword_Mesh_Trail_System_User_Guide.docx.pdf   벤더 문서 14쪽
```

> `Marta chan` · `Procedural fire` 는 같은 벤더의 **다른 패키지**이고 미추적이다. 궤적은 이 둘에
> 의존하지 않는다(전이 의존 검사 완료). 벤더 데모 씬만 `Marta_chanV2.prefab` 을 참조하는데
> 그 씬은 빌드 세팅에 없다.

## 확장 후보

`docs/spec/README.md` Follow-up Backlog 에 등록돼 있다.

- **보스 전용 크기** [S] · 호 ~4타일 vs 사거리 2. 보스 Variant 에서 Point A/B 만 좁힌다
- **무기 종류별 프리셋 분기** [S] · 도끼/둔기/마법무기에 다른 색·수명
- **타격 순간 강조** [S] · `hitDelaySec` 시점에 밝기 펄스. 기존 `attackVfxPrefab` 과 역할 분담 필요
- **구조물 호스트** [M] · 레시피 D. 실제 소비자가 생기면
- **공격 애니 다양성** [S] · 읽히는 건 `Attack3` 뿐이라 9종이 같은 모션이다. 다른 모션을 쓰려면 그 모션의 스윙 폭부터 키워야 한다(스켈레톤 저작)
- **절차 스윙(본 비의존)** [M] · 두 점을 본이 아니라 스크립트 아크로 구동. 애니 궤적이 원하는 형태가 아닐 때의 탈출구
- **모바일 실기기 프로파일** [S] · 동시 근접 유닛 최대치에서 `LateUpdate` CPU 측정

## 알려진 잠재 결함

`WeaponTrailRig._stopPending` 은 GameObject 비활성화 시 코루틴만 죽고 플래그가 남아 이후 **영구 방출**이 된다. 현재 Spine 유닛은 `SetActive(false)` 없이 `Destroy` 만 해 도달 불가 — **풀링을 도입하면 같이 손볼 것.**
