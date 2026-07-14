# 5 — 드래그 포커스 (스와이프 줌인 + 방향 lookat 리드)

## 목적

배치 드래그(스와이프) 중 카메라가 드래그 유닛을 중심으로 줌인 포커스하고, 스와이프 방향으로 시선이 살짝 앞서가는(lookat 리드) 연출. 후속 후보 "드래그 중 카메라 반응"의 승격·확장. README의 탈취 규칙에 세 번째 지시 채널(드래그 포커스)로 편입된다.

## 변경 대상

- 수정 `Assets/_Project/Scripts/Presentation/CameraDirector.cs` — focus 채널 (`SetDragFocus`/staleness 자동 해제)
- 수정 `Assets/_Project/Scripts/Presentation/CameraComposeMath.cs` — `CameraPoseDelta.yawDeg` 축 신설 + `FocusDelta` 순수 함수
- 수정 `Assets/_Project/Scripts/Data/CameraDirectionConfig.cs` + 에셋 — 포커스 파라미터
- 수정 `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 드래그 Update에서 유닛 위치·스프링 속도 피드
- 수정 `Assets/_Project/Tests/EditMode/CameraComposeMathTests.cs`

## 구현

- **입력**: 드래그 컨트롤러 `Update()`(스프링 스텝 후, `_onBoard` 일 때)가 `SetDragFocus(_ringWorld, _unitVelWorld)` 를 매 프레임 호출. **룩앳/줌 타겟 = 포인터(고리) 위치** — 유닛(스프링) 위치가 아님 (rev 1, 사용자 결정: 스프링 흔들림 비전이 + 시선이 손끝 추종). 속도는 스프링 속도. 명시 해제 없이 **프레임 staleness**(마지막 피드로부터 2프레임 초과 시 비활성)로 자동 종료 — 컨트롤러 파괴/세션 정리 누락에도 포커스가 붙박이지 않는다.
- **포커스 델타** (`FocusDelta` 순수 함수, base = 홈⊕비행 위치 기준):
  - dolly: 유닛 방향 단위벡터 × `focusDolly` (홈 축 localPos)
  - lookat: 유닛 방향의 yaw/pitch 풀각 × `focusLookWeight` (부분 블렌드). **lookWeight 는 포인터→카메라 되먹임 루프의 수축 계수** — 정지 포인터의 최종 각 변위 = 오프셋 × w/(1-w) (0.25 → +33%), **w=1 은 발산(무한 회전)**. SO Range·순수 함수 양쪽에서 0.5 캡(리뷰 MAJOR 반영). 하이라이트 셀 체감 튜닝 시 이 비선형 증폭식 기준으로 판단.
  - 스와이프 리드: 스프링 속도를 홈 right/up 에 사영 × `focusLeanPerSpeed`, `±focusLeanMaxDeg` 클램프. 보드 평면상 "위쪽(멀어지는)" 스와이프는 홈 z 성분이 커서 리드가 수평보다 약함(사영 특성) — Play 체감에서 세로 리드가 죽어 보이면 pitch 리드 계수 분리로 조정(후속 튜닝 항목)
  - FOV: `focusFovDelta` (음수=줌인, 최종 FOV 클램프 통과)
- **가중치**: 피드 중 1, 해제 시 0 으로 `focusFadeInSec`/`focusFadeOutSec` MoveTowards. 페이즈 비행 중 목표 0(비행 최우선). 브리딩은 억제하지 않음(진폭 차수가 달라 무시 가능).
- **yaw 축**: `CameraPoseDelta.yawDeg` 신설(홈 up 둘레 회전, Compose 에서 yaw→pitch→roll 순). 기존 채널은 yaw 0.
- **하이라이트 셀 계약 유지**: 배치 대상 칸은 포인터 기준(`UpdateHoverAtTarget`) 그대로 — 카메라 이동이 유닛/하이라이트의 월드 위치를 바꾸지 않는다. 카메라 이동으로 포인터 아래 칸이 미세하게 바뀔 수 있는 것은 진폭 튜닝으로 관리(스펙 상한 = 배치 정확도 체감 훼손 없음).
- **TimeManager 슬로우모 무관**: 전 채널과 동일하게 unscaled 시간.

## 완료 기준

- EditMode: yaw 합성(Compose/Add/Lerp), FocusDelta — 정면 타겟 yaw 0 / 우측 타겟 +yaw / dolly 방향 / 속도 리드 클램프 / 가중 0 항등.
- Play: 드래그 시작 시 줌인 페이드인, 스와이프 방향으로 시선 리드, 드롭/취소 시 부드럽게 복귀(붙박이 없음), 오프보드 드래그 시 포커스 해제.
- 배치 정확도 체감: 하이라이트 칸이 안정적이고 드롭 좌표감이 훼손되지 않음(사용자 확인). 거슬리면 dolly/lookWeight 하향이 1차 조치.
- 콘솔 클린, 기존 테스트 전부 통과.
