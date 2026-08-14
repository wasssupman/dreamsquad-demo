# elite-whirlpot — 엘리트 적 「Whirlpot」 (구름 화분 팽이)

> 상태: **구현 중 2026-08-14.** unit 0 → 1 → 2 순서.
> 선행: [`elite-enemy-tier`](../elite-enemy-tier/README.md) (완료 2026-08-13) — `EnemyTier` 축이
> 거기서 왔다. 이 spec 은 **그 위에 콘텐츠 1종만 얹는다.**

## 픽션

누군가의 자아 속 **구름 화분**이 꿈에서 환유된 악몽. 화분은 «내가 돌보던 것» 이고, 그것이
거꾸로 서서 **팽이처럼 돌면** 심어 키운 구름이 원심으로 흩뿌려져 주변을 후벼팬다.

**팽이는 멈추면 죽는 사물이다.** 돌지 않는 팽이는 팽이가 아니다 — 그래서 이 악몽은 멈추지
못하고, 방어유닛을 만나면 그 자리에 박혀 영원히 돈다. 동사 하나가 존재 조건이라 **능력 설명과
세계관 설명이 같은 문장**이 된다.

## 목표

엘리트 3번째 적을 출하한다. 동사는 **「돈다」 하나**이고 조건절이 없다 —
슬라임의 «갈라진다» · 드래곤의 «뿜는다» 와 같은 층위에 맞춘다.

**행동 한 줄** — 방어유닛에 닿으면 그 자리에 박혀 회오리를 켜고, 반경 안의 모든 것을 짧은
주기로 계속 깎는다.

## 검증 질문

> 팽이가 방어유닛에 닿았을 때 **그 자리에 멈춰서** 반경 안의 **전원**을 계속 깎는가?
> **걸어오는 동안에는 돌지 않는가** — 즉 회전이 교전에서만 나오는가?
> **가디언이 붙잡아도 회오리가 접히지 않는가**(unit 0)?
> 회오리가 끊겨 보이지 않는가(연출은 지속, 판정은 연타)?
> 동료 적과 적 마음을 때리지 않는가?
> 기존 엘리트 2종 · 보스 3종 · 일반 14종의 행동은 **무회귀**인가?

## 기존 엘리트와의 분업

| | 슬라임 | 드래곤 | **Whirlpot** |
|---|---|---|---|
| 시험하는 것 | 광역 화력 | 배치 밀집 | **처리 위치** |
| 위협의 성격 | 지나간다(마릿수) | 지나간다(비행 폭격) | **점거한다** |
| 플레이어의 답 | 광역으로 쓸기 | 흩어 놓기 | **닿기 전에 원거리로 끝내기** |

로스터의 **첫 「자리를 점거하는 위협」**이다. 그리고 전선의 근접 유닛을 실제로 죽여
`DefenderDeathEvent` 에 압력을 만든다 — 지금 방어유닛이 죽는 일이 드물다.

## 회오리는 신규 메커니즘이 아니다 — 이미 있던 축이다

초판 설계는 `AreaSpin` payload 를 신설해 `AttackN(1)` 으로 발동시키려 했다. **폐기했다** —
`AttackUnitData.attackTargetCount` 가 *"melee AoE. **Nearest N in-range targets hit per
attack**"* 로 이미 그 일을 한다. `AttackSystem`(≈1407~1465)의 광역 보조 루프가

- `bestTarget` 을 씨앗으로 **최근접 후보를 N-1 회 더** 고르고
- **세 술어를 이미 적용**한다 — 진영 마스크 · 통행층(`PlacementLayers.CanTarget`) · 자기 제외
- 범위를 **Chebyshev 타일 거리 ≤ `attackRange`** 로 잰다

즉 내가 새로 쓰려던 것(최근접 N · Chebyshev 반경 · 세 술어)이 **글자 그대로 이미 있다.**
그리고 죽은 코드가 아니다 — 방어유닛 5종과 보스 짱쎈을 포함해 **10개 에셋이 이미 쓴다.**

그래서 이 spec 의 실질은 **저작 3칸**(`Melee` · `attackRange 2` · `attackTargetCount 10`)이고,
코드에서 할 일은 그 축을 막고 있던 **공유 규칙 한 줄의 정정**(unit 0)과 **적 쪽 연출 개방**
(unit 1)뿐이다.

## Feature-wide 계약

1. **이 엘리트는 «메커닉» 이 없다 — 저작된 공격 축으로 성립한다.** `nightmareMechanics` 는
   비운다. 시스템상 문제 없다: `elite-enemy-tier` unit 0 이 **티어와 메커닉 보유를 이미
   분리**했다(보스 특권은 `tier == Boss` 에서만 나온다). 다만 그 spec 의 «엘리트 = 특수 메커니즘
   1개» 라는 *서술*과는 어긋나므로, 팽이의 정체성은 «특수하게 저작된 공격 축» 으로 기록한다.
   ★**「돌고 있다」를 표현하는 런타임 상태를 만들지 말 것** — 공격 사건이 곧 회전이다.
