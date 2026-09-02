# dreamcatcher-attach-range-preview — 부착 단계에서 카드의 적용 범위를 보여준다

> 상태: **구현 완료 · 투트랙 리뷰 반영(`abff12fd`) · 검증 마감 대기(2026-09-03).** 남은 일은
> [`5_handoff_summary.md`](5_handoff_summary.md) — 골든 정본 조건 결정 + 재베이크 1회, unit 4 육안·실기기, 그 뒤 「완료」 선언.
> 골든: 0a 전/후 A/B 8건 바이트 동일(킬 경제 변동 0). 재베이크 커밋은 **조건 드리프트**(configHash 변화, 0a 무관)로
> 보류 — 정본 조건 결정 후 1회. 상세는 `0a_area_circle_sim.md` 완료 기준.
> 발원: `distance-based-range` 종료 시 사용자 제안. 그 spec 의 표기 계약(판정과 1:1)을 **소비**하는
> 후속이다. 2026-09-02 결정으로 **스킬 광역의 마지막 사각 잔존을 원으로 접는 판정 변경 1건(unit 0a)** 을
> 선행 단위로 품는다 — 그 이후 단위는 sim 무변이다.

## 배경

드림캐쳐를 유닛에 부착할 때 화면은 「누구에게 붙나」(락온)만 말하고 **「붙으면 어디에 작용하나」는
말하지 않는다.** 궁지폭발·실드수면처럼 공간 카드는 부착을 확정하고 발동을 본 뒤에야 범위를 알게 된다 —
배치 유닛의 사거리 링이 해결한 것과 같은 문제가 부착 단계에 남아 있다.

재료는 이미 있다:
- **판정 자**: 전투 판정은 거리 기반이다(`distance-based-range` units 12~19). 단 스킬 광역 도형 하나가
  **사각(`RangeMetric.Chebyshev`, 반폭 N+0.5)** 으로 남아 있다 — 그 spec 결정 4 의 「같은 N 인데 사거리는
  원, 광역은 사각 — 알고 남긴다」. unit 14 는 멤버십(몸 걸침)만 바꿨고 도형은 그대로다. **배치 스킬 7종과
  보스 3기도 이 arm 을 쓴다** — unit 0a 가 처음으로 원으로 바꾼다.
- **표기 어휘**: `PlacementRangeRing.shader`(SDF) · `TilemapMapView` 의 링 쿼드(`_rangeRing`, 연속 타일
  중심) · 소유권 = `BattleBridge.RangeDisplayOwner`.
- **환경 사실**: 방어유닛 27종은 **전부 2×2** 다(1×1 없음). 몸 중심은 앵커 + (0.5, 0.5) = 셀 경계 교점.

## 검증 질문

> **부착을 확정하기 전에, 이 카드가 이 유닛에서 어디에 작용하는지를 화면이 판정과 같은 자로 —
> 그리고 dim 아래 · 엄지 밑에서도 읽히게 — 말하는가.**

## 카드 공간성 분류 (2026-09-02 · 코드 대조 · 카드는 `id` 로 지목)

키는 **트리거 × 페이로드 → concrete** 다(`SelfTileAoe` 가 트리거에 따라 다른 concrete 로 간다).
라우팅 정본 `BattleBridge.SkillIdForMechanic` 을 unit 1 이 순수 함수로 추출한다.

| 표기 | concrete | 페이로드 (트리거) | 판정 자 = 복사할 값 · 오늘 카드 |
|---|---|---|---|
| **원** r = `N + 0.5` | `SelfAreaBlastSkill` | `SelfTileAoe` × OnDamagedN · OnShieldBreak · HealthThreshold | 투사체 TileAoe 착탄식 `InBodyReach(d, N, 0.5, targetR)` · `cornered_burst` 궁지폭발 · `shield_burst` 실드폭발 · `tremor_plate` 진동갑주 |
| **원** r = `N + 0.5` | `AreaSleepSkill` · `AreaCcSkill` · `AreaDotSkill` · `AreaStackSkill` · `AreaTauntSkill` · `StatAuraSkill` 3종 · `GrantShieldSkill`(N>0) | `AreaSleep` · `AreaCc` · `AreaDot` · `AreaApplyStack` · `AreaTaunt` · `AllyMoveSpeedAura` · `AllyStatAura` · `OpponentStatAura` · `GrantShield` | **unit 0a 이후** `RangeMetric.AreaCircle` = 같은 식 · 카드 `shield_lull` **실드수면** 1장. 나머지는 배치 스킬·보스 SO — 카탈로그는 타입으로 덮는다 |
| **원** r = `N` | `EmitPatternSkill` | `EmitProjectilePattern`(N>0) | 사거리 자 `InBodyReach(d, N, 0, targetR)` — 탄 비행 거리라 칸 반폭 없음 · `moth_swarm` 불나방떼 N=0 → 표시 0 |
| 부채꼴 | `ConeBreathSkill` | `AreaBreath` | **제외 확정** — 카드 0장 |
| **노출 X** | `DeathSiteBlastSkill` · `DeathSiteHazardSkill` | `SelfTileAoe` × OnKill · OnDeath · OnRetire / `SpawnHazard` × OnKill | 자리가 없다 · `farewell` 사망폭발 · `corpse_burst` · `severance_meteor` 퇴직위로금 · `ember_field` 잿불 |
| **표시 0** | 그 외 전부 | 스탯·코스트·대상형·즉발형·`PlacementAura`·… | 없는 범위를 지어내지 않는다 |

