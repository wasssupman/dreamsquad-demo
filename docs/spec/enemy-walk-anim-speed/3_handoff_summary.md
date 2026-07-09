# Handoff — Enemy Walk Anim Speed Match

## Commit
`feat(anim-speed): 걷기 애니↔이동속도 동기 + 공격 애니↔공속 압축` (걷기+공격 동일 커밋, 공유 파일 다수).

## Implemented
- 적 Spine 걷기 애니를 실제 이동 변위 기반으로 재생속도 변조 → 발 미끄러짐(문워크) 제거.
- `SpineUnitView` 가 프레임당 view 변위로 sim-time 정규화 속도 추정 → `walkFactor = clamp(speed/refSpeed, min, max)`.
- 최종 `skeleton.timeScale = battleScale × walkFactor`. TimeManager 슬로우모/정지와 자연 합성(정지=프리즈).
- 포탈 텔레포트는 프레임 변위 임계값으로 무시, standoff 정지는 minTimeScale 바닥.
- **공격/사망/배치 회귀 수정**: timeScale 은 트랙 전역이라 걷기 배율이 공격 애니까지 늦췄던 문제 →
  `ApplyTimeScale` 이 로코모션 루프(`IsLocomotionLoopPlaying`, Loop==true)일 때만 walkFactor 적용, 원샷은 배율 1.
  `PlayAttack`/`PlayDeploy`/`Kill` 은 세팅 직후 `ApplyTimeScale()` 로 즉시 반영.
- 파라미터는 `WalkAnimSpeedStyle` SO + BattleBridge 정적 미러. 튜닝 확정: referenceSpeed 1.2, maxTimeScale 3.0, min 0.15, smoothing 0.2, teleportGuard 1.5.

## Key Files
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` — 변위 측정/합성/로코모션 게이트.
- `Assets/_Project/Scripts/Data/WalkAnimSpeedStyle.cs` + `Assets/_Project/Data/Config/WalkAnimSpeedStyle.asset`.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 정적 미러 + 초기화 복사.
- `Assets/_Project/Scripts/Presentation/SpineUnitPool.cs` — ScaleChanged fan-out(기존).
- BattleScene: BattleBridge.walkAnimSpeedStyle 배선(씬 diff 1줄).

## Verified
- compile 클린(에러 0). Play 진입 시 미러 end-to-end 흐름(Enabled=True). 라이브 walkFactor 실측(속도 1.5→factor≈1.25).
- 사용자 Play 육안 통과("이건 좋다").

## Notes
- 되돌리지 말 것: 로코모션 게이트(원샷 애니는 walkFactor 미적용) — 없으면 공격/사망이 느려짐.
- sim-time 정규화(disp/(realDt×battleScale))는 슬로우모 이중감산 방지 목적. realDt 만 쓰면 정지에서 오동작.
- QuadUnitView(걷기 사이클 없는 폴백)·디펜더(타일 고정)는 자연 제외.

## Follow-up
- 방향 전환/코너 감속 시 발 접지 스냅(root motion 근사) — 범위 밖.
- Android 실기 프로파일(변위 계산·GetComponentsInChildren 비용) 미확인.
