# 2 — `Enemy_Whirlpot` 에셋 + cloud-pot Spine + 카탈로그/덱

## 목적

동작과 연출이 착지한 뒤 몸을 붙이고 라이브에 편입한다. **마지막에 두는 이유** — 아트가
잘못된 동작을 예쁘게 포장하지 않게 한다.

## 변경 대상

- `Assets/_Project/Data/Enemies/Enemy_Whirlpot.asset` (+ `.meta`)
- `EnemyCatalog` — 등록
- 라이브 덱 7종 `attackUnitPool` (`Serpent`·`Coil`·`Twin`·`Spiral`·`Zig`·`Hook`·`Endless`)
- ★**공성 dev 덱 3종**(`Duel`·`Ford`·`Isle`) — `MapDocumentPoolDevEntriesTests` 가 *"적 풀 크기가
  라이브와 다르다 — 비행·엘리트가 빠졌을 수 있다"* 로 라이브와의 **동수**를 요구한다
- ★**`WavePlan_Tutorial.asset`** — `TutorialEntry_TeachesEveryLiveEnemyTypeInTenWaves` 가
  라이브 풀 전종의 등장을 요구한다. 저작된 10웨이브 플랜이라 자동으로 안 따라온다
- `Assets/_Project/VFX/` — 회오리 프리팹(unit 1 산출물)을 이 에셋에 배선

> 「라이브 7종만」이 아니다 — **덱 10종 + 튜토리얼 플랜**이 로스터 추가에 연동된다. 세 가드가
> 각각 다른 이유로 이것을 요구하며, 전부 커밋 전에 빨간불로 알려준다.

## 구현