2. **「멈춤」은 작업이 아니다.** `engageMovement: Halt` 가 *"타겟 사거리 도달 시 정지하고 공격"*
   이고 **이미 폴백 기본값**이다(`MovementSystem`). 저작 한 칸으로 끝난다.
3. **어그로는 primary 선정만 지배한다 — 광역 폭은 어그로와 무관하다**(unit 0).
   유닛별 예외 플래그를 만들지 않는다. 규칙 하나, 예외 0. 단 **sticky primary override
   (사거리 밖이면 미발사)는 유지**한다 — 풀면 적이 가디언에 도착하지 못한다.
4. **판정 술어를 새로 쓰지 않는다.** 진영 마스크·통행층·자기 제외는 광역 보조 루프에 이미
   있다. 신규 순수 함수 0 · 신규 payload 0.
5. **반경 = `attackRange`.** 한 필드가 «멈추는 거리» 와 «도는 거리» 를 겸한다 — 팽이에는
   그게 맞다(닿는 거리 = 도는 거리). 둘을 분리하려면 payload 경로로 돌아가야 한다.
6. **연출은 지속, 판정은 연타.** 채널링 기계(공격자별 지속 상태 + 틱 시스템 + 중단 규칙)를
   만들지 않는다 — `elite-enemy-tier` 가 드래곤 「지속 콘」을 조사해 **비용 M · 선례 0** 으로
   접었고, 팽이는 그 벽을 우회할 수 있다(회전이 원래 주기 사건이다).
7. **어느 적이 회오리를 갖는지는 «프리팹 유무» 가 결정한다.** id·이름 분기 금지이며
   `attackTargetCount > 1` 로도 판정하지 않는다(Basic·Tanker·짱쎈까지 회오리가 생긴다).
   빔 유닛 판정과 같은 규율.
8. **회오리 VFX 는 「누가 하는 것인가」가 읽혀야 한다.** 혼동 후보가 둘이다 —
   ① `Burnout_Smoke`(방어유닛의 *상태*) ② **플레이어의 토네이도 스킬**(`Active_Tornado`).
   ⚠ **현재는 `Tornado_SKELETON.prefab` 을 그대로 재사용한다**(잠정). 이 프로젝트 자체 에셋이고
   이미 플레이에서 검증된 회오리라 코드 완성과 동시에 볼 수 있다는 장점이 있지만, **②와 같은
   그림**이다. 화면에서 혼동되면 복제 후 색을 가른다 — 색이 머티리얼이 아니라 3개 시스템의
   Color-over-Lifetime 그라디언트에 있어서(`OuterRotatingGale`·`InnerCounterGale`·`DustColumn`)
   머티리얼 교체로는 안 되고 프리팹 작업이다. 판단은 육안 확인 후.
11. **엘리트의 `enemyClass` 를 `Tanker` 로 저작하지 않는다.** `Concept_Heavy` 는 웨이브 컨셉
   5종 중 **유일하게 슬롯이 1개**이고 Tanker 만 필터한다. 엘리트는 `maxPerWave 1` 이 강제되므로
   그 슬롯에 뽑히면 **웨이브 전체가 1기로 붕괴**한다. 상세와 실측은
   [2_whirlpot_assets.md](2_whirlpot_assets.md) 하단.
9. **`targetFactions` 를 저작하지 않는다(0 유지).** 복제로 만들면 `13` 이 묻어와 **방어 본능을
   못 때린다**(`feda9054`). 가드 = `AuthoredTargetMaskTests`.
10. **전 수치는 SO** — 하드코딩 금지(제약 6).

## 유닛 사양 (초기값 제안 — 전부 SO 소유, 튜닝 대상)

