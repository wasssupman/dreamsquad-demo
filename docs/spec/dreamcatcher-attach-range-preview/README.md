# dreamcatcher-attach-range-preview — 부착 단계에서 카드의 적용 범위를 보여준다

> 상태: **상세화 완료 · 착수 대기** (2026-09-02). unit 0 부터 순서대로 구현한다.
> 발원: `distance-based-range` 종료 시 사용자 제안. 그 spec 의 표기 계약(판정과 1:1)을
> **소비**하는 후속이다. 2026-09-02 결정으로 **스킬 광역의 마지막 격자 잔존(사각)을 원으로
> 접는 판정 변경 1건(unit 0)** 을 선행 단위로 품는다 — 그 이후 단위는 sim 무변이다.

## 배경

드림캐쳐를 유닛에 부착할 때 화면은 「누구에게 붙나」(락온)만 말하고 **「붙으면 어디에
작용하나」는 말하지 않는다.** 궁지폭발(반경 1)·자장가(반경 1)처럼 공간 카드는 부착을 확정하고
발동을 본 뒤에야 범위를 알게 된다 — 배치 유닛의 사거리 링이 해결한 것과 정확히 같은 문제가
부착 단계에 남아 있다.

재료는 이미 있다:
- **판정 자**: 전투 판정은 거리 기반이다(`distance-based-range` units 12~19). 단 스킬 광역
  도형 하나가 **사각(`RangeMetric.Chebyshev`, 반폭 N+0.5)** 으로 남아 있었다 — 그 spec 결정 4 의
  「같은 N 인데 사거리는 원, 광역은 사각이 공존한다 — 알고 남긴다」. unit 0 이 이것을 닫는다.
- **표기 어휘**: `PlacementRangeRing.shader`(SDF, `_HalfExtent`·`_Range`) · `TilemapMapView` 의
  링 쿼드(`_rangeRing`, 연속 타일 중심) · 소유권 = `BattleBridge.RangeDisplayOwner` 채널.
- **카드 공간성 분류**: 아래 표(2026-09-02 코드 대조로 재작성).

## 검증 질문

> **부착을 확정하기 전에, 이 카드가 이 유닛에서 어디에 작용하는지를 화면이
> 판정과 같은 자로 말하는가.**

## 카드 공간성 분류 (2026-09-02 · 코드 대조 완료)

키는 **트리거 × 페이로드 → 스킬 concrete** 다(`SelfTileAoe` 가 트리거에 따라 다른 concrete 로
간다). 라우팅 정본은 `BattleBridge.SkillIdForMechanic` → unit 1 이 순수 함수로 추출한다.

| 표기 | concrete | 발생 페이로드 (트리거) | 판정 자 = 표기가 복사할 값 |
|---|---|---|---|
| **원** r = `N + 0.5` | `SelfAreaBlastSkill` | `SelfTileAoe` × OnDamagedN · OnShieldBreak · HealthThreshold | 투사체 TileAoe 착탄식 `InBodyReach(d, N, 0.5, targetR)` — 오늘 카드 3장(궁지폭발·실드폭발·진동갑주) |
| **원** r = `N + 0.5` | `AreaSleepSkill` · `AreaCcSkill` · `AreaDotSkill` · `AreaStackSkill` · `AreaTauntSkill` · `StatAuraSkill` 3종 · `GrantShieldSkill`(N>0) | `AreaSleep` · `AreaCc` · `AreaDot` · `AreaApplyStack` · `AreaTaunt` · `AllyMoveSpeedAura` · `AllyStatAura` · `OpponentStatAura` · `GrantShield` | **unit 0 이후** `RangeMetric.AreaCircle` = 같은 식. 오늘 카드 1장(자장가). 나머지는 적·보스 SO 전용 — 카탈로그는 타입으로 덮는다 |
| **원** r = `N` | `EmitPatternSkill` | `EmitProjectilePattern`(N>0) | 사거리 자 `InBodyReach(d, N, 0, targetR)` — 탄 비행 거리라 칸 반폭을 더하지 않는다. 오늘 카드(불나방떼) N=0 → 표시 0 |
| **부채꼴** | `ConeBreathSkill` | `AreaBreath` | **제외 확정** — 사용 카드 0장(보스 전용). 첫 부채꼴 카드가 생기면 어휘 신설 |
| **노출 X** (자리 없음) | `DeathSiteBlastSkill` · `DeathSiteHazardSkill` | `SelfTileAoe` × OnKill · OnDeath · OnRetire / `SpawnHazard` × OnKill | 죽은 자리·비워진 칸 — 부착 시점에 위치가 없다 |
| **표시 0** (비공간) | 그 외 전부 | 스탯·코스트·대상형·즉발형·`PlacementAura`(축 집합)·`UltimateLeap`·`SplitOnDeath` … | 없는 범위를 지어내지 않는다 |