⚠ `lullaby_dart` **자장가**는 `ApplyCcToTarget`(N=0) — 비공간이다. 「자장가 = AreaSleep」은 스킬 어휘일 뿐
카드가 아니다.

### 액티브 스킬 전수 (2026-09-02 · 6종이 전부) — 규칙 「조준 타일 중심 · 원 N + 0.5 · 몸 접촉 · range 0 = 그 한 타일」

| 스킬 | range | concrete → 캐리어 | 판정(오늘) | 규칙 대비 |
|---|---|---|---|---|
| `slow_field` 둔화 장판 | 2 | `TileStatBurstSkill`(원샷) | **사각** 반폭 2.5 ⊕ 몸 | **0a 가 원으로** — 유일한 판정 변경 |
| `power_surge` 공격폭증 · `rapid_fire` 속사 | 1 | `AllyBuffFieldSkill` → `AllyBuffField`(틱) | 원 1.5 + 아군 몸(2×2 → 1.0, 실효 2.5) | ✓ 무변 |
| `tornado` 회오리 | 2 | `PullFieldSkill` → `TornadoField`(틱) | 원 2.5 + 몸 | ✓ 판정 무변 · VFX 반경 N → 0b 가 N+0.5 |
| `meteor` 운석 | 2 | `TileMeteorSkill` → 투사체 TileAoe(원샷, 유일한 텔레그래프) | 원 2.5 + 몸 | ✓ 판정 무변 · 착탄 VFX 반경 N → 0b |
| `portal` 포탈 | 0 | `PortalLink`(입구·출구 두 칸) | 입구 셀 중심 반경 0.5 **점** 판정(몸 없음, `TileRange` 미사용) | ✓ **무변** — 칸 내접원 = 「그 한 타일」. 몸 항을 더하면 스치기만 해도 텔레포트 + 재진입 루프 위험 |

중심은 6종 전부 `ctx.CellCenter` 라 바꿀 것이 없다. 표기는 6종 공통으로 사각 채움(`squareShape`, 상한 술어
부재로 r1 = 7×7 · r2 = 9×9)이라 0b 가 원으로 옮긴다. 영향 수 카운터(`Count*InTileRange`)는 로그 전용·UI 소비처 0
이라 존치(Q8). 반경은 `int`(`SkillFiredEvent.TileRange`, `ceil`)라 소수 저작은 올림된다 — 비목표.

### 겸직 `tileRange` — kind 로 막는다 (값이 아니라)

`tileRange` 는 kind 별로 **7가지** 뜻을 갖는다. 시트(`DcSheetApplier`)가 값을 덮으므로 값 추정은 금지.

| 축 · kind | 뜻 | 카드 | 표시 |
|---|---|---|---|
| `BountyMark` | 받는 피해 −N% | `sub_fattened_offering` 제물표식 30 | 0 (`EnemyMark` 모드라 부착 경로 밖) |
| `SelfStatBuff` | **누적 상한**(`MagnitudeCap`) — 잔존값 아님 | `frenzy` 광란 10 | 0 |
| `ApplyStackToTarget` | maxStack(0 = 기본 5) | `frostbite` 동상 5 · `ember_bite` 출혈 0 | 0 |
| `ProjectileToTarget` | 폴백 반경(폭탄맨·캐스터 host, `PickFallbackTarget` 셀 체비셰프) | `poke_needle` 비수 · `boomerang` 4 | 0 (후속) |
| `SelfOrbitProjectile` | 궤도 반경(궤적) | `flame_spinner` 불꽃팽이 1 | 0 (후속) |
| `GrantShield` | 0 = 자기만 / >0 = 반경 | — | 0 이면 비공간 |
| **`attackMods[].tileRange`** | 팅김 탐색 반경(`BounceRetarget`, 이미 원) — `mechanics` 와 **다른 축** | `bouncy_bead` 바운스샷 3 | 0 (착탄점 기준이라 host 중심 아님) |

## Feature-wide 계약

1. **표기는 그 concrete 의 술어 입력의 복사본이다.** 카탈로그가 준 반경을 그대로 넣는다.
   ⚠ 배치 사거리 링(`사거리 + selfR + 0.25`)과 공식이 다르다 — 광역은 시전자 몸을 더하지 않는다.
