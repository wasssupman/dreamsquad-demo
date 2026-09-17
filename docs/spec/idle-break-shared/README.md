# Idle Break Shared — 대기 컷 로직 공유 + 신규 Spine 2종

상태: **구현 완료 2026-09-17 · 사용자 Play 육안 확인 대기** (units 0~3 구현·EditMode 초록·MCP Play 계측 통과 — `4_handoff_summary.md`)

## 검증 질문

**「기본 대기 루프가 항상 돌고, N초마다 그 외 idle 모션 하나를 한 번 끼워 넣는다」는 대기 규칙이 Spine 유닛과 스프라이트 유닛에서 같은 코드로 돌고,
CH2(스나이퍼)·CH3(실드셔틀)가 그 규칙으로 판에 서는가.**

## 상위 목표

`sprite-unit-backend` unit 6 이 스프라이트 쪽에 만든 대기 컷 로직(`SpriteUnitView` 안의 `TickIdleCycle`/`EnterIdleRest`)을
**백엔드 중립 순수 구조체**로 빼고, Spine 뷰도 같은 것을 소비하게 한다 — *「로직은 공유하고 비주얼 처리는 각 카테고리에서」*
(2026-09-17 사용자). 동시에 신규 고유 리그 CH2·CH3 를 스나이퍼·실드셔틀에 저작해 그 규칙의 첫 Spine 소비자로 세운다.

## 사용자 결정 (2026-09-17)

- **틀 = 기본 루프 + 주기적 컷.** *「기본이 되는 대기모션(ch2-idle, ch3-idle2)을 하나 정해서 평시는 계속 플레이하고 N초마다 그 외 idle 모션을
  한 번씩 플레이」*. 「쉼(0프레임 정지)」 개념은 **양 백엔드에서 폐기** — 스프라이트도 기본 idle 루프가 돌고 N초마다 컷 한 바퀴.
  풀 = **그 외 idle 만**(기본 idle 은 풀에 없다). 주기는 `idleBreakInterval = (min, max)` 초, 고정 N 이면 (N, N).

- **소환사(CH1)도 통일한다.** 지금의 「루프를 이어 붙여 쉬지 않는」 Spine 변형(summon-patrol-defender unit 10 계약 5)은 은퇴하고
  같은 쉼/컷 규칙을 탄다. 즉 이 spec 은 unit 10 의 계약 5·7 을 **대체**한다(그 문서에 각주).
- CH2 `attack-Loop` · CH3 `attack2` 는 **지금 안 쓴다** — 슬롯이 없다. 용도가 정해지면 슬롯을 열 때 연결.

## 작업 단위

| 파일 | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 순수 로직 | `0_idle_break_cycle.md` | `IdleBreakCycle` 구조체 추출 + EditMode 테스트 · `SpriteUnitView` 가 소비(동작 무변) |
| 1 | 데이터 | `1_spine_data.md` | Spine SO `idleVariants` → `idleBreaks`(FormerlySerializedAs) · `idleBreakInterval` 추가 · 인터페이스 getter |
| 2 | Spine 뷰 | `2_spine_consumer.md` | `SpineUnitView` 가 `IdleBreakCycle` 소비 — 루프 이어붙임 은퇴, 쉼 = idle 루프 정상 재생 |
| 3 | 저작·검증 | `3_ch2_ch3_authoring.md` | CH2 → 스나이퍼 · CH3 → 실드셔틀 저작 + Play 확인 |

handoff 는 `4_handoff_summary.md`.

## Feature-wide 계약

- **규칙의 정본은 순수 구조체 하나.** `IdleBreakCycle`(Presentation) — 대기 타이머·컷 타이머·직전 인덱스를 소유하고
  `Tick(dt) → 전이 시점`, `PickBreak(breakCount, roll)`(직전 회피 = `UnitAnimationChoice.ChooseNext`), `BeginLoop(interval)`, `BeginBreak(duration)`.
  아키텍처 타입 0. 두 뷰는 **전이 시점에 무엇을 보여줄지만** 안다: 기본 루프 재생 · 컷 재생 · 컷의 한 바퀴 길이.

- **「한 바퀴」는 뷰가 길이를 재서 끝낸다 — 시트/트랙의 loop 플래그를 보지 않는다.** 스프라이트는 `FlipbookMath.Duration`,
  Spine 은 `Animation.Duration`. 이유는 양쪽 다 같다: Spine 은 unit 10 계약 5(«loop:false 로 이어붙이지 않는다 — `IsLocomotionLoopPlaying`
  과 원샷 게이트가 `Loop` 를 판정 기준으로 쓴다»), 스프라이트는 sprite-unit-backend unit 6 critic M1(«슬롯별 루프 정책은 상수»).
  그래서 Spine 의 컷은 **`loop:true` 엔트리**로 틀고 타이머로 끊는다.

