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
9. **히스테리시스 전환.** `_smoothedSpeed`(units 1 재사용) vs `WalkAnimRefSpeed`(SO) 분율 — on `>ref×0.35`, off `<ref×0.15`. 별도 SO 필드 없이 이동 스케일과 연동(계약 6 하드코딩 금지 정신 준수). 전환은 `MixDuration` 크로스페이드.
10. **원샷 불간섭.** track0 이 loop=false(공격/사망/배치) 이면 전환 스킵 — 복귀는 `PlayAttack`/`PlayDeploy` 의 큐가 `ResolveLocomotionAnimation` 로 처리(공격 후 이동상태 맞는 루프 복귀).
11. **timeScale 로직 불변.** 계약 5/5b 그대로 — 정지 시 idle 이 `minTimeScale`(0.15)로 재생(디펜더와 동일, 자연스러운 미세 idle). "walk @0.15 슬로모"가 "idle @0.15 미세"로 바뀌는 게 이 unit 의 본질.

## 완료 기준

- [ ] 컴파일 + EditMode 무회귀(데이터 필드 append, 프레젠테이션 무관).
- [ ] Play: 보스가 **이동 중 Walk**(발 구름, 속도 동기) / **정지 중 Idle**(슬로모 걷기 없음). 전환이 튀지 않음(크로스페이드).
- [ ] 기존 적(walkAnimation 빈 값) 걷기 무변경 · 디펜더 idle 무변경 — 무회귀.
- [ ] (재스폰 필요: 이미 스폰된 유닛은 옛 데이터 캐시.)

확인 대기 — 사용자 Play 재시작 후.
