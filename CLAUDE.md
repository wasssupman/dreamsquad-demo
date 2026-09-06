# Project Context — Defense Tournament

> 이 문서는 Claude Code 세션 시작 시 자동으로 컨텍스트에 주입된다. 프로젝트의 정체성, 작업 방식, 필수 제약만 담는다. 상세는 참조 문서를 읽는다.

---

## 프로젝트 한 줄

비동기 토너먼트 디펜스 게임을 만든다. 프로토타이핑 단계(Phase 0~10)를 끝내고 **프로젝트 구체화 단계** 에 진입했다. 이후 모든 구현은 `docs/spec/{feature-slug}/` 단위 스펙으로 관리한다.

## 운영 원칙

- **스펙 단위 개발**: 기능 추가/변경은 먼저 `docs/spec/{feature-slug}/` 에 분산 스펙을 작성한 뒤 작업 단위 파일(0~N) 순서로 구현한다.
- **스코프 엄수**: 현재 작업 중인 spec 의 범위를 넘는 기능은 만들지 않는다. 관련 후보는 같은 spec 폴더의 "후속 후보" 섹션이나 별도 spec 초안으로 분리한다. 단, 적용되는 대상에 뭔지에따라 확장 가능 여부를 물어보고 진행한다.
- **코드 품질 우선**: 만든 코드는 본 게임에서 계속 쓴다. "확장 가능"을 이유로 과도한 추상화를 쌓지 않지만, 버릴 코드라는 전제로 쓰지도 않는다.

## 작업 방식 전환 이력

- **Phase 0~10 (프로토타이핑)**: `Phase N → 검증 → 다음 Phase` 순서 워크플로우. 관련 문서 (`PHASE*.md`, `phase*-prep.md`, `phase*-decisions.md`, `residual-issues.md`) 는 모두 `docs/prototype/` 로 보존.
- **현재**: spec-driven. `docs/spec/{feature-slug}/` 에 feature 단위 분산 스펙을 작성하고 파일번호 순서로 구현/커밋한다. Phase 개념은 더 이상 쓰지 않는다.

프로토타이핑 이력이 필요하면 `docs/prototype/PHASE{0..10}.md` 참조.

## 기술 스택

- **엔진**: Unity 6.4 (`6000.4.3f1`) · URP 17.4
- **언어**: C#
- **아키텍처**: 하이브리드 ECS — 전투 시뮬레이션만 ECS, 나머지 MonoBehaviour
- **필수 패키지**: Entities 6.4.0, Entities Graphics 6.4.0, Burst, Collections, Mathematics, Jobs, TextMeshPro, Input System, spine-unity, spine-csharp
- **ECS 버전 기준**: 이 프로젝트의 타겟은 Entities 6.4.0 이다. Entities 1.x 기준 문서/패턴을 source of truth 로 삼지 않는다.
- **타겟**: Android 실기기(주) + Unity Editor 플레이 + iOS Ad Hoc 내부 QA 빌드(보조)

## ECS 맥락 분리

전투 시뮬레이션은 **맥락(Context)별로 분리**된다:

- **Units** — 유닛 정의, 배치 상태, Health, 생성/소멸, IncomingDamage 버퍼, 사망 이벤트 큐
- **Movement** — 경로 따라가기, 위치 갱신, Portal 텔레포트, Tornado field pull step
- **Combat** — 타겟팅, 공격 쿨다운, 데미지 적용, 사거리 판정, 투사체, Meteor 해결, defender attack event
- **Effects** — 상태이상(Slow/DamageBoost/CooldownReduction), 스킬 캐리어(TornadoField/PortalLink/AllyBuffField) — 인접 시너지는 은퇴했다(2026-09-03 기능 제거, `ModifierOrigin.Synergy`/stackId 1 슬롯은 append-only 계약으로 번호만 보존) — `MeteorPending` 은 은퇴했다(투사체 수렴, 코드 0건)

