# Spec — Aggro Targeting (자석 디펜스 어그로)

> 상태: Unit 0~8 완료 2026-06-18(근접 모델) · **히트 구동 재설계 Unit 9~13 완료 2026-07-09**(코드+테스트+씬배선, 커밋 `1f8246d6`/`b84b6887`/`5ea07f6c`, EditMode 604 통과, Play 진입 에러 0) · **잔여: 아이콘/전투 육안 Play 스모크(Unit 14, 포커스 필요)** — handoff `15_hit_driven_handoff.md`
> 출처 기획: `꿈결타게팅어그로 상세 기획 v0.1.1` (approved). 본 spec 은 그 기획의 **어그로 부분만** 코드 구현 단위로 내린 것. 도발(에픽 가디언)·점수·드림캐쳐·인접 시너지는 범위 밖.

## ⚠ 재설계 (2026-07-09): 근접 즉시 배정 → 히트 구동

Unit 0~8 은 **근접 즉시 배정**(가디언 사거리 안 적을 매 틱 자동 어그로)으로 구현됐다. 이는 원 기획의 "가디언 기본공격이 명중한 대상을 어그로" 의도와 어긋났다(당시 초안에서 히트 모델이 "시간당 어그로 수량 rate" 후속으로 밀려 있었음). **Unit 9~14 에서 히트 구동으로 전환**한다:

- 트리거 = **가디언 공격 명중**(근접 RESOLVE). 근접 자동 획득 삭제.
- 드림캐쳐 2계층 격리(정의=순수함수 `AggroPolicy`/`AggroTargeting`, 해석=ECS 시스템 + Mono 아이콘). `docs/reference/dreamcatcher-portability.md` 계승.
- `AggroProvider`(+range) 폐기 → `AggroCapacity{max,held}`. 어그로된 적 머리 위 아이콘(Mono 소비).
- ecs-reviewer critic 반영: H1 드레인 프로토콜, H2 aggro-aware 타겟팅, M1 사망 3중 판정.

아래 "핵심 루프/검증 질문/계약"은 **히트 모델 기준으로 갱신됨**. 근접 모델 계약(구 5·10 등)은 Unit 0~8 히스토리로 보존.

### 파이프라인 커버리지 (어그로 아이콘 — 오버헤드 View)

아이콘은 신규 ECS 아키타입이 아니라 **`EnemyHitBar` 오버헤드 View 파이프라인 재사용**. 아키타입별 정거장 대조:

| 정거장 | 어그로 아이콘 |
|---|---|
| 데이터(SO) | `AggroIconStyle`(스프라이트/오프셋/크기) |
| ECS 상태 | `Aggroed`(Effects) — 아이콘용 신규 컴포넌트 없음 |
| 생성 트리거 | BattleBridge 매 프레임 reconcile(`Aggroed` 유무) |
| 뷰/풀 | `AggroIconSpawner`(Dict+Queue) / `AggroIconView`(빌보드) |
| 베이크 | N/A — 런타임 상태 구동, 베이크 대상 아님 |
| 투사체/무버 | N/A — 정지 오버헤드 요소 |
| teardown | `Spawner.Clear()` |

object-pipeline-map 에 "오버헤드 View" 아키타입 추가 필요 여부는 Unit 14 에서 판정.

## 목표

가디언이 적을 **자기 자리로 끌어와 한 점에 겹쳐 모으는** 자석(magnet) 디펜스 핵심 루프를 구현한다. 길막(blocking)이 아니라 어그로로 적을 모으고, 모인 팩을 광역으로 정리한다.

핵심 루프(히트 모델): **가디언이 사거리 내 적을 공격해 명중 → 명중한 적 어그로 획득(capacity 내) → 적이 스스로 가디언으로 보행 → 가디언 타일에 겹쳐 정지 → 가디언 sticky 공격 → 가디언 사망 시 해제 → 출구 복귀.**

## 검증 질문

