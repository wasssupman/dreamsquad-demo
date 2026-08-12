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

### 브레스 VFX

`VFXPACK_FIRE_WALLCOEUR` 는 불꽃/연기 앰비언스 팩이고 부채꼴 프리팹은 없다. 조립한다:

- 본체: `VFX_GroundFire_Line.prefab` 을 부채꼴로 몇 장 겹치거나 `VFX_Fire.prefab` 을 대상 방향으로
  늘려 배치
- 착탄감: `VFX_GroundFire_Circle.prefab` 을 콘 중심선 끝에 1장
- ⚠ **벤더 원본을 SO/스포너에 직접 참조하지 않는다.** `Assets/_Project/VFX/` 아래 복제본만 연결한다
  (`projectile-ga-reskin` 공통 원칙)
- 트리거는 unit 4 가 정한 «`VfxSpawner` 직접 호출» 경로. 슬롯이 null 이면 `LogError` — 코드 폴백 없음

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
