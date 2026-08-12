# 6 — 슬라임 에셋 (부모 + 분열 자식)

## 목적

엘리트 슬라임과 그 분열 자식을 저작하고 라이브 로스터에 노출한다. unit 5 의 기계가 실제 콘텐츠로
돌아가는지 확인하는 단위다.

## 변경 대상

- `Assets/_Project/Data/Enemies/Enemy_Slime.asset` (신규)
- `Assets/_Project/Data/Enemies/Enemy_Slime_Small.asset` (신규)
- `Assets/_Project/Data/EnemyCatalog.asset` — 2종 등록
- `Assets/_Project/Scripts/Data/WavePlans/WavePlan_SlimeTest.asset` (신규) +
  `Assets/_Project/Data/Config/TestModeConfig.asset` `planCatalog` 등록 — 검증 경로(아래)
- ~~`Deck_*.asset` `attackUnitPool`~~ → **별도 커밋으로 분리**(아래 «검증 경로» 참조)
- Spine: `Assets/Spine Examples/Spine Skeletons/sack/sack-pro_SkeletonData.asset` 참조

## 구현

### 저작값 — **2단계 분열** (사용자 결정 2026-08-12)

`슬라임 → 중간 ×2 → 작은 ×4`. 단계마다 체력 절반, 공격력은 전 단계 계승.

| | `slime` | `slime_mid` ×2 | `slime_small` ×4 |
|---|---|---|---|
| tier | **Elite** | Normal | Normal |
| health | 120 | **60** | **30** |
| outputs | `Damage 12` | 동일(계승) | 동일(계승) |
| moveSpeed / attackRange / attackCooldown | 1.8 / 1 / 0.9 | 동일 | 동일 |
| enemyClass / attackMethod / targetMode / engageMovement | Bruiser / Melee / FocusUntilDead / Halt | 동일 | 동일 |
| **awakeningReward** | **3** | **0** | **0** |
| **killScore** | **3** | **0** | **0** |
| **stabilityDamage** | 2 | **1** | **1** |
| spineVisualScale | 0.55 | 0.42 | 0.30 |
| maxPerWave / minWaveNumber | 1 / 3 | — (생성 대상 아님) | — |
| **waypointPathIndex** | **-1** | **-1** | **-1** |
| nightmareMechanics | `OnDeath × SplitOnDeath(2, slime_mid)` | `OnDeath × SplitOnDeath(2, slime_small)` | **비움 = 사슬 종료** |

**보상 불변식 — 분열은 보상을 나누지 않는다.** 분열체의 `awakeningReward`·`killScore` 는 **0** 이고
총량은 엘리트 본체 하나 몫(각성 3 / 점수 3)이다. 단계를 더 늘려도 총량이 안 변한다.
근거: 각성 20 = 드림캐쳐 1장(`AwakeningConfig.costSquad/Unit/Active`)이고 **보스가 5** 다.
자식에 1씩 주면 `3 + 2×2 + 4×1 = 11` 로 웨이브 슬롯 하나가 보스 두 배를 뱉는 **처치 7회짜리
각성 농장**이 된다.

**반대로 `stabilityDamage` 는 자식에게 남긴다(1씩).** 그건 보상이 아니라 **놓쳤을 때의 대가**라
마릿수만큼 커지는 것이 맞다 — 4마리가 다 새면 벌이 2 → 4 로 커진다.

⚠ **유효 체력은 단계마다 유지된다**(120 / 2×60 / 4×30 = 각 120). 즉 웨이브 슬롯 하나가 총 360
체력 + 처치 7회다. `maxPerWave 1` 을 유지할 것.

⚠ **`waypointPathIndex = -1` 은 계약 3 이다.** 경로를 저작하면 자식이 부모의 순서 진행도를
물려받지 못해 **부모가 이미 지난 지점으로 되돌아간다.**

### 재귀 차단이 «사슬 검증» 으로 옮겨졌다

초판 가드는 «자식이 메커닉을 갖고 있으면 경고» 였고, 1단계에서는 그게 재귀 방어였다.
**2단계가 의도가 되면 그 경고는 거짓 신호**가 된다(중간은 메커닉을 가져야 한다).
무한 분열을 실제로 만드는 것은 **사슬이 자기에게 돌아오는 것**이므로 판정을 옮겼다 —
`Data/SplitChain.cs`(순수 함수, `Validate`/`NextInChain`) + `SplitChainTests`(자기순환·간접순환·
과길이·null 종료). bake 는 그 술어를 호출해 loud 거절한다.

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
- `attackUnitPool` 은 **후속 커밋**. 넣을 때 **부모만** 넣고(자식은 분열로만 등장 —
  `Enemy_Skimmer` 선례) 삽입 위치는 **풀 중간**이다(맨 뒤면 `ResolveWaveEligibleIndex` 의 전방
  순환이 초반 웨이브를 `pool[0]` 로 쏠리게 한다).

## 검증 경로 — 라이브 덱을 건드리지 않는다

⚠ **이 단위에서는 `attackUnitPool` 에 넣지 않는다.** 넣는 순간 그 덱의 웨이브가 1번부터 전부
재추첨돼서 「슬라임이 동작한다」와 「라이브 밸런스가 바뀐다」가 한 커밋에 섞인다. 대신
**TEST MODE**(`wave-authoring-test-mode`)로 검증한다:

- `Assets/_Project/Scripts/Data/WavePlans/WavePlan_SlimeTest.asset` — 웨이브 1 = 슬라임 1기
  (마지막 적 상황 재현) · 웨이브 2 = 슬라임 3기 · 웨이브 3 = 잡몹. `timerDurationSec 0` = endless
- `Assets/_Project/Data/Config/TestModeConfig.asset` `planCatalog` 에 등록 → 아웃게임
  **TEST MODE** 버튼에서 「엘리트 슬라임 분열 e2e」 선택

라이브 덱 등록은 **별도 커밋**으로 분리한다(새 웨이브 baseline 을 diff 에 드러내기 위해).

## 완료 기준

- [ ] Unity 콘솔 에러·경고 0 (스폰 시 material/skeleton 경고 없음)
- [ ] **sack 애니 실측 확인** — Unity 인스펙터에서 `fall-in`·`walk` 존재를 눈으로 확인한다
      (바이너리 `.skel.bytes` 라 문자열 추출로는 완전 목록을 보증하지 못했다)
- [ ] Play: 슬라임이 걷고, 방어유닛에 붙어 `fall-in` 으로 때린다
- [ ] Play: 죽으면 **모션 없이 즉시 사라지고** 같은 자리에 작은 슬라임 2기가 생긴다
- [ ] Play: 작은 슬라임을 죽여도 더 생기지 않는다
- [ ] Play: 두 크기가 화면에서 구분된다(스케일 육안)
- [ ] EditMode 전체 통과. 이 단위는 라이브 풀을 건드리지 않으므로 **웨이브 pin 테스트가
      그대로 초록이어야 한다**(빨개지면 풀을 잘못 만진 것이다)
- [ ] 자동 검증(unit 5·6 공통): `SlimeSplitAuthoringTests`(EditMode) + `SlimeSplitE2ETest`
      (PlayMode) 통과. e2e 는 **`bridge.StartBattle()` 이 필수**다 — `Update` 가
      `if (!_running) return;` 로 막혀 있어 시작하지 않으면 브리지 드레인이 한 번도 돌지 않고,
      분열은 그 드레인에 살아 있어서 «자식 0» 이 되며 원인이 구현처럼 보인다(실제로 겪었다)
