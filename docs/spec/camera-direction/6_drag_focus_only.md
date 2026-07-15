# 6 — 드래그 포커스만 유지

## 목적

스와이프 드래그 중 포커스 연출만 남기고, 그 밖의 모든 런타임 카메라 연출을 끈다.

## 변경 대상

- 수정 `Assets/_Project/Scripts/Data/CameraDirectionConfig.cs`
- 수정 `Assets/_Project/Data/Camera/CameraDirectionConfig.asset`
- 수정 `Assets/_Project/Scripts/Presentation/CameraDirector.cs`

## 구현

- `enableNonDragEffects` SO 토글을 추가하고 기본 에셋에서는 `false`로 둔다.
- 토글이 꺼져 있으면 페이즈 비행·줌 펄스·킬 스트릭 셰이크·앰비언트 브리딩·임팩트 킥 입력과 잔여 상태를 즉시 무효화한다.
- 드래그 컨트롤러의 `SetDragFocus` 채널과 홈 포즈 캡처/합성은 계속 동작한다. 기존 채널별 수치는 보존해, 필요 시 SO 토글만으로 재활성화한다.

## 완료 기준

- Play: 드래그하지 않을 때 카메라가 씬 authored 홈 포즈에 고정된다.
- Play: 스와이프 드래그 중에는 기존 포커스/복귀가 유지된다.
- 콘솔 클린, 기존 테스트 통과.

확인 완료: 2026-07-14 사용자 Play 확인 — 비드래그 카메라 연출이 꺼지고 드래그 포커스는 유지됨.