### 겸직 `tileRange` — kind 로 막는다 (값이 아니라)

`tileRange` 는 kind 별로 **6가지 다른 뜻**을 갖는다. 시트(`DcSheetApplier`)가 값을 매 로그인마다
덮을 수 있으므로 「30 이면 퍼센트겠지」 식의 값 추정은 금지다. 카탈로그는 **concrete/kind 만** 본다.

| kind | `tileRange` 의 뜻 | 카드 예 | 표시 |
|---|---|---|---|
| `BountyMark` | 받는 피해 −N% | 살찐 제물 30 | 0 (그리고 `EnemyMark` 모드라 부착 경로에 들어오지도 않음) |
| `SelfStatBuff` | **누적 상한**(`MagnitudeCap`) — 잔존값이 아니다, 지우면 중첩이 덮어쓰기로 바뀐다 | 광란 10 | 0 |
| `ApplyStackToTarget` | maxStack(0 = 기본 5) | 서리 화살 5 · 잉걸불 0 | 0 |
| `ProjectileToTarget` | **폴백 반경** — 폭탄맨·캐스터 host 에서만 자기선택(`PickFallbackTarget`, 셀 체비셰프) | 니들·부메랑 4 | 0 (후속 후보) |
| `SelfOrbitProjectile` | 궤도 반경(궤적, 범위 아님) | 궤도 화염구 1 | 0 (후속 후보) |
| `GrantShield` | 0 = 자기만 / >0 = 반경 | — | 0 이면 비공간 |

## Feature-wide 계약

1. **표기는 그 concrete 의 술어 입력의 복사본이다.** 링 호출부는 카탈로그가 준 반경을 그대로
   넣는다 — 여기서 모양을 다시 그리지 않는다(`distance-based-range` unit 5 규율 승계).
   ⚠ 배치 사거리 링(`사거리 + selfR + 0.25`)과 **공식이 다르다**: 광역은 시전자 몸(selfR)을
   더하지 않고(`EcsSkillContext` arm 이 0 을 넣는다), 이 spec 은 표준 상대 항도 넣지 않는다(계약 2).
2. **표기 = 도형 가장자리.** 표준 상대 몸을 더하지 않는다(사용자 결정 2026-09-02 Q3). 읽기는
   「**대상 그림자가 링에 닿으면 걸린다**」 — 판정 `SDF ≤ targetR` 와 근사가 아니라 동치다.
   몸이 큰 대상은 더 멀리서 걸리지만 그건 그림자가 말한다(무통보 관용 아님 — 정확한 읽기).
3. **비공간 카드는 아무것도 그리지 않는다.** 판정은 kind(concrete) 로만, 값은 보지 않는다.
   `shape == None` 이면 채널을 **건드리지 않는다** — `SetPlacementRange` 가 `tileRange <= 0` 에서
   조용히 return 하며 소유권만 가져가는 함정(포탈 카드가 겪음)을 카탈로그 층에서 막는다.
