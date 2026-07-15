# 7 — 드래그 포커스 복귀·추종 튜닝

## 목적

드래그 해제 뒤의 선형적인 카메라 복귀를 더 빠르고 역동적인 감쇠로 바꾸고, 드래그 중 lookat 추종은 더 느리고 안정적으로 만든다.

## 변경 대상

- 수정 `Assets/_Project/Scripts/Presentation/CameraComposeMath.cs`
- 수정 `Assets/_Project/Scripts/Presentation/CameraDirector.cs`
- 수정 `Assets/_Project/Data/Camera/CameraDirectionConfig.asset`
- 수정 `Assets/_Project/Tests/EditMode/CameraComposeMathTests.cs`

## 구현

- 포커스 해제 가중치는 선형 `MoveTowards` 대신 cubic ease-out으로 감쇠한다. 해제 직후 빠르게 홈으로 향하고, 도착 직전에는 감속해 자연스럽게 정착한다.
- 복귀 시간은 `focusFadeOutSec`을 0.35초에서 0.24초로 단축한다.
- NDC lookat 스프링은 최종 `focusSpring` 60→12, `focusDamping` 14→10으로 변경한다. 임계감쇠보다 높은 감쇠로 포인터를 더 천천히 따라가며 급격한 회전을 줄인다.

## 완료 기준

- EditMode: cubic ease-out의 시작·중간·종료 및 범위 클램프 회귀 테스트 통과.
- Play: 드롭/취소 뒤 즉시 반응하면서도 홈 포즈에 부드럽게 정착한다.
- Play: 빠른 스와이프 중 lookat이 포인터를 과도하게 휙휙 따라 돌지 않는다.
- 콘솔 클린, 기존 테스트 통과.

확인 완료: 2026-07-14 사용자 Play 확인 — 빠른 ease-out 복귀와 느린 lookat 추종 통과.
