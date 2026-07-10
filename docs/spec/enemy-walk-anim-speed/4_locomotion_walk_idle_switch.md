# 4 — 이동/정지 로코모션 애니 자동 전환 (walk ↔ idle)

> rev 2 (2026-07-11) — `enemy-hunter-targeting` 실플레이에서 발견. 정지가 잦은 적(헌터 보스)이 노출한 갭.

## 문제

units 0~2 는 로코모션 루프를 **단일 애니**(`SpineIdleAnimation`)로 두고 timeScale 만 이동속도로 변조한다. 계약 5(정지 바닥값)에 따라 정지 유닛의 walkFactor 는 `minTimeScale`(0.15)로 내려간다. 이동이 잦은 적은 이 단일 애니가 "Walk"라 자연스럽지만(느린 걷기), **정지가 잦은 적**(헌터 보스: 방어유닛에 붙어 오래 멈춤)은 "Walk" @0.15 = **슬로모 걷기**로 보인다.

## 해법

로코모션 루프를 **이동=Walk / 정지=Idle** 로 자동 전환한다. 뷰가 이미 산출하는 `_smoothedSpeed`(units 1)로 판정, 히스테리시스로 경계 flicker 방지. **순수 프레젠테이션**(ECS 변경 0, 계약 1 유지).

## 변경 대상

- `Data/ISpineUnitVisualData.cs` — `SpineWalkAnimation` 추가
- `Data/AttackUnitData.cs` — `walkAnimation` 필드(기본 "") + 프로퍼티
- `Data/DefenderUnitData.cs` — `SpineWalkAnimation => ""` (디펜더 이동 없음)
- `Presentation/SpineUnitView.cs` — `UpdateLocomotionAnimation` + `ResolveLocomotionAnimation`
- 에셋: `Enemy_Boss_Nightmare` — idle "Walk"→"Idle", walk "Walk"

## 계약 (기존 feature 계약에 추가)

8. **walk 애니 옵트인.** `SpineWalkAnimation` 비면 **단일 idle 루프**(units 0~2 현행 = 회귀 없음). 설정 시에만 이동 중 walk / 정지 중 idle 전환. 기존 적·디펜더 전부 빈 값 → 무영향.
   - **커플링 주의(critic)**: `_moving` 은 `UpdateWalkTimeScale` 안에서만 갱신되고, 그 함수는 `WalkAnimSpeedEnabled`(=`WalkAnimSpeedStyle` SO 할당) 가드 뒤에 있다. 따라서 walk↔idle 스위칭은 **walkAnimation 설정 + SO 할당 둘 다** 필요 — SO 미할당이면 `_moving` 이 항상 false 라 walk 애니가 침묵 실패(항상 idle). 실전 보스는 둘 다 배선. 옵트인은 walkAnimation 만으로 게이트되는 듯 읽히나 실제론 SO 의존이 있다.
9. **히스테리시스 전환.** `_smoothedSpeed`(units 1 재사용) vs `WalkAnimRefSpeed`(SO) 분율 — on `>ref×0.35`, off `<ref×0.15`. 별도 SO 필드 없이 이동 스케일과 연동(계약 6 하드코딩 금지 정신 준수). 전환은 `MixDuration` 크로스페이드.
10. **원샷 불간섭.** track0 이 loop=false(공격/사망/배치) 이면 전환 스킵 — 복귀는 `PlayAttack`/`PlayDeploy` 의 큐가 `ResolveLocomotionAnimation` 로 처리(공격 후 이동상태 맞는 루프 복귀).
11. **정지 = 자연속도(계약 5 정정).** 걷기 배율은 **이동 중일 때만** 적용한다. 정지 유닛(디펜더/멈춘 적/보스)은 `factor 1`(=battleScale 만) 으로 애니를 **정상 속도** 재생. `minTimeScale`(0.15)은 느린 **이동**의 하한이지 정지 유닛에 쓰는 값이 아니다. 이동/정지는 `_smoothedSpeed` 히스테리시스(ref×0.15 이동 / ×0.05 정지)로 판정하며, 느린 이동은 여전히 `_moving=true`라 배율 동기(발 접지)를 유지한다.
    - **정정 사유(2026-07-11 실플레이)**: units 0~2 는 `IsLocomotionLoopPlaying()`(임의 루프)에 배율을 곱해 **정지 유닛의 idle 까지 0.15 로 슬로모** 재생했다(디펜더 idle·멈춘 보스 전부). 사용자 지적("모두 슬로우모션") — 이동 동기 배율이 정지 유닛에 새어든 것. 이동 게이트(`_moving`)로 차단.

## 완료 기준

- [x] 컴파일 + EditMode 무회귀(데이터 필드 append, 프레젠테이션 무관). EditMode 641/643 그린.
- [x] Play: **정지 중 슬로모 걷기 없음** — 사용자 확인 2026-07-11 ("슬로우모션은 아닌데"). 이동=Walk/정지=Idle 전환 + `_moving` 게이트로 정지 factor 1(자연속도).
- [x] 기존 적(walkAnimation 빈 값) 걷기 무변경 · 디펜더 idle 무변경 — 무회귀(옵트인, 빈 값=단일 idle 루프).
- [x] (재스폰 필요: 이미 스폰된 유닛은 옛 데이터 캐시.)

확인 2026-07-11 — 슬로모 해소 사용자 Play 확인. 커밋 `e53fba46`(스위칭) `d569ec68`(정지 factor 게이트) + 문서 정정(`815d7bd4`). walk feel 미세 튜닝(히스테리시스 상수)은 필요 시 후속.

확인 대기 — 사용자 Play 재시작 후.
