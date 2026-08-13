# elite-whirlpot — 엘리트 적 「Whirlpot」 (구름 화분 팽이)

> 상태: **초안 2026-08-13 — 사용자 승인 대기.** 승인 후 unit 0 부터 순서대로 구현한다.
> 선행: [`elite-enemy-tier`](../elite-enemy-tier/README.md) (완료 2026-08-13) — `EnemyTier` 축과
> 적 `AttackN` 개방이 거기서 왔다. 이 spec 은 **그 위에 콘텐츠 1종만 얹는다.**

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

> 팽이가 방어유닛에 닿았을 때 **그 자리에 멈춰서** 반경 안의 모든 것을 계속 깎는가?
> **걸어오는 동안에는 돌지 않는가** — 즉 회전이 교전에서만 나오는가?
> 회오리가 **끊겨 보이지 않는가**(연출은 지속, 판정은 연타)?
> 동료 적과 적 마음을 태우지 않는가?
> 기존 엘리트 2종 · 보스 3종 · 일반 14종의 행동은 **무회귀**인가?

## 기존 엘리트와의 분업

| | 슬라임 | 드래곤 | **Whirlpot** |
|---|---|---|---|
| 시험하는 것 | 광역 화력 | 배치 밀집 | **처리 위치** |
| 위협의 성격 | 지나간다(마릿수) | 지나간다(비행 폭격) | **점거한다** |
| 플레이어의 답 | 광역으로 쓸기 | 흩어 놓기 | **닿기 전에 원거리로 끝내기** |

로스터의 **첫 「자리를 점거하는 위협」**이다. 그리고 전선의 근접 유닛을 실제로 죽여
`DefenderDeathEvent` 에 압력을 만든다 — 지금 방어유닛이 죽는 일이 드물다.

## Feature-wide 계약

1. **메커니즘은 `AttackN(period=1) × AreaSpin` 하나다.** `PeriodicTimer` 를 쓰지 않는다 —
   *"공격을 해결했다" = "사거리 안에 대상이 있다" = "멈춰 있다"* 이므로 **「멈춰서 돌 때만
   돈다」가 트리거에서 저절로 나온다.** 타이머로 하면 걸어오면서도 돌아 실루엣이 무너진다.
   ★**「돌고 있다」를 표현하는 런타임 상태 변수를 만들지 말 것** — 그 사실은 이미 트리거가 안다.
2. **「멈춤」은 작업이 아니다.** `engageMovement: Halt` 가 *"타겟 사거리 도달 시 정지하고 공격"*
   이고 **이미 폴백 기본값**이다(`MovementSystem`). 저작 한 칸으로 끝난다.
3. **원형 도형을 새로 쓰지 않는다.** `TileAoe.IsInTileRange` 가 이미 Chebyshev 원을 잰다 →
   **신규 순수 함수 0**. 드래곤은 `IsInCone` 을 새로 써야 했지만 팽이는 그것조차 없다.
   광역 «도형 어휘»(`EffectArea` 계열) 신설 금지 — `elite-enemy-tier` 계약 6 을 계승한다.
   payload 마다 도형이 상수이므로 이 spec 은 「도형 통합」의 착수 조건을 만들지 않는다.
4. **콘 순회의 세 술어를 그대로 가져온다** — ① 진영 마스크 ② 통행층 교집합 ③ 자기 제외.
   `AttackSystem` 후보 배열은 **전 진영 통합 풀**이다. 빠뜨리면 팽이가 동료와 적 마음을 간다.
   (`elite-enemy-tier` 인계 노트 2 가 지우지 말라고 못 박은 그것.)
5. **회전 반경 ≥ `attackRange`.** 자기를 때리는 근접 유닛이 원 밖에 있으면 「붙으면 깎인다」가
   거짓이 된다. bake 가 위반을 경고한다.
6. **연출은 지속, 판정은 연타.** 채널링 기계(공격자별 지속 상태 + 틱 시스템 + 중단 규칙)를
   만들지 않는다 — `elite-enemy-tier` 가 드래곤 「지속 콘」을 조사해 **비용 M · 선례 0** 으로
   접었고, 팽이는 그 벽을 우회할 수 있다(회전이 원래 주기 사건이다).
7. **가장 가까이 붙은 유닛은 두 번 맞는다** — 기본 공격 출력 + 회전. **의도다.** 팽이를
   껴안은 것이 가장 아픈 게 맞고, 그게 「전선을 갈아낸다」의 실체다.
8. **회오리 VFX 는 번아웃 먹구름과 달라야 한다.** 번아웃은 *방어유닛의 상태* 신호이고 이건
   *적의 공격* 신호다. 같은 그림이면 플레이어가 둘을 뒤섞어 읽는다.
9. **`targetFactions` 를 저작하지 않는다(0 유지).** 기존 에셋 복제로 만들면 `13` 이 묻어와
   **방어 본능을 못 때린다** — 신규 적 4종이 정확히 그렇게 태어났다(`feda9054`).
   가드 = `AuthoredTargetMaskTests.OnlySpecialEnemies_NarrowTheirTargets`.
10. **전 수치는 SO** — 하드코딩 금지(제약 6).

## 유닛 사양 (초기값 제안 — 전부 SO 소유, 튜닝 대상)

