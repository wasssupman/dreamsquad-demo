# Common Rules

> 두 consumer bundle에 byte-identical하게 들어가는 공통 의미다. 독립 consumer package가 아니다.

## 목적

Production Client와 Game Server가 runtime·transport와 무관하게 동일하게 해석해야 할 identity,
intent/result, authoritative state/event, ordering, correction과 versioning 규칙을 정의한다.

## 읽기 순서

1. [`rules/authority-identity-and-results.md`](rules/authority-identity-and-results.md)
2. [`rules/ordering-resync-and-versioning.md`](rules/ordering-resync-and-versioning.md)

## 제외

- Unity/ECS/MonoBehaviour/GameObject/asset reference 타입
- Server domain object, database, tick scheduler 구현
- Client render state, prefab, animation, VFX/SFX/UI timing
- Wire DTO, opcode, serializer, authentication과 transport

Production protocol은 이 의미를 보존해야 하지만 이 문서가 protocol을 승인하지 않는다.