2. **표기 = 도형 가장자리**(Q3). 표준 상대 항 없음. 읽기 = 「대상 그림자가 링에 닿으면 걸린다」 =
   `SDF ≤ targetR` 와 동치. 이 읽기가 부모 결정 8 의 원문이고, 배치 링의 `+0.25` 가 거기서 **이탈한 쪽**이다
   — 이 spec 은 배치 링을 바꾸지 않는다(후속 후보).
3. **비공간은 채널을 건드리지 않는다.** 판정은 concrete/kind 로만. `shape == None` 이면 호출 자체가 없다
   (포탈 카드가 겪은 「안 칠하면서 소유권만 가져가는」 함정 회피).
4. **소유권 한 채널 + Placement 양보.** `RangeDisplayOwner.AttachPreview` 추가, 기존 규칙(획득 시 덮어쓰기 ·
   반납은 소유자만 · Placement 만 유효성 면제) 승계. Defender 모드 카드 드래그는 `IsAiming` 을 켜지 않아
   **배치 드래그와 상호배제가 없다** — `_rangeOwner == Placement` 이면 프리뷰는 **그리지 않고 양보**한다.
   제약 12 근거: 범위 채널·Entity→위치가 이미 브리지 소유, `SetSkillAimRange` 선례. 브리지는 도형 데이터만.
5. **판정 변경은 unit 0a 하나에 격리.** 사각 → 원 `N + 0.5`(몸 걸침). `RangeMetric` 은 **`AreaCircle = 0`
   으로 번호 교체**해 기본값이 원이 되게 하고, `Chebyshev` 는 다른 값으로 옮겨 `[Obsolete(error: true)]` —
   참조하면 컴파일 오류. 사각 술어 본체 `SkillMath.BodyOverlapsSquare` 와 테스트는 보존(「기능은 남긴다」).
   **밸런스 파급(배치 스킬 7종 N=2~3 · 보스 3기 N=3~4, 면적 −21.5%)은 수치로 보고하고 승인을 받는다.**
   골든 재베이크는 격리 커밋. **unit 0b 이후는 sim 무변 · 골든 바이트 무변.**
6. **단일 도형 불변식.** 카드당 공간 spec ≤ 1. 에셋 lane 테스트가 `mechanics` 와 `attackMods` **양쪽**을 훑는다.
7. **라우팅은 한 함수.** bake 와 카탈로그가 같은 순수 함수(`DcApplicability` 선례).
8. **표면 시점 = 유효 락온 순간, 소멸 = 손 떼는 순간 즉시**(D5). 무효 락온은 표시 0.
9. **look 값은 SO, 채움이 주신호.** `DreamcatcherFocusConfig.attachRangeStyle`(색 · 채움 알파 · 선 알파).
   팔레트 규칙: **hue = 무엇에 대한 말인가**(라임 = 내 유닛 공격 도달 · 시안 = 드림캐쳐 행위 · 빨강 = 충돌/피격
   · 밝은 무채 = 지형 불가 · 노랑 = 점유), **같은 hue 두 표면은 형태로 갈린다**(UI 오버레이 = 선, 바닥 캐리어 =
   채움). 카드 범위 색은 시안 *계열*이되 base-ring·리티클과 **같은 값 금지**(dim 곱 후 「죽은 시안」). 청보라
   보드에서 실패하면 따뜻한 무채(달빛) 2안.
10. **dim 아래에서 읽혀야 한다**(`attach-lockon` 계약 3 대조). 링은 dim(α 0.42, 화면 공간 UI) **아래** 월드
    캐리어이고 정렬 −8 로 유닛 아래다 — 이건 의도(2026-08-31 「링은 유닛 아래」)라 바꾸지 않는다. 대신
    **채움이 주신호**이고, unit 4 판독 기준은 「실기기 · dim 켜짐 · 엄지가 호스트 위」다. 부족하면 후속 F3.

## 작업 단위

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| [`0a_area_circle_sim.md`](0a_area_circle_sim.md) | sim | 스킬 광역 사각 → 원. `RangeMetric` 번호 교체 + `Chebyshev` Obsolete · concrete 10곳 · 페이크 몸 인식 승격 · 파급 표 · 골든 재베이크(격리 커밋) |
| [`0b_area_display_and_squareshape.md`](0b_area_display_and_squareshape.md) | 표기 | `SetAreaRange`(링 전용, 스타일) · 액티브 조준/텔레그래프 = 조준 셀 중심 원 · `squareShape` 삭제 · 골든 무변 |
| [`1_routing_and_spatial_catalog.md`](1_routing_and_spatial_catalog.md) | 순수 함수 | `DcSkillRouting` + `DcRangeCatalog` + EditMode 양 lane |
| [`2_attach_preview_channel.md`](2_attach_preview_channel.md) | 브리지 | `AttachPreview` owner · `SetAttachPreview(host, spec, style)` · Placement 양보 · LateUpdate 추종 |
| [`3_lockon_binding.md`](3_lockon_binding.md) | UI | 드래그 슬롯 락온 전환에 arm/clear · 하드 클리어 |
| [`4_play_verification.md`](4_play_verification.md) | 검증 | 실기기 · dim · 엄지 판독 + 카드 `id` 체크리스트 |
| `5_handoff_summary.md` | 인계 | 종료 시 |