**맥락 간 통신 규칙**:
- Component는 소유 맥락이 있다. 다른 맥락은 **읽기만** 가능, 쓰기는 소유 맥락만.
- 맥락 간 이벤트는 Buffer 또는 NativeQueue 싱글턴을 통한다. 직접 Component 수정 금지.
- 현재 운영 중인 NativeQueue 채널 (30개): `BossLeapVisualEventsSingleton`, `UltimateLeapVisualEventsSingleton`, `GoalReachedEventsSingleton`, `GoalCollapsedEventsSingleton`, `DefenderDeathEventsSingleton`, `UnitAttackVisualEventsSingleton`, `ProjectileHitEventsSingleton`, `HealAppliedEventsSingleton`, `DamageNumberEventsSingleton`, `EnemyKilledEventsSingleton`, `EnemyCcEventsSingleton`, `StatModifierApplyEventsSingleton`, `StackModifierApplyEventsSingleton`, `HazardRuntimeEventsSingleton`, `HazardDestroyedEventsSingleton`, `HazardSpawnRequestsSingleton`, `AttackOutputLogEventsSingleton`, `AggroAcquireEventsSingleton`, `ThreatHitEventsSingleton`, `BlinkRequestEventsSingleton`, `CcClearRequestsSingleton`, `MeteorBarrageRequestsSingleton`, `ShieldGrantedEventsSingleton`, `ShieldBreakEventsSingleton`, `CastEventsSingleton`, `DcTriggerFiredEventsSingleton`, `KnockupVisualEventsSingleton`, `DotApplyEventsSingleton`, `SkillFiredEventsSingleton`, `DetectionEventsSingleton`. (`MeteorBurstEventsSingleton` 은 Meteor 의 투사체 수렴으로 은퇴 — projectile-trajectory-payload unit 8. `AggroAcquireEventsSingleton` 은 Combat→Effects 히트 구동 어그로 — aggro-targeting unit 11. `ThreatHitEventsSingleton` 은 Combat→Combat 보스 위협 귀속, `BlinkRequestEventsSingleton` 은 Combat→Movement 텔레포트 seam — nightmare-catcher unit 1·3. `CcClearRequestsSingleton` 은 Units→Effects wake-on-hit(Sleep 해제) — combat-action-lock unit 3. `MeteorBarrageRequestsSingleton` 은 Effects→Bridge 사직서 임계 메테오 barrage cast — season-gimmick-clockout unit 3·4. `ShieldGrantedEventsSingleton` 은 Effects→Bridge 실드 부여 원샷 VFX — shield-guardian-defender unit 4. `ClockOutRefundEventsSingleton` 은 season-gimmick-clockout unit 8 재설계(강제 퇴근 제거 → 사망 시 사직서 드랍)로 은퇴 — 퇴근 코스트 환급 폐기. `ShieldBreakEventsSingleton` 은 Units→Bridge 실드 피격 파열(Sum>0→0 감지) → OnShieldBreak 페이로드(자기중심 폭발/주변 수면) 실행 — dreamcatcher-shield-break unit 0. `CastEventsSingleton` 은 Effects→Combat 해저드 캐스트 성사 = 그 host 의 공격 사건(캐스터는 attackRange 0 이라 RESOLVE 에 못 감) — attack-decoupling unit 4. `HazardCastSystem` 은 `[UpdateBefore(AttackSystem)]` 로 같은 프레임 소비를 보장한다. `KnockupVisualEventsSingleton` 은 Combat→Bridge 넉업 띄우기 연출 — 심에서 넉업의 실체는 짧은 Stun 이라 뷰가 `CcEffect.kind` 로는 일반 스턴과 구분할 수 없다. 그래서 **띄운 쪽이 대상을 직접 신호**한다 — knockup-fighter-defender unit 3. `BossLeapVisualEventsSingleton` 은 Combat→Bridge 보스 도약 비행 신호 — sim 은 `BlinkRequestEventsSingleton` 으로 즉시 텔레포트하고 **뷰만** 아치로 날린다(출발/착지 퍼프 타이밍도 이 채널이 소유해서 착지 VFX 가 뷰 도착보다 먼저 터지지 않는다) — boss-jjangssen unit 6. `DotApplyEventsSingleton` 은 지속 피해 부여 seam — **지속 피해는 crowd control 이 아니라서** `CcEffect` 에서 떼어내 `DotEffect` 자기 버퍼를 갖는다. 한 버퍼를 쓰던 시절엔 성격이 다른 세 producer(스택 임계 파생·해저드 장판·배치 스킬)가 피해자당 슬롯 하나를 공유하며 scalar 를 덮어써, 출혈 중인 적이 화염 장판을 밟으면 장판을 나가도 장판 요율로 계속 타는 과피해가 났다. 병합 키 = **`(DotOrigin, DotElement)` 2축**. `DotOrigin`(Stack·Zone·OnPlace) = 슬롯을 가르는 기준 = 어느 파이프라인이 만들었나, `DotElement`(Bleed·Fire·Ice·Poison) = 화면에 보이는 그림. **둘을 한 필드로 겸직시키지 말 것** — 지금은 원소 하나가 파이프라인 하나에서만 나와(출혈=스택, 화염=장판) 우연히 1:1 이지만, 화염을 스택으로도 만드는 순간 장판 화염과 중첩 폭발 화염이 한 슬롯에서 서로를 덮어 같은 과피해가 재현된다. source(Entity)를 축으로 쓰는 것도 안 된다 — 존은 해저드 엔티티를 만들 수 없고, 난도질꾼 2기는 source 가 둘인데 둘 다 출혈이라 식별에 기여가 없다. 오라는 `element` 만 읽어 여러 origin 이 한 그림으로 접힌다. `CcKind.DoT` 는 해저드 저작 토큰으로만 잔존한다(`CcKind.Slow` 와 같은 형태) — dot-effect-extraction unit 0. `UltimateLeapVisualEventsSingleton` 은 Combat→Bridge 궁극기 도약(이탈→예고→강습) 연출 신호. `BossLeapVisualEventsSingleton`(아치 하나)을 재사용할 수 없는 이유: **이탈과 강하가 예고 시간만큼 떨어진 별개 사건**이라 한 이벤트에 실으려면 발동 시점에 도착 시각을 알아야 하는데, 그 시점은 sim 시퀀스(`UltimateLeapSystem`, Battle 도메인 시계)가 결정한다. 그래서 `kind`(Ascend/Descend) 2종으로 나눠 보내고 브리지는 예고 시간을 **복제하지 않는다**(복제하면 두 시계가 갈린다). 이 채널의 뷰는 게임 규칙을 하나도 소유하지 않는다 — 피해도 텔레포트도 sim 이 이미 끝냈고 브리지는 슬램 VFX 타이밍(뷰 도착)만 가진다(일반 도약이 착지 슬램을 브리지에서 쏘는 것과 다른 점) — ultimate-leap unit 3. `GoalCollapsedEventsSingleton` 은 Units→Bridge 골 붕괴(안정도 0) 알림 — **연출/로그 전용**이며 유출 전환은 골 엔티티 부재(공성 게이트가 매 프레임 `GoalPoint` 쿼리로 판정)가 담당해 브리지는 상태를 갱신하지 않는다 — goal-stability unit 4. `SkillFiredEventsSingleton` 은 **감지자→스킬 레이어**의 단일 채널 — skill-layer-migration. 다른 채널과 성격이 다르다: 나머지가 「무슨 일이 일어났다」를 나른다면 이것은 **「어떤 스킬이 발동했다」**를 나르고, 실행은 `ISkill` concrete 가 한다(도메인은 ECS 를 모른다). 이벤트가 **자기 seam 을 말한다**(`SkillFiredEvent.Seam`, 7종) — 감지자마다 same-frame 하류 계약이 다르고 그 구간이 서로 겹치지 않아 단일 드레인이 산술적으로 불가능하기 때문이다. 남의 seam 것은 큐 꼬리로 되돌리고, `budget = queue.Count` 스냅샷이 종료를 보장한다. 이벤트는 **값 스냅샷**이다(자리·피해·반경·층·시전자 진영) — 자기 죽음 seam 은 정의상 파괴 뒤에 돌아 드레인 시점에 시전자가 없다. `DetectionEventsSingleton` 은 Combat→Bridge **발견 사건** — 적이 감지 반경 안에서 방어유닛을 처음 찾은 순간(`hunting` 0→1 전이)에만 1건. 매 프레임 쏘면 초당 60건이라 표식이 화면을 덮고 트레이스가 무의미해진다. 관성(grace) 중에는 `hunting` 이 1로 유지돼 전이가 안 일어나므로 「죽이고 다음 놈을 무는」 연속 사냥은 표식을 한 번만 낸다. ⚠ 페이로드의 `targetSimId` 는 **트레이스 전용**이다 — 화면이 그 대상을 가리키면 안 된다. 감지는 직선 최근접 legal 을 고르는데 이동은 공용 사냥판(`DefenderFieldSingleton`)이라 **실측 5.0%** 에서 둘이 갈리고, 그 구간에서 화면이 규칙을 틀리게 가르치게 된다 — enemy-detection-range unit 5.)

