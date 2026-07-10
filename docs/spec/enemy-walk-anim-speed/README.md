# Spec — Enemy Walk Anim Speed Match (걷기 애니 ↔ 이동속도 동기)

> 상태: **완료 2026-07-10** (units 0~2) + **rev 2 (2026-07-11) unit 4** — 이동=Walk/정지=Idle 자동 전환(`enemy-hunter-targeting` 실플레이서 발견). 사용자 Play 통과("이건 좋다"), referenceSpeed 1.2 튜닝 확정. handoff `3_handoff_summary.md`.
> 출처: 사용자 요청. 적 걷기 애니가 이동속도와 무관하게 고정 재생돼 발이 미끄러지는(문워크) 어색함 제거.

## 문제

`SpineUnitView` 는 걷기/idle 을 고정 `timeScale`(평소 Battle 도메인 스케일=1.0)로 루프한다. 이동속도는 `PathFollowState.speed × moveSpeedMul` 로 시뮬에서 결정되지만 애니 재생속도와 연결이 없다 → 느린 적은 발 미끄러짐, 빠른 적은 애니가 못 따라옴. (QuadUnitView 는 걷기 사이클 없는 폴백 → 대상 아님.)

## 검증 질문

> "이동하는 적의 걷기 사이클이 지면 이동량에 맞아 발 미끄러짐이 눈에 띄게 줄었는가? 느린/빠른 적이 각각 느리게/빠르게 걷는가? standoff 정지·포탈 텔레포트·슬로우모/정지에서 애니가 튀거나 얼지 않는가? 파라미터를 SO 에서 Play 중 실시간 튜닝할 수 있는가?"

## 해법 요약 (사용자 결정: 뷰 실측 변위)

`SpineUnitView` 가 프레임당 실제 view-space 변위로 고유 이동속도를 산출해 애니 `timeScale` 을 변조. 순수 프레젠테이션(ECS 변경 0). aggro 슬로우·토네이도 pull·standoff 정지까지 실제 움직임 그대로 반영.

```
realDt   = Time.deltaTime
simDt    = realDt × battleScale                 // battleScale=0(정지) → 아래 가드로 유지
disp     = |ToView(world) − ToView(prevSim)|    // 이번 프레임 view 변위
if simDt > eps && disp < teleportGuard:         // 포탈 점프 무시
    simSpeed = disp / simDt                      // 슬로우모 무관 고유 속도
    smoothed = Lerp(smoothed, simSpeed, smoothing)
walkFactor = Clamp(smoothed / refSpeed, minTimeScale, maxTimeScale)
skeleton.timeScale = battleScale × walkFactor    // 기존 time-manager 동기와 합성
```

## feature-wide 계약

1. **순수 프레젠테이션.** ECS 컴포넌트/시스템/큐 변경 0. `SpineUnitView` 내부에서만 변위 측정·변조.
2. **battleScale 과 합성.** 최종 `timeScale = battleScale × walkFactor`. battleScale(슬로우모/정지)은 여전히 `SpineUnitPool` 이 `ScaleChanged` 로 fan-out; 뷰가 이 값을 캐시해 매 프레임 walkFactor 와 곱한다. 정지(battleScale=0)는 그대로 프리즈.
3. **sim-time 정규화.** simSpeed = disp / (realDt × battleScale) → 고유 속도만 반영해 슬로우모 이중감산 방지. simDt≤eps 프레임은 측정 스킵(직전 smoothed 유지).
4. **텔레포트 가드.** 한 프레임 view 변위가 `teleportGuard` 초과면 측정 스킵(포탈 점프가 애니를 튀게 하지 않음).
5. **정지 바닥값.** ~~standoff 등 변위 0 → walkFactor 는 `minTimeScale` 로 클램프~~ → **⚠ unit 4 계약 11 로 정정됨(2026-07-11).** `minTimeScale` 은 느린 **이동**의 하한이지 **정지 유닛에 쓰는 값이 아니다** — 원 설계가 "정지"와 "느린 이동"을 혼동해 정지 유닛 idle 을 0.15x 슬로모로 재생하던 결함(사용자 실플레이 지적 "모두 슬로우모션"). 정정: 걷기 배율은 **이동 중(`_moving`)일 때만** 적용, 정지 유닛은 factor 1(자연속도). 상세 = `4_locomotion_walk_idle_switch.md` 계약 11. minTimeScale 은 이제 느린-이동 walk 의 하한으로만 유효.
5b. **로코모션 루프에만 적용.** `timeScale` 은 Spine 트랙 전역 배율이라 걷기 배율을 그대로 곱하면 공격/사망/배치 애니까지 느려진다(정지 유닛의 walkFactor→minTimeScale 회귀). 따라서 walkFactor 는 **track0 현재 애니가 루프(걷기/idle)일 때만** 적용하고, 원샷(loop=false: 공격/사망/배치)에는 배율 1(=battleScale 만). 원샷 시작 시(`PlayAttack`/`PlayDeploy`/`Kill`) 즉시 재평가해 첫 프레임부터 정상속도.
6. **하드코딩 금지.** refSpeed / minTimeScale / maxTimeScale / smoothing / teleportGuard 는 `WalkAnimSpeedStyle` SO 에서. BattleBridge 정적 미러로 뷰에 주입(기존 `CharacterVisualScale` 등과 동일 패턴). SO 미할당 시 배율 1.0 고정(현행 동작 = 회귀 없음).
7. **디펜더 무영향.** 디펜더는 타일 고정(이동 없음) → 변위≈0. 배치 스킬/공격 애니는 명시 `SetAnimation` 경로라 timeScale 변조와 독립. 필요 시 디펜더는 refSpeed 게이트로 자연 제외.

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | SO + 미러 | `0_walk_anim_speed_style.md` | `WalkAnimSpeedStyle` SO 스키마 + BattleBridge 정적 미러 |
| 1 | 뷰 변조 | `1_spine_view_speed_modulation.md` | SpineUnitView 변위 측정 + timeScale 합성 |
| 2 | 배선·검증 | `2_authoring_and_wiring.md` | 에셋 생성 + BattleScene 배선 + Play 튜닝 검증 |
| 4 | 로코 전환 | `4_locomotion_walk_idle_switch.md` | 이동=Walk/정지=Idle 자동 전환(walkAnimation 옵트인, 히스테리시스). rev 2 |

## 파이프라인 커버리지

신규 아키타입/정거장 추가 없음. 기존 **적 Spine 뷰(생성→렌더)** 의 애니메이션 재생 파라미터만 변조한다 (`docs/reference/object-pipeline-map.md` 의 "적 유닛(Spine)" 표의 렌더 정거장 내부 조정). 생성 경로·정렬·그림자·틴트 등 다른 정거장 불변 → 표 복사 N/A.

## 후속 후보

- **방향 전환/코너 감속 시 발 접지 스냅**(root motion 근사) — 본 spec 범위 밖.
- **공격→걷기 전이 블렌드 타임 튜닝** — attack-hit-delay/status 계열과 함께.
- **디펜더 idle 미세 애니 속도** — 별도 판단.
