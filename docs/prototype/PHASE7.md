# Phase 7 — 랜덤 2종 스킬 로드아웃 + 신규 스킬 3종

> Phase 7은 Phase 6까지의 기본 루프에 "매 판 스킬 조합이 달라지는" 변수를 추가한 단계다. 기존 3종 스킬에 Tornado / Meteor / Portal을 더하고, 매 판 6종 중 2종을 뽑아 SkillBar에 바인딩한다.

---

## 1. 목표

- 매 판 시작 시 스킬 풀 6종 중 2종을 결정적으로 roll한다.
- Draft 화면에서 이번 판 스킬 2종을 보여준다.
- SkillBar는 고정 3슬롯에서 2슬롯 동적 loadout으로 전환한다.
- Tornado / Meteor / Portal 3종 신규 스킬을 ECS 전투에 연결한다.
- JSON 로그에 skill pool / picked / seed / Portal 두 번째 타일을 기록한다.

### 비목표

- 스킬 업그레이드/레벨업.
- 스킬 3개 이상 동시 장착.
- Flow Field 기반 길찾기. Portal/Tornado의 waypoint 한계는 Phase 9에서 해결한다.

---

## 2. 확정 결정

| 항목 | 구현 결과 |
|---|---|
| 스킬 풀 | SlowField / RapidFire / PowerSurge / Tornado / Meteor / Portal |
| 판당 스킬 수 | 2종 |
| Restart | 같은 skill loadout 유지 |
| Redraft | `SkillLoadoutController.Roll` 재호출 |
| Portal 캐스트 | 2탭 입력: entry tile → exit tile |
| Meteor | warning ring 후 지연 AoE damage |
| Tornado | Phase 7 초기 cast-time pull. Phase 8 §17에서 지속 field로 재설계 완료 |
| 로그 | `skill.pool`, `skill.picked`, `skill.seed`, `target_tile_b` |

---

## 3. 신규 스킬 스펙

### 3.1 Tornado

- 중심 타일과 radius를 기준으로 적을 중심으로 끌어당기는 CC 스킬.
- Phase 7 초기 구현은 cast 시점에 범위 내 적에게 pull을 적용했다.
- Phase 8 §17에서 `TornadoField` carrier entity 기반 지속 field로 전환되어, duration 중 새로 진입한 적도 영향을 받는다.
- pull 이후 waypoint 기반 복귀가 기계적인 문제는 Phase 9 Flow Field 전환 대상이다.

### 3.2 Meteor

- 타일 지점에 warning ring을 표시하고, warning 시간이 끝나면 Combat 시스템이 AoE damage를 적용한다.
- `MeteorPending` carrier entity를 사용한다.
- `MeteorResolutionSystem` 이 damage를 적용하고 `MeteorBurstEventsSingleton` 으로 VFX 타이밍 이벤트를 보낸다.
- Phase 8에서 `Meteor_Falling_SKELETON` 과 `Meteor_Burst_SKELETON` prefab VFX가 연결됐다.

### 3.3 Portal

- 첫 탭은 entry tile, 두 번째 탭은 exit tile.
- `PortalLink` carrier entity가 entry/exit, radius, duration, `exitWaypointIndex`를 보관한다.
- `MovementSystem` 이 entry radius 안의 적을 exit로 teleport하고 waypoint index를 조정한다.
- exit가 경로 밖이거나 뒤쪽 waypoint로 resolve되는 경우 동선 이상이 발생할 수 있으며, Phase 9 Flow Field에서 해결한다.

---

## 4. Loadout / UI

- `SkillLoadoutController` 는 6종 pool에서 seed 기반으로 2종 picked를 산출한다.
- Draft 성공 경로에서 loadout roll이 실행된다.
- DraftView는 "이번 판 스킬 2종" 패널을 표시한다.
- SkillBar는 2개의 동적 슬롯을 생성/바인딩하며, cost/cooldown/phase gate는 Phase 6 규칙을 따른다.
- Redraft는 새 loadout, Restart는 기존 loadout을 유지한다.

---

## 5. 로그

- `BattleLogSchema.SkillRecord` 는 pool, picked, seed를 포함한다.
- `SkillUsageLog.target_tile_b` 는 Portal 전용 두 번째 타일을 기록한다.
- Portal이 아닌 스킬은 sentinel 값으로 미사용 상태를 표현한다.

---

## 6. 작업 결과

- [x] P7-01 — `SkillEffectType` enum에 Tornado/Meteor/Portal 추가 + `SkillData.warningSec`.
- [x] P7-02 — Tornado/Meteor/Portal SO 에셋 생성.
- [x] P7-03 — `SkillLoadoutController` 구현 + GameManager 연결.
- [x] P7-04 — 6종 스킬 pool 구성.
- [x] P7-05 — Draft 성공 경로에서 loadout roll.
- [x] P7-06 — Tornado pull 구현. Phase 8 §17에서 지속 field로 보강 완료.
- [x] P7-07 — Meteor carrier + delayed AoE + warning visual.
- [x] P7-08 — Portal carrier + teleport + 2탭 캐스트 UI.
- [x] P7-09 — DraftView 스킬 패널.
- [x] P7-10 — SkillBar 2슬롯 동적 바인딩.
- [x] P7-11 — skill pool/picked/seed 로그.
- [x] P7-12 — Redraft reroll / Restart 유지.
- [x] P7-13 — `target_tile_b` 로그.
- [ ] P7-14 — SkillLoadoutController 결정성 EditMode 테스트 확인.
- [ ] P7-15 — 사용자 Play 회귀: 드래프트 2종 스킬 패널 / Tornado·Meteor·Portal / Restart·Redraft.

---

## 7. 종료 조건

- 매 판 진입 시 6종 중 2종 스킬이 결정적으로 선택된다.
- Restart는 같은 loadout을 유지하고 Redraft는 새 loadout을 만든다.
- Tornado/Meteor/Portal이 ECS 전투에서 의도한 효과를 낸다.
- JSON 로그에 skill pool/picked/seed 및 Portal `target_tile_b`가 기록된다.
- P7-15 사용자 Play 회귀가 통과해야 Phase 7 검증 완료로 본다.

---

## 8. TRD 금지 패턴 재적용

- SkillLoadoutController는 비싱글톤이며 GameManager가 참조한다.
- 모든 스킬 수치(cost/cooldown/range/magnitude/warningSec)는 `SkillData` SO에 둔다.
- Portal/Tornado는 Movement 맥락이 Position 쓰기를 소유한다.
- Meteor damage 적용은 Combat 맥락이 소유한다.
- `ISkillEffect` 같은 선행 추상화는 만들지 않고 기존 enum switch를 유지한다.

---

**문서 버전**: v1.0 (구현 스펙 통합)
**상태**: 구현 완료. P7-14/P7-15 검증은 residual에서 추적.
