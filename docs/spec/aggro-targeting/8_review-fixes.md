# Unit 8 — Codex 리뷰 반영

## 목적

Codex ECS 리뷰(2026-06-18) 지적 반영. 기능 버그(마지막 가디언 사망 시 orphan)와 맥락 경계 위반(Effects가 Combat 컴포넌트 쓰기)을 고친다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/AggroAssignmentSystem.cs`
- (신규) `Assets/_Project/Scripts/Battle/Combat/TauntAttackGrantSystem.cs`
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Tests/EditMode/AggroAssignmentTests.cs`, (신규) `EnemyTargetPriorityTests.cs`

## 구현

### HIGH 1 — orphan 해제 (마지막 가디언 사망)
`AggroAssignmentSystem.OnCreate` 의 `RequireForUpdate<AggroProvider>()` → **`RequireAnyForUpdate(AggroProvider, Aggroed)`**. provider 가 0이어도 `Aggroed` 가 남아 있으면 시스템이 돌아 해제 패스가 orphan 을 푼다.

### HIGH 2 — 맥락 경계: 도발공격 grant/strip 을 Combat 으로 이관
- 신규 `TauntAttackGrantSystem`(Combat), `[UpdateAfter(AggroAssignmentSystem)]` + `[UpdateBefore(AttackSystem)]`, `RequireAnyForUpdate(Aggroed, TauntAttackGranted)`.
  - grant: `Aggroed` + `AggroAttackProfile`, `WithNone<AttackState, TauntAttackGranted>` → AttackState + AttackOutputElement + TauntAttackGranted.
  - strip: `TauntAttackGranted`, `WithNone<Aggroed>` → 위 3개 제거.
- `AggroAssignmentSystem` 에서 도발 grant/strip 로직·관련 lookup·`using Wassup.Battle.Combat` 제거. Effects 는 `Aggroed`/`AggroProvider` 만 쓴다.

### MEDIUM 2 — sticky AoE 보조타겟 차단 → **철회됨 (2026-08-14)**
~~`AttackSystem` outputs melee 경로의 `desiredCount` 를 어그로 적이면 1로 강제.~~

> **`elite-whirlpot` unit 0 이 이 항목을 되돌렸다.** 근거 셋:
> ① 계약 4 가 말한 것은 «**타겟**»(단수) = primary 이고, 광역 **폭**까지 줄인 것은 확장 해석이었다.
> ② 이 항목은 **완료 기준에 테스트가 없었다** — 어그로 테스트 4종에 `attackTargetCount` 가
> 등장하지 않아 2년 가까이 어떤 그물에도 걸리지 않았다.
> ③ 실제 효과가 도발과 무관했다: **어그로가 적의 공격 «형태» 를 바꿨다.** 광역 적이 붙잡히면
> 단일 적이 되어 **안 붙잡았을 때보다 덜** 때렸다 — 도발의 대가가 아니라 숨은 방어 버프다.
>
> 지금은 폭이 어그로와 무관하고, primary override(unit 5)만 남는다. 이번엔 두 축을 각각
> `AggroAoeWidthTests` 가 고정한다(폭은 안 접힌다 / 가디언 사거리 밖이면 미발사).
> 영향받은 기존 콘텐츠: `Enemy_Basic`·`Enemy_Tanker`·`Enemy_WaypointBasic`·
> `Enemy_WaypointBasicAlt`(count 2) · `Enemy_Boss_Jjangssen`(count 3).

### MEDIUM 1 — 해제→재획득 1프레임 지연: **의도된 동작**으로 문서화(아래).

### LOW — 필터 우선순위 EditMode 테스트 추가
`EnemyTargetPriorityTests`: Shooter(prio=Ranger)가 더 가까운 Guardian 대신 Ranger 에 IncomingDamage. 비우선 대조군은 최근접.

## 계약 보강 (README 반영)

- **해제→재획득은 1틱 지연된다**(ECB playback 순서). 가디언 교체 시 1프레임 flow blip 은 허용 동작.
- 도발공격 grant/strip 은 **Combat(`TauntAttackGrantSystem`)** 소유. Effects 는 `Aggroed` 만 쓴다.

## 완료 기준

- [x] 컴파일 + Burst 호환. (OnCreate는 RequireAnyForUpdate의 managed array 때문에 비-Burst; OnUpdate는 Burst)
- [x] EditMode: 기존 6 + orphan 해제 + 필터 우선순위 2 통과, 전체 337 중 335 pass/0 fail.
- [x] orphan 해제: `LastGuardianDestroyed_ReleasesOrphanedAggro` 통과(마지막 provider 파괴 후 해제).
- [x] 맥락 경계: Effects(AggroAssignmentSystem)는 Combat 컴포넌트 미기록(grep — 주석만). grant/strip은 TauntAttackGrantSystem(Combat) 전담.

완료: 2026-06-18 / 커밋 해시 `13269e6`
