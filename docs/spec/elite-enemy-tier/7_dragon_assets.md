# 7 — 드래곤 에셋 (비행 · 화염 스택 · 브레스 VFX)

## 목적

엘리트 드래곤을 저작한다. 기본공격은 킨들러의 화염 스택 파이프라인을 그대로 타고, 3타마다
unit 4 의 부채꼴 브레스가 터진다.

## 변경 대상

- `Assets/_Project/Data/Enemies/Enemy_Dragon.asset` (신규)
- `Assets/_Project/Data/EnemyCatalog.asset` — 등록
- `Assets/_Project/Scripts/Data/Decks/Deck_*.asset` (라이브 7덱) — `attackUnitPool`
- `Assets/_Project/VFX/` — `VFXPACK_FIRE_WALLCOEUR` 프리팹 **복제본** + `VfxSpawner` 슬롯
- Spine: `Assets/Spine Examples/Spine Skeletons/Dragon/dragon_SkeletonData.asset` 참조

## 구현

### 저작값 (README 유닛 사양 표가 정본)

| 필드 | 값 | 비고 |
|---|---|---|
| tier | **Elite** | |
| health / moveSpeed | 110 / 2.0 | |
| **traversalLayers** | **`Air`** | 비행 타입 |
| **flightLift** | 1.4 | `Enemy_Skimmer` 와 같은 값 |
| attackRange / **attackCooldown** | 2 / **1.2** | ★계약 4 — 내리지 말 것 |
| hitDelaySec | 0.3 | 기존 슈터 관례 |
| enemyClass / attackMethod | Shooter / Projectile | |
| **engageMovement** | **`Halt`** | 사거리 진입 후 정지 후 사격(킨들러와 동형). 아래 정정 참조 |
| **targetMode** | **`FocusUntilDead`** | ★`Nearest` 면 **어느 방어유닛도 5스택에 못 간다** — 킨들러 계약이 ★로 표시한 그 함정이다 |
| outputs | `Damage 6` + `ApplyStack(Fire, +1, perApp 3.0, maxStack 5)` | perApp 3.0 > cd 1.2 (사격 중 스택 만료 방지) |
| projectile | `Projectile_Enemy_Fireball.asset` **재사용** | 킨들러와 같은 탄. 신규 복제 없음 |
| killScore / awakeningReward / stabilityDamage | 3 / 3 / 2 | |
| maxPerWave / minWaveNumber | 1 / 4 | |
| waypointPathIndex | -1 | |
| nightmareMechanics | `AttackN(period 3) × AreaBreath(magnitude 20, tileRange 3, coneHalfAngleDeg 45)` | |

`maxStack 5` ↔ `StackModifier_Fire.maxStack 5` · output `duration 3.0` ↔ SO `perAppDuration 3.0` 을
**양쪽 명시 저작**한다. 한쪽만 바꾸면 조용히 어긋난다(`enemy-fire-stack-shooter` 계약 4).
폴백(`stackMaxStack 0`)에 의존하지 않는다.

**`Halt` + `FocusUntilDead` 로 정정한 이유** (리뷰 HIGH): 초판은 `Advance` + `Nearest` 였고 근거는
«정지하면 `flying` 이 슬로모로 늘어진다» 였다. **그 버그는 이미 고쳐져 있다** —
`enemy-walk-anim-speed unit 4` 가 `SpineUnitView.ApplyTimeScale` 을
`factor = (_moving && IsLocomotionLoopPlaying()) ? _walkFactor : 1f` 로 바꿨고, 주석이
*«minTimeScale 은 느린 이동 하한이지 정지 유닛에 쓰라는 게 아니다»* 라고 못 박아뒀다. 정지한
드래곤은 배율 1 = 자연속도로 날갯짓한다(= 제자리 호버링으로 읽힌다).

근거가 사라지면 `Advance` + `Nearest` 는 **설계의 절반을 죽인다**: 이동하며 최근접을 쏘면 대상이
계속 바뀌어 스택이 5에 도달하지 못하고, 화상이 영영 안 터진다. 아래 완료 기준의 «5스택에서 화상이
터진다» 와 정면 충돌한다.

### Spine — 애니가 `flying` 하나뿐인 것의 귀결

