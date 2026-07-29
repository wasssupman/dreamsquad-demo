# 3 — 텔레포트 (HealthThreshold × SelfBlink)

## 목적

"최대체력이 누적 10% 감소할 때마다, 자신에게 가장 많은 데미지를 입힌 방어유닛 근처로 순간이동"을 완결 메커닉으로 정의한다. 두 세만틱이 신규 — 임계치 트리거(반복·래치)와 SelfBlink(Movement 소유 위치 쓰기). 로직 완결성(렌즈 A)의 최난도 문서.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/DcTrigger.cs` — HealthThreshold 순수함수
- `Assets/_Project/Scripts/Battle/Combat/` — HealthThreshold 평가 arm + SelfBlink 발화
- Combat→Movement 텔레포트 seam (신규 이벤트 채널 or 기존 재사용)
- 슬롯 상태: `nextBoundaryIndex`(래치)

## 구현 (메커닉 완결 정의)

### 트리거 — HealthThreshold (반복·하향 엣지·래치)

- **가드**: `fraction<=0` 이면 발동 안 함(계약 9).
- 슬롯마다 `nextBoundaryIndex k`(초기 1). 경계 = `maxHpRef * (1 - k*fraction)` (fraction=0.10 → 90%,80%,…).
- **`maxHpRef` = 스폰 시점 maxHp 스냅샷**(런타임 max 변동과 무관). 현재 코드에 `Health.max` 런타임 쓰기는 없지만(grep 확인), 훗날 max 버프가 들어와도 경계가 흔들려 다중발동/스킵 나지 않도록 스냅샷 고정.
- 매 평가 시 현재 HP 가 경계 미만이면 **발동 + k 전진**. HP 는 Health(Units 소유) RO 조회.
- **다중 경계 동시 돌파**(대형 히트로 HP 가 여러 경계 관통): while-loop 로 최심 경계까지 k 점프하되 **텔레포트는 1회만**. (한 틱 다중 텔레포트 방지.)
- **래치(단조 하향)**: 힐로 HP 가 경계 위로 회복해도 k 되돌리지 않음 → 텔레포트 핑퐁 익스플로잇 차단.
- 순수함수 `HealthThresholdEval(hp, maxHpRef, fraction, k) -> (fired, newK)` EditMode. 조건 `hp < boundary`(strict), 오프바이원 없음.

### 타겟 — 위협 리더 (§1 의존)

> **은퇴 2026-07-29** — 착지 앵커는 `boss-jjangssen` unit 4 에서 "방어유닛 밀집도 최대 셀" 로 교체됐다. 아래 서술은 이 spec 시점의 계약 기록이며 현재 코드가 아니다. 현행은 `docs/spec/boss-jjangssen/4_density_blink.md`.

- 목적지 기준 = `ThreatLeader(보스 ThreatTable)`(§1). 근접+원거리 귀속으로 채워지므로 **일반적으로 리더가 존재**(폴백은 상시 경로 아님, HIGH-2 반영).
- 리더 alive = 조회 시점 `LocalTransform` 존재(§1 정의 공유). 같은 프레임 파괴면 없는 것으로 처리 → 폴백.
- **폴백(진짜 엣지)**: 위협 0(보스 무피해) 또는 리더 사망 시 → 최근접 생존 방어유닛. 방어유닛 0 이면 **텔레포트 skip**(발동 소모 유지 — k 는 이미 전진).

### 페이로드 — SelfBlink (신규 세만틱, 맥락 경계)

- 목적지 = 리더 world 위치 + **결정론 오프셋** 1타일(리더→보스 방향의 반대, 즉 리더에 접근), **최근접 walkable 셀 중심으로 스냅**(`FlowFieldSingleton` walk 마스크).
- **방향 degenerate(MED-4/N1)**: 보스가 리더와 인접/동일 셀이면 방향 벡터 ≈ 0 → `normalize(0)=NaN`. 길이 < epsilon 이면 **런타임-무의존 하드 상수축**(컴파일 상수, 예: world `-Z`)으로 폴백. "경로 진행 방향" 등 **런타임 상태 파생 축 금지** — 보스 정지(velocity≈0)/goal 셀/곡선 맵에서 그 축도 0·미정의라 NaN 재발(N1). NaN 목적지 금지.
- **링 순회 종료(MED-5)**: 스냅 셀이 점유/블록이면 인접 walkable 링을 **`maxRingRadius`(예: 3타일) 까지만** 순회. 못 찾으면 **텔레포트 skip**(k 는 전진). 무한 확장 금지.
- **walkable 정의**: flow-field walk 마스크 기준. 방어유닛 점유 셀이 walkable 로 잡히는지는 배선 시 확정하되, 계약상 "리더 인접 walkable 셀"이 목적지 — 리더 셀 자체가 blocked 면 링 첫 후보에서 자연 제외.
- **맥락 경계 엄수(계약 8)**: 위치는 Movement 소유. Combat arm 이 `transform.Position` 직접 쓰기 **금지**. → **Combat→Movement 텔레포트 요청 이벤트**(신규 NativeQueue `BlinkRequestEventsSingleton { entity, destWorld }` or 보스 캐리어 컴포넌트)로 넘기고, `MovementSystem` 이 소비해 위치 대입. 텔레포트 직후 다음 프레임 flow field 가 방향 재공급(포탈 텔레포트 선례, `MovementSystem.cs:90`).
- 순간 이동 → `AiState` 변경·캐스팅 상태 불요(계약 4).

### 엣지 degradation

- **방어유닛 전멸**: 발동해도 목적지 없음 → skip(k 는 전진, 재발동 안 함).
- **폭격/기본공격과 직교**: 텔레포트는 폭격 timer·`AttackState` 리셋 안 함(독립 슬롯).
- **텔레포트 직후 즉시 재-임계치**: 위치만 바뀌고 HP 불변 → 같은 틱 재발동 없음(k 단조).

## 완료 기준

- [x] `HealthThresholdEval` EditMode(반복 경계·다중 돌파 1회·래치 비회귀·k 전진·**`fraction<=0` no-fire 가드**·maxHpRef 스냅샷·hp=0 종료).
- [x] SelfBlink 가 Combat→Movement seam 으로만 위치 변경(직접 position 쓰기 0) — `BlinkApplySystem`(Movement)이 유일한 쓰기, Combat 은 `BlinkRequestEventsSingleton` enqueue 만.
- [x] 목적지 결정론: **방향 degenerate → 기본축(world -Z 상수)**, **링 `maxRingRadius` 초과 → skip** — BlinkMath EditMode 7종(NaN·비종료 없음).
- [x] 폴백 체인(리더→최근접(동점 entity index)→skip) 결정론 — 코드 검증 + Leader EditMode.
- [ ] (배선 후) Play: 보스 HP 10%씩 감소마다 위협 리더 근처로 blink, 힐 회복 시 재발동 없음. (보스 에셋 — unit 6)

확인 2026-07-10 — 컴파일 클린 + EditMode 640/642 그린(신규 13) + code-review(low) findings 0. walkable 확정 = `FlowFieldSingleton.dist != int.MaxValue`(도달 가능 셀 — 갇힘 방지까지 보장, defender 점유 셀 자연 제외). ThreatHit 드레인은 이 arm 상단에 배치(렌즈 B M2 폐색). 커밋은 unit 3 코드 커밋 해시 참조.
