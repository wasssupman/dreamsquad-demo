# keyring-cord-preview

> 상태: **완료 2026-07-05** · 브랜치 `feature/keyring-cord` (머지 결정은 사용자)
> 최종 커밋: `08ac035`(마우스 고정 하이라이트). 핵심 구현: `716222c`(스프링+속도상한)·`286d707`(camUp 수직분리)·`e4c9cc9`(손가락=고리 A안).

## 목표

드래그 배치 프리뷰를 **키링**처럼: 상단 **고리(ring, 손가락 위치)** → **줄(cord)** → 아래 **유닛 실루엣**. 유닛은 보드에 서서 무게추처럼 스프링으로 뒤따라오며 흔들린다.

검증 질문: **손가락으로 고리를 잡고 스와이프하면 유닛이 무게추처럼 뒤따라 흔들리되, 배치 칸은 흔들리지 않고 정확한가?**

## 동작 모델 (여러 번 전환 — 최종만 유효)

폐기: 자유 Verlet 로프("너무 줄 같다") → 제어형 스프링 진자(고리=preview 위) → **최종: 손가락=고리 모델**.

**최종 모델:**
- **고리 = 손가락/마우스 위치**(공중). 손가락 ray 를 보드보다 `camUp*(유닛키+줄)` 높은 지점과 교차 → 고리가 손가락 스크린 위치에 뜬다. **수직 분리는 camUp(화면 세로)** 기준(월드-up 은 기울어진 카메라에서 화면상 안 올라가 겹침).
- **유닛 = 보드에 발이 선다**(안 묻힘). 고리 바로 아래 보드점을 목표로, **스프링+감쇠+속도상한**으로 지연 추종 → 무게추처럼 뒤따라오며 흔들림(탄성).
- **하이라이트/배치 칸 = 마우스 위치**. 흔들리는 유닛 위치(`_unitPosWorld`)도, 화면 아래로 매달린 발점(`_unitTargetWorld`)도 아니라 **손가락의 보드 히트**(`_fingerBoardWorld`) 로 칸 산출 → 유닛은 흔들려도 배치 대상은 마우스에 붙는다(배치 정확도).

## 연결 문서

- 전신: `docs/spec/placement-drag-preview-polish/`
- 코드: `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`, `Assets/_Project/Scripts/Data/DragSwaySettings.cs`

## 구현 문서

| # | 문서 | 목적 |
|---|---|---|
| 1 | `1_settings_and_integration.md` | 최종 모델 구현: SO 파라미터 + 컨트롤러(고리/줄/유닛 배치·follow·하이라이트) |
| 2 | `2_handoff_summary.md` | 인계 요약 |

> `0_keyring_cord_solver.md`(Verlet)는 모델 전환으로 삭제됨.

## feature-wide 계약 (load-bearing)

1. **고리 = 손가락**(camUp 기반 ray-plane, `TryComputeRingUnit`). 수직 오프셋은 camUp(화면 세로) — 월드-up 금지(겹침).
2. **유닛 = 보드 위 스프링 follow.** `_unitPosWorld` 가 `_unitTargetWorld`(고리 아래 보드) 를 spring/damping 으로 추종 + `maxSpeed` 속도상한(빠른 스와이프 튐 방지). **워밍업 금지**(가속 억제→풀림 시 큰 스냅).
3. **하이라이트/판정 = `_fingerBoardWorld`(손가락 보드 히트) 칸.** 스윙하는 `_unitPosWorld` 도, 매달린 발점 `_unitTargetWorld` 도 아니다 — 후자로 판정하면 칸이 `totalDrop`(유닛키+줄×visualScale)만큼 화면 아래로 밀려 **보드 상단 N행이 영구 배치 불가**가 된다(2026-07-28 실측: 15×11 맵 상단 3행 + 화면 하단 절반이 row 0 에 뭉침). 프리뷰는 계속 발점을 쓰고 판정만 손가락을 따른다 — 프로젝트의 다른 모든 포인터→셀 경로(`bridge.TryScreenToCell`: armed 보드 드래그·재배치·조준·인스펙트)와 같은 기준. 회귀 가드 = `Tests/PlayMode/DragPlacementReachTest`.
   - 따라서 `ropeLength` 는 **도달성과 무관한 순수 아트 노브**다(판정을 발점에 묶어두면 조작 가능 범위를 결정하는 숨은 노브가 된다). 대가로 하이라이트와 고스트 발이 어긋나므로 값이 커질수록 시각 정합이 나빠진다.
4. **유닛 머리 자동정렬**: `skelRenderer.localBounds` 로 머리를 endNode 에 맞춰 발이 보드에. 머리 = 발+camUp*unitHeight. 기울임은 `swingPivot`(머리 중심, `maxAngle` 클램프).
5. **렌더링**: 줄=`LineRenderer(useWorldSpace,2점)` 고리→머리(폭 sub-pixel 이면 컬링). 고리=로컬 원 루프. 실루엣=`endNode(Billboard)`→`swingPivot`→`spineChild`. root scale 1. 줄/고리 공유 머티리얼 1개(OnDestroy 파괴).
6. **모든 수치 = `DragSwaySettings` SO**: ropeLength/maxAngle/spring/damping/maxSpeed/cordWidth/cordColor/ringRadius/charmDrop. `unscaledDeltaTime`. 스코프 = Spine 드래그 프리뷰만.

## 후속 후보 (현 스코프 밖)

- **중력 드롭 방식** [S] — 움직일 땐 유닛이 손가락에 붙고, 멈추면 중력으로 툭 떨어져 매달리는 물리감(사용자 제안, 스프링 대안).
- **실제 고리/줄 아트 스왑** [S] — 현재 절차적 링 + 단색 LineRenderer.
- **줄 sag 곡선** [S] · **드롭 착지 반동** [S] · **폴백 capsule 로프** [S].
