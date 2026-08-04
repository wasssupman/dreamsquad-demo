# M1 청사진 ② — 데이터 대응표 + 게이트 이식 매트릭스

> unit 8 산출물 · 2026-08-04. 전수 원자료는 부속 2편이 담는다 —
> [m1_data_inventory_components.md](m1_data_inventory_components.md)(컴포넌트 97+21 전수) ·
> [m1_data_inventory_gates.md](m1_data_inventory_gates.md)(게이트 44 전수 · WithNone 48 사이트 ·
> HasComponent 부재-상태 20건 · 쓰기 지도 44행). 본 문서는 **번역 규칙과 예외**를 소유한다.

## 0. 계수 확정 (완료 기준 "빠짐 0" 증명)

- `IComponentData` **97** = 맥락 4폴더 96 + 루트 `BattleTimeScale` 1(설계 정본 §6 의 96 은 맥락 기준
  계수 — 오류 아님, 범위 차이). `IBufferElementData` **21** = 기대 일치.
- `ISharedComponentData`·`ICleanupComponentData` 구현 **0**(전 프로젝트) — 이식 대상에서 두 관용구
  자체가 부재.
- 종별 분포: tag 12 · data 48 · channel-singleton 27 · config-singleton 10 · buffer 21.
- 게이트: `RequireForUpdate` 35(기대 일치) + `RequireAnyForUpdate` 4 + 무게이트 5 = 44 전수.

## 1. 종별 매핑 규칙 5종

| 종별 | 신 sim 형태 | 규칙 |
|---|---|---|
| tag (12) | per-entity 상태 struct 의 bool/enum flag 또는 집합 멤버십 | `DeadTag`·`PendingDeployment`·`PastGoalTag` 처럼 **부재가 상태인** 태그는 플래그 명시화. ⚠ 이름이 Tag 인데 데이터인 3종(`FactionTag`·`DefenderClassTag`·`HitFlashTag`)은 data 규칙 |
| data (48) | plain struct 필드 그대로 | **`Entity` 필드는 전부 `SimEntityId` 로 치환** — §2 목록. `Random` 필드 2종은 §5 |
| channel-singleton (27) | **큐 소멸** | 출력 18 → 세션 이벤트(청사진 ① §4 판정), 내부 9 → phase 간 직접 전달(청사진 ③ §2 — 기전은 unit 9 소유). 큐 컨테이너를 이식하지 않는다 |
| config-singleton (10) | 성격별 3갈래 | ① 기믹 4종(`Burnout/ClockOut/Onsen/RedBull`) — "**존재 = 활성**" 관용구를 `MatchConfig` 의 활성 플래그+파라미터로 물질화 ② world-state 5종(`FlowField/DefenderField/Hazard/Obstacle Singleton`·`PickupSpawnState`) — sim 내부 상태로, §4 스냅샷 판정 동반 ③ `BattleTimeScale` — RateManager 소멸과 함께 폐기 후보(시계 정책 unit 소관) |
| buffer (21) | `List<T>` 또는 고정 슬롯 배열 | `[InternalBufferCapacity]` 는 레이아웃 힌트라 **폐기**(비보존 — 설계 정본 §3). `IncomingDamage` 류 "인박스 버퍼"는 tick 내 drain 계약 유지 |

## 2. Entity → SimEntityId 치환 필드 전수

per-entity 참조 14필드: `Aggroed.guardian` · `SummonedBy.owner` · `FocusTarget.current` ·
`SummonerState.current` · `FrontmostAttackLock.target` · `PatrolSpawnRequest.owner` ·
`ProjectileSpawnRequest.{target,owner,priorityTarget}` · `ProjectileState.{target,owner}` ·
`EmitterInstance.lockedTarget` (buffer) `ThreatEntry.attacker` · `ShieldSlot.source` ·
`IncomingDamage.source` · `IncomingShield.source` · (event payload 는 청사진 ① 이 이미 정규화).
치환 후 **생존 판정은 id 등록부 질의**로 — `Entity.Version` 이 하던 재활용 방어를 SimId 의
비재사용 성질이 대체한다(사망 3중 판정 관용구의 번역 근거).

## 3. 게이트 번역 규칙

게이트 4범주(부속 A-6)별로 운명이 다르다:

| 범주 | 시스템 수 | 번역 |
|---|---|---|
| 콘텐츠 존재(`AttackState`·`ProjectileTag` 등 18종) | 다수 | phase 함수의 **early-return**(대상 컬렉션 비면 skip) — 행동 등가 직역 |
| 기믹 활성(config 4종) | 6 | `MatchConfig` 활성 플래그 조건 — 의미 동일 |
| 인프라(world-state 4종) | 8+ | sim 내부 상태는 **항상 존재**하게 되므로 게이트 소멸 — 단 "맵 빌드 전" 시맨틱은 세션 수명(Create 이후에만 tick)이 흡수 |
| 이벤트 채널(8종) | 다수 | **게이트 자체가 소멸**(큐가 함수화) — "채널 부재 = 파이프라인 정지"는 초기화 순서 산물이지 게임 규칙이 아님을 salvage 판정에 전달 |

