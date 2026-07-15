# 5 — 드래그 포커스 (스와이프 줌인 + 방향 lookat 리드)

## 목적

배치 드래그(스와이프) 중 카메라가 드래그 유닛을 중심으로 줌인 포커스하고, 스와이프 방향으로 시선이 살짝 앞서가는(lookat 리드) 연출. 후속 후보 "드래그 중 카메라 반응"의 승격·확장. README의 탈취 규칙에 세 번째 지시 채널(드래그 포커스)로 편입된다.

## 변경 대상

- 수정 `Assets/_Project/Scripts/Presentation/CameraDirector.cs` — focus 채널 (`SetDragFocus`/staleness 자동 해제)
- 수정 `Assets/_Project/Scripts/Presentation/CameraComposeMath.cs` — `CameraPoseDelta.yawDeg` 축 신설 + `FocusDelta` 순수 함수
- 수정 `Assets/_Project/Scripts/Data/CameraDirectionConfig.cs` + 에셋 — 포커스 파라미터
- 수정 `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 드래그 Update에서 포인터 스크린 좌표 피드
- 수정 `Assets/_Project/Tests/EditMode/CameraComposeMathTests.cs`

## 구현

- **입력 = 터치/포인터 스크린 좌표 그대로** (rev 3, 사용자 결정). 드래그 컨트롤러 `Update()`(`_onBoard` 일 때)가 `SetDragFocus(_lastScreenPos)` 를 매 프레임 호출, Director 가 NDC(-1..1)로 정규화. **월드 좌표(유닛/고리) 입력 금지가 계약** — 월드 타겟은 카메라 포즈에 의존해 "카메라 회전→타겟 재계산" 되먹임을 만든다(rev 1~2 시행착오: 유닛=스프링 출렁 전이, 고리=camUp 의존 재계산). 스크린 좌표는 카메라 비의존이라 루프가 원천적으로 없다.
- **추종은 스프링-댐핑** (rev 3, 사용자 결정: "확확" 금지): Director 가 NDC 를 `KeyringSim.SpringStep`(`focusSpring`/`focusDamping`, 임계감쇠≈2√spring 이상이면 무진동)으로 추종. **스프링 속도가 곧 스와이프 리드 속도** — 정지 시 0 수렴, 별도 차분/EMA 불필요. 첫 활성화는 현 포인터에 스냅(스테일 스윙 방지).
- 명시 해제 없이 **프레임 staleness**(마지막 피드로부터 2프레임 초과 시 비활성)로 자동 종료 — 컨트롤러 파괴/세션 정리 누락에도 포커스가 붙박이지 않는다.
- **포커스 델타** (`FocusDelta` 순수 함수 — 입력은 스프링 스무딩된 NDC/NDC 속도 + 홈 FOV/aspect):
  - 포인터 ray 의 홈-로컬 방향 복원: `dir = normalize(ndc.x·tanH, ndc.y·tanV, 1)` (tanV=tan(FOV/2), tanH=tanV×aspect)
  - dolly: `dir × focusDolly` (홈 축 localPos — 포인터 방향 전진)
  - lookat: `dir` 의 yaw/pitch 풀각 × `focusLookWeight` (부분 블렌드). rev 3 부터 되먹임 루프는 없지만(스크린 입력) 각 증폭 상한으로 0.5 캡 유지(풀 lookat 은 배치 좌표감 파괴).
  - 스와이프 리드: NDC 스프링 속도 × `focusLeanPerSpeed`(단위: 도/(NDC/s)), `±focusLeanMaxDeg` 클램프
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

확인 완료: 2026-07-14 사용자 Play 확인 — 드래그 포커스·복귀와 배치 좌표감 통과.