| 항목 | 값 | 근거 |
|---|---|---|
| health / moveSpeed | 320 / **1.2** | 「천천히 와서 박히는 모루」. 현 최저속은 나이트메어 1.0 |
| attackMethod / attackRange | Melee / 1 | 근접. 원거리 처리로 답을 만들려면 자기는 근접이어야 한다 |
| attackCooldown | **0.5** | 연타 = 회오리의 체감. 짧게 |
| outputs | `Damage 4` | ★낮게. 주 피해는 회전이다(계약 7 로 근접 1기는 4+6) |
| mechanics | `AttackN(1) × AreaSpin` — 피해 **6** · 반경 **2**타일 | 붙은 1기 20 DPS · 반경 내 추가 1기당 12 DPS |
| engageMovement / targetMode | **`Halt`** / `Nearest` | 계약 2. 특정 대상을 쫓는 게 아니라 «가장 가까운 것에 박힌다» |
| traversalLayers | 0(지상 기본) | 비행은 드래곤 몫 |
| tier | **Elite** | 보스 특권 0 |
| killScore / awakening / stability | 3 / 3 / 2 | 엘리트 대역 |
| maxPerWave / minWaveNumber | 1 / **5** | 슬라임 3 · 드래곤 4 다음 자리 |
| Spine | `cloud-pot` · idle=walk=루프 1개 · attack=**빈 값** · death=**빈 값** | 계약은 [2_whirlpot_assets.md](2_whirlpot_assets.md) |

## 작업 단위

| 파일 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | code | [0_area_spin_payload.md](0_area_spin_payload.md) | `AreaSpin` payload + `AttackSystem` arm + EditMode |
| 1 | code | [1_whirl_visual.md](1_whirl_visual.md) | 지속으로 읽히는 회오리 연출 (원샷 이어붙이기) |
| 2 | asset | [2_whirlpot_assets.md](2_whirlpot_assets.md) | `Enemy_Whirlpot` + cloud-pot Spine + 카탈로그/덱 |
| 3 | docs | `3_handoff_summary.md` | 인계 요약 (구현 종료 시) |

**순서 근거** — 0 이 먼저다(1·2 가 전부 그 payload 를 전제한다). **2 는 마지막** — 아트가
잘못된 동작을 예쁘게 포장하지 않게 한다(`elite-enemy-tier` 와 같은 근거).

## 파이프라인 커버리지

`docs/reference/object-pipeline-map.md` **적(Enemy)** 아키타입 대조. 신규 플레이 오브젝트 1종.

| 정거장 | 이 spec 에서 |
|---|---|
| 데이터 SO | `Enemy_Whirlpot` 신규 + `EnemyCatalog` 등록 + 라이브 덱 7종 `attackUnitPool` **중간** 삽입. ⚠ 삽입이 웨이브 baseline 을 바꾼다 → `waveSeed` 갱신해 diff 에 드러낸다 |
| 등급 축 | `tier: Elite`. 신규 enum 값 0 |
| 스폰 진입점 | **변경 0** — 레인 스폰(`SpawnUnit` → `CreateEnemyEntity`) 그대로. 위치 지정 스폰이 필요 없다(분열 없음) |
| ECS 컴포넌트 | **신규 0.** 표준 적 세트 + `DcTriggerSlot`(엘리트도 받는다). 계약 1 이 상태 변수를 금지한다 |
| 시뮬 시스템 | **`AttackSystem` 단 하나** — `AreaSpin` arm. 신규 시스템 0 · `ProjectileHitSystem` 무변경 |
| 이벤트 큐 | **신규 채널 0.** 연출은 기존 `UnitAttackVisualEvent` 필드 append(드래곤 브레스 선례) |
| View/Pool | 기존 `SpineUnitPool`. ★attack·death 애니 빈 값 = `PlayAttack` early-return + 즉시 `Destroy` (드래곤 선례) |
| 체력 표시 | 변경 없음 — `UnitOverheadUiLayer` |
| 씬 wiring | 회오리 프리팹 슬롯 1개(`VfxSpawner`). ★프리팹 슬롯은 **`VfxSpawner` 가 소유** — 브리지에 두지 않는다(`b7750a4b` 에서 이관한 소유권) |
| VFX | 회오리 = `Assets/_Project/VFX/` 아래 신규 또는 벤더 복제본. ⚠ 벤더 원본 직접 참조 금지 · 번아웃 먹구름과 구분(계약 8) |

## 후속 후보 (현 spec 범위 밖)

- **회전 예고(telegraph)** — 즉발이고 공격 애니가 없어 신호가 VFX 하나다. 드래곤 브레스와
  같은 상황이며 그때 「지금은 읽힌다」로 판정했다. 안 읽히면 `hitDelaySec` + 바닥 링.
- **회전 중 이동 저항 / 밀어내기** — 「팽이가 부딪히면 튕겨낸다」. `CcKind.Impulse` 가 있으나
  **동사가 둘이 되므로** 이 spec 에서 하지 않는다(엘리트 = 메커니즘 1개).
- **회오리 반경 시각화** — 반경이 저작값인데 화면에 경계가 없다. 밸런스 튜닝 시 필요해질 수 있다.
- **VFX 카탈로그 등재** — `common-skill-vfx-reference.md` 에 회오리 항목 신설은 **사용자 승인
  필요**(스킬 규칙). 콘 브레스도 같은 이유로 미등재 상태다.
