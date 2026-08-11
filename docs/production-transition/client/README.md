# Client Package

> 상태: **preparing — official Client input/adoption 아님**

Client가 사용자 intent를 받고 authoritative 결과를 화면에 해석하는 계약을 축적한다.
Gameplay 결과를 판정하거나 canonical state를 소유하지 않는다.

## 포함

- 사용자 입력 intent, pending/accepted/rejected UX
- authoritative projection과 correction/resync/reconnect 표현
- cue deduplication, playback와 stable ID→asset/catalog mapping
- presentation-only acceptance와 product 이해 검증 요구

## 제외

- 데미지, death, score, terminal과 ordering의 authoritative 판정
- production wire/auth/transport와 final API
- 실제 Unity scene/prefab/VFX/runtime 구현

## 현재 inventory

- [`cards/unit-lifecycle.md`](cards/unit-lifecycle.md): lifecycle projection/cue pilot
- Somnia Client의 기존 `docs/demo-migration/` 문서는 target-side preparatory proposal다.
  미래 official destination은
  `docs/migration-input/dreamsquad-demo/<freeze-id>/`이며 지금 생성하지 않는다.