폴더 구조: `Assets/_Project/Scripts/Battle/{Units,Movement,Combat,Effects}/` + 스킬 디스패처 `Battle/Skills/`(도메인은 `Scripts/Skills/` 별도 asmdef). 세 축(유닛·드림캐쳐·맵)이 맞물리는 구조 지도는 `docs/reference/battle-core-architecture.md`.

## 절대 제약 (위반 시 정지하고 질문)

1. **ECS 경계 엄수**: `BattleBridge` 클래스가 MonoBehaviour ↔ ECS 통신의 유일한 창구다. 그 외 MonoBehaviour에서 `EntityManager` / `World.DefaultGameObjectInjectionWorld` / `SystemAPI` 직접 호출 금지.
2. **맥락 경계 엄수**: Component 쓰기는 소유 맥락만. 맥락 간 직접 호출 금지.
3. **SubScene 금지**, **SystemBase 남발 금지**(ISystem 우선), **네트워크 코드 완전 금지**.
4. **Authoring/Runtime 분리**: ScriptableObject/프리팹/Spine/Particle/UI 는 MonoBehaviour 계층에 두고, ECS 런타임 상태는 unmanaged Component/Buffer 중심으로 유지한다.
5. **Manager 싱글톤 제한 완화** (2026-07-07 사용자 결정): 기존 "GameManager 1개만" 하드 캡 해제. 명확한 단일 역할의 매니저(예: `SoundManager`)는 허용한다. 단 무분별한 `XxxManager` 남발은 지양 — 기능이 실제로 전역 매니저를 요구할 때만 신설하고, 애매하면 질문한다.
6. **하드코딩된 수치 금지**. 모든 유닛 스탯/공격 패턴/스킬 값/VFX 파라미터는 ScriptableObject 또는 프리팹에서 나온다.
7. **상속 2단계 최대** (MonoBehaviour, ScriptableObject에 적용).
8. **인터페이스는 구현체 2개 이상일 때만 생성**. "나중을 위한" 추상 레이어 금지.
9. **현재 작업 중인 spec 범위를 넘어서는 기능 구현 금지**. 범위 밖 항목은 별도 spec 초안 또는 해당 spec 폴더의 "후속 후보" 섹션으로 이관 후 대기.
10. **아키텍처 중립 로직은 순수 함수로 분리** (2026-07-10 사용자 결정): 계산 로직이 Mono/ECS 아키텍처와 **본질적으로** 얽히지 않으면(스탯 모디파이어 결합, 속도·타이밍→배율 변환, 클램프·정규화 등), ISystem/MonoBehaviour 같은 아키텍처 종속 메서드 안에 인라인하지 말고 **plain 값 입력 → plain 값 출력** 순수 static 함수로 둔다. 스탯 모디파이어와 그 적용 산식은 순수하게 값을 **결정**하고, 결정된 값은 각 아키텍처(ECS 시뮬 / Mono 프레젠테이션)가 **알아서 해석·소비**한다(값 자체는 아키텍처를 모른다). 순수 함수는 EditMode 단위 테스트 대상. **모범**: `ModifierMath.CombineMul`(순수 결합) → ECS 가 적용 / 뷰가 해석, `ModifierMathTests` 로 검증. 판정 기준 = "이 계산이 `EntityManager`/`SkeletonAnimation`/`Time` 같은 아키텍처 타입을 실제로 필요로 하는가?" 아니면 순수 함수로 뺀다.
   - **이 원칙의 핵심은 "모디파이어가 값을 순수하게 결정하고 결정된 값이 아키텍처-blind 하게 흐른다"는 *shape* 이지, "모든 수식을 함수로 빼라"가 아니다.** 자명한 한두 줄 산술을 호출처 하나뿐인데 별도 static/타입으로 빼는 건 제약 8("나중을 위한 추상 레이어 금지")과 충돌하는 **과잉 추상화**다. 추출은 로직이 **(a) 비자명(분기·다단계)** 이거나 **(b) 실제 재사용(2+ 호출처)** 이거나 **(c) 회귀 테스트 가치가 있는 sim-critical 계산**(데미지/이동/타겟팅)일 때만. 셋 다 아니고 값이 이미 plain 하게 흐르면 인라인이 맞다.
11. **Production-transition firewall** (2026-08-11 사용자 결정): Demo가 유일한 upstream이다. `docs/production-transition/`은 Project owner가 미래 전환을 위해 미리 보관하는 **dormant downstream 자료**이며 Demo의 설계·구현·검증 정본이 아니다.
   - 현재 사용자 요청이 production-transition의 시작·갱신·검증을 **명시적으로** 지시하지 않으면 해당 subtree와 전용 verifier를 읽거나 실행하거나 작업 후보로 제안하지 않는다. 최근 커밋, stale 표시, watch path 변화, backlog 링크는 활성화 근거가 아니다.
   - Demo의 정본 우선순위는 `CLAUDE.md` → 활성 `docs/spec/{feature-slug}/` → `docs/reference/`(게임 규칙 `ingame-flow.md` · 구조 `battle-core-architecture.md`) → 코드·에셋·테스트다. Transition 문서와 충돌하면 Demo를 고치는 대신 transition 자료가 stale한 것으로 둔다.
   - Transition maintenance/change register/coverage/decision/freeze audit는 Demo 작업의 시작·완료·검증·커밋을 절대 차단하지 않는다. Demo 변경에 맞춘 transition 문서 갱신도 같은 작업에 끼워 넣지 않으며, 명시적인 별도 후행 task와 별도 commit에서만 수행한다.
   - Freeze, cutover, production import와 후속 wave의 시점·범위는 Project owner만 결정한다. 명시적 활성화 전 agent는 이를 계획하거나 선제 작업하지 않는다.
   - Transition과 무관한 Demo 아키텍처 변경은 Demo 목표만으로 별도 승인받고 이 파일과 `docs/reference/battle-core-architecture.md` 를 먼저 갱신해야 한다. Transition 문서를 근거로 ECS 경계나 네트워크 금지를 우회할 수 없다.
