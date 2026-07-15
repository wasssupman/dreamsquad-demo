# 4 — Handoff Summary

## Commit

미커밋 (사용자 확인 대기). 변경 표면: 기존 파일 **+125줄 / 삭제 0** + 신규 2파일 + spec 폴더.

## Implemented

- 보드 방어유닛 **press** → **부착 유무와 무관하게 선택**(unit 4, 사용자 결정) + 선택 유닛 쪽 **카메라 줌인**.
- 부착 드림캐쳐(최대 3장)의 성능 텍스트를 유닛 옆 스택 패널로 표시. 부착 0장이면 패널만 생략(선택·줌·슬로우는 유지).
- 패널 열림 중 Battle 도메인 **슬로우 0.3** (`AwakeningConfig.slomoTimeScale`, TimeManager lease priority 50).
- 행 = [미니 아트][이름 + 코스트][`DreamcatcherCardText.Body`]. 타입별 보더(Squad 골드 / Unit 청록 — `DcIconStrip` 색 언어).
- 닫힘 = 재탭(토글) / 다른 유닛 전환 / 빈 보드 / **부착 0장** / 손패 오픈 / 배치 드래그 / 조준 / 페이즈 이탈 / 호스트 사망 / `OnDisable`. 전 경로 lease 해제.
- 앵커 추종은 `LateUpdate`(카메라 포즈는 `CameraDirector(-90)` 가 LateUpdate 에서 확정). 우측 배치 + safe area 좌우 플립 + 상하 클램프 + 카메라 뒤(`z<=0`) 숨김/복귀.
- seam 2점: `DefenderDragPlacementController.IsDragging`, `DefenderSelector.DragController`(컨트롤러가 런타임 `AddComponent` 라 씬 배선 불가 → 수명 소유자 경유).
- ECS 변경 0 · 채널 0 · 신규 에셋 0.

## Key Files

- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — 입력/선택/슬로우. 보드 raw 탭의 **단일 소비자**.
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectPanelView.cs` — UGUI 패널(sortingOrder 9). `Entity`/`BattleBridge` 를 모른다.
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` / `DefenderSelector.cs` — seam 1줄씩.
- `Assets/_Project/Scripts/Presentation/CameraDirector.cs` — 인스펙트 포커스 채널(unit 4). `Assets/_Project/Scripts/Data/CameraDirectionConfig.cs` + `Assets/_Project/Data/Camera/CameraDirectionConfig.asset` — 튜닝값.
- `Assets/_Project/Scenes/BattleScene.unity` — `DcInspect`(배선 8점) / `DcInspectPanel`(배선 1점).

## Verified

- compile 클린 · 콘솔 에러 0 (Play 진입/이탈 teardown 포함).
- Play e2e 자율 검증(1920x1080/sf=1): 탭→패널+`ScaleOf(Battle)` 1→0.3 / 내용 정확 / 본문 줄바꿈 / 행 높이 내용 반응.
- **lease 대칭 = 누수 0** — 전 닫힘 경로에서 priority-50 요청 수 1→0 실측(`TimeManager._requests` 직접 계수).
- M1 수정 실증: `z<=0` → 루트 비활성 → 정상 카메라 → **복귀**.
- **unit 4 줌 실측**: 부착 0장 유닛도 선택됨(`cards=0` 인데 `selected` 세팅) · dolly 실측 4.00(SO 값과 일치) · fov 43→41(클램프 경계) · 유닛 중앙거리 589px→297px · **패널이 줌 중에도 유닛 추종**(패널 x = 유닛 x + 46) · 닫힘 시 홈 완전 복귀(weight 0, camDist 0, fov 43) · **컨트롤러 강제 비활성화 시 줌 자동 해제**(staleness 안전망).
- UI 가드 결백 실증: 유닛 위 `IsOverUi=False`, `RaycastAll` 히트 0 — `RaycastAll` 로 바꾼 가드가 보드 탭을 막지 않는다.
- 씬 diff 112줄 중 109줄이 신설 GO, 3줄은 무관 재직렬화(동작 중립 — unit 3 참조).

## Notes (되돌리지 말 것)

