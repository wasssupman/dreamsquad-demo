# 5. UX 순수 로직 — DirectionAimLogic

## 목적

공격방향 페이즈의 제스처 해석(데드존/방향 스냅/하이라이트/확정 전이)을 아키텍처를 모르는 순수 static 함수로 만든다. unit 6 의 Mono 컨트롤러는 포인터 좌표를 넣고 상태를 받아 UI 에 반영만 한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/DirectionAimLogic.cs` (신규)
- `Assets/_Project/Tests/EditMode/DirectionAimLogicTests.cs` (신규)

## 구현

plain 값 입출력(float2 픽셀 좌표·임계값 → 방향/전이). UnityEngine.Input·Time·컴포넌트 참조 금지.

- `AimSample Evaluate(float2 pressOrigin, float2 currentPos, float deadZonePx)` → `{ bool hasDirection; int2 cardinal; }` — 델타 크기 < deadZone 이면 방향 없음, 이상이면 지배 축(|dx| vs |dy|)으로 상하좌우 스냅. 대각 동률은 수평 우선(결정론 — 테스트로 고정).
- `AimPhaseResult OnRelease(AimSample lastSample)` → `{ bool confirmed; int2 cardinal; }` — 방향 있는 채 릴리즈 = 확정, 데드존 릴리즈 = 미확정(가이드 유지·재스와이프 대기, 계약 9).
- 화면 스와이프 방향 → 보드 cardinal 매핑: 카메라가 보드를 내려보는 구도라 화면 상하좌우와 보드 축이 1:1 이 기본이되, 변환이 필요하면 **호출부(컨트롤러)가 view→board 변환을 책임**지고 이 로직은 화면 기준 cardinal 만 반환한다 — 로직은 카메라를 모른다.

수치(deadZonePx 등)는 unit 6 의 설정 SO 에서 주입 — 여기는 파라미터일 뿐.

## 완료 기준

- [ ] compile + EditMode 테스트 green: 데드존 경계(미만/이상), 4방향 스냅, 대각 동률 규칙, 릴리즈 확정/미확정 전이
- [ ] 기존 테스트 회귀 없음
