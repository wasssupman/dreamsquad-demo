# Shared Package

> 상태: **preparing — official package 아님**

Client와 Game Server가 같은 의미를 읽기 위한 transport-neutral package다. Stable ID,
command intent, authoritative state/event/result와 ordering만 담는다.

## 포함

- ID의 안정성과 수명주기 의미
- command intent와 accept/reject/correct 결과 의미
- canonical state/event/result vocabulary와 ordering invariant
- resync/replay가 보존해야 할 semantic fact
- 양쪽 소비자가 공동 검토한 acceptance fixture

## 제외

- Unity, ECS, MonoBehaviour, GameObject, prefab와 asset reference 타입
- wire DTO, serializer, protocol opcode, auth와 transport
- server runtime/tick 구현 선택과 Client frame/VFX timing

Live protocol의 정본은 production ADR gate가 정한다. Shared card는 그 ADR이 보존해야 할
의미를 제공할 뿐 구현 shape를 선결하지 않는다.

## 현재 inventory

- [`cards/unit-lifecycle.md`](cards/unit-lifecycle.md): 첫 pilot semantic vocabulary
- 기존 product/architecture/evidence 문서는 registry가 current로 승격하기 전까지
  Demo source의 `docs/production-transition/README.md`가 분류하는 historical references다.
