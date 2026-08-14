# Game Server Migration Dossier — Legacy Preparation Index

> **DORMANT · OWNER-GATED · NOT DEMO AUTHORITY.** Project owner의 명시적 transition 활성화 전에는 Demo 설계·작업 후보·검증 gate로 사용하거나 갱신하지 않는다.

> 상태: **historical · stale · preparatory**
>
> 근거 기준: `2d35df0680ce97d29b78101120cb9fae63c5a8ad`
>
> 전역 정본: [`../README.md`](../README.md)

## 목적과 현재 권위

이 폴더는 Demo의 gameplay 의미를 13개 Game Server 영역으로 조사하기 시작한 기존 자료를
보존한다. 유용한 source pointer와 미결 질문을 제공하지만 다음을 승인하지 않는다.

- 현재 Demo gameplay 또는 production gameplay contract
- official freeze/export 범위
- Server runtime, protocol, tick, numeric, ID, RNG 또는 implementation activation
- Client/Shared package의 누락을 정당화하는 server-only copy

예전 charter의 `conditional`, accepted gap, dossier-only freeze/copy와 re-freeze 규칙은
폐기됐다. 공식 transition에는 [`../governance/registry.json`](../governance/registry.json)과
전역 strict gate만 적용한다.

## 보존 구조

```text
migration-dossier/
  README.md
  coverage.md
  decisions.md
  review-ledger.md
  cards/
    _template.md
    core-combat-lifecycle.md
```

Preparation 중에는 이 폴더나 다른 곳에 `freezes/`를 만들지 않는다. 미래 snapshot은
전역 정본의 `freezes/<freeze-id>/{manifest,shared,client,game-server,references}` 구조로
단 한 번 publish한다.

## 13개 legacy area

| area_id | gameplay_area |
|---|---|
| `MIG-AREA-001` | Match lifecycle와 terminal |
| `MIG-AREA-002` | Time, ordering, numeric, identity와 randomness 의미 |
| `MIG-AREA-003` | Map, path와 occupancy |
| `MIG-AREA-004` | Spawn과 wave |
| `MIG-AREA-005` | Unit movement와 breach |
| `MIG-AREA-006` | Targeting과 attack |
| `MIG-AREA-007` | Damage, heal과 death |
| `MIG-AREA-008` | Projectile, effect, status와 hazard |
| `MIG-AREA-009` | Placement, relocation과 facing |
| `MIG-AREA-010` | Resource, cost와 cooldown |
| `MIG-AREA-011` | Card와 skill |
| `MIG-AREA-012` | Mode와 content rule |
| `MIG-AREA-013` | Score와 result |

`coverage.md`의 모든 행은 legacy candidate다. 현재 13개 전체를 backfill하지 않는다. Demo의
활성 spec 변경과 production-v1 위험이 연결된 area 하나만 새 package card와 registry record로
옮겨 검증한다. 첫 사례는 기존 core card를 자동 승격하지 않고 새 `unit-lifecycle` pilot으로
분리한다.

## Legacy card 해석

기존 card의 `coverage: complete`는 문서 범위만 뜻하며 approval/readiness가 아니다.
`migration_readiness: conditional`도 strict gate에서는 `blocked`와 같다. Card의 관찰 사실과
production 이식 결정을 한 record에 섞지 않고 새 registry에서는 각각 별도 ID·owner·review를
갖게 한다.

새 Game Server package card는 [`../game-server/`](../game-server/README.md)에 작성한다.
Legacy card는 source pointer로 참조할 수 있으나 `as_of_commit` 이후 `watch_paths`를 검토하고
current로 재작성하기 전에는 export할 수 없다.

## Decision 계약

[`decisions.md`](decisions.md)의 기존 `open`, `proposed`, `deferred`는 모두 미결이다.
`decision: none`을 accepted gap으로 승인해 readiness를 우회할 수 없다. 공식 include를
해제하는 값은 owner가 현재 source와 영향 범위를 확인해 전역
[`decisions.json`](../governance/decisions.json)에 기록한 `decided`뿐이다.

질문 의미가 바뀌면 ID를 재사용하지 않는다. 새 decision ID를 만들고 영향 card/record를
`stale`로 전환한다.

## Review 계약

Approval key는 다음 4-tuple이다.

```text
(area_id, card_id, document_revision, source_commit)
```

한 card가 여러 area를 다루면 area별로 required reviewer 승인을 따로 기록한다. Review request,
defer 또는 repository owner의 진행 지시는 gameplay 의미 승인이 아니다. 기존
`MIG-REVIEW-001`은 `legacy_reviews`의 non-approving history로만 보존한다.

## 장기 preparation workflow

1. Gameplay 결과에 영향을 주는 활성 Demo spec 변경을 registry `watch_paths`와 대조한다.
2. 영향 record를 `stale`로 전환한다.
3. 가장 위험한 area 하나의 server/shared/client card와 decision을 갱신한다.
4. area/revision/source별 owner review를 기록한다.
5. preparation validator와 package dry-run을 수행한다.
6. Server 저장소에는 중간 snapshot, weekly bundle 또는 intake ledger를 만들지 않는다.

## 미래 one-time cutover

Product가 production-v1 include/exclude 목록을 잠근 뒤 전역 정본의 절차만 사용한다.

1. clean source commit에서 모든 include record의 `complete/current/reviewed/ready`와
   `decided` blocker를 확인한다.
2. temp dry-run으로 closure, target collision, SHA-256과 Shared byte identity를 검증한다.
3. 하나의 freeze ID와 immutable bytes를 한 번 publish한다.
4. Client는 `shared+client`, Server는 `shared+game-server`를 같은 event에서 받는다.
5. 중단 시 같은 freeze ID/bytes만 재개한다.
6. 이후 오류는 production errata/ADR/change control로 처리하며 Demo re-freeze/re-export는
   하지 않는다.

Game Server 구현은 import와 별개의 사용자 승인 아래 여러 implementation wave로 진행한다.

## Preparation 완료 기준

- 13개 legacy area가 삭제되지 않고 실제 상태를 유지한다.
- current package로 옮긴 record만 새 schema와 exact review key를 사용한다.
- runtime/test/gameplay data/scene/project setting을 변경하지 않는다.
- official freeze나 production input을 만들지 않는다.
- repository Markdown, JSON, UTF-8와 whitespace 검사를 통과한다.