4. **소유권 한 채널.** `RangeDisplayOwner` 에 `AttachPreview` 를 추가하고 기존 규칙(획득 시
   덮어쓰기 · 반납은 소유자만 · **Placement 만** 유효성 리셋 면제)을 따른다. 부착 드래그(Defender
   모드) 중엔 `SkillAim` 을 쓰지 않으므로 경합이 없다.
   **제약 12 근거**: 범위 채널·Entity→위치가 이미 브리지 소유이고, 드래그 슬롯이
   `SetSkillAimRange` 를 부르는 선례가 있다. 브리지는 **도형 데이터만** 받고 연출 분기를 갖지 않는다
   (`attach-lockon` 계약 9 와 충돌 없음 — 링은 UI 오버레이가 아니라 보드 캐리어다).
5. **판정 변경은 unit 0 하나에 격리한다.** unit 0 = 스킬 광역 사각 → 원(`N + 0.5`, 몸 걸침),
   `Chebyshev` arm 은 **은퇴하되 코드는 남긴다(dormant, 소비처 0)**. 골든 재베이크는 격리 커밋.
   **unit 1 이후는 sim 파일 무변 · 골든 바이트 무변**이 완료 기준이다.
6. **단일 도형 불변식.** 채널이 하나라 카드당 공간 페이로드는 **최대 1개**여야 한다. Assets lane
   테스트가 전 카드를 훑어 위반을 loud 로 잡는다(오늘 위반 0).
7. **라우팅은 한 함수.** 트리거×페이로드→concrete 는 bake(`BattleBridge`)와 카탈로그가 **같은
   순수 함수**를 부른다(`DcApplicability` 의 「preflight 와 bake 가 한 함수」 선례).
8. **표면 시점** = 카드 드래그 중 락온이 **유효하게** 성립한 순간. 무효 락온(full 3/3·비기여)은
   표시 0. 락온 대상이 바뀌면 따라가고, 손을 떼면(커밋·취소·강제 종료) 즉시 사라진다.
9. **look 값은 SO.** 색은 `DreamcatcherFocusConfig.attachRangeColor`(신설) 하나. 호출로 넘기고
   뷰는 kind 를 모른다.

## 작업 단위

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| [`0_area_circle_regression.md`](0_area_circle_regression.md) | sim + 표기 | 스킬 광역 사각 → 원(`N + 0.5`). `Chebyshev` dormant · `AreaCircle` arm 신설 · 액티브 조준/텔레그래프 표기도 원 · 골든 재베이크 |
| [`1_routing_and_spatial_catalog.md`](1_routing_and_spatial_catalog.md) | 순수 함수 | `SkillIdForMechanic` 추출(`DcSkillRouting`) + `DcRangeCatalog`(concrete → 도형·반경) + EditMode 양 lane |
| [`2_attach_preview_channel.md`](2_attach_preview_channel.md) | 브리지 + 뷰 | `RangeDisplayOwner.AttachPreview` · `SetAttachPreview(host, spec, color)` · 링 전용 경로 · LateUpdate 추종 |
| [`3_lockon_binding.md`](3_lockon_binding.md) | UI | 드래그 슬롯 락온 전환에 arm/clear · 하드 클리어 · Squad 동일 |
| [`4_play_verification.md`](4_play_verification.md) | 검증 | 원 3장 + 자장가 + 비공간 3장 + 무효 락온 + 액티브 조준 원 + 잔류 0 |
| `5_handoff_summary.md` | 인계 | 종료 시 작성 |

## 사용자 확정 결정

### 2026-09-01
1. **표면 = 카드 드래그 중 락온이 성립한 순간에만.** 드래그 시작 시 후보 전원 점등은 기각(밀집
   소음), 인스펙트 재열람은 후속 후보.
2. **부채꼴(AreaBreath) 제외** — 사용 카드 0장(보스 전용).
3. **공간이 필요 없는 카드는 노출하지 않는다** — 비공간은 물론 조건 발동 장판(OnDeath 계열)도
   위치가 없으므로 노출 X. 없는 범위를 지어내는 표시가 이 spec 최대의 금기다.

