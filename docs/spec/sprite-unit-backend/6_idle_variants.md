# 6 · 대기 변형 — 쉼 → 변형 원샷 → 쉼

## 목적

`idle1`, `idle2` … 처럼 대기 시트가 여러 장인 캐릭터의 idle 상태를 만든다 (2026-09-16 사용자 결정):

> *「idle 상태에서 idle1, idle2 를 랜덤하게 재생하고, 뭐라도 한번 재생하고 텀이 있을 때에는 idle1 의 0 프레임을 대기 상태 비주얼로 사용.」*

README 가 「`idleVariants` 는 넣지 않는다」고 적었던 항목이 사용자 요청으로 열렸다. **Spine 의 `idleVariants` 와 성질이 다르다** —
그쪽은 루프를 한 바퀴마다 이어 붙이고, 여기는 **원샷을 한 번 틀고 쉰다.** 이름만 같고 계약은 이 파일이 정본이다.

## 변경 대상

- 수정 `Data/UnitSpriteMotionSet.cs` — `idleVariants`(리스트) · `idleRestGap`(초 범위) · 풀 접근자 · `OnValidate`
- 수정 `Presentation/SpriteUnitView.cs` — `PlayLocomotion` 의 idle 분기 · `TickIdleCycle` · `EnterIdleRest`
- 수정 `Tests/EditMode/UnitSpriteMotionSetTests.cs` — 풀 순서 · 쉼 시간 (2건)

## 구현

**데이터** — 풀 = `idle`(0번) + `idleVariants`(1번~). **쉬는 그림은 항상 `idle` 의 0 프레임.** 저작자는 `idle` 슬롯에 idle1,
리스트에 idle2… 를 넣는다. `idleRestGap = (min, max)` 초, 기본 1~3. 변형 모드(`HasIdleVariants`)에선 **풀의 시트가 전부
원샷**이어야 한다 — 루프면 영영 안 끝나 다음 변형이 안 나오므로 `OnValidate` 가 idle 까지 포함해 경고한다.
변형이 비면 종전(idle 단일 루프) 그대로 — 기존 세트 무회귀.

**뷰** — `ResolveLocomotion` 이 idle 을 돌려주고 변형이 있으면 루프 대신 순환에 들어간다:
`EnterIdleRest`(재생기 `Stop` + `idle.FrameAt(0)` + 쉼 시간 추첨) → `TickIdleCycle`(쉼 타이머 소진 → `UnitAnimationChoice.ChooseNext`
로 직전과 다른 것을 뽑아 `Play`) → 완주(`!IsPlaying`) → 다시 쉼. 시계는 배틀 스케일(hop 과 같은 이유). 난수는
`UnityEngine.Random`(프레젠테이션 — sim 난수와 섞지 않는다, Spine 과 같은 선택).
공격/배치/사망 원샷이 끼어들면 `PlayOneShot` 이 순환을 끄고, 완주 후 `PlayLocomotion(force)` 가 **쉼부터** 다시 시작한다
(사용자 문장의 「한번 재생하고 텀」). walk 로 나가면 순환이 꺼지고 돌아오면 다시 쉼부터.

**폴백 소비자(드래그 실루엣·deploy 폴백)** 는 그대로 `idle` 을 `Play` 한다 — 변형 모드에선 그게 원샷이라 한 번 돌고 마지막
프레임에 선다. drag/deploy 시트를 저작한 유닛(roy·rosa)은 그 경로에 안 걸린다.

## 완료 기준

- EditMode `UnitSpriteMotionSetTests` +2 초록 · 기존 총계 유지.
- 변형 0개 세트(roy·rosa 현재)는 동작 무변 — idle 루프.
- 변형 1개 이상 세트를 꽂은 유닛: 스폰 직후 idle 0프레임으로 서고, 1~3초 뒤 풀에서 하나가 한 번 돌고, 다시 0프레임으로 선다.
  공격이 끼어들면 공격 후 쉼부터. 같은 변형이 연속으로 나오지 않는다(풀 ≥ 2).