| 항목 | 값 | 근거 |
|---|---|---|
| health / moveSpeed | 320 / **1.2** | 「천천히 와서 박히는 모루」. 현 최저속은 나이트메어 1.0 |
| attackMethod | **Melee** | `attackTargetCount` 는 melee/outputs 경로 전용 |
| attackRange | **2** | = 회오리 반경(계약 5). Chebyshev 2 = 5×5 |
| **attackTargetCount** | **10** | 회오리의 실체. 반경 2 안의 방어유닛 수가 보드 총원보다 적어 실질 **무제한**이고, 10 은 «큰 회오리» 의 선언 겸 안전 상한이다 |
| attackCooldown | **0.6** | 연타 = 회오리의 체감 |
| outputs | `Damage` **5** | 대상당 8.3 DPS. 별도 단일 타격은 **없다** |
| nightmareMechanics | **비움** | 계약 1 |
| engageMovement / targetMode | **`Halt`** / `Nearest` | 계약 2. 특정 대상을 쫓는 게 아니라 «가장 가까운 것에 박힌다» |
| **enemyClass** | **`Bruiser`** | ★`Tanker` 금지 — 계약 11(유일한 1슬롯 컨셉이 Tanker 를 필터한다). 분류상으로도 「근접 광역 난동꾼」이 맞다 |
| traversalLayers | 0(지상 기본) | 비행은 드래곤 몫 |
| tier | **Elite** | 보스 특권 0 |
| killScore / awakening / stability | 3 / 3 / 2 | 엘리트 대역 |
| maxPerWave / minWaveNumber | 1 / **5** | 슬라임 3 · 드래곤 4 다음 자리 |
| Spine | `cloud-pot` · idle=walk=루프 1개 · attack=**빈 값** · death=**빈 값** | [2_whirlpot_assets.md](2_whirlpot_assets.md) |
| VFX | `attackVfxPrefab` = 회오리 · `attackVfxScalePerTile` 실측 | [1_whirl_visual.md](1_whirl_visual.md) |

## 라이브 등장 빈도 (실측 2026-08-14)

**풀에 넣는 것과 뽑히는 것은 다르다.** 컨셉 블록이 클래스로 슬롯을 거르는데, Whirlpot(Bruiser ·
Ground)을 받는 컨셉은 **「평소」 하나뿐**이다 — 중장=Tanker · 원거리=Shooter · 벌떼=Runner ·
공습=Air 필터라 전부 제외된다. 게다가 「평소」는 weight 0.6 으로 가장 낮다.

100웨이브 생성 실측(덱별):

| 덱 | 등장 횟수 | 첫 등장 |
|---|---|---|
| Serpent · Zig | 4 | **w17** |
| Coil · Twin · Spiral · Hook | 2 | **w11** |
| Endless | 6 | **w12** |

⚠ **일반 플레이 세션에서는 못 볼 가능성이 높다.** 확실히 보려면 **튜토리얼 맵 10웨이브**
(저작 플랜이라 1기 보장)를 쓴다.

⚠ **2기짜리 웨이브가 새로 가능해졌다.** 「평소」는 무필터 슬롯이 2개인데 이제 그 후보에 **Bruiser
엘리트가 둘**(슬라임·Whirlpot) 있다. 두 슬롯이 모두 엘리트를 뽑으면 `maxPerWave 1` 이 각각을 1로
자르고 **잘린 몫은 재분배되지 않아** 웨이브 총원이 2가 된다(w17·w60 에서 실측). 붕괴 가드
(`totalCount > 1`)는 통과하지만 wave 60 에 2기는 빈 웨이브에 가깝다. 이 spec 이전에는 「평소」에
들어갈 수 있는 엘리트가 슬라임 하나뿐이라 **구조적으로 불가능했던 경우**다.

## 작업 단위

| 파일 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | code | [0_aggro_aoe_contract.md](0_aggro_aoe_contract.md) | 어그로 광역 접기 **철회** + 계약을 primary 로 좁힘 + 테스트. **단독 커밋** |
| 1 | code | [1_whirl_visual.md](1_whirl_visual.md) | 적 SO 에 유닛별 공격 VFX 개방 + 회오리 스폰 |
| 2 | asset | [2_whirlpot_assets.md](2_whirlpot_assets.md) | `Enemy_Whirlpot` + cloud-pot Spine + 카탈로그/덱 |
| 3 | docs | `3_handoff_summary.md` | 인계 요약 (구현 종료 시) |

**순서 근거** — **0 이 먼저이고 단독**이다: 기존 적 5종(보스 포함)이 같은 코드를 타므로
되돌릴 때 팽이 콘텐츠와 딸려가면 안 된다. **2 는 마지막** — 아트가 잘못된 동작을 예쁘게
포장하지 않게 한다.

## 파이프라인 커버리지

`docs/reference/object-pipeline-map.md` **적(Enemy)** 아키타입 대조. 신규 플레이 오브젝트 1종.

