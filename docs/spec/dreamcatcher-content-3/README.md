# dreamcatcher-content-3 — 액션형 유닛 카드 5장 (기존 메커니즘 재사용)

상태: 초안 (사용자 승인 대기)

## 상위 목표

구현돼 있으나 카드가 쓰지 않는 트리거/페이로드 조합으로 **액션형(비-스탯) 유닛 카드 5장**을 추가한다. 원칙: 코드 최소 — 5장 중 2장은 코드 0줄, 1장은 enum append 수준, 2장만 기존 발동 지점에 arm 분기를 더한다. 스탯 버프 페이로드 신설 금지(이 spec 은 트리거→액션만).

## 카드 목록

| # | id | 표시명 | 조합 | 코드 비용 |
|---|---|---|---|---|
| 0 | `gale_shove` | 밀치기 | AttackN × ApplyCcToTarget(**Impulse**) | 0줄 (카드 에셋만) |
| 1 | `frostbite` | 동상 | AttackN × ApplyStackToTarget(**Ice**) | 0줄 (SO 2개 + 씬 배선) |
| 2 | `lullaby_dart` | 자장가 | AttackN × ApplyCcToTarget(**Sleep**) | enum append + 번역/문안 case |
| 3 | `corpse_burst` | 시체폭발 | **OnKill** × SelfTileAoe(킬 위치) | arm 분기 + EnemyKilledEvent 위드닝 |
| 4 | `tremor_plate` | 진동갑주 | **HealthThreshold** × SelfTileAoe(자기 위치) | bake 호이스팅 + arm 분기 |

## 작업 단위

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| `0_gale_shove_card.md` | 데이터 | 넉백 카드 — Impulse CC 최초 카드 사용 |
| `1_frostbite_card.md` | 데이터+배선 | 얼음 스택 카드 — 3스택 슬로우 / 5스택 동결 |
| `2_lullaby_dart_card.md` | 소규모 코드 | 수면 온-히트 카드 — DcCcKind.Sleep 개통 |
| `3_corpse_burst_card.md` | 소규모 코드 | 킬 위치 폭발 — OnKill 지점에 SelfTileAoe arm |
| `4_tremor_plate_card.md` | 소규모 코드 | 피격 임계 폭발 — HealthThreshold 지점에 SelfTileAoe arm |
| `5_handoff_summary.md` | (종료 시) | 인계 요약 |

## Feature-wide 계약

- **정의 계층 불변식 유지**: `DcMechanic.cs` 는 ECS 무지·append-only. 이 spec 의 유일한 정의 계층 변경은 `DcCcKind` 에 `Sleep` append (unit 2).
- **신규 NativeQueue 채널 금지**: 시체폭발은 `EnemyKilledEvent` 필드 위드닝으로 운반한다 — `DefenderDeathEvent.hasOnDeathAoe`(작별 선물) 선례와 동형. 22번째 채널을 만들지 않는다.
- **AoE 실행은 기존 정거장 재사용**: 모든 폭발은 `SkyFall × TileAoe` 투사체(flightTime 0) — OnDeath 폭발(`DrainDefenderDeathEvents`)·실드 파열(`DrainShieldBreakEvents`)과 동일 경로. 신규 아키타입 없음.
- **카드 flat 데미지 원칙**: 카드발 데미지에 attacker damageMul 미적용 (기존 계약 유지).
- **v1 단순화 선례 유지**: OnKill/HealthThreshold AoE 는 "첫 매칭 슬롯만" (OnDeath v1 선례). 카드 2장 중복 부착 시 폭발 1회.
- **art = null**: 카드 아트는 category 색 폴백으로 출시, 실아트는 후속(guid 유지 교체 관례).
- **문안**: `DreamcatcherCardText` 가 조합을 지원하면 자동 문안, 아니면 SO `description` 폴백. 신규 조합마다 `DreamcatcherCardTextTests` 케이스 1개.
- **시트 push**: feature 종료 시 1회 (비파괴 업서트). 유닛별로 하지 않는다.

## 파이프라인 커버리지

N/A — 플레이 오브젝트 신설·생성→렌더 경로 변경 없음. 폭발 연출은 기존 투사체(TileAoe) 아키타입과 기존 AOE-view ProjectileData 를 재사용하고, CC(넉백/수면)·스택은 기존 상태 표현을 그대로 쓴다.

## 후속 후보

- Fire / Poison 스택 카드 (StackModifier SO 만 추가하면 동상과 동형 — 출혈과의 차별화 설계 필요)
- 동상 오버헤드 스택 아이콘 (`OverheadStackKind` 확장 + surfacing — 현재 Bleed 도 아이콘 없음)
- 넉백 방향의 경로-역방향 옵션 (현재는 공격자→적 방향)
- OnDamagedN 트리거 범용화 (현재 NextAttackDoubleFire 전용 특례 경로)
- PeriodicTimer 트리거 디펜더 카드 개방 (현재 보스 스폰 경로만 bake)
- 5장 실아트 (guid 유지 교체)