12. **BattleBridge 진입은 최후 수단** (2026-08-28 사용자 결정): 새 값·상태가 필요할 때 `BattleBridge` 에 serialized 필드나 static 미러를 **반사적으로 추가하지 않는다.** 제약 1 은 "ECS 통신의 유일한 창구"라는 뜻이지 "모든 전역값의 집" 이 아니다 — 편의로 얹은 진입점 하나가 다음 사람에게는 «그 값의 소유자는 브리지» 라는 잘못된 신호가 되고, 브리지는 이미 그렇게 비대해졌다.
   - **판단 순서**: (a) 그 값을 **이미 소유한 곳**이 노출하고 있지 않은지 먼저 확인한다 — 보드 평면은 `BoardSpace.RaycastPlane()`, 유닛별 값은 `ISpineUnitVisualData`/SO, 저작 값은 프리팹. (b) 없으면 **소유자 쪽에** 얇은 접근자를 추가한다. (c) 그래도 브리지가 유일한 자리라는 근거를 댈 수 있을 때만 추가한다.
   - 브리지 진입이 정말 필요하다고 판단되면 **그때 드러난 리팩토링 범위를 먼저 측정한다** — 관련 필드·미러·소비처가 몇 개인지 세고, 그 정리가 **현재 spec 의 검증 질문에 답하는 데 필요한지** 판정한다. 필요하면 같은 spec 안에 작업 단위로 넣고, 아니면 제약 9 대로 "후속 후보" 로 이관한다. 어느 쪽이든 **범위와 포함/이관 사유를 사용자에게 보고**한 뒤 진행한다.
   - 이미 브리지에 있는 필드의 **의미를 교체**하는 것(예: 절대 Y → 평면 상대 리프트)은 진입점 신설이 아니다 — 이 제약의 대상이 아니다.
13. **전투 판정 산식은 하나다 — 예외 없음** (2026-09-06 사용자 결정, 위반이 두 번 나와 제약으로 승격):

    ```
    도달 = |좌표 차| ≤ 범위 + «원점의 몸» + «대상의 몸»
    ```

    - **「원점의 몸」은 원점이 무엇이냐가 정한다** — 원점이 **유닛**이면 그 유닛의 `HitRadius`
      (= footprint 가로/2), 원점이 **칸**이면 칸 반폭(`SkillMath.CellHalfWidthTiles` = 0.5).
      예외 둘이 아니라 **한 규칙의 두 경우**다. 자기중심 광역의 폭심은 «점»이 아니라 **몸 있는 유닛**이다.
    - **적용 대상 = 전투에서 「닿나/들어갔나」를 묻는 전부**: 일반 공격(발사·피해 선정) · 배치 스킬 ·
      액티브 스킬 · 드림캐쳐의 각종 효과 · 오라 · 장판 · 투사체 착탄 · 어그로/도발 · 감지 ·
      **그리고 앞으로 추가되는 모든 전투 기능과 효과.** 판정을 새로 만들 때 **자를 새로 만들지 않는다.**
    - 예외는 **판 위 배치(placement) 판정 하나뿐**이다 — 그건 격자 점유라 성격이 다르다.
    - **금지**: 도달 판정을 인라인으로 쓰기 · 「내 몸」 자리에 상수를 손으로 넘기기 ·
      칸-칸 비교로 대상을 점으로 접기. 새 판정은 정본 진입점을 **호출만** 한다.
    - **왜 제약인가**: 같은 결함이 두 번 났다. `distance-based-range` unit 22(배스티온이 「공격은
      하는데 피해 0」)와 unit 23(배스티온 도발 범위가 1칸 좁음 · 실드 파열 폭발이 양쪽 몸을 다 무시).
      **원인이 같다** — 몸이 상수 가정에서 데이터로 승격됐는데, 그 값을 *쓰던* 곳이 아니라
      **가정하던** 곳이 안 고쳐졌다. 흔적이 리터럴도 심볼도 아니라 **함수 뒤에 숨어** grep 이 못 잡는다.
      그래서 「고쳤다」가 아니라 **「호출부가 원점이 무엇인지 선언하게 만든다」**가 이 제약의 이행 방식이다.

**추가 제약** (구 `docs/TRD.md` §3 추상화 규칙 · §5 금지 패턴에서 2026-09-03 이관. TRD/PRD 는 Phase 시절 문서라 은퇴했고 이 목록이 제약의 전부다):

