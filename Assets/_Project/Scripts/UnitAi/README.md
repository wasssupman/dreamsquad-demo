# Wassup.UnitAi — 유닛 행동 로직 레이어 (아키텍처 무관)

`noEngineReferences: true`. UnityEngine·Entities·MonoBehaviour 를 **컴파일러가 막는다** — `Wassup.Skills` 와 같은 결.

- 여기 있는 것: 「지금 이 유닛이 무엇을 할 수 있나 / 다음에 무엇을 하나」의 **판정**. 입력은 plain 값 스냅샷, 출력은 plain 결정.
- 여기 없는 것: 컴포넌트 읽기·쓰기, 애니메이션, 큐. 그건 적용 레이어(ECS 시스템 / 브리지 / 뷰)가 한다.
- 시작(defender-deploy-phase, 2026-09-21): `UnitActionPhase`(행동 시작 가능 여부) · `DeployPhaseClock`(배치 페이즈 시계).
- 다음(defender-autobattle-ai): `DefenderAiInput → DefenderAi.Decide → DefenderDecision`, 적 FSM `Evaluate` 편입.
