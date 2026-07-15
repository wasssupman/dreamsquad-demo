# 1 — 페이즈 전환 비행

## 목적

페이즈 전환 시 카메라 포즈 스냅을 보간 비행으로 바꾼다. 페이즈마다 다른 포즈(예: Draft는 얕은 pitch로 넓게, Battle은 깊은 pitch로 몰입)를 SO로 정의하고, `GameManager.PhaseChanged`에 반응해 커브 비행한다. 카메라 탈취가 허용되는 유일한 연출.

## 변경 대상

- 수정 `Assets/_Project/Scripts/Presentation/CameraDirector.cs` — flight 채널 실동작 + `PhaseChanged` 구독
- 수정 `Assets/_Project/Scripts/Data/CameraDirectionConfig.cs` — 페이즈 포즈 테이블 추가
- 수정 `Assets/_Project/Scripts/Presentation/CameraComposeMath.cs` — 비행 보간 순수 함수
- 에셋 `CameraDirectionConfig.asset` — 페이즈별 델타 명시값

## 구현

- config에 `PhasePose[]` 테이블: `{ GamePhase phase, Vector3 localPosOffset, float pitchOffset, float fovOffset, float flightSec, AnimationCurve ease }`. **미등록 페이즈 = hold** — 현재 델타를 유지하고 비행하지 않는다. (홈 복귀가 아님 — 홈 복귀로 하면 Draft 튜닝 시 Gift 진입마다 카메라 연출이 발생해 README의 "Gift 카메라는 범위 밖" 결정과 모순.) 등록된 페이즈로의 전환만 카메라를 움직인다.
- `PhaseChanged(phase)` 수신 → 현재 flight 델타에서 목표 델타로 `flightSec` 동안 커브 보간. 비행 중 재전환 오면 현재 보간값에서 새 목표로 재시작(스냅 금지).
- **스냅 규칙**: Director 활성화 시점의 최초 적용만 즉시 세팅. 이후 모든 전환은 비행 — 매치 재시작도 일반 전환으로 취급한다(`PhaseChanged`만으로는 재시작을 판별할 수 없고, 짧은 비행은 재시작 UX로도 자연스럽다. 별도 리셋 신호 신설 금지).
- 보간은 unscaledDeltaTime. 순수 함수 `EvaluateFlight(from, to, t01, curve적용은 호출부) → delta` — plain 값 in/out.
- 델타 기본값은 **전 페이즈 항등으로 시작**(도입 시 시각 변화 0 — 안전 롤아웃). 튜닝은 에셋에서: 1차 튜닝 목표는 Draft/Placement(pitch 얕게, 살짝 멀리) ↔ Battle(홈 포즈) ↔ Result(살짝 push-in) 정도의 소폭 차등. 구체 수치는 Play 체감으로 잡고 에셋에 기록.
- 비행 중 배치/드래그 입력 잠금은 두지 않는다(README 계약 — 스크린→셀 변환이 라이브 카메라 기준이라 정합).
- **킥/구두점 축 주의**: 현재 킥 델타는 **홈 축 기준**(구 컴포넌트는 라이브 transform 축)이다. 페이즈 델타가 항등이면 동일하지만, pitch 를 크게 튜닝하면 킥의 "아래" 방향이 체감상 어긋날 수 있다 — 페이즈 포즈 튜닝 시 킥 체감을 함께 확인하고, 어긋나면 "홈⊕비행 후 축" 기준으로 델타 적용 지점을 옮기는 것을 이 유닛에서 결정한다.

## 완료 기준

- EditMode: 보간 순수 함수(`CameraComposeMath.Lerp`) 경계(t=0/1)·중점·오버슈트 통과 테스트. (재타겟 연속성은 MonoBehaviour 상태(`_flightFrom = 현재 평가 델타`)에 있어 순수 함수 테스트 대상이 아님 — Play 검증으로 대체, 리뷰 합의)
- Play(스크립트 배틀 e2e): Draft→Battle→Result 전환에서 스냅 없이 비행, 전환 총시간이 `flightSec`과 일치, 비행 후 포즈 = 홈⊕델타 정확.
- 매치 재시작 시에도 비행이 자연스럽게 이어짐(포즈 점프/잔상 없음), 콘솔 클린.
- 사용자 Play 체감 확인 (속도감/멀미 없음).