- **생성 패턴**: 팩토리/빌더는 객체 생성이 3줄 이상일 때만. ECS 엔티티 조립은 `BattleBridge` 의 직접 `EntityManager` 호출 + 작은 변환 헬퍼로 충분하다 — 별도 팩토리 레이어 금지, Baker/SubScene 은 별도 spec 없이 도입하지 않는다.
- **`UnityEvent` 금지**(디버깅 불가). **제네릭 타입 파라미터는 1개까지** — 2개 이상이면 설계를 의심하고 구체 타입으로 분할.
- **`[SerializeField] private`** — public 필드 남발 금지(ECS Component struct 의 public 필드는 정상).
- **MonoBehaviour 에 전투 로직 직접 작성 금지**(전투는 ECS 시스템에서만). UI 가 ECS Component 를 직접 읽거나 쓰지 않는다. "나중을 위한" 확장 포인트 · enum+switch 떡칠 금지.
- **패키지/API**: 런타임 코드에 에디터 전용 API 금지 · Burst 컴파일이 실패하는 API 를 ECS 시스템에 금지 · DOTween/Zenject 등 범용 라이브러리는 근거 없으면 금지.
- **`Shader.Find(...) + new Material(shader)` 금지** — 모바일 shader stripping 으로 null 이 돌아와 렌더가 깨진다. 런타임 Material 은 `Wassup.Rendering.RuntimeMaterialFactory.CreateOpaque / CreateTransparent` 경유(`Assets/Resources/RuntimeMaterials/*.mat` always-included). 새 런타임 shader 는 `Assets/_Project/Shaders/` 명시 추가 + Resources 머티리얼 등록.
- **Manager 싱글톤의 의도된 예외 2건**: `Wassup.Core.TimeControl.TimeManager`(도메인 스코프 시간 제어, `docs/spec/time-manager/`) · `SoundManager`(전역 SFX, `docs/spec/score-hud-impact-upgrade/` unit 4). 그 외는 제약 5.
- **로깅은 마지막이 아니라 첫 축** — 브리지 드레인은 첫 줄에서 트레이스를 기록한다(`LegacyTraceRecorder.Ev`). 새 sim 사건 채널을 열면 로그/트레이스 정거장을 함께 연다.

## 원격 저장소 · 푸시 전략 (2026-07-27 확정)

- **GitHub 이 정본이다.** 모든 작업은 GitHub `main` 에 커밋·푸시한다 (`origin` = `github.com/wasssupman/dreamsquad-demo`).
- **`GitHub main` ≡ `GitLab master`.** GitLab(`gitlab.playlinks.co/cash-royale/dreamsquad-demo`)은 미러이고 기본 브랜치는 **`master`** 다. GitLab `main` 브랜치는 **은퇴** — 쓰지 않는다(중복 계보로 분기를 만들었던 이력).
- **동기화 2줄**: `git pull` → `git push gitlab main:refs/heads/master`. GitLab 이 앞서 있으면(동료가 GitLab 에 직접 푸시) 앞에 `git fetch gitlab && git merge gitlab/master` 를 붙여 **편입한 뒤 양쪽에 푸시**한다 — 한쪽으로 수렴시켜야 이후 fast-forward 가 유지된다.
- **GitLab remote 는 SSH 만 쓴다**: `git@gitlab.playlinks.co:cash-royale/dreamsquad-demo.git`. HTTPS 는 AWS ALB 경유라 대용량 전송이 끊긴다(클론 실패 · 초기 푸시 HTTP 413). 진단·우회 상세는 `docs/reference/lessons/02-dev-workflow-git-scene.md`.
- **GitLab 에서 커밋하지 않는다.** 웹 편집·GitLab 발 MR 로 GitLab 전용 커밋이 생기면 다음 fast-forward 가 막히고, 기본 브랜치는 보호돼 force push 도 불가하다. GitLab 프로젝트 merge method 는 **Fast-forward** 유지.
- **푸시는 사용자 승인제.** 커밋은 자율 판단으로 진행해도 되지만 `git push` 는 **매번 사용자의 명시 승인 후**. 원격 force push·원격 브랜치 삭제는 특히 승인 필수이며, 무엇이 폐기되는지 먼저 확인해 보고한다.
- **여러 세션이 같은 워크트리를 공유한다.** pull/push 전 `git status -sb` 로 현재 위치를 확인하고, 스테이징은 **경로 명시**로만 한다(무관한 dirty 파일 혼입 방지). `.git/index.lock` 경합 시 락을 지우지 말고 기다린다.

## 참조 문서 (필요 시 읽는 순서)