번역 시 보존해야 할 **행동 함의** 3건(부속 A 에 전수, 여기는 대표):
- `DamageApplicationSystem` 게이트는 `IncomingDamage` **버퍼 부재**만 본다(비어 있음이 아니라) —
  버퍼가 스폰 시 전원 부착되는 현 구조에서 사실상 "유닛 존재" 게이트. 직역하면 함의가 어긋난다.
- `AttackSystem` 게이트 정지 시 Cast 드레인도 동반 정지(큐 적재) — 함수화하면 이 동반 정지가
  사라진다 = 행동 차이 후보로 명시.
- `StackModifierTickSystem` 의 3중 AND(하나만 없어도 스택 영구 잔존) vs `StatModifierTickSystem`
  무게이트 비대칭 — 채널 소멸과 함께 자연 해소되지만, 해소 자체를 명시 변경으로 기록.

`RequireAnyForUpdate` 4건(OR — Taunt strip·Aggro orphan 해제·EffectTick 캐리어·UnitLifecycle)은
"회수 패스가 살아야 한다"는 의도가 본질 — early-return 조건을 OR 로 직역.

## 4. world-state 5종의 스냅샷 판정

| 상태 | 성격 | 스냅샷 |
|---|---|---|
| `FlowFieldSingleton` | 맵에서 파생(불변 — version 필드 있음) | **재구축**(config 의 맵으로) — 직렬화 불요 |
| `DefenderFieldSingleton` | 매 틱 재빌드(defender 배치에서 파생) | **재구축** — 다음 틱 P1 이 다시 만든다 |
| `HazardSingleton.cellToEffects` / `ObstacleSingleton.blockedCells` | 매 틱 재빌드 | **재구축** |
| `PickupSpawnState` | `candidateCells`(맵 파생) + `elapsed` + **`rng`(진행 상태!)** | candidateCells 재구축 + `elapsed`·`rng.state` **직렬화 필수** |

규칙: "매 틱 재빌드되는 파생물은 재구축, 진행 상태(타이머·RNG)는 직렬화" — 청사진 ① §5 의
entities 범위 판정에 같은 잣대를 적용한다(예: `HitFlashTag` 는 뷰성 진행 상태 — salvage 에서
sim 잔류 여부 판정).

## 5. 특수 이식 규칙

- **enableable 1종** `ModifierStatsDirty` → 명시 dirty set(`HashSet<SimId>`) — enable 토글 3지점
  (ModifierApply·StatModifierTick 이 set, Aggregate 가 clear)을 add/remove 로 직역.
- **RNG 필드 2종**(`PickupSpawnState.rng`·`BombLauncherState.rng`) — **소비 후 write-back 이
  결정론 조건**(실측 주석 확인). 신 sim 도 값 타입 xorshift 를 상태로 들고 되쓴다.
- **lazy-attach 2-pass 3건**(MaxHealthScale·FatigueAccrual·HeatAccrual — 중간 Playback 관용구) —
  신 sim 은 즉시 추가 가능하므로 1-pass 로 접되, "부재 = 미부착" 판정(B-1)이 같은 틱 안에서
  안 바뀌도록 컬렉션 순회 규칙(청사진 ③ §5) 준수.
- **ECB·EntityManager 혼용 1건**(ModifierApplySystem — 같은 드레인 루프 동일 타깃 2회 이벤트에서
  슬롯 덮임 방지) — 신 sim 의 즉시 적용에서는 문제 자체가 소멸하나, **같은 틱 다중 이벤트 병합
  순서**(등록 순서)는 계약으로 보존.
- **부재-상태 주의 1급**: `DamageApplicationSystem` 의 UltimateLeapState 무적은 `WithNone` 이
  아니라 **버퍼 Clear + continue** 다 — WithNone 으로 직역하면 이탈 2초분 피해가 적립돼 착지
  프레임에 터진다(지연 폭탄). 부속 B-2 의 20건이 전부 이 급의 함정 후보.
- **단독 writer 표**(부속 C-6)는 sim lib 모듈 경계의 초안이다 — CLAUDE.md 제약 2(맥락 쓰기
  소유권)의 후계 불변식(README 이행표)이 이 표를 승계한다.

## 6. 완료 기준 대조

- 컴포넌트 97+21 전수 ✓(부속 ①, ISharedComponentData 0 확인) · 게이트 44 전수 ✓(부속 ② A) ·
  부재-상태 별도 섹션 ✓(부속 ② B — WithNone 48 + HasComponent 20) · 코드 변경 0 ✓.
