# Game Server Package

> 상태: **preparing — official Game Server input/implementation 승인 아님**

Game Server가 production에서 권위 있게 재정의해야 할 gameplay 의미와 acceptance를
축적한다. Demo의 Unity/ECS 구현 shape를 서버 구조로 복사하지 않는다.

## 포함

- canonical rules/config/state와 invariant
- logical command validation과 atomic outcome
- ordering, time/tick, numeric, stable identity, RNG와 score에 필요한 결정
- authoritative acceptance fixture와 unresolved gameplay question

## 제외

- Client presentation, prefab/VFX/UI와 asset 구현
- wire/auth/transport의 최종 선택
- Server implementation plan 자동 승인

## 현재 inventory

- [`cards/unit-lifecycle.md`](cards/unit-lifecycle.md): lifecycle authority pilot
- Demo source의 `docs/production-transition/migration-dossier/`와 record
  `PT-LEGACY-GS-DOSSIER-001`: 13-domain historical/stale preparatory 조사. 이 자료의 예전
  freeze/copy 규칙은 더 이상 정본이 아니며 Server delivery dependency가 아니다.

미래 official destination은
`docs/migration-input/dreamsquad-demo/<freeze-id>/`이며 preparation 중에는 만들지 않는다.