| 상황 | 읽을 문서 |
|---|---|
| "이 게임의 규칙과 설계 지향은? 왜 이렇게 만드나?" | `docs/reference/ingame-flow.md` — 정식 플레이 흐름 · 전투 중 동사 4개 · **설계 지향 7축** · 드림캐쳐 사용 규칙 · 미결. (구 `docs/PRD.md` 는 프로토타입 가설 문서라 2026-09-03 은퇴 — 필요하면 git 이력) |
| "Project owner가 production-transition 작업을 이번 요청에서 명시적으로 지시했나?" | 그때만 [`docs/production-transition/README.md`](docs/production-transition/README.md) 참조. 평상시에는 읽지 않는다. 이 subtree는 **owner-gated dormant downstream**이며 현재 Demo 구현 명세가 아니다. |
| "어떤 기술 제약이 있나?" | **이 파일**의 「절대 제약」12 + 「추가 제약」이 전부다. (구 `docs/TRD.md` 는 Phase 시절 문서라 2026-09-03 은퇴 — 살아 있던 규칙은 위로 이관됐고, 나머지는 git 이력) |
| "유닛·드림캐쳐·맵이 코드에서 어떻게 맞물리나?" | `docs/reference/battle-core-architecture.md` — 세 축의 런타임 정체 · 한 판의 생애 · 프레임 순서 계약 · 교차점 매트릭스 · 채널 30 지도 · 불변식 17. **아키텍처 변경 전 대조 필수** |
| "feature 구현 상세는?" | `docs/spec/{feature-slug}/` — 분산 스펙 (README + 0~N 작업 단위). 하단 "문서화 구조" 참조 |
| "다음에 뭐 할까 / 후속 후보는?" | `docs/spec/README.md` 하단 **Follow-up Backlog** 섹션 — 종료된 spec 에서 이관된 후보. 새 spec 시작 전에 먼저 확인 |
| "과거 어떻게 만들어졌나?" | `docs/prototype/PHASE{0..10}.md` — 프로토타이핑 단계 종료 스펙 (읽기 전용 아카이브) |
| "이 프로젝트/환경 고유의 함정은?" | `docs/reference/lessons/` — 실제로 겪은 지뢰 모음 (Unity MCP 운용·git/씬 위생·Spine/타일맵/프랍·시뮬 설계). **Unity 조작·에셋 작업·커밋 전에 해당 주제 파일 一讀** |
| "테스트를 어떻게/언제 돌리나? 새 테스트는 어디에?" | `docs/reference/test-procedure.md` — 3개 어셈블리(코어 26초 / 에셋 5초 / PlayMode 8분)와 상황별 실행표 · lane 판별 한 줄 · **밸런스 수치를 리터럴로 못박지 않는 규율**. 시트·에셋 편집 후엔 Assets lane 추가 실행 필수 |
| "새 플레이 오브젝트의 생성→렌더 정거장은?" | `docs/reference/object-pipeline-map.md` — 아키타입별 파이프라인 체크표. **플레이 오브젝트 spec README 작성 시 대조 필수** |
| "점수는 어디서 나오고 얼마인가?" | `docs/reference/score-formula.md` — **1킬 = 1점 고정**(시간·스트레스 배점 축은 은퇴). 판이 끝나는 통로 2개 + 마음이 판정에 관여하되 점수엔 관여하지 않는 계약 · 값 바꾸는 곳 요약. 설계 이력은 `docs/spec/battle-score-formula/` · `docs/spec/heart-stress-axis/` |
| "맵/웨이브 난이도를 조정하려면?" | `docs/reference/map-wave-balancing.md` — 맵 로테이션·웨이브 knob·몬스터 스탯 조정 위치 + **결정론 규칙(waveSeed 비0=같은 맵 같은 웨이브)**. 자주 바꾸는 값 모음 |
| "적이 어떻게 길을 찾고 움직이나?" | `docs/reference/enemy-movement-algorithm.md` — 프레임 의사결정 순서도(시스템 순서 · MovementSystem 분기 · 평활화 · 충돌 · 분리) + 쓴 알고리즘 계보와 **쓰지 않은 것의 이유** + 값 바꾸는 곳. 설계 이력은 `docs/spec/continuous-agent-movement/` |
| "무기 궤적을 켜거나 바꾸려면?" | `docs/reference/weapon-trail-authoring.md` — 유닛에 붙이기·룩 추가·모양 튜닝·본 없는 호스트 레시피 + 증상→원인 표. **코드 0 이 원칙**. 설계 이력은 `docs/spec/spine-weapon-trail/` |
| "맵 스테이지 프리팹을 직접 만들려면?" | `docs/reference/map-stage-authoring.md` — 구성 스크립트·양자화 규칙·형식 제약(하드 실패 목록)·절차 예시·증상→원인 표. bake 없음 — 프리팹이 곧 정본. 설계 이력은 `docs/spec/map-diorama-stage/` |
| "적을 새로 만들거나 등장 조건을 바꾸려면?" | `.claude/skills/enemy-wave-integration/` 스킬 — 풀 삽입 위치·시드 재기준·컨셉 자동 귀속·튜토리얼 로스터 계약. **`AttackUnitData` 신설 또는 `minWaveNumber`/`maxPerWave`/`enemyClass`/`traversalLayers` 변경 시 필수** |
| "웨이브 생성 로직을 밸런스로 손보려면?" | 위와 **같은 스킬**. 그 문서의 규칙이 `WavePatternGenerator`·`AttackDeck`·`WaveConceptData`·`WavePlanAsset` 에 매여 있어, 그 코드를 바꾸면 **같은 커밋에서 스킬을 갱신**한다(스킬 안의 「갱신 트리거」 표). 밸런스 값 자체를 바꾸는 위치는 `docs/reference/map-wave-balancing.md` |
| "VFX 를 만드려면?" | `.claude/skills/unity-vfx-authoring/` + `unity-vfx-integration/` 스킬 |
| "Unity 씬 와이어링?" | `.claude/skills/unity-feature-wiring/` 스킬 |

## 문서화 구조 (spec 분산 형식)

**단일 대형 plan 문서 금지**. feature-level 구현 스펙은 `docs/spec/{feature-slug}/` 폴더로 분산한다.

### 폴더 레이아웃

```
docs/spec/{feature-slug}/
├── README.md                ← 개요 + 공통 원칙 + 파일 목록 표 + 후속 후보
├── 0_{topic}.md             ← 첫 작업 단위 (enum/contract 같은 토대)
├── 1_{topic}.md
├── ...
├── N_{topic}.md
└── {N+1}_handoff_summary.md ← 구현 종료/세션 인계 요약 (필요 시)
```

각 파일 **1~3KB 범위**, 작업 단위당 "목적 / 변경 대상 / 구현 / 완료 기준" 4섹션 구조.

문서의 source-of-truth 계층:

- **README.md**: 최신 상태와 feature-wide 계약의 source of truth. 세션 진입 시 먼저 읽는다.
- **`{N}_{topic}.md`**: 해당 작업 단위 계약과 완료 기준의 source of truth. 구현 상세를 사후 복제하지 않는다.
- **`{N+1}_handoff_summary.md`**: 커밋 이후 인계 지도. 최신 계약은 README/번호 문서가 우선한다.
- **코드 + 커밋 히스토리**: 구현 상세의 source of truth.

계약이 바뀌면 문서를 갱신한다. 단, diff 설명이나 구현 내부 흐름을 문서에 장황하게 복제하지 않는다.

**스펙 문서의 성격 = 현재 구현 + 미래 길찾기.** 넣는 것: 이 feature 를 구현·탐색하는 데 필요한 것 — 계약, 작업 단위, **재사용할 기존 코드·시스템 포인터**(이름·위치로 가리키는 길찾기). 넣지 않는 것: 타 세션/스펙과의 **조율 로그**(누가 뭘 편집 중·index.lock·커밋 해시 추적), 완료된 **타 스펙의 구현 내역** 서술, 이 feature 밖 콘텐츠와의 **emergent 상호작용**. 이런 transient 맥락은 금세 stale 해지고 "이 feature 를 어떻게 구현/탐색하나"라는 문서의 핵심 신호를 흐린다 — 세션 조율·충돌 판단은 대화에서 답하고 필요하면 메모리에 남긴다. (재사용 대상 시스템을 가리키는 것과 타 스펙의 진행/조율을 서술하는 것은 다르다 — 전자만 스펙에 둔다.)

### 구성 원칙

