# 6 · 대기 컷(idle breaks) — 쉼 → 한 바퀴 → 쉼

## 목적

`idle1`, `idle2` … 처럼 대기 시트가 여러 장인 캐릭터의 idle 상태를 만든다 (2026-09-16 사용자 결정):

> *「idle 상태에서 idle1, idle2 를 랜덤하게 재생하고, 뭐라도 한번 재생하고 텀이 있을 때에는 idle1 의 0 프레임을 대기 상태 비주얼로 사용.」*

README 가 「`idleVariants` 는 넣지 않는다」고 적었던 항목이 사용자 요청으로 열렸다. Spine 의 `SpineIdleVariants`(애니 이름 ·
루프를 이어 붙임)와 **성질이 다르므로 이름을 갈랐다** — `idleBreaks`. 같은 유닛 SO 가 두 필드를 갖는데 이름까지 같으면
다음 사람이 어느 쪽을 채울지 파일만 보고 알 수 없다(critic M6 · 직렬화 키가 아직 없어 개명이 공짜인 창).

## 변경 대상

- 수정 `Data/UnitSpriteMotionSet.cs` — `idleBreaks`(리스트) · `idleRestGap`(초 범위) · 풀 접근자 · `OnValidate`
- 수정 `Presentation/SpriteUnitView.cs` — `PlayLocomotion` 의 idle 분기 · `TickIdleCycle` · `EnterIdleRest` · `CurrentAnimationName`
- 수정 `Tests/EditMode/UnitSpriteMotionSetTests.cs` — 풀 순서 · 쉼 시간 (2건)

## 구현

**데이터** — 풀 = `idle`(0번) + `idleBreaks`(1번~). **쉬는 그림은 항상 `idle` 의 0 프레임.** 저작자는 `idle` 슬롯에 idle1,
리스트에 idle2… 를 넣는다(idle1 을 리스트에 **다시 넣지 않는다** — 풀에 두 번 들어가 「직전과 다른 것」 회피가 무력해진다).
`idleRestGap = (min, max)` 초, 기본 1~3 · `(0,0)` = 쉼 없이 연속. 대기 컷이 비면 종전(idle 단일 루프) 그대로 — 무회귀.

**핵심 결정 — 「한 바퀴」는 뷰가 길이를 재서 끝낸다. 시트의 `loop` 체크박스를 보지 않는다.**
첫 구현은 「대기 컷 모드에선 idle 도 원샷이어야 한다」로 갔는데 critic 이 땜빵으로 판정했다(M1): 슬롯별 루프 정책이
상수라는 불변식이 형제 필드의 함수가 되고, 그 루프에 기대던 폴백 3경로(드래그 실루엣 · walk 없는 유닛의 이동 · deploy 폴백)가
「한 번 돌고 마지막 프레임에 선다」로 깨졌다. 지금은 `FlipbookMath.Duration(fps, frameCount)` 만큼 틀고 끊으므로 idle 은
변형 유무와 무관하게 **루프 슬롯 그대로**고, 폴백 경로는 전부 종전대로 idle 루프를 받는다.

**뷰** — `ResolveLocomotion` 이 idle 을 돌려주고 대기 컷이 있으면 루프 대신 순환에 들어간다. 타이머 **하나**(`_idleTimer`)가
쉼과 재생을 둘 다 잰다: `EnterIdleRest`(재생기 `Stop` + `idle.FrameAt(0)` + 쉼 시간 추첨) → 타이머 소진 →
`UnitAnimationChoice.ChooseNext` 로 직전과 다른 컷을 뽑아 `Play` + 타이머 = 그 컷의 한 바퀴 길이 → 소진 → 다시 쉼.
재생기 `IsPlaying` 을 폴링하지 않아 `_oneShot` 축과 섞이지 않는다. 시계는 배틀 스케일(hop 과 같은 이유).
난수는 `UnityEngine.Random`(프레젠테이션 — sim 난수와 섞지 않는다).
공격/배치/사망 원샷이 끼어들면 `PlayOneShot` 이 순환을 끄고, 완주 후 `PlayLocomotion(force)` 가 **쉼부터** 다시 시작한다
(「한번 재생하고 텀」). walk 로 나가면 순환이 꺼지고 돌아오면 다시 쉼부터.
쉬는 동안 `CurrentAnimationName` 은 `idle` 이름을 돌려준다(직전 컷 이름을 돌려주면 거짓 — critic M4).

**비행(`SetFlightView`) 중**에는 `UpdatePosition` 이 안 와 순환이 그대로 돈다 — 퇴근 스냅 0.3초라 보이지 않는다(critic 미채점 항목, 방치).

## 완료 기준

- EditMode `UnitSpriteMotionSetTests` +2 초록 · 기존 총계 유지.
- 대기 컷 0개 세트(roy·rosa 현재)는 동작 무변 — idle 루프.
- 대기 컷 1개 이상 세트를 꽂은 유닛: 스폰 직후 idle 0프레임으로 서고, 1~3초 뒤 풀에서 하나가 **한 바퀴** 돌고, 다시 0프레임으로 선다.
  공격이 끼어들면 공격 후 쉼부터. 같은 컷이 연속으로 나오지 않는다(풀 ≥ 2). 드래그 실루엣은 종전대로 drag→idle **루프**.
- ⚠ 대기 컷을 저작한 에셋이 아직 없어 위 항목은 **시트가 오면** Play 로 확인한다(critic M5 — 「굴러간 적 없는 기능」).
