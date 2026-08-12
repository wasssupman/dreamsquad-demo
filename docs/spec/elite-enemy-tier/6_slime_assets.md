# 6 — 슬라임 에셋 (부모 + 분열 자식)

## 목적

엘리트 슬라임과 그 분열 자식을 저작하고 라이브 로스터에 노출한다. unit 5 의 기계가 실제 콘텐츠로
돌아가는지 확인하는 단위다.

## 변경 대상

- `Assets/_Project/Data/Enemies/Enemy_Slime.asset` (신규)
- `Assets/_Project/Data/Enemies/Enemy_Slime_Small.asset` (신규)
- `Assets/_Project/Data/EnemyCatalog.asset` — 2종 등록
- `Assets/_Project/Scripts/Data/Decks/Deck_*.asset` (라이브 7덱) — `attackUnitPool` 에 **부모만**
- Spine: `Assets/Spine Examples/Spine Skeletons/sack/sack-pro_SkeletonData.asset` 참조

## 구현

### 저작값 (README 유닛 사양 표가 정본)

| | 부모 `slime` | 자식 `slime_small` |
|---|---|---|
| tier | **Elite** | **Normal** |
| health | 120 | **60** (부모의 50%) |
| outputs | `Damage 12` | **`Damage 12`** (계승) |
| moveSpeed / attackRange / attackCooldown | 1.8 / 1 / 0.9 | 동일 |
| enemyClass / attackMethod / targetMode / engageMovement | Bruiser / Melee / Nearest / Halt | 동일 |
| hitDelaySec | 0.3 | 0.3 |
| killScore / awakeningReward / stabilityDamage | 3 / 3 / 2 | 1 / 1 / 1 |
| maxPerWave / minWaveNumber | 1 / 3 | — (생성 대상 아님) |
| **waypointPathIndex** | **-1** | **-1** |
| traversalLayers | None(→`Path` 폴백) | 동일 |
| nightmareMechanics | `OnDeath × SplitOnDeath(magnitude 2, splitUnit = Enemy_Slime_Small)` | **비움** |

⚠ **`waypointPathIndex = -1` 은 계약 3 이다.** 경로를 저작하면 자식이 부모의 순서 진행도를
물려받지 못해 **부모가 이미 지난 지점으로 되돌아간다.**

⚠ **자식의 `nightmareMechanics` 는 반드시 비어 있어야 한다.** 채우면 무한 분열이 열린다(계약 2 의
재귀 차단이 이 한 칸이다).

### Spine

| 필드 | 값 | 근거 |
|---|---|---|
| skeletonDataAsset | `sack-pro_SkeletonData` | |
| idleAnimation | `walk` | sack 에는 idle 애니가 **없다**(실측: `fall-in`·`walk` 2종) |
| walkAnimation | `walk` | |
| attackAnimation | **`fall-in`** | 사용자 지정 — 몸통 내리찍기로 읽힌다 |
| deathAnimation | **빈 값** | 사용자 지정 «죽으면 그냥 분리». `SpineUnitView` 가 빈 값에서 `Destroy(gameObject)` 를 즉시 호출하므로 그 프레임에 사라진다 |
| partSkins | 비움 | 고유 스켈레톤 분기(`partSkins` 가 비면 단일 스킨) |
| spineVisualScale | **측정해서 정한다** | 자식은 부모의 약 60% |
| visualOffset | 측정해서 정한다 | 피봇이 이동타일 중심에 오도록 |

`visualMaterial` 은 반드시 채운다 — `SpawnUnit` 이 null 이면 «entity will not render» 경고를 내고
**스폰을 포기**한다.

### 로스터 노출

- `EnemyCatalog.units` 에 **2종 다** 등록한다(id → SO 해석은 스탯 시트 갱신 경로가 쓴다).
- `attackUnitPool` 에는 **부모만** 넣는다. 자식은 분열로만 등장한다(`Enemy_Skimmer` 의 «라이브
  일반 덱에 아직 넣지 않는다» 선례).
- 삽입 위치는 **풀 중간**. 맨 뒤면 `ResolveWaveEligibleIndex` 의 전방 순환이 초반 웨이브를
  `pool[0]` 로 쏠리게 한다.

## 완료 기준

- [ ] Unity 콘솔 에러·경고 0 (스폰 시 material/skeleton 경고 없음)
- [ ] **sack 애니 실측 확인** — Unity 인스펙터에서 `fall-in`·`walk` 존재를 눈으로 확인한다
      (바이너리 `.skel.bytes` 라 문자열 추출로는 완전 목록을 보증하지 못했다)
- [ ] Play: 슬라임이 걷고, 방어유닛에 붙어 `fall-in` 으로 때린다
- [ ] Play: 죽으면 **모션 없이 즉시 사라지고** 같은 자리에 작은 슬라임 2기가 생긴다
- [ ] Play: 작은 슬라임을 죽여도 더 생기지 않는다
- [ ] Play: 두 크기가 화면에서 구분된다(스케일 육안)
- [ ] `WaveKillBudgetPinTests` 를 포함한 EditMode 전체 통과 — 풀이 바뀌면 **웨이브 baseline 이
      바뀌는 것이 정상**이므로, 실패하는 pin 테스트는 새 baseline 으로 갱신하고 그 사실을 커밋
      메시지에 명시한다
