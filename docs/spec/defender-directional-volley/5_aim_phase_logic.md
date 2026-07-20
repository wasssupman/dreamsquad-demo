# 5. UX 순수 로직 — DirectionAimLogic

## 목적

공격방향 페이즈의 제스처 해석(데드존/방향 스냅/하이라이트/확정 전이)을 아키텍처를 모르는 순수 static 함수로 만든다. unit 6 의 Mono 컨트롤러는 포인터 좌표를 넣고 상태를 받아 UI 에 반영만 한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/DirectionAimLogic.cs` (신규)
- `Assets/_Project/Tests/EditMode/DirectionAimLogicTests.cs` (신규)

## 구현

plain 값 입출력(float2 픽셀 좌표·임계값 → 방향/전이). UnityEngine.Input·Time·컴포넌트 참조 금지.

- `AimSample Evaluate(float2 pressOrigin, float2 currentPos, float deadZonePx, float2 axisRight, float2 axisUp)` → `{ bool hasDirection; int2 cardinal; }` — 델타 크기 < deadZone 이면 방향 없음, 이상이면 **보드 축 투영에 대한 지배 성분**으로 스냅. 동률은 보드 X 축 우선(결정론 — 테스트로 고정).
- `AimPhaseResult OnRelease(AimSample lastSample)` → `{ bool confirmed; int2 cardinal; }` — 방향 있는 채 릴리즈 = 확정, 데드존 릴리즈 = 미확정(가이드 유지·재스와이프 대기, 계약 9).
- **rev1 (unit 6 구현 시 정정)**: 초안은 "화면 기준 cardinal 반환 + 필요 시 호출부가 변환"이었으나, 그 모델은 **iso 보드에서 깨진다** — 보드 축이 화면 대각으로 투영되면 "화면 위"가 +Y 와 −X 사이에 걸려 어느 레인인지 결정할 수 없다. 대신 **호출부가 보드 +X/+Y 의 화면 투영(정규화)을 넘기고** 로직은 그 축들과 스와이프를 비교해 **보드 cardinal 을 직접** 반환한다. 로직은 여전히 카메라를 모른다(축은 그냥 값). pitch-only 전투 카메라에서는 축이 (1,0)/(0,1) 로 나와 화면 스냅과 동일하게 degenerate.

수치(deadZonePx 등)는 unit 6 의 설정 SO 에서 주입 — 여기는 파라미터일 뿐.

## 완료 기준

- [x] compile + EditMode 테스트 green: 데드존 경계(미만/이상), 4방향 스냅, 대각 동률 규칙, 릴리즈 확정/미확정 전이
- [x] 기존 테스트 회귀 없음

확인 2026-07-16 — EditMode green · 리뷰 통과 · 커밋 80b26662
rev1 2026-07-17 — 축 투영 모델로 정정(iso 대응) + iso 테스트 2건 추가. EditMode 898 green
