# Production Game Server Transition Input

> **Dormant · owner-gated · not Demo authority · not Production implementation approval**

Production Game Server는 gameplay ruleset, canonical state와 상태 전이, command validation,
ordering·time·numeric·RNG, score/result의 유일한 실행 권위를 가진다. Demo의 Unity/ECS shape와
Client presentation을 복사하지 않는다.

## 읽기 순서

1. [`../common/README.md`](../common/README.md)
2. [`rules/authority-and-state.md`](rules/authority-and-state.md)
3. [`rules/time-ordering-numeric-rng.md`](rules/time-ordering-numeric-rng.md)
4. [`rules/content-result-and-replay.md`](rules/content-result-and-replay.md)
5. [`domain-coverage.md`](domain-coverage.md)
6. [`plans/implementation-waves.md`](plans/implementation-waves.md)
7. [`plans/acceptance-gates.md`](plans/acceptance-gates.md)

## Production-local 우선순위

Imported 문서는 Somnia Game Server의 master roadmap, AGENTS와 accepted production decision을
override하지 않는다. Production 구현은 `Somnia.Game.Simulation`의 non-Unity, infrastructure-
independent deterministic 경계와 `SessionRuntime` 책임을 현지 정본에서 다시 확인한다.

## 제외

- UnityEngine, ECS component/system/update order와 ScriptableObject 구현
- Client view, prefab, VFX/SFX/UI/camera/haptics timing
- Host/transport/database/topology의 미승인 선택
- Public API/DTO/protocol 또는 production dependency 자동 승인