| 정거장 | 이 spec 에서 |
|---|---|
| 데이터 SO | `Enemy_Whirlpot` 신규 + `EnemyCatalog` 등록 + 라이브 덱 7종 **중간** 삽입(⚠ `waveSeed` 갱신). `AttackUnitData` 에 연출 필드 2개 append(`attackVfxPrefab`·`attackVfxScalePerTile`) — `DefenderUnitData` 의 같은 필드 대칭 |
| 등급 축 | `tier: Elite`. 신규 enum 값 0 |
| 스폰 진입점 | **변경 0** — 레인 스폰(`SpawnUnit` → `CreateEnemyEntity`) 그대로 |
| ECS 컴포넌트 | **신규 0.** 표준 적 세트. `DcTriggerSlot` 조차 안 받는다(메커닉 없음 = bake 가 버퍼를 안 붙인다) |
| 시뮬 시스템 | `AttackSystem` **한 줄 삭제**(unit 0). 신규 시스템 0 · 신규 순수 함수 0 |
| 이벤트 큐 | **신규 채널 0 · 신규 필드 0.** 기존 `UnitAttackVisualEvent` 가 매 공격 START 에 `attacker`+`attackAnimPeriod` 를 이미 싣는다 |
| View/Pool | 기존 `SpineUnitPool`. ★attack·death 애니 빈 값 = `PlayAttack` early-return + 즉시 `Destroy`(드래곤 선례) |
| 체력 표시 | 변경 없음 — `UnitOverheadUiLayer` |
| 씬 wiring | **수작업 배선 0.** 회오리 **프리팹**은 적 SO 가 갖는다(방어유닛 `attackVfxPrefab` 대칭) — `VfxSpawner` 에 프리팹 슬롯을 만들지 않는 이유가 계약 7(유닛별 opt-in)이다. 단 **수명 배수 knob 하나**(`unitAttackAoeSustainMul`)는 `VfxSpawner` SerializeField 다(기본값 2 = 배선 불요). 브레스 knob 4개와 같은 자리 — 「수명은 VfxSpawner 소유」(`b7750a4b`)를 지키기 위해 |
| VFX | 회오리 = `Assets/_Project/VFX/` 아래 신규. ⚠ 벤더 원본 직접 참조 금지 · 번아웃 먹구름과 구분(계약 8) |

## 후속 후보 (현 spec 범위 밖)

- **회전 예고(telegraph)** — 즉발이고 공격 애니가 없어 신호가 VFX 하나다. 드래곤 브레스와
  같은 상황이며 그때 「지금은 읽힌다」로 판정했다. 안 읽히면 `hitDelaySec` + 바닥 링.
- **반경과 정지거리 분리** — 계약 5 가 한 필드로 겸직시킨다. 「멀리서 멈춰 크게 돈다」나
  「붙어서 좁게 돈다」를 저작하려면 그때 payload 경로를 부활시킨다.
- **`attackTargetCount` 상한 감각** — 10 은 실질 무제한이다. 진짜 상한 knob 이 필요해지면
  `AoeTargetCap.SelectNearest`(투사체 AoE 가 쓴다)를 melee 경로에도 끌어오는 것이 정석이다.
- **등장 빈도가 너무 낮다면** [S~M] · 실측 2~6회/100웨이브(위 표). 올리는 lever 는 셋이고 성격이
  다르다: ① 「평소」 `weight` 0.6 → 상향(전 유닛에 영향) ② 다른 컨셉에 Bruiser 슬롯 신설
  (예 「중장」에 variant 추가 — 단 1슬롯 붕괴 규칙 확인) ③ 엘리트 전용 컨셉 신설. **어느 것도 이
  spec 범위가 아니다** — 웨이브 컨셉 저작은 `wave-concept-blocks` 소유다.
- **「평소」 2슬롯이 모두 엘리트일 때의 2기 웨이브** [M] · 잘린 몫이 재분배되지 않아 생긴다. 진짜
  수리는 「엘리트로 잘린 몫을 다른 슬롯에 넘긴다」이고 그건 `WavePatternGenerator` 소관이다.
  임시 완화는 「평소」 슬롯을 3개로 늘리는 것(전 웨이브 편성에 영향).
- **회오리 프리팹 경량화** [S] · 지금 붙은 `Tornado_SKELETON` 은 파티클 시스템 3개(각
  `maxNumParticles 1000`)다. 플레이어 스킬은 한 번에 하나지만 회오리는 **상시 2개가 겹친다**
  (`VfxSpawner.unitAttackAoeSustainMul` = 2). 모바일 예산(일반 50 / 임팩트 100)과 어긋나므로, 육안에서
  맥박이나 프레임 저하가 보이면 배수를 올리기 전에 **전용 경량 프리팹**을 만드는 것이 먼저다.
  그때 색도 함께 갈라 플레이어 토네이도와 구분한다(계약 8).
- **회오리 반경 시각화** — 반경이 저작값인데 화면에 경계가 없다. 밸런스 튜닝 시 필요해질 수 있다.
- **VFX 카탈로그 등재** — `common-skill-vfx-reference.md` 에 회오리 항목 신설은 **사용자 승인
  필요**(스킬 규칙). 콘 브레스도 같은 이유로 미등재 상태다.