| 필드 | 값 |
|---|---|
| skeletonDataAsset | `dragon_SkeletonData` |
| idleAnimation / walkAnimation | **`flying` / `flying`** |
| attackAnimation | **빈 값** — `PlayAttack` 이 early-return 해서 `flying` 루프가 끊기지 않는다 |
| deathAnimation | **빈 값** — 즉시 `Destroy` |
| spineVisualScale | **측정해서 정한다.** dragon 스켈레톤은 저작 폭 1287 로 매우 커서 큰 축소가 필요하다 |
| visualOffset | 측정해서 정한다 |

### 스케일 — 측정으로 잡는다 (추정 금지)

세 스켈레톤 모두 `SkeletonDataAsset.scale = 0.01` 이라 저작 크기 비교가 그대로 유효하다:

| 스켈레톤 | 저작 크기 | `spineVisualScale` | 화면 실효 |
|---|---|---|---|
| Casual Character (기존 적 전원) | 125×188 | 1.3 | 162×244 |
| sack (슬라임 본체) | 519×813 | 0.55 | 285×447 |
| **dragon** | **660×643** | **0.6** | **396×386** |

⚠ 초판은 `0.12` 였다 — 잠깐 검토했다가 기각한 `raptor-pro` 의 저작 폭 **1287 을 드래곤 값으로
착각**해 나온 수이고, 실제로는 잡몹의 1/3 크기(79×77)로 보였다. 스켈레톤을 바꿀 때
`skeleton.width/height` 를 **다시 읽을 것**.

### facing — 데이터로 정규화한다 (코드 분기 금지)

Dragon 리그는 프로젝트 규약과 **반대 방향**을 본다. 규약은 `SkeletonFlipXModifier` 주석이
정의한 *«rig faces −x (left) at Skeleton.ScaleX = +1»* 이고 `SpineUnitView.FaceToward` 의 부호
규칙이 그것을 전제한다.

해법은 `Assets/_Project/Characters/SkeletonFlipX.asset` 을 `dragon_SkeletonData.asset` 의
`skeletonDataModifiers` 에 넣는 것 — **저작 1줄, 코드 0**. 로드 시 루트 본 setup-pose 의
`ScaleX` 를 force-negative 로 만들어 리그 전체를 한 번 미러링하고, `FaceToward` 와
`net facing = Skeleton.ScaleX × rootScaleX` 로 자연 합성된다.
(`summon-patrol-defender` unit 8 이 세운 규약 — 유닛별 flip 플래그를 런타임에 흘리지 않는다.
이 모디파이어의 **첫 실사용**이다.)

### 브레스 VFX

`Assets/_Project/VFX/AreaBreath_Fire_SKELETON.prefab` — `VFXPACK_FIRE_WALLCOEUR` 의
`VFX_GroundFire_Line` **복제본**(벤더 원본 직접 참조 금지 — `projectile-ga-reskin` 원칙).
바꾼 것은 `looping: 1 → 0`(팩은 전부 앰비언스용 루프)과 루트 이름뿐. 3 emitter × 20 = **60 파티클**
(임팩트 예산 100 안). `_SKELETON` 접미사 유지 — 정식 승격은 사용자 승인 후(스킬 Iron Law).

⚠ **한 번 잘못 만들었다(2026-08-13).** 처음엔 `Meteor_Burst_SKELETON` 을 복제해 `arc`·회전만
바꿨다 — 「이 프로젝트에서 검증된 화염 원샷」이라는 근거였지만 그건 **방사형 임팩트 버스트**라
아키타입 자체가 브레스와 다르다. 사용자 판정: *「그냥 화면 덮는 메테오 떨어진 느낌」*.
형판 재사용이 **아키타입 일치를 대체하지 못한다**.

⚠ **화면을 덮은 실제 원인은 스케일 산식이었다.** 초판은 콘 기하를 그대로 그리려 했다 —
`폭 = rangeWorld × tan(반각) × 2` → 사거리 3 · 반각 50° 면 **7.15 유닛**. 부채꼴 «입» 너비로는
기하적으로 맞지만 화면에서는 광역 폭발로 읽힌다. 이제 연출은 **저작된 크기**를 쓴다
(`areaBreathVfxScalePerTile` × 사거리, `areaBreathVfxScaleMax` 로 상한). 반각은 **판정
파라미터일 뿐**이고 화면 크기에 관여하지 않는다 — 판정과 연출이 갈리는 것을 의도적으로 받아들인다.

