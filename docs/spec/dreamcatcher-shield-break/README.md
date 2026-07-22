# Dreamcatcher — 실드 파괴(OnShieldBreak) 트리거 Spec

**상태**: 완료 2026-07-22 (unit 0~5) — 새 드림캐쳐 트리거 `OnShieldBreak`(부여된 실드가 **피격으로 소멸**할 때, 시간만료 제외) + 드림캐쳐 2종(산산조각·고요한 파문) + 배틀로그 기록. 사용자 Play + 로그(`shield_break_events`) 진단으로 검증(범위/개수 정상 확인) → 고요한 파문 튜닝(범위 2→1·대상 3→2). 인계: [6_handoff_summary.md](6_handoff_summary.md).

## 목표

1. **`OnShieldBreak` 트리거**: 유닛에 부여된 실드가 **피격으로 완전 소진**(shield Sum > 0 → 0)될 때 발동. **시간만료로 사라지는 경우는 제외** — 현재 `ShieldSlot` 은 duration 필드가 없어 시간만료 경로 자체가 없고, 탐지점이 `ShieldMath.Absorb`(피격) 전용이라 향후 시간만료가 추가돼도 구조적으로 배제된다.
2. **드림캐쳐 A — 실드 파열 폭발**: OnShieldBreak → **자기 중심 N타일 AoE로 데미지**. 기존 `SelfTileAoe` 페이로드 재사용(트리거만 신규).
3. **드림캐쳐 B — 실드 파열 진정**: OnShieldBreak → **N타일 이내 가장 가까운 M명의 적을 L초 수면(Sleep)**. 신규 `AreaSleep` 페이로드.

## 검증 질문 (이 spec 이 답해야 할 것)

- 실드를 부여받은 유닛이 **피격으로 실드가 깨질 때** OnShieldBreak 가 1회 발동하는가? (부분 흡수/시간경과로는 발동 안 하는가?)
- A: 실드 파열 시 자기 중심 N타일에 적 데미지가 들어가는가?
- B: 실드 파열 시 N타일 이내 가까운 M명이 L초 잠드는가? (범위 밖/초과분은 안 걸리는가?)
- 결정론: 같은 상황 → 같은 대상 선정(AoeTargetCap 인덱스 tiebreak)인가?
- 실드가 없거나 OnShieldBreak 미보유 유닛에선 무동작인가?

## 재사용 지도 (신규 최소화)

| 조각 | 재사용 | 신규 |
|---|---|---|
| 정의 계층 | `DcTriggerKind`/`DcPayloadKind`(append-only) · `DcMechanic`/`DcTriggerSlot` bake(BattleBridge.Dreamcatcher) | `OnShieldBreak` 트리거 + `AreaSleep` 페이로드 enum/bake |
| 탐지 | `DamageApplicationSystem` 의 `ShieldMath.Absorb` + `Sum` + `_dcTriggerSlotLookup`(OnKill 선례) | 실드 파열 감지 + 이벤트 emit |
| 이벤트 | Units→Bridge NativeQueue 선례(`DefenderDeathEventsSingleton` 등) | `ShieldBreakEventsSingleton` |
| A 실행 | `BattleBridge.SpawnProjectile`(SkyFall×TileAoe) — `DrainDefenderDeathEvents` OnDeath 폭발/메테오 동형 | 없음(cast 재사용) |
| B 실행 | `AoeTargetCap.SelectNearest`(M-cap, 결정론·테스트됨) · `GridMath` Chebyshev · `EnemyCcEvent{Sleep}`→`EnemyCcEventsSingleton`→`CcApplySystem` · wake-on-hit(`CcClearRequests`) | 적 선정+Sleep enqueue drain |
| 카드/카탈로그 | 기존 드림캐쳐 카드 SO + 카탈로그 sync | 카드 2종 |

## 작업 단위 (초안 — 승인 후 번호 문서화)

| # | 작업 | 태그 | 목적 |
|---|---|---|---|
| 0 | 트리거 정의 + 탐지 + 채널 | [ECS] | `DcTriggerKind.OnShieldBreak` append · `ShieldBreakEventsSingleton`(Units→Bridge) · `DamageApplicationSystem` 에서 Sum>0→0(피격) 감지 + host `DcTriggerSlot` OnShieldBreak 읽어 이벤트 emit · 채널 lifecycle |
| 1 | `AreaSleep` 페이로드 정의 + bake | [ECS] | `DcPayloadKind.AreaSleep` append · bake(magnitude=M cap·tileRange=N·duration=L → DcTriggerSlot). (A 의 SelfTileAoe 는 기존 bake 로 트리거 무관 동작 — 신규 없음) |
| 2 | `DrainShieldBreakEvents` 실행 | [ECS] | BattleBridge drain, payload 분기 — SelfTileAoe→SpawnProjectile(SkyFall×TileAoe, host cell); AreaSleep→적 Chebyshev 쿼리+AoeTargetCap(M)+`EnemyCcEvent{Sleep,L}` enqueue. 드레인 호출 배선 |
| 3 | 카드 2종 + 카탈로그 | [data] | 드림캐쳐 A(OnShieldBreak+SelfTileAoe)·B(OnShieldBreak+AreaSleep) 카드 SO + 카탈로그 등록. 수치 SO |
| 4 | Play 통합 검증 | [ECS] | shield-guardian + 대상 유닛 + 적 → 피격 실드파괴 → A 폭발/B 수면 실측 |
| 5 | 배틀로그 기록 | [logging] | 실드 파열 발동 + 대상별 효과(누구에게 뭐)를 `shield_break_events[]` 에 기록 (사용자 요청, unit 4 Play 관측용) |

