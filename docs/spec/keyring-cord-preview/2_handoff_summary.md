# 2 · Handoff Summary — keyring-cord-preview

## Commit

브랜치 `feature/keyring-cord` (main 미머지). 핵심 커밋:
- `08ac035` 하이라이트를 마우스 안정 위치 기준(최종)
- `716222c` 스프링(탄성) 복원 + maxSpeed 속도상한
- `286d707` 수직 분리 월드up→camUp (겹침 해결)
- `e4c9cc9` 손가락=고리·유닛=보드·하이라이트=유닛 (A안)
- `52f8c9f` Verlet→제어형 스프링 전환(KeyringCord 삭제)
- (폐기 경로: `0f63597`/`2b815db` Verlet)

## Implemented

- 드래그 배치 프리뷰를 키링화: 고리(손가락, 공중) → 줄 → 유닛(보드에 서서 스프링으로 뒤따라 흔들림).
- 고리 위치 = 손가락 ray, 수직 오프셋은 camUp(화면 세로)로 풀어 고리·유닛 화면상 분리.
- 유닛 = 보드 스프링 follow(spring/damping) + maxSpeed 속도상한(빠른 스와이프 튐 방지). 워밍업 없음.
- 배치 하이라이트 = 마우스 바로 아래 고정 칸(스윙하는 유닛 위치 아님) → 배치 정확도.
- 유닛 머리 localBounds 자동정렬(유닛 높이 무관), swingPivot 머리중심 기울임(maxAngle 클램프).
- 줄/고리 = LineRenderer(공유 머티리얼 1개), 줄 폭 sub-pixel 컬링 주의(cordWidth 충분히).

## Key Files

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — 전체 로직(배치/follow/하이라이트/rig).
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` + `Assets/_Project/Data/Config/DragSwaySettings.asset` — 파라미터(ropeLength/maxAngle/spring/damping/maxSpeed/cordWidth/cordColor/ringRadius/charmDrop).
- (`Assets/_Project/Scripts/Presentation/KeyringCord.cs` 는 Verlet 폐기로 삭제됨)

## Verified

- compile 클린, 콘솔 에러 0(반복 확인).
- 좌표 수학 실카메라 검증: 고리 손가락 0px, 머리 고리보다 화면상 ~80px 아래, 발 보드 Y(안 묻힘).
- 사용자 라이브 feel 확정("좋다"/"마무리").

## Notes (되돌리지 말 것)

- **수직 분리는 camUp 기준.** 월드-up 으로 올리면 기울어진 카메라에서 화면상 안 올라가 고리·유닛 겹침.
- **하이라이트는 `_unitTargetWorld`(마우스), 스윙 `_unitPosWorld` 아님.** 배치 정확도 직결.
- **워밍업(가속 램프) 금지.** 억제 후 풀릴 때 큰 탄성 스냅. 대신 maxSpeed 로 튐 제한.
- 실루엣 회전은 swingPivot(머리 중심). 발 중심이면 반대로 흔들림.
- 리컴파일→도메인 리로드가 Play 재시작→보드 미초기화(BoardSpace null)라 라이브 MCP 재현 불가. 좌표는 실카메라+합성 평면으로 검증.

## Follow-up

- 중력 드롭 방식(움직일 땐 붙고 멈추면 툭 떨어짐, 사용자 제안) · 실제 고리/줄 아트 · 줄 sag 곡선 — README 후속 후보.
- main 머지 결정은 사용자(브랜치 격리 중). 머지 시 `docs/spec/README.md` Follow-up Backlog 에 이관.