크기 튜닝 knob 을 인스펙터에 둔 이유: 구현자가 화면을 볼 수 없어 코드에 박으면 매번 재컴파일
왕복이 된다(제약 6 의 정신과도 맞다).

### 연출 소유권 = `VfxSpawner` (브리지 아님)

⚠ **초판은 이 전부를 `BattleBridge` 에 넣었다** — 프리팹 슬롯 + `Instantiate` + 정렬 변이 +
`Destroy` 타이머 + 튜닝 knob 4개. 브리지는 **ECS 창구**이고 원샷 VFX 의 프리팹 슬롯·스폰·수명은
`Presentation/VfxSpawner` 가 소유한다(`object-pipeline-map` 의 VFX 아키타입 — «프리팹 소스 =
VfxSpawner SerializeField 슬롯»). `unity-vfx-integration` 스킬도 «`VfxSpawner` 나 presenter
계층에 연결» 이라고 명시한다. **2026-08-13 사용자 지적으로 이관.**

현재 형태 — `SpawnHealApplied`/`SpawnGoalCollapse` 와 같은 관용구:
- `VfxSpawner.SpawnAreaBreath(originView, aimDirXZ, rangeWorld, halfAngleDeg)` 가 슬롯·스폰·
  정렬·수명을 전부 소유. 슬롯이 비면 `LogWarning` + 링 펄스 폴백
- 브리지는 드레인에서 **뷰 앵커만 풀어서 넘긴다**(`spineUnitPool` 접근이 거기 있으므로)
- 수명은 **`ConfigureOneShot`** 이 정한다 — 루프형 벤더 프리팹을 *스폰 인스턴스에서만*
  단발화하고 자가 파괴 시각을 돌려주는 기존 헬퍼. 그래서 프리팹 복제본의 `looping` 은
  **건드리지 않는다**(초판은 YAML 에서 껐는데 그것도 관용구 위반이었다 — 「공유 에셋 무접촉」)

트리거는 unit 4 의 `UnitAttackVisualEvent` 필드 append 경로.
배선은 `BreathVfxPrefab_IsWiredOnVfxSpawner` 가 고정한다 — 씬 YAML 직접 편집이라 자동 검증이
없으면 잘못된 `fileID` 로 `null` 이 된 것을 육안으로만 알 수 있고 실제로 첫 시도가 그랬다.
같은 테스트가 **브리지가 슬롯을 되가져가지 않았는지**도 단언한다(소유권 회귀 방지).

### 로스터 노출

`EnemyCatalog` 등록 + `attackUnitPool` **중간** 삽입 (unit 6 과 같은 이유).

## 완료 기준

- [ ] Unity 콘솔 에러·경고 0
- [ ] Play: 드래곤이 **떠서** 이동하고, 지상 방벽·차단 해저드를 무시한다(Air 층)
- [ ] Play: **대공사수만** 드래곤을 때린다. 기존 지상 전용 방어유닛은 사거리 안에 있어도 못 때린다
- [ ] Play: 체력 게이지가 lift 를 따라 올라간다 (`waypoint-routing` 미확인 항목 — 여기서 닫는다)
- [ ] Play: 기본공격이 화염 스택을 쌓고 5스택에서 화상이 터진다(오라 점등 육안)
- [ ] Play: **3번째 공격마다** 부채꼴 화염이 터지고 그 안의 방어유닛만 피해를 받는다
- [ ] 화상 펄스 확인 — 화상이 **끊긴다**(상시 화상이 아니다). 계약 4 의 부등식이 살아 있다는 증거
- [ ] `flying` 루프가 공격/사망에서 끊기지 않고, **정지 중에도 자연속도**로 돈다(위 정정의 실측)
- [ ] Play (아군 오사): 드래곤 브레스가 같은 웨이브 동료·적 마음을 태우지 않는다 (unit 4 계약)
- [ ] EditMode 전체 통과 — 웨이브 pin 테스트는 새 baseline 으로 갱신하고 커밋 메시지에 명시