- **1 파일 = 1 커밋 단위 작업**. subagent-driven-development 의 implementer 가 해당 파일 하나만 읽고 작업 완료 가능해야 함
- **README.md**: 상태 라인 + 상위 목표 + 작업 단위 목록 표 (파일번호 / 작업 구분 / 문서 / 목적) + feature-wide 계약 4~10 bullet + "후속 후보" 섹션(현 spec 범위 밖 항목)
- **파일번호는 작업 순서**: 같은 feature 에 추가 작업이 생기면 기존 파일번호 뒤에 누적 (rev 표기 가능)
- **완료 기준**: 각 파일 하단에 "완료 기준" 섹션 필수. compile / 테스트 / 시각 검증 기준을 명시
- **변경 대상**: 파일 경로 명시 (예: `Assets/_Project/Scripts/Bridge/BattleBridge.cs`)
- **파이프라인 커버리지**: 플레이 오브젝트(유닛/적/투사체/해저드/VFX 등)를 신설하거나 생성→렌더 경로를 변경하는 spec 의 README 에는 `파이프라인 커버리지` 섹션 필수. `docs/reference/object-pipeline-map.md` 의 가장 가까운 아키타입 표를 복사해 대조하고, 해당 없는 정거장은 빈 칸이 아니라 **`N/A + 이유`** 로 적는다.
- **handoff summary**: feature 구현이 커밋되었거나 세션이 넘어갈 때 `{N+1}_handoff_summary.md` 를 작성한다. 길이는 30~80줄, "구현됨 / 핵심 파일 / 검증 / 주의점 / 다음 후보" 만 담는다. handoff 는 source of truth 가 아니라 다음 에이전트가 커밋과 spec 을 빠르게 찾기 위한 지도다.

### 참고 예시

- `docs/spec/map-system/` — 맵 시스템 재설계 (21 작업 단위, 프로토타이핑 종료 시점의 최종 spec)
- `docs/spec/defender-on-place-skills/` — 방어 유닛 배치 시 스킬 pipeline spec
- `docs/spec/defender-drag-drop-deployment/` — D&D 배치 전환 spec

### design.md 와의 관계

`docs/plans/YYYY-MM-DD-{topic}-design.md` 는 **얇은 브레인스토밍 결과물** (목표, 아키텍처 요약, `spec/` 폴더 포인터). 실제 구현 상세는 모두 `docs/spec/{feature-slug}/` 안에 둔다. writing-plans 스킬은 생략 가능 — spec 파일이 곧 각 task 의 plan 역할.

## 작업 지침

### 기본 워크플로우

1. 사용자가 새 feature 를 요청하면, 먼저 `docs/spec/{feature-slug}/README.md` 를 만들어 목표 + 작업 단위 목록을 잡는다. 기존 spec 의 추가/수정이면 기존 폴더에 새 파일번호로 이어 쓴다.
2. 사용자 승인 후, 작업 단위 파일 `0_{topic}.md` 부터 순서대로 구현한다. **한 번에 한 파일**. 선행 의존이 있으면 같이 언급.
3. 구현 완료 후 사용자에게 "완료 확인"을 요청한다. 에디터 또는 실기기에서 확인 가능한 방식을 구체적으로 알려준다.
4. 사용자가 통과를 확인하면 해당 작업 단위 파일의 "완료 기준" 섹션 하단에 확인 일자 + 커밋 해시를 한 줄 추가하고 커밋한다.
5. feature 전체 종료 시 `docs/spec/{feature-slug}/README.md` 상단에 "상태: 완료 YYYY-MM-DD" 를 기재하고 `{N+1}_handoff_summary.md` 를 작성한다. 이때 파이프라인 맵(`docs/reference/object-pipeline-map.md`)에 구조 변경(새 아키타입/정거장 추가·제거, 앵커 파일 이동)이 생겼는지 확인하고 필요 시 같은 커밋에서 맵을 갱신한다.
6. 커밋 이후 다른 에이전트가 이어받을 가능성이 있으면, 커밋 해시와 검증 결과를 handoff summary 에 먼저 반영한 뒤 필요하면 별도 docs 커밋으로 묶는다.

### Handoff 작성 규칙

handoff summary 는 세션 간 맥락 차이를 줄이기 위한 짧은 인계 문서다. 구현 상세를 반복하지 말고, 다음 작업자가 읽을 순서와 위험 지점을 압축해서 남긴다.

필수 섹션:

- **Commit**: 관련 커밋 해시와 제목
- **Implemented**: 완료된 동작 5~10개 bullet
- **Key Files**: 실제로 이어서 볼 파일 경로
- **Verified**: compile / test / Play smoke / console 상태
- **Notes**: 되돌리면 안 되는 의도, fallback, 경계 조건
- **Follow-up**: 아직 하지 않은 확인 또는 다음 후보

금지:

- diff 전체를 prose 로 재작성하지 않는다.
- 오래 유지될 보장 없는 추측을 사실처럼 쓰지 않는다.
- 실패 로그를 숨기지 않는다. 해결했으면 원인과 최종 상태를 적는다.
- unrelated dirty worktree 를 정리했다고 쓰지 않는다. 실제로 정리한 것만 적는다.

### 리뷰 반영 규칙

critic/review 지적은 문서 계층을 깨지 않게 반영한다.

- 코드 버그를 유발하는 계약 공백: 코드 + 테스트 + 관련 spec 계약 갱신
- 구현과 문서의 표현 불일치: 문서 갱신
- 단순 구현 설명 요구: handoff 에 짧게 쓰거나 생략
- 미래 확장/취향 제안: README 의 후속 후보 또는 handoff Follow-up 으로 이동

### 작업 시작 전 자가 점검

코드를 작성하기 전에 스스로 점검한다:

- [ ] 이 기능이 현재 spec 범위 안인가?
- [ ] 이 코드에 테스트를 작성하는 것이 자연스러운가?
- [ ] "확장 가능"을 이유로 만드는 구조가 지금 실제로 쓰이는가?
- [ ] Component 쓰기가 소유 맥락 내에서만 일어나는가?
- [ ] 상속 계층이 3단계를 넘지 않는가?

### ECS 설계의 불확실성 대응

1. **작은 결정은 에이전트가 내리고 짧게 설명한다** — 사용자가 실시간으로 ECS를 학습하는 효과
2. **아키텍처 수준의 결정은 사용자에게 질문한다** — 여러 정답이 있는 경우에만. 작업 단위마다 질문하지 않고 묶어서 한 번에.