## 사용자 확정 결정

### 2026-09-01
1. 표면 = 락온 성립 순간(드래그 시작 시 후보 전원 점등 기각). 2. 부채꼴 제외. 3. 비공간·조건 발동 장판 노출 X.

### 2026-09-02 (상세화)
- **Q1** 라우팅 추출 공유. **Q2** 사각 → 원, 판정 변경 — Chebyshev 는 기능 보존·비활성화. 범위 = `RangeMetric`
  소비처만(Q8: 니들 폴백·해저드 형상·`DefenderDensity`·BFS 소스는 그대로). **Q2-b** 반경 `N + 0.5`.
  **Q3** 가장자리. **Q4** 별도 색. **Q5** 무효 락온 표시 0. **Q6** 폴백·궤도·실드 0 = 표시 0.
  **Q7** 사각 채움 `(2N+5)²` 결함은 경로 삭제로 해소(0b).

### 2026-09-02 (리뷰 반영 — critic + UX)
- **D1** dim 대응 = 채움 주신호(스타일 3값 SO) + 실기기·dim·엄지 판독 기준. 부족 시 F3.
- **D2** 밸런스 파급 수용, 단 골든 재베이크 후 **킬 경제 변동 수치 보고 → 승인** 게이트.
- **D3** `AreaCircle = 0` 번호 교체 + `Chebyshev` `[Obsolete(error: true)]`.
- **D4** 콜아웃 범위 문구는 **넣지 않는다**(프리젠터 비접촉). 오독 위험은 unit 4 관찰 후 판단.
- **D5** 소멸 = 손 떼는 순간 즉시(확정 비트 잔류 기각).
- **D6** 액티브 스킬 = 터치/커서 타일 중심에서 N 거리 원, 원과 접하는 유닛이 대상. 조준 링 채움 노브 + 펄스는
  구현 기본값(SO)으로 두고 실기기에서 튠.

## 의존하는 기존 동작

- `baseRingLockedFade`(0.15): 락온 유닛의 UI base-ring 이 죽고 바닥 링이 살아나는 교대. attach-lockon 튠이
  이 값을 되돌리면 시안 원 두 겹이 겹친다.
- `TileAoe.cs` 헤더의 「반경 2 정대각·반경 3 얕은 대각 누락 = 사거리와 같은 의도한 일관성」 — unit 0a 의 근거.

## 파이프라인 커버리지

새 플레이 오브젝트 없음 — 링 쿼드 `_rangeRing` 재사용. 스폰·bake·ECS 컴포넌트·채널·VFX **N/A**.

## 비목표

- 스킬 광역 외 격자 잔존(Q8) · 부착 flow UX(`attach-lockon`·`dc-use-flow`) · 전투 중 상시 범위 표시 ·
  `EnemyMark`/`TileAim` 모드 프리뷰(0b 는 후자의 **모양**만 바꾼다) · 콜아웃 문구(D4) · 확정 비트 잔류(D5) ·
  배치 사거리 링 공식.

## 후속 후보

- **F1** 카드 면 「범위 있음」 글리프 — 46장 중 4장이라 드래그 전 예고가 학습을 닫는다.
- **F2** 오라 카드 문안 「**발동 시** 반경 N칸 안의 …」 — `StatAura` 는 TTL 만 회수(반경 이탈 무관). 첫 카드와 함께.
- **F3** dim 면제 렌더 또는 링 라이브 중 `dimAlpha` 완화 — D1 채움 보정이 실기기에서 부족할 때.
- **F4** 조준 중심 마커(토네이도 「중심으로 당김」) — SDF 중심 도트 또는 중심 셀 1칸.
- **F5** 인스펙트 재열람 — 손가락 없이 링을 보는 유일한 자리라 학습 채널로 우선.
- **F6** 배치 링 `+0.25` 를 결정 8(가장자리)로 정합 — 이 spec 은 바꾸지 않는다.
- 링 등장 성장 애니(`_Range` 0→r 0.1s) · 콜아웃 「반경 N칸」 힌트(D4 기각분) · 폴백/궤도 반경 표시 · 부채꼴 어휘
  · `IsPlacementRangeCell` 포워더 삭제(소비처 0) · dormant `BodyOverlapsSquare` 삭제 시점.