1. **`EventSystem.IsPointerOverGameObject()` 로 되돌리지 마라.** 그 API 는 `EventSystem.Update`(순서 0)가 세운 **지난 프레임** 상태를 읽는데 이 컨트롤러는 -50(계약 4) 이라 먼저 돈다. 터치는 hover 가 없어 press 프레임에 pointer 상태가 없다 → 항상 false → **UI 를 눌러도 뒤 보드가 선택되는 Android 전용 결함**. 마우스는 hover 잔상에 가려 에디터에선 재현 불가. `PlacementInput.cs:63~65` 가 같은 패턴이지만 클릭 배치가 은퇴해 검증된 적 없다 — 선례로 삼지 마라. (README 계약 5b)
2. **`GameManager.AimCanceled` 는 죽은 코드다** (발행자·구독자 0). `GameManager.cs:62~66` 주석이 살아있는 배타 버스처럼 서술하지만 미구현 설계 의도다. 설계 rev 1 이 여기 걸려 REVISE 받았다.
3. **`DcInspectPanelView.LateUpdate` 게이트에 `_root.activeSelf` 를 넣지 마라.** `z<=0` 경로가 루트를 끈 순간 영구 early-return 이라 복구선이 dead 가 되고, 패널은 사라진 채 슬로우만 남는다(코드리뷰 M1).
4. **TMP 는 활성 상태로 측정하라.** 비활성 계층 `AddComponent` 는 Awake 를 안 돌려 `textWrappingMode=NoWrap`(enum 기본 0) + `GetPreferredValues` 가 1/10 을 답한다. `Show` 가 측정 전 `SetActive(true)` + `ForceMeshUpdate()` 하는 이유. `wasHidden` 플래그도 그래서 필요하다.
5. **`panel?.` 금지** — `?.` 는 Unity 수명 인지 `==` 를 우회한다. `!= null` 로.
6. 뷰는 `Entity`/`BattleBridge`/`DreamcatcherHandController` 를 모른다 — 컨트롤러가 앵커/코스트를 해석해 plain 값으로 넘긴다(`DcIconStripSpawner`→`DcIconStripView` 선례).
7. 스트립(월드 SR)과 패널(UGUI)은 **공존**한다 — 스트립=무엇이 붙었나(어포던스), 패널=그게 뭘 하나(상세).
8. **줌 NDC 는 홈 포즈 기준으로 뽑는다**(`CameraDirector.SetInspectFocus`). 현재 포즈로 뽑으면 카메라가 다가갈수록 NDC 가 0 으로 줄어 오프셋이 사라지고 다시 벌어지는 **진동**이 된다. 홈 포즈는 고정이라 그 루프가 없다.
9. **줌은 `config.enableNonDragEffects` 에 묶이지 않는다.** 그 토글은 앰비언트 연출(킥/펄스/브리딩/비행) 억제용이고 **현재 에셋에서 꺼져 있다** — 묶으면 기능이 조용히 죽는다.
10. **줌의 실질은 dolly 다. FOV 는 장식이다** — 에셋 `fovMin=41`, 홈 FOV ≈43 이라 `Compose` 클램프가 남기는 여유가 **2도뿐**(실측). `inspectFovDelta` 에 큰 음수를 적어도 조용히 -2 로 깎이므로 실효치와 같은 수를 적어둔 것이다. fovMin 은 기존 안전 계약이라 이 spec 에서 건드리지 않았다.
11. `CameraDirector` 가 카메라 포즈의 **유일한 쓰기 주체**다. 컨트롤러는 타겟만 피드하고 카메라를 직접 만지지 않는다.

## Follow-up

- **사용자 Play 확인 필요**(에이전트 판정 불가): 실기기 탭 체감·슬로우 0.3 체감·포탈 2탭 직후 미개방(계약 4)·카드 부착 touchup 직후 미개방(계약 3)·카메라 킥 중 미끄러짐. 상세는 `3_wiring_play_validation.md`.
- **Jua SDF 폰트 아틀라스 글리프 누락** — Play 스크린샷에서 "요새처럼 버**□**다"(`틴` 없음). 이 spec 과 무관한 기존 폰트 커버리지 문제지만 이 패널에서 드러났다. 별도 처리 필요.
- 해상도/노치는 손계산으로 위험 낮음 확인(unit 3) — 실기기 육안 확인으로 갈음.
- 나머지 후속(트리거 진행도 뱃지 · 배치 유닛 범위 표시 · 유닛 스탯 병기 · 세로 오버플로 고도화)은 README "스코프 밖 / 후속 후보".

## 이 spec 에서 나온 durable 지식

`docs/reference/lessons/` 로 승격됨 (프로젝트 규범: durable 지식은 lessons 먼저):

- `01-unity-mcp-operation.md` — execute_code 의 `Screen.*` 는 에디터 창(게임뷰는 `cam.pixelRect`) · Overlay 캔버스는 카메라 스크린샷에 안 잡힘(`ScreenCapture.CaptureScreenshot`) · 스크립트 배틀은 캡처 중 끝남(배틀 클럭 스톨)
- `03-rendering-assets.md` — 비활성 계층 TMP 초기화 함정 · `IsPointerOverGameObject` 실행 순서 함정