**스탯** — 값은 [README 유닛 사양](README.md#유닛-사양-초기값-제안--전부-so-소유-튜닝-대상) 표를
정본으로 저작한다. 여기 복제하지 않는다. 형태만 못 박으면:

- `attackMethod: Melee` — **`attackTargetCount` 는 melee/outputs 경로 전용**이다. Projectile 이면 다른 분기를 타고 광역이 안 나온다
- `attackTargetCount: 10` · `attackRange: 2` — 회오리의 실체. **반경 = `attackRange`** 라 이 한 필드가 «멈추는 거리» 와 «도는 거리» 를 겸한다(팽이에는 그게 맞다)
- `outputs`: `Damage` **1개** — 이것이 회오리 피해다. 별도 단일 타격은 존재하지 않는다
- `nightmareMechanics`: **비움** — 이 엘리트는 메커닉이 아니라 저작된 공격 축으로 성립한다(README 계약 1)
- `attackVfxPrefab`: 회오리 · `attackVfxScalePerTile`: 실측 확정

★**`targetFactions` 는 0(미저작)으로 둔다.** 기존 적 에셋을 복제해서 만들면 `13` 이 묻어오고
**방어 본능을 못 때린다** — 2026-08-13 에 신규 적 4종이 정확히 이걸로 태어났다(`feda9054`).
가드는 `AuthoredTargetMaskTests.OnlySpecialEnemies_NarrowTheirTargets`(의도적 좁힘은
heartseeker 하나뿐). **0 으로 두면 기본값 변경을 따라간다 — 29 를 박아넣지 말 것.**

**Spine — `cloud-pot`** (`Assets/Spine Examples/Spine Skeletons/cloud-pot/cloud-pot_SkeletonData.asset`).
바이너리 `.skel.bytes` 라 애니 목록은 **유니티에서 실측**한다. 저작 규칙:

- `idle` = `walk` = 그 리그의 루프 애니 **하나**. 화분은 걷지 않으므로 별개 이동 애니를 찾지
  말고 하나를 둘에 쓴다 — 드래곤이 `flying` 하나로 그렇게 산다
- `attack` = **빈 값**. `PlayAttack` 이 `IsNullOrEmpty` 에서 early-return 해 **루프가 끊기지
  않는다.** 「돈다」는 이펙트가 전담하므로 공격 애니는 오히려 방해다
- `death` = **빈 값** → `SpineUnitView` 가 즉시 `Destroy`
- `spineVisualScale` = **실측 후 확정**(슬라임 0.55 · 드래곤 0.6 참고). 회오리가 몸을 삼키지 않아야 한다
- 톤은 그대로 쓴다 — 밝고 캐주얼한 색조가 이 프로젝트 visual direction 에 이미 맞는다

**덱 편입** ⚠ **`attackUnitPool` 에 1종을 더하면 그 덱의 웨이브가 1번부터 전부 재추첨된다.**
`WavePatternGenerator` 가 `rng.NextInt(0, pool.Count)` 로 뽑으므로 `waveSeed` 가 고정이어도
구성이 바뀐다. 그래서:

- 삽입은 **풀 중간에** — 맨 뒤면 `ResolveWaveEligibleIndex` 의 전방 순환이 초반 웨이브를
  `pool[0]` 로 쏠리게 한다
- `waveSeed` 를 갱신해 **새 baseline 을 커밋 diff 에 드러낸다**
- `maxPerWave 1` 이 수량을 잡는다(0 이면 일반 웨이브 최대 24기)

★**`.meta` 를 짝으로 커밋한다** — 경로 지정 `git add` 에서 빠지면 다른 머신이 GUID 를
재생성해 씬/카탈로그 참조가 깨진다.

## ⚠ 실측으로 드러난 함정 — 엘리트를 `Tanker` 로 저작하면 안 된다

초판 저작은 `enemyClass: Tanker`(느리고 단단하니까)였고 그것이 **유일한 지뢰를 정확히 밟았다.**

- 웨이브 컨셉 5종 중 **`Concept_Heavy` 만 슬롯이 1개**이고, 그 슬롯의 `classFilter` 가 **Tanker** 다
- 엘리트는 `maxPerWave 1` 이 강제된다(`EliteWaves_DoNotCollapseToASingleUnit` 이 별도로 요구)
- 따라서 Tanker 엘리트가 그 슬롯에 뽑히면 **웨이브 전체가 1기로 붕괴**한다

슬라임(`Bruiser`)·드래곤(`Shooter`)이 무사한 이유는 실력이 아니라 구조다 — 어떤 컨셉도
`Bruiser(3)` 를 필터하지 않고, Shooter 를 쓰는 `Concept_Ranged` 는 슬롯이 2개다.

→ `enemyClass: Bruiser`. 「근접 광역 난동꾼」에 분류상으로도 더 맞고(슬라임과 같은 반),
`WhirlpotAuthoringTests.Whirlpot_IsNotTankerClass_...` 가 **원인을 결정론으로** 고정한다
(`EliteWaves_DoNotCollapseToASingleUnit` 은 «뽑혔을 때만» 보는 시드 의존 관측이다).

## 완료 기준

- [ ] Unity 컴파일 에러 0 · 콘솔 에러 0 · 임포트 경고 0
- [ ] `AuthoredTargetMaskTests` 통과(= `targetFactions` 함정 회피 확인)
- [ ] EditMode 전량 — 신규 실패 0. 웨이브 pin 테스트가 새 baseline 으로 갱신됐고 **그 갱신이 diff 에 보인다**
- [ ] **Play 검증 — 검증 질문에 하나씩 답한다**:
  - 걸어오는 동안 **돌지 않는다**(회오리 없음 · 주변 피해 0)
  - 방어유닛 사거리에 닿으면 **그 자리에 멈춘다**
  - 멈춘 동안 **반경 안의 방어유닛 전원**이 계속 깎인다(1기만이 아니다)
  - **가디언이 붙잡아도 회오리가 접히지 않는다**(unit 0 의 관측)
  - **동료 적과 적 마음이 피해를 입지 않는다**
- [ ] 회오리가 끊겨 보이지 않고 번아웃 먹구름과 구분된다
- [ ] 다른 적 16종에 회오리가 생기지 않는다(프리팹 유무 판정의 관측)
- [ ] 기존 엘리트 2종(슬라임 분열 사슬 · 드래곤 브레스+화염)과 보스 3종 무회귀