> (히트 모델) "가디언이 사거리 안 적을 공격해 **명중**하면 그 적이 어그로되어(capacity 내) 스스로 가디언 타일로 걸어와 겹쳐 멈추고 가디언이 죽을 때까지 가디언만 공격하는가? capacity 초과분은 명중해도 어그로가 안 걸리는가(데미지만)? 가디언이 죽으면 흩어지는가? 어그로된 적 머리 위에 아이콘이 뜨는가?"

## feature-wide 계약 (load-bearing)

1. **어그로는 가디언 전용.** 어그로 capacity 필드는 `DefenderUnitData` 에 **공유로** 두되 가디언만 `>0`, Fighter·Ranger=0. (`aggroCapacity`)
2. **어그로 이동 ≠ 토네이도 풀.** 토네이도는 강제 변위(pullSpeed). 어그로는 적이 **자기 moveSpeed 로 가디언을 향해 보행**(목적지만 출구→가디언으로 교체). MovementSystem 에서 TornadoField pull 을 재사용하지 않는다.
3. **겹쳐 정지(stack).** 끌린 적은 가디언 타일에 겹쳐 멈춘다. 흩어지지 않는다. (별도 줄세움/충돌 없음 — 위치 overlap 허용)
4. **sticky / 전체 override.** 어그로된 적은 기존 타게팅을 전부 버리고 타겟=링크된 가디언으로 고정. 해제 전까지 불변.
5. **선점 고정.** 한 적은 먼저 어그로 건 가디언이 해제까지 보유. 이미 어그로된 적은 다른 가디언이 가져가지 않는다.
6. **해제 = 가디언 사망/소멸.** 링크 가디언이 없어지면 그 가디언의 어그로 적 전체 해제 → 기본 거동(출구행) 복귀.
7. **어그로 적은 가디언을 공격한다.** 가디언이 적 사거리에 들면 공격. 공격 능력 없는 Runner/Swift 도 어그로 시 **도발 공격 프로필**로 가디언을 때린다. (A안)
8. **맥락 소속**: 어그로 상태(`AggroProvider`, `Aggroed`, `TauntAttackGranted`)는 **Effects 맥락 소유**. `AggroAssignmentSystem`(Effects)이 획득·해제·count 를 전담하고 **`Aggroed` 만 쓴다**. 도발공격 grant/strip(Combat 소유 `AttackState`/`AttackOutputElement` 구조변경)은 **`TauntAttackGrantSystem`(Combat)** 이 `Aggroed` 를 읽고 수행한다(맥락 경계 준수, Unit 8). Movement·Combat 은 `Aggroed` 를 **읽기 전용**으로만 참조. (Unit 8) **해제→재획득은 1틱 지연**(ECB playback 순서) — 가디언 교체 시 1프레임 flow blip 은 허용 동작.
9. **수치는 전부 SO authored placeholder.** aggroCapacity 등 구체값은 밸런싱 spec 위임. 하드코딩 금지.
10. **공격필터(targetFilter)는 어그로의 override 대상.** 적의 기본(비어그로) 타게팅 = 아군 클래스 비트마스크 필터 + 클래스 우선순위. 어그로가 걸리면 이 필터/우선순위를 **전부 무시**하고 타겟=가디언(계약 4)이 된다. 본 spec 범위: **Shooter 적만 Ranger 우선**, 나머지 적 전부 `all`(최근접). 디펜더 클래스는 `DefenderClassTag` 로 ECS 에 노출한다.

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 데이터 필드 + 컴포넌트 | `0_components-and-fields.md` | aggroCapacity, 도발공격 필드, AggroProvider/Aggroed |
| 1 | BattleBridge 베이킹 | `1_bridge-baking.md` | 가디언에 AggroProvider, 적에 도발공격 프로필 부착 |
| 2 | 획득·해제 시스템 | `2_acquisition-and-release.md` | AggroAssignmentSystem (Effects): 근접 획득 + capacity + 선점 + 해제 |
| 3 | 어그로 이동 | `3_aggro-movement.md` | MovementSystem: 어그로 적은 가디언으로 보행 후 겹쳐 정지 |
| 4 | 적 공격필터 + 우선순위 | `4_enemy-target-filter.md` | 디펜더 클래스 ECS 노출 + 적 targetFilter(Shooter→Ranger, 그외 all) |
| 5 | sticky 타게팅 + 도발 공격 | `5_sticky-and-taunt-attack.md` | AttackSystem: 어그로 시 필터 override·가디언 고정 + Runner/Swift 공격 활성 |
| 6 | 테스트 + handoff | `6_tests_and_handoff.md` | EditMode 획득/상한/선점/해제/필터, PlayMode smoke |
| 8 | Codex 리뷰 반영 | `8_review-fixes.md` | orphan 해제(HIGH1) + 도발 grant Combat 이관(HIGH2) + sticky AoE 차단 + 필터 테스트 |
| — | **↓ 재설계: 근접→히트 구동 (2026-07-09)** | | |
| 9 | 정의 계층 순수함수 | `9_hit-driven-policy.md` | `AggroPolicy.CanAcquire`/`AggroTargeting.SelectTargets`/`ShouldRelease` + EditMode |
| 10 | 상태 재설계 + 베이크 | `10_capacity-state-baking.md` | `AggroCapacity` 신설·`AggroProvider`/`aggroRange` 폐기·Guardian tgt≥2 |
| 11 | Combat arm | `11_combat-hit-arm.md` | AttackSystem aggro-aware 타겟팅 + 히트 emit + `AggroHitEvents` 채널 |
| 12 | Effects arm | `12_effects-state-system.md` | `AggroStateSystem`(해제/held재계산/드레인 프로토콜) |
| 13 | Mono 소비: 아이콘 | `13_aggro-icon-presentation.md` | `AggroIconSpawner`/View + BattleBridge reconcile + SO |
| 14 | 테스트 + handoff | `14_hit-driven-tests-handoff.md` | H1 시스템 테스트(완료) + PlayMode smoke(잔여) + handoff + 이식 가이드 |
| 15 | handoff 요약 | `15_hit_driven_handoff.md` | 커밋/구현/검증/잔여 인계 지도 |