### 2026-09-02 (상세화 세션)
- **Q1 라우팅**: `SkillIdForMechanic` 을 순수 static 으로 추출해 bake 와 프리뷰가 공유.
- **Q2 사각 → 원, 판정 변경**: 「타일 기반은 지운다, N 거리 이내 원으로」. `Chebyshev` arm 은
  **기능은 남기고 비활성화**(dormant). 범위 = `RangeMetric` 소비처만(Q8) — 니들 폴백 선정·해저드
  형상·방어유닛 밀도 계산의 셀 거리는 그대로.
- **Q2-b 반경 = `N + 0.5`** — 자기 자리 폭발·투사체 착탄과 같은 식. N=1 이 대각 인접을 유지한다.
- **Q3 표기 = 도형 가장자리** — 표준 상대 항 없음.
- **Q4 색 = 별도**(`DreamcatcherFocusConfig`). 라임 = 내 사거리 / 카드 범위는 다른 색.
- **Q5 무효 락온 = 표시 0.**
- **Q6 경계 3종(폴백 반경·궤도 반경·실드 0) = 표시 0**, 앞의 둘은 후속 후보.
- **Q7 사각 채움 선재 결함**(`squareShape` 경로가 `(2N+5)²` 칸 페인트) = unit 0 에서 경로가
  dormant 가 되므로 1줄 경계 가드만 얹어 되살릴 때의 함정을 없앤다.

## 파이프라인 커버리지

새 플레이 오브젝트 없음 — 기존 표기 정거장(링 쿼드 `_rangeRing`) 재사용. 스폰·bake·ECS
컴포넌트·채널·VFX 전부 **N/A**(표기 전용). unit 0 은 판정 산식만 바꾸고 컴포넌트·시스템을 만들지
않는다.

## 비목표

- 스킬 광역 외 격자 잔존(Q8): `PickFallbackTarget` 니들 폴백(셀 체비셰프) · `HazardShapeSampler`
  셀 리스트 · `DefenderDensity.IsInTileRange` · 사거리 BFS 소스 · `CountEnemiesInTileRange`(로그 전용).
- 부착 flow 자체의 UX(락온·손패 — `dreamcatcher-attach-lockon`·`dc-use-flow` 소유).
- 전투 중 상시 범위 표시(부착된 카드의 오라 시각화는 별개 축).
- `EnemyMark`(살찐 제물)·`TileAim`(액티브) 모드의 프리뷰 — 전자는 대상이 적, 후자는 이미 자기
  표기(`SkillAim`)가 있다. unit 0 이 후자의 **모양**만 원으로 바꾼다.

## 후속 후보

- 인스펙트 패널에서 부착된 카드의 범위 재열람(2026-09-01 결정 1).
- 부채꼴 표기 어휘 — 첫 부채꼴 카드가 생기는 날 unit 으로 추가.
- 폴백 반경 표시(니들 × 폭탄맨/캐스터 host) — host archetype 을 카탈로그 입력에 추가하면 되나,
  `PickFallbackTarget` 이 셀 체비셰프라 먼저 자를 원으로 바꿔야 표기가 참말이 된다(Q8 비목표).
- 궤도 화염구 궤도 반경 표시(「지나가는 자리」 어휘).
- **배치 사거리 링의 `+0.25` 표준 상대 항 재검토** — 코드 주석은 「그림자가 링에 닿으면 안」이라
  적었으나 3항 공식은 「소형 적 중심이 안이면」 읽기다. 이 spec 이 광역 링을 가장자리(계약 2)로
  그리면 두 링의 읽기가 갈린다. 통일 여부는 `distance-based-range` 후속.
- `StatAuraSkill` 은 반경 이탈로 회수하지 않는다(TTL 만) — 그려진 도형은 「부여 위치」다.
  플레이어 혼란이 실측되면 문안으로 보강.
- Chebyshev dormant 코드(`RangeMetric.Chebyshev` · `BodyOverlapsSquare` · `squareShape`) 삭제 시점.
