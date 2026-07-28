# 0 — 하마(dismount) 궤적 순수 수학

## 목적

반동(Hermite)→솟음·착지(수직 끝접선 아치)의 한 점 평가를 plain 값 in/out 순수 함수로 제공한다. 제약 10 근거: (a) 3구간 분기 비자명 계산, (c) sim-critical 은 아니나 회귀 테스트 가치가 있는 프레젠테이션 핵심 수학.

## 변경 대상

- `Assets/_Project/Scripts/UI/KeyringSim.cs` — static 메서드 추가
- `Assets/_Project/Tests/EditMode/KeyringSimTests.cs` (기존 파일 있으면 이어서, 없으면 신설)

## 구현

```csharp
// 반동+아치 통합 평가. t01 ∈ [0,1]. recoilFrac = 반동 시간 비율(예 0.12/0.45 ≈ 0.267).
// 반동 구간 [0, recoilFrac]: Hermite — p0=start, v0=startVel(반동 시간 스케일), p1=dip, v1=0.
//   dip = start − camUp·dipDistance. 잔여 스윙 속도 흡수(플릭일수록 반동 큼).
// 비행 구간 (recoilFrac, 1]: CubicBezier(dip, c1, c2, end).
//   arcHeight = max(|dip−end|·arcHeightFactor, minArcHeight)  ← 절대 하한이 계약
//   c1 = Lerp(dip, end, launch.x) + camUp·(arcHeight·launch.y)   ← unit 1 dropLaunchControl
//   c2 = end + camUp·(arcHeight·landingHeight)  ← end 바로 위 = 끝접선 순수 -camUp(스틱 착지)
public static Vector3 DismountPoint(
    Vector3 start, Vector3 startVel, Vector3 end, Vector3 camUp,
    float recoilFrac, float dipDistance,
    float arcHeightFactor, float minArcHeight, Vector2 launch, float landingHeight,
    float t01)
```

- Hermite 는 이 파일에 private 헬퍼로 (KeyringSim 에 아직 없음). 좌우 lateral 변주는 넣지 않는다 — 하마는 제자리 수직 도약이 어휘(ThrowArcControls 재사용 대신 별도 제어점인 이유).
- 구간 경계에서 C0 연속(위치 일치)만 계약. C1(속도) 불연속은 **의도** — 분리 순간의 스냅.
- 시간 이징(OutCubic 등)은 호출측(unit 2) 책임. 이 함수는 기하만.

## 완료 기준

- EditMode 테스트 (신규 5+):
  - `t=0` → start 정확, `t=1` → end 정확 (오차 < 1e-4)
  - `t=recoilFrac` 양측 극한 위치 일치 (C0)
  - `t=1` 근방 차분 접선이 -camUp 과 평행 (dot > 0.99) — 수직 착지
  - apex(camUp 성분 최대)가 `max(start,end) camUp 성분 + minArcHeight·0.5` 이상 — 하한 동작
  - `startVel=0` 일 때 반동 구간이 단조 하강(dip 방향)
- compile 클린 · 기존 테스트 무회귀
