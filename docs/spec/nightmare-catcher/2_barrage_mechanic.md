# 2 — 융단폭격 (PeriodicTimer × AreaBarrage)

## 목적

"10초마다 임의 **방어유닛** 기준 3타일 이내 모든 **방어유닛**에게 10 데미지 폭격"을 **아키텍처와 무관하게 완결된 메커닉**으로 정의한다. (진앙도 피해 대상도 방어유닛 — 보스 시점의 "적"은 방어유닛이다. 표기 일관.) 로직 완결성(렌즈 A) 검증 대상.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/DcTrigger.cs` — PeriodicTimer accumulator 순수함수
- `Assets/_Project/Scripts/Battle/Combat/` — 주기 틱 시스템(슬롯 accumulator) + AreaBarrage arm
- 슬롯 상태: `DcTriggerSlot` 에 accumulator 필드(elapsed) 추가 or 병렬 slot

## 구현 (메커닉 완결 정의)

### 트리거 — PeriodicTimer

- **가드**: `periodSeconds<=0` 이면 발동 안 함(계약 9). 아니면 accumulator 진행.
- 슬롯마다 `elapsed` accumulator. 매 시뮬 틱 `elapsed += dt`(TimeManager 도메인 시간, `Time.timeScale` 금지). `elapsed >= periodSeconds` 면 **1회 발동 후 `elapsed -= periodSeconds`**(잔여 이월, 드리프트 방지). `period ≫ dt` 가정 — 랙 스파이크로 여러 주기 밀려도 틱당 1발 drip(무해, period=10s ≫ dt).
- **시작 위상**: 부착(스폰) 시점 `elapsed=0` → 첫 발동은 스폰 후 `periodSeconds`. (즉발 아님.)
- **결정론**: dt 는 고정 시뮬 스텝. 순수함수 `PeriodicTick(elapsed, dt, period) -> (fired, newElapsed)` EditMode.

### 진앙(epicenter) 선택 — 결정론

- 대상 풀 = 현재 생존 방어유닛. **index 기반 round-robin**: 슬롯의 `fireCount` 를 안정 정렬된 방어유닛 리스트 크기로 나눈 나머지 → 진앙. seeded RNG 금지(계약 7).
- 안정 순서 = 방어유닛 셀 인덱스(row-major) 오름차순. **실제 발동 시에만** `fireCount++`(0-defender no-op 은 미증가 → 위상 드리프트 방지).
- 로스터 churn(방어유닛 사망) 시 `fireCount % N` 은 완전 안정 회전은 아님(인덱스 시프트로 중복/스킵 가능) — "임의"의 결정론적 **근사**로 허용. 정밀 회전이 필요해지면 후속.

### 페이로드 — AreaBarrage (기존 프리미티브 재사용 + 진영축 1개 추가)

- 진앙 셀 중심 **Chebyshev `tileRange`(=3) 이내 모든 방어유닛**에 `magnitude`(=10) flat 데미지.
- 전달 = 플레이어 Meteor 파이프라인 재사용: `SkyFall × TileAoe` 투사체 1발을 진앙 셀에 발사(`impactTileRange=tileRange`, `damage=magnitude`, `flightTime=duration` — unit 0 rev 2). 낙하/이동은 기존 `ProjectileMoveSystem` 그대로.
- **발사 경로 = ECS 캐리어 요청 (rev 2 명시)**: arm 이 `ecb.CreateEntity` 캐리어에 `ProjectileSpawnRequest` + `ProjectileRequestCarrier` 를 실어 기존 drain 에 태운다(dc-trigger 계약 6 선례 — 보스 본체의 기본공격 request 와 같은 프레임 충돌 방지). bridge 의 `CastSkillAtTile`(Mono, 현 Active 카드 경로)은 **사용하지 않는다** — ECS arm 은 bridge 를 호출할 수 없고, SkyFall 비주얼 파라미터(`dataIndex`/`dropHeight` 등)는 슬롯에 베이크된 `projectileDataIndex` 로 온다(unit 5, dc-trigger 슬롯 선례).
- ⚠ **진영 반전 (렌즈 A HIGH-1)**: 기존 `ProjectileHitSystem` 의 TileAoe 착탄 풀은 `WithAll<AttackUnitTag>`(=적) 로 만든다(`ProjectileHitSystem.cs:59`). 이를 **verbatim** 태우면 진앙(방어유닛) 주변의 **적**이 맞고 방어유닛은 0 데미지 — 검증 질문 정면 위반. → 착탄 arm 을 **진영 파라미터화**(투사체에 target-faction 플래그 → `DefenderUnitTag` 풀 순회)한다. **"새 데미지 경로 0" 아님, 진영축 1개 추가한 재사용.** 플래그 **기본값 = 적(enemy)** — 플레이어 Meteor/기존 투사체는 플래그 미설정이라 기존 enemy-타겟 그대로(무회귀, N3). 보스 폭격만 defender 플래그 세팅.
- **보스 자해 없음**: 착탄 풀이 방어유닛 전용이므로 보스(적)는 애초에 미포함(진영 파라미터화의 부수 효과).
- flat 데미지(계약 8): 공격자 `damageMul` 미적용.
- **(렌즈 B 후속, 같은 뿌리)** `DamageApplicationSystem` 의 데미지 넘버/처치 이벤트가 `AttackUnitTag` 게이트라, 방어유닛 폭격 피해엔 팝업/스코어가 안 뜬다 — 진영축 확장 시 함께 검토.

### 엣지 degradation

- **생존 방어유닛 0**: 진앙 없음 → 발동 no-op. **timer 는 리셋**(`elapsed -= period`), 백로그 누적 안 함.
- **동시 다중 슬롯**: 슬롯 2개가 같은 틱 발동 시 각자 자기 진앙에 독립 폭격(정상). 시각 겹침은 후속.
- **기본공격과 직교**: 폭격 발동이 보스의 `AttackState`/이동/`AiState` 를 건드리지 않는다(계약 4). 보스는 폭격 중에도 계속 이동·기본공격.
- **슬로모 하 감속 (rev 2)**: 손패 열림 동안 Battle 도메인 0.3x(awakening-hand) — accumulator 는 도메인 dt 로 tick 하므로 주기·낙하 텔레그래프도 시뮬 전체와 함께 감속. 의도된 동작(별도 처리 금지).
- **진앙 셀-락**: 발동 시점 진앙 셀을 SkyFall `impact` 로 락. 낙하 텔레그래프 동안 진앙 방어유닛이 이동/사망하면 빈 셀 타격 — 방어유닛은 타일 고정이라 대체로 무해, 정의된 동작(빗나감 허용).

## 완료 기준

- [x] `PeriodicTick` EditMode(주기 발동·잔여 이월·위상·**`period<=0` no-fire 가드** + 랙 스파이크 drip).
- [x] 진앙 round-robin 결정론 EditMode(같은 입력 → 같은 진앙 순회 + 스냅샷 순서 무관, no-op 미증가는 arm 코드).
- [x] AreaBarrage 착탄이 **방어유닛 진영**을 때린다(진영 파라미터화) — 적/보스 자해 없음. (`targetFaction=Defender` 유일 setter = 이 arm)
- [ ] (배선 후) Play: 보스 스폰 후 10초 주기 진앙 폭격, 3타일 내 다중 방어유닛 10 데미지. (보스 에셋 — unit 6)

확인 2026-07-10 — 컴파일 클린 + EditMode 627/629 그린(신규 8) + code-review(low) findings 0. 사용자 무회귀 스모크 승인("문제없어 보인다"). arcHeight(SkyFall 낙하높이)는 ECS arm 이 SO 를 못 읽어 drain 폴백으로 보충. 커밋은 unit 2 코드 커밋 해시 참조.