## Feature-Wide 계약

- **정의 계층 무오염**: `DcTriggerKind`/`DcPayloadKind` **append-only**(기존 카드가 int 직렬화 — 중간 삽입 금지). 정의 계층(`DcMechanic`)은 `Unity.Entities`/`Battle` 미참조 — 해석은 BattleBridge/Combat 만(아키텍처 스왑 = 번역기만 재작성).
- **탐지 = 피격 소멸 전용**. `DamageApplicationSystem`(Units)에서 `ShieldMath.Absorb` **전 `preSum = Sum(slots)`, 후 `Sum(slots)`** — `preSum > 0 && post == 0` 이면 이번 프레임 피격으로 실드 파열. host 의 `DcTriggerSlot`(Combat, RO — OnKill 선례) 에 `OnShieldBreak` 슬롯 있으면 `ShieldBreakEvent` emit. **시간만료 미탐지**(경로 없음/구조적 배제).
- **파열 의미 = 실드 풀 전체 소멸(Sum>0→0), 1회**. 다출처 슬롯이면 마지막 슬롯이 피격 소진되는 순간 1회. (부분 흡수·개별 슬롯 소진 중간엔 미발동.)
- **호스트 = 실드를 부여받은 유닛**. 발동 중심 = host 위치. "실드 소멸 시 주변" = host 주변. (플레이 전제: shield-guardian 등으로 host 가 실드를 받는 편성.)
- **A(데미지) = 기존 `SelfTileAoe` 재사용**. 트리거 무관 bake(payload 기준). 실행 = `SpawnProjectile`(SkyFall×TileAoe, `targetFaction=Enemy`, owner=Null) — OnDeath 폭발/메테오와 동형. **Combat 투사체 코드 불변**.
- **B(수면) = 신규 `AreaSleep`**. 필드 재사용(신규 DcPayloadSpec 필드 0): `magnitude=M`(적 수 cap, floor int)·`tileRange=N`(Chebyshev 반경)·`duration=L`(Sleep 초). 실행 = host cell Chebyshev N 내 적(`AttackUnitTag`) 수집 → `AoeTargetCap.SelectNearest`(거리² 오름차순 M, 동률=인덱스, 결정론) → 각 적에 `EnemyCcEvent{ kind=Sleep, remainingTime=L }` enqueue. `DcCcKind` 확장 불필요(payload 자체가 Sleep 확정).
- **Sleep = wake-on-hit**(기존 combat-action-lock). 다른 데미지에 맞으면 조기 해제 — 의도된 리스크(B 는 데미지를 안 줌). A/B 는 별 카드라 상호 간섭 없음.
- **채널**: 신규 `ShieldBreakEventsSingleton`(Units→Bridge NativeQueue). 실행측이 `EnemyCcEventsSingleton`(기존)에 Sleep enqueue. CLAUDE.md 채널 목록 갱신(구현 단위에서).
- **맥락 경계**: 탐지·이벤트 emit = Units(실드/Health 소유). 페이로드 실행 = BattleBridge(Mono 게이트웨이 — 제약 1: EntityManager/SpawnProjectile/큐 접근의 정당한 자리). Sleep 은 EnemyCcEvents(Effects 소유) 채널로만.
- **모든 수치 SO**: A(데미지·반경·AoE view)·B(M·N·L). 하드코딩 금지.

## 파이프라인 커버리지

N/A(재사용) — 새 플레이 오브젝트 없음. A 는 기존 SkyFall×TileAoe 투사체 파이프라인 소비, B 는 CC 만. 트리거/페이로드는 기존 드림캐쳐 bake→실행 정거장 재사용.

## 열린 결정 (승인 시 확인)

- **파열 의미**: 실드 풀 전체 소멸(Sum→0) 1회 — 채택(가장 직관적). 개별 슬롯마다 발동을 원하면 알려주세요.
- **B 대상 선정**: 가장 가까운 M명(결정론) — 채택. 랜덤 M 을 원하면 시드 파생 필요.
- **수치 기본값(플레이 후 튜닝)**: A = 데미지 80·반경 1(3×3). B = **반경 1(3×3)·M 2명·L 2.5초** (튜닝 2026-07-22: 범위 2→1·대상 3→2, 로그 진단 후 연발 체감 완화). (SO 값, 조정 자유.)

## 비목표

- 실드 시간만료 메커니즘 신설(현재 없음, 이 spec 범위 밖).
- 실드 파열 전용 아트/VFX(A 는 기존 AoE view, B 는 기존 수면 표현 재사용 — 전용 연출은 후속).
- 적이 부여한 실드/보스 실드 상호작용(현재 실드 부여자 = defender 계열).
- A/B 외 OnShieldBreak 페이로드(예: self-buff, blink) — 필요 시 후속.

## 후속 후보

- `AreaCc` 일반화(ccKind 로 Stun/Sleep 선택) — 현재 Sleep 전용. `DcCcKind` 에 Sleep 추가 필요.
- 실드 파열 전용 VFX(파편/파동) + 사운드.
- OnShieldBreak × 기존 페이로드 조합 카드(self 공속버프 등).
- 실드 파열 텔레메트리(로그 v2).