**질문 가치가 있는 결정의 예**:
- Component 소속 맥락이 애매할 때
- 맥락 간 이벤트를 Buffer로 할지 NativeQueue로 할지
- SystemGroup 구성과 업데이트 순서
- Burst 호환 불가한 API가 필요해 보일 때

**질문하지 않아도 되는 결정의 예**:
- 폴더 내 파일 네이밍
- private 메서드 분할
- 로컬 변수 이름
- using 순서, 코드 포맷

### 기술적 결정이 필요할 때 (우선순위)

1. 현재 spec README 의 "공통 원칙" 또는 해당 작업 단위 파일에 명시돼 있으면 그대로 적용
2. 없으면 이 파일의 「절대 제약」·「추가 제약」, 구조 판단은 `docs/reference/battle-core-architecture.md` 참조
3. 없으면 `docs/reference/ingame-flow.md` 의 「설계 지향 7축」에 가장 도움 되는 선택. 그래도 모호하면 **단순한 쪽**
4. 없으면 **작업 시작 전에 사용자에게 한 번에 묶어서 질문**. 작업 중간에 질문하지 않는다.

### 버그 수정 절차 (2026-08-10 확정 — 실패 사례에서 나온 규칙)

버그 보고를 받으면 **`superpowers:systematic-debugging` 스킬을 태운다.** 자체 판단으로 건너뛰지 않는다.

핵심 3줄:

1. **재현이 먼저다.** 수정 전에 증상을 계측기 안에서 눈에 보이게 만든다. 재현이 안 되면 원인을 못 찾은 것이다 — 데이터를 더 모으고, 추측으로 넘어가지 않는다.
2. **재현 대상은 사용자의 문장이다.** "안 움직인다" → 단언은 `셀이 N프레임 안에 바뀐다`. *내가 고친 함수가 옳은 값을 내는가*가 아니다. 증상 단언을 **먼저 넣고 빨간 것을 확인**한 뒤 고친다.
3. **경계마다 계측한다.** 다중 컴포넌트(시스템 A → 컴포넌트 → 시스템 B → 뷰)면 각 경계의 입출력을 찍어 **어디서 끊기는지 증거를 확보한 뒤** 그 구간을 조사한다.

보고할 때 **두 주장을 섞지 않는다**:

- "내 변경이 의도대로 구현됐다" ← 테스트가 말해주는 것
- "당신의 증상이 사라졌다" ← 증상 재현으로만 말할 수 있는 것

**3회 고쳤는데 증상이 남으면 정지하고 접근법을 의심한다.** 4번째 수정을 시도하지 않는다.

> **실패 사례**(traversal-layers unit 5): "순찰병이 안 움직인다"를 세 번 고쳤고 매번 EditMode/PlayMode 전부 초록이었다. 얼어붙은 유닛도 스폰·컴포넌트·앵커·반경 단언을 전부 통과하기 때문이다. 원인(`MovementSystem` 의 충돌 `NavGrid` 가 통행 층을 몰랐다)은 프레임별로 `dir` 과 셀 좌표를 같이 찍은 **첫 시도**에 드러났다. 그 계측을 첫 수정 **앞**에 했어야 했다.

### 테스트

- ECS 시스템 내부의 순수 계산 함수는 **EditMode 단위 테스트**를 작성한다.
- 판 흐름 수준의 통합은 **PlayMode 테스트** 1개 이상.
- 커버리지는 목표가 아니다. **회귀 방지 수준**이면 충분하다.
- 테스트 작성이 작업 진행의 병목이 되면 우선순위를 낮춘다. 다만 ECS 시스템의 핵심 계산(데미지, 이동, 타겟팅)은 반드시 단위 테스트를 유지한다.

### 금지 행동

- **스펙 스코프를 임의로 넓히지 않는다.** "이왕 만드는 김에..." 같은 판단 금지. 범위 밖 항목은 spec 의 "후속 후보" 섹션이나 별도 spec 초안으로 이관.
- **추상화 먼저 만들지 않는다.** 인터페이스부터 정의한 뒤 구현하는 방식 금지. 구체 구현부터 시작해서 반복이 생기면 그때 추출한다.
- **사용자 확인 없이 다음 작업 단위로 넘어가지 않는다.**
- **경계를 유혹적으로 넓히지 않는다.** "이 한 줄만 예외로 하면..." 금지. 경계 위반이 필요해 보이면 정지하고 질문.
- **맥락 폴더를 임의로 만들지 않는다.** 현재 허용된 맥락은 Units / Movement / Combat / Effects 4개. 새 맥락이 필요해 보이면 질문. (Presentation 폴더는 ECS 맥락이 아닌 MonoBehaviour View 계층임을 명심.)
- **Unity 씬 wiring 을 "사용자 수작업" 으로 미루지 않는다.** UnityMCP로 자동화 가능한 것은 전부 자동화한 뒤 Play 검증까지가 완료.

## 기억할 것

- **코드 품질은 타협 대상이 아니다.** 프로토타이핑 단계는 끝났다. 만든 코드는 본 게임에서 계속 쓰인다.
- **각 spec 은 고유의 검증 질문이 있다.** 그 질문에 답하는 데 필요하지 않은 모든 것은 제외된다. 작업 단위 파일의 "완료 기준" 을 그 질문의 구체 표현으로 삼는다.
- **"가벼운 설계"와 "재사용 가능"은 양립 가능하다.** 방법은 맥락 분리 + 추상화 규칙 준수 + 현재 spec 범위 유지.

## Visual Direction

This project is a casual defense game.

When generating or requesting images for this game, do not default to RPG concept art,
tarot-card illustration, dark high-fantasy key art, realistic character splash art, or
heavy collectible-card painting styles.

Use casual defense game art direction instead:
- readable at small in-game sizes
- bright, approachable, playful, and clean
- simple silhouettes and clear gameplay affordances
- mobile-game friendly colors and contrast
- asset-focused game art, not lore-heavy RPG illustration

For Dreamcatcher content specifically, do not draw a physical dreamcatcher object unless
the user explicitly asks for one.
