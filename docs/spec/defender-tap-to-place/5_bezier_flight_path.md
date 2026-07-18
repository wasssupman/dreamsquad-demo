# 5 · 베지어 곡선 비행 경로 (bezier flight path)

## 목적

탭 배치 시뮬 비행이 트레이→타일을 **거의 직선**(`Vector3.Lerp`)으로 이동하던 것을,
**2차 베지어 곡선**으로 바꿔 유닛이 아치를 그리며 날아가게 하고, **매 탭마다 다른 경로**가 되게 한다.

검증 질문: 여러 번 탭 배치했을 때, 유닛이 매번 조금씩 다른 곡선(좌/우로 휘는 아치)으로 날아가
목표 칸에 정확히 안착하는가?

## 변경 대상

- `Assets/_Project/Scripts/UI/KeyringSim.cs` — 순수 베지어 평가 함수 추가
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — 아치/좌우 튜닝 SO 필드
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `RunSimulatedDrag` 곡선 구동
- `Assets/_Project/Tests/EditMode/KeyringSimTests.cs` — 베지어 회귀 테스트

## 구현

1. **`KeyringSim.QuadraticBezier(Vector3 a, Vector3 control, Vector3 b, float t)`** — 순수 static.
   `u=1-t; u²a + 2ut·control + t²b`. 좌표계 비의존(월드 발점). endpoints 정확(t=0→a, t=1→b) → 착지 오차 0.
2. **`DragSwaySettings`** (탭 배치 시뮬레이션 헤더):
   - `tapArcHeightFactor`(기본 0.32, `Range(0,1)`) — 제어점을 카메라-up 으로 직선거리×이만큼 띄움(아치 높이).
   - `tapArcLateralFactor`(기본 0.22, `Range(0,1)`) — 보드 좌우 변주 최대폭(직선거리 배수).
3. **`RunSimulatedDrag`**:
   - `startFeet/endFeet/boardN` 확정 후, 루프 전에 제어점 계산 —
     `mid = (startFeet+endFeet)/2`, `flightDist = |end-start|`.
     좌우 변주는 **결정론적 저불일치 수열**(황금비): `_tapFlightSeq` 증가 → `frac((seq+0.5)·φ)·2−1 ∈ [−1,1]`.
     (RNG 아님 — 비행은 프레젠테이션이라 전투-시뮬 결정론 규칙 밖이지만, 프로젝트의 index 기반 결정론 관례를 따른다.
      매 탭 부호·크기가 달라 경로가 매번 다름.)
     `control = mid + camUp·(flightDist·height) + boardRight·(flightDist·lateral·lateralUnit)`.
     `boardRight = ProjectOnPlane(camT.right, boardN)`; 퇴화(‖‖≈0) 시 좌우 생략.
   - 루프 내부: `Vector3.Lerp(startFeet, endFeet, e)` → `KeyringSim.QuadraticBezier(startFeet, control, endFeet, e)`.
     `e`(OutCubic 이징)는 그대로 — 곡선은 경로, 이징은 속도 프로파일로 분리.
   - 루프 후 `_unitTargetWorld/_ringWorld` 를 `endFeet` 기준으로 세팅 → 정확 안착 유지(기존).
4. **필드**: `private int _tapFlightSeq;` — 탭 비행 좌우 변주 인덱스.
5. **테스트**: `QuadraticBezier` endpoints(t=0/1) + midpoint(0.25a+0.5c+0.25b) 검증.

주의: 곡선은 발점(feet) 경로에 적용 → 유닛·고리·줄 전체가 아치. unit 4 의 포커스 고정과 독립
(포커스는 `targetCell` 직접 사용, 비행 경로와 무관). 스프링 추종이 코너를 살짝 컷 하는 건 의도된 관성.

## 완료 기준

- 컴파일 클린 + EditMode `KeyringSimTests` 그린(베지어 케이스 포함).
- Play(에디터): 반복 탭 배치 시 매번 다른 곡선 아치, 목표 칸 정확 안착.
- 아치/좌우 폭이 `DragSwaySettings.asset` 인스펙터에서 라이브 튜닝됨(0 이면 직선 회귀).

> **확인 2026-07-18**: 컴파일 클린 · EditMode 927(925 pass/0 fail, 베지어 테스트 포함) · 코드리뷰 clean · 사용자 승인 커밋. Play 체감은 사용자 확인.