## 비목표 / 후속 후보

- **도발(에픽 가디언)** — aggroCount 무한 해제 + 범위 일괄 어그로 + 최근 우선 중첩. 별도 spec.
- ~~**"시간당 어그로 수량" 파워 스탯 정식화**~~ — Unit 9~14 히트 구동 전환으로 **채택**(공격 명중이 어그로 발생). rate 세분화(공격당 대상수는 `attackTargetCount`, 속도는 쿨다운으로 이미 표현)는 밸런싱 위임.
- **투사체(원거리) 가디언** — 현재 가디언 전원 근접(Guardian/Bastion). 투사체 가디언 신설 시 히트 emit arm 을 `ProjectileHit/ImpactSystem` 에 추가. v1 근접 전용.
- **적 클래스 거동(behavior) 정식화 — 클래스는 상위 축, 행동은 클래스 안에서 세분화(sub-variant).** 클래스 enum 에 행동을 박지 않고 별도 데이터(공격 패턴/sub-type)로 확장한다.
  - 탱커/러너: **walk-only(공격 안 함)**.
  - 브루저: "이동 → 사거리 진입 시 대기·공격모션 → 공격 → 이동"(stop-and-attack, 현재 `movePauseOnAttackSec` 로 부분 표현) + **focus-fire(타겟 죽을 때까지 sticky)**.
  - 슈터: "이동하면서 투사체 계속 발사"(move-and-shoot) + focus-fire.
  - 본 spec 은 기존 per-tick 최근접 공격 + movePause 거동을 유지하고 **필터/우선순위만** 도입. 위 거동 차등은 후속 단위.
- **적 브루저 집중·AoE 2+** — enemy-class 거동. 별도 spec.
- **공격필터 일반화** — Shooter 외 적에 클래스 제한/우선순위 부여(현재 전부 all). 밸런싱/enemy-class spec.
- **Fighter 인접 시너지 ↔ 적 브루저 AoE 겹침** 자리싸움 — 시너지/밸런싱 spec.
- **점수·드림캐쳐 후크** — 각 spec.
