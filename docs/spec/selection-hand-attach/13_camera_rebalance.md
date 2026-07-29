# 13 — 카메라 재배합 (FOV 압축 + 전환 스무딩)

> 추가 2026-07-30 (사용자 결정). 다른 unit 과 독립 — 언제 해도 된다.

## 목적

선택 줌인을 **유지하되** 부담을 줄이고 유닛을 더 부각한다. 두 가지다:

1. `inspectDolly` 만으로 당기는 현행을 **dolly↓ + FOV 압축** 조합으로 바꾼다. dolly 만이면
   "가까이 갔다"이고, FOV 를 좁히면 원근이 압축돼 배경이 납작해지며 **"주목했다"** 가 된다.
2. **선택 전환(A→B)이 스냅한다.** 이 spec 의 핵심 가치가 연속 부착인데 카메라가 유닛마다
   튀면 위치 감각을 잃는다.

## 현행 실측

`CameraDirectionConfig.asset`: `inspectDolly 4.92` · `inspectFovDelta 0` · `inspectLookWeight 0.5`
· `inspectFadeInSec 0.22` · `inspectFadeOutSec 0.3`.

**전환이 튀는 원인**: `SetInspectFocus`(`CameraDirector.cs:253`)가 `_inspectNdc` 를 **직접 대입**한다.
가중치(`_inspectWeight`)만 페이드가 걸려 있고 NDC 자체에는 스무딩이 없어, 대상이 바뀌면
그 프레임에 프레이밍이 통째로 점프한다. 피드가 끊기지 않으므로 "재줌"은 원래 일어나지 않는다 —
문제는 재줌이 아니라 **NDC 스냅**이다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/CameraDirector.cs` — NDC 추종
- `Assets/_Project/Scripts/Data/CameraDirectionConfig.cs` — 추종 노브
- `Assets/_Project/Data/Camera/CameraDirectionConfig.asset` — 값

## 구현

### A. FOV 압축

`inspectDolly` 를 낮추고 `inspectFovDelta` 에 음수를 넣어 같은 부각을 나눠 만든다.
**합성식은 그대로** — `FocusDelta` 가 이미 두 인자를 받는다. 코드 분기 0, 값만 바뀐다.

**선행 확인 — FOV 여유**: `CameraDirectionConfig.cs` 주석이 "여유 2도뿐(fovMin 41 / 홈 43)"이라
dolly 단독을 권했으나 그건 `fovMin` 이 41 이던 시절 기준이다. 실측 2026-07-30:
**`fovMin 31` / 홈 FOV `60` → 여유 ≈29도**. 클램프에 조용히 깎이지 않으므로 FOV 를 실제
레버로 쓸 수 있다. 주석도 이 실측으로 갱신한다.

### B. 전환 스무딩

`_inspectNdc` 를 목표값으로 두고 추종시킨다(`_inspectNdcTarget` 신설, `SetInspectFocus` 는 목표만
갱신). 합성은 추종된 현재값을 쓴다.

**획득 프레임은 스냅해야 한다** — `_inspectWeight` 가 0 에서 올라오는 첫 피드(= 새 선택 시작)는
현재값을 목표로 즉시 맞춘다. 안 그러면 이전 유닛 위치에서 **가로질러 날아온다**(선택 리티클이
"날아오지 않고 pop" 인 것과 같은 이유). 추종이 도는 건 **weight 가 이미 살아 있는 전환**뿐이다.

추종 계수는 `[SerializeField]` 노브. 기존 스프링 헬퍼(`KeyringSim.SpringStep`)를 쓰거나 지수
감쇠 하나면 충분하다 — 새 수학을 만들지 않는다.

### B-2. 연출 pitch (rev 2026-07-30 사용자 발의)

지금까지 인스펙트에 걸리던 틸트는 `FocusDelta` 가 **lookat 에서 파생**한 것뿐이었다
(`pitchFull × lookWeight`) — 선택 유닛을 바라보느라 생기는 각도이지 연출로 의도한 틸트가 아니다.
손패 헤드룸(`handHeadroomPitchDeg`)과 이동모드 오버뷰(`moveOverviewPitchDeg`)에는 명시 pitch
노브가 있는데 **인스펙트만 없었다**. 초판이 "다른 채널은 만지지 않는다"로 스코프를 잡으며
이 비대칭을 놓쳤다.

`inspectPitchDeg`(기본 -5) 신설. 음수 = 카메라를 낮춰 올려다본다 → 유닛이 서 있는 각도감이
생겨 dolly/FOV 로는 못 얻는 부각이 붙는다. 적용은 헤드룸과 같은 형태(가중치 비례 가산)이고
`FocusDelta` 는 건드리지 않는다. 0 이면 구 동작(lookat 파생 각도만).

### C. 건드리지 않는 것

- `inspectLookWeight`·fade 초·staleness 2프레임 자동 해제 규약은 **불변**.
- 조준 중 피드 중단(unit 0)도 불변 — 그 경로가 끊기면 여기 추종도 함께 멈춘다.
- 다른 채널(드래그 포커스 / 손패 헤드룸 / 이동모드 오버뷰)은 만지지 않는다.

## 완료 기준

- [x] compile 클린 (2026-07-30 — Unity 콘솔 error 0)
- [ ] Play: 카메라가 낮아져 유닛을 **올려다보는** 각도감이 생긴다(`inspectPitchDeg`)
- [x] 값 적용: `inspectDolly 4.92 → 3` · `inspectFovDelta 0 → -6` · `inspectFollowRate 12`(신설)
      · `inspectPitchDeg -5`(신설. 참고: `handHeadroomPitchDeg` 는 -2 라 그보다 강한 틸트다).
      에셋 재저장 시 `moveOverview*` 4필드가 함께 직렬화됐는데 **전부 클래스 기본값과 동일**
      (`-4.5 / 0 / 90 / 16`)이라 동작 변화가 없다.

**아래 Play 항목은 값 튜닝의 출발점 검증이다** — 체감이 과하거나 모자라면 `inspectDolly` ·
`inspectFovDelta` · `inspectFollowRate` 노브만 조정한다(코드 분기 금지, 계약 9).
- [ ] Play: 유닛 선택 → 줌인이 과하지 않고 유닛이 배경에서 도드라진다(FOV 압축 체감)
- [ ] Play: **유닛 A → B 연속 선택** → 카메라가 튀지 않고 미끄러지듯 옮겨간다
- [ ] Play: **무선택 → 첫 선택** → 이전 위치에서 날아오지 않고 그 자리에서 줌인된다(스냅 규칙)
- [ ] Play: 선택 해제 → 홈 프레이밍 복귀(fade out 무회귀)
- [ ] Play: 선택 중 Active 카드 조준 → 줌 해제 후 커밋/취소 시 복귀(unit 0 무회귀)
- [ ] 콘솔 에러/경고 0
