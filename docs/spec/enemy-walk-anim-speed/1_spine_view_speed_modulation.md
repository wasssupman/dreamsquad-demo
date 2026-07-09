# 1 — SpineUnitView 변위 측정 + timeScale 합성

## 목적

`SpineUnitView` 가 프레임당 실제 view 변위로 고유 이동속도를 추정하고, 이를 애니 `timeScale` 배율(walkFactor)로 변환해 기존 battleScale 과 곱한다. 발 미끄러짐 제거의 실제 구현.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs`

## 구현

### battleScale 캐시 (합성의 전제)

현재 `SetAnimationTimeScale(scale)` 은 `_skeleton.timeScale = scale` 를 직접 세팅한다(`SpineUnitPool` 이 `ScaleChanged` 로 호출). 이를 **캐시 후 합성**으로 바꾼다:

- 필드 `float _battleScale = 1f;`
- `SetAnimationTimeScale(scale)` → `_battleScale = scale;` 후 `ApplyTimeScale();`
- `ApplyTimeScale()` → `if (_skeleton != null) _skeleton.timeScale = _battleScale * _walkFactor;`

이름은 유지(풀 fan-out 호환). Spawn 초기화 경로도 그대로 동작.

### 변위 기반 walkFactor (매 프레임)

`UpdatePosition(world)` 안, `ApplyRenderPosition` **이전**에 측정(이전 `_simWorld` 가 직전 프레임 값):

```
float realDt = Time.deltaTime;                       // 실프레임(비스케일)
float simDt  = realDt * _battleScale;
if (WalkAnimSpeedEnabled && simDt > Eps)
{
    float disp = Vector3.Distance(ToView(world), ToView(_simWorld)); // xz 평면 view 변위
    if (disp < WalkAnimTeleportGuard)                // 포탈 점프 무시
    {
        float simSpeed = disp / simDt;
        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, simSpeed, WalkAnimSmoothing);
        _walkFactor = Mathf.Clamp(_smoothedSpeed / WalkAnimRefSpeed,
                                  WalkAnimMinTimeScale, WalkAnimMaxTimeScale);
        ApplyTimeScale();
    }
    // teleport 프레임은 측정·factor 갱신 스킵(직전 유지)
}
```

- `WalkAnimSpeedEnabled==false`(SO 미할당) → `_walkFactor` 는 초기값 1f 유지 → 현행 동작 그대로(회귀 없음).
- `_walkFactor` 필드 초기값 1f. `_smoothedSpeed` 초기값 0f.
- `disp`/`simSpeed`/`refSpeed` 모두 view-space 동일 단위 → 비율은 무차원.

### 주의

- **공격/사망/배치 회귀 (해결됨)**: timeScale 은 Spine 트랙 전역 배율이라 walkFactor 를 무조건 곱하면 원샷 애니까지 느려진다. 정지 유닛(standoff 적·타일 고정 디펜더)은 변위≈0 → walkFactor→minTimeScale → **공격이 minTimeScale 배로 느려짐**(최초 발견 회귀). 해결: `ApplyTimeScale` 이 `IsLocomotionLoopPlaying()`(track0 현재 애니 `Loop==true`)일 때만 walkFactor 를 적용, 원샷(공격/사망/배치, loop=false)은 배율 1. `PlayAttack`/`PlayDeploy`/`Kill` 은 세팅 직후 `ApplyTimeScale()` 호출로 첫 프레임부터 정상속도(특히 Kill 이후엔 UpdatePosition 미호출).

## 완료 기준

- compile 성공, `read_console` 에러 0.
- SO 미할당 상태에서 애니 속도 = 현행(회귀 없음).
- (Play, unit 2) 느린 적/빠른 적 걷기 사이클이 이동량에 맞게 느려짐/빨라짐, 발 미끄러짐 육안 감소.
- **공격/사망/배치 애니는 정상속도**(정지 유닛도 느려지지 않음) — 걷기 배율은 로코모션 루프에만.
- 슬로우모/정지에서 애니가 튀거나 완전 프리즈 붕괴 없음.