- **평시 = 기본 idle 루프가 정상 재생.** 스프라이트는 `idle` 플립북 루프(`Play(idle)`), Spine 은 idle 루프 엔트리. 타이머가 끝나면 컷을
  얹고(Spine 은 크로스페이드) 한 바퀴 뒤 기본 루프로 돌아온다. 「비주얼 각자」의 실체는 재생 수단(재생기 vs AnimationState)과 길이 계산뿐이다.

- **시계는 배틀 스케일.** 두 뷰 모두 `Time.deltaTime × _battleScale` 로 Tick 한다(hop 과 같은 이유). Spine 은 컷 길이를 `Animation.Duration`
  그대로 쓴다 — 트랙이 `_skeleton.timeScale` 로 같이 느려지므로 같은 배틀 시간 안에 한 바퀴가 맞아떨어진다.

- **끼어드는 것의 우선순위는 지금 그대로다.** 원샷(공격·배치·사망) > 루프 오버라이드(소환사 능력) > walk > 대기 컷 순환.
  원샷이 끝나면 **기본 루프 + 새 타이머**부터 다시 시작한다. 오버라이드가 걸려 있으면 순환은 멈춘다(지금 `OnIdleVariantComplete` 가 `_loopOverride` 를 보는 것과 같은 자리).

- **데이터는 양 백엔드가 같은 모양.** 스프라이트: `UnitSpriteMotionSet.idleBreaks` + `idleRestGap` → **`idleBreakInterval`** 로 개명(FormerlySerializedAs).
  Spine: 유닛 SO 의 `idleVariants` 를 **`idleBreaks` 로 개명**(`[FormerlySerializedAs]`) + `idleBreakInterval` 추가. 같은 이름 = 같은 뜻.

- **풀 = idleBreaks 만.** 기본 idle 은 풀에 없다(항상 도는 것이지 끼워 넣는 것이 아니다). 소환사 `[idle, idle2, idle3]` → `[idle2, idle3]`,
  CH3 는 기본 `idle2` + 풀 `[idle1, idle3]`(사용자 지정).

- **Spine 본문 변경은 이 spec 의 범위다.** sprite-unit-backend 의 「Spine 무변경」 전제는 그 spec 의 것이고, 여기는 사용자가 명시적으로
  Spine 규칙을 바꾸기로 한 spec 이다. 변경 지점은 `SpineUnitView` 의 idle 변형 블록(`AdvanceIdleVariant`·`HookIdleVariantCycle`·
  `OnIdleVariantComplete`·`ResolveLocomotionAnimation` 의 변형 분기)으로 한정한다.

- **CH2·CH3 는 고유 리그 관용구를 그대로 탄다**(summon-patrol-defender unit 8 · CH1 선례): `skeletonDataAsset` 교체 · `spineSkinName` 비움 ·
  `partSkins` 비움 · 애니 이름 저작 · `spineVisualScale` 실측 · facing 규약(«ScaleX=+1 = 왼쪽») 위반 시 `SkeletonFlipX` 모디파이어. **코드 0.**
  death 애니가 없어 사망은 즉시 파괴(Spine `Kill` 의 기존 폴백), drag 는 idle 로 폴백(키링 프리뷰의 기존 후보 체인).

## 이 spec 이 확장하지 않는 것

| 요소 | 왜 |
|---|---|
| `SpriteFlipbookPlayer` · `UnitAnimationChoice` | 그대로 호출만 |
| `BattleBridge` · 스폰 게이트 · seam | 뷰 내부 규칙이라 브리지는 모른다 |
| `SetLoopOverride`/`ClearLoopOverride` | 우선순위 유지, API 무변 |
| 유닛 모션 슬롯 6개 | 새 슬롯 없음(attack-Loop·attack2 미사용) |

## 파이프라인 커버리지

플레이 오브젝트 신설 없음. 방어유닛 아키타입의 **데이터 SO**(필드 개명+1) · **View/Pool**(Spine 뷰 idle 규칙) 두 정거장만 바뀐다.
스폰·ECS·이벤트·씬 wiring N/A.

## 후속 후보

- **attack-Loop(CH2)·attack2(CH3) 용 슬롯** — 용도가 정해지면(지속 사격 루프 / 공격 변형).
- **CH2/CH3 death 애니** — 시트 쪽과 같은 공백.
- **`SpineIdleVariants` 를 쓰던 문서(summon-patrol-defender unit 10)의 계약 5·7 정식 개정** — 이 spec 에선 각주만.
