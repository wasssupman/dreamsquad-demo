# Production Client Transition Input

> **Dormant · owner-gated · not Demo authority · not Production implementation approval**

Production Client는 사용자 intent와 즉시 feedback을 소유하고, authoritative Server 결과를
화면·소리·촉각으로 해석한다. Gameplay 판정과 canonical state mutation은 소유하지 않는다.

## 읽기 순서

1. [`../common/README.md`](../common/README.md)
2. [`rules/authority-and-projection.md`](rules/authority-and-projection.md)
3. [`rules/presentation-and-catalog.md`](rules/presentation-and-catalog.md)
4. [`demo-experience-map.md`](demo-experience-map.md)
5. [`plans/implementation-waves.md`](plans/implementation-waves.md)
6. [`plans/acceptance-gates.md`](plans/acceptance-gates.md)

## Production-local 우선순위

Imported 문서는 Somnia Client의 accepted ADR, task router, current project facts와 validation
matrix를 override하지 않는다. Production 구현은 native Android/iOS 2D mobile, non-ECS,
feature-first/asmdef boundary와 Addressables 기본 규칙을 현지 정본에서 다시 확인한다.

## 제외

- Damage/death/score/terminal과 ordering의 authoritative 판정
- Server runtime, persistence, anti-cheat와 topology
- Wire/auth/transport 또는 public DTO의 최종 선택
- Demo Unity scene, ECS bridge, prefab 또는 code의 복사 지시
