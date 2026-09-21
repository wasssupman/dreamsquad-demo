# Wassup.UnitAi — 유닛 행동 로직 레이어 (아키텍처 무관)

`noEngineReferences: true`. UnityEngine·Entities·MonoBehaviour 를 **컴파일러가 막는다** — `Wassup.Skills` 와 같은 결.

- 여기 있는 것: 「지금 이 유닛이 무엇을 할 수 있나 / 다음에 무엇을 하나」의 **판정**. 입력은 plain 값 스냅샷, 출력은 plain 결정.
- 여기 없는 것: 컴포넌트 읽기·쓰기, 애니메이션, 큐. 그건 적용 레이어(ECS 시스템 / 브리지 / 뷰)가 한다.
- 시작(defender-deploy-phase, 2026-09-21): `UnitActionPhase`(행동 시작 가능 여부) · `DeployPhaseClock`(배치 페이즈 시계).
- 다음(defender-autobattle-ai): `DefenderAiInput → DefenderAi.Decide → DefenderDecision`, 적 FSM `Evaluate` 편입.

⚠ asmdef 는 `Unity.Burst` 를 참조한다(`Wassup.Skills` 와 동일). 엔진 참조가 아니라 **Burst 메타데이터 해시** 때문이다 — 이 어셈블리의 타입(enum 등)이
Burst 컴파일되는 ISystem 의 컴포넌트/호출에 들어가면, 참조가 없을 때 "Could not find type … in Burst.Compiler.IL.AssemblyNameReferenceAndMetadata"
내부 컴파일러 오류로 AttackSystem 이 통째로 깨진다(2026-09-21 실측: 관련 EditMode 25건 빨강).
