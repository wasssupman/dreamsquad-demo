# Dreamsquad Demo → Game Server Migration Dossier Charter

상태: **장기 preparation charter — migration initiative 미시작**

## 1. 목적과 실행 경계

이 charter는 계속 개발되는 demo가 훗날 서버 권위 Simulation 이관에 필요한
gameplay 의미를 장기간 준비하도록 한다. Preparation은 demo 저장소 안에서만
진행한다. Game Server는 준비 중간 결과를 감시·복사·intake하지 않는다.

실제 migration/import는 사용자가 별도 initiative 시작을 명시적으로 승인한 뒤
한 번 시작한다. Initiative kickoff에서 하나의 cutover를 고정하고 완성된 dossier
전체를 Game Server로 한 번만 복사한다. Dossier review와 실제 Simulation 구현
activation은 서로 다른 승인 단계다.

이 charter에 따른 작업은 gameplay 구현을 바꾸지 않는다. Demo의 일반 개발은
별도 승인과 change set으로 계속될 수 있지만 dossier 작업과 섞지 않는다.

## 2. Canonical dossier 구조

이 문서는 demo에서 다음 위치의 `README.md`로 byte-for-byte 복사한다.

```text
docs/production-transition/migration-dossier/
  README.md
  coverage.md
  decisions.md
  review-ledger.md
  cards/
    _template.md
    core-combat-lifecycle.md
```

`freezes/`는 preparation 중에는 만들지 않는다. 사용자가 migration initiative를
시작한 뒤에만 다음 단일 freeze artifact를 만든다.

```text
docs/production-transition/migration-dossier/
  freezes/
    MIG-FREEZE-YYYYMMDD-<sha8>.md
```

이 디렉터리 밖의 runtime source, 기존 test, gameplay data, scene, project 설정과
기존 gameplay spec은 dossier 작업으로 수정하지 않는다. Client framework,
toolchain, asset, presentation, 내부 실행 구조, raw source inventory와 file
provenance는 dossier에 포함하지 않는다. Server API, DTO, protocol, numeric type,
tick rate, ID allocator, RNG와 state-hash algorithm도 여기서 설계하지 않는다.

## 3. `coverage.md` 인터페이스와 초기 domain

`coverage.md`는 dossier의 전체 범위 index다. 다음 열을 정확히 사용한다.

| 필드 | 값 |
|---|---|
| `area_id` | 영구 ID, 재사용 금지 |
| `gameplay_area` | 아래 candidate domain 중 하나 |
| `disposition` | `candidate`, `include`, `defer`, `exclude` |
| `card` | 관련 card 상대 경로 또는 `none` |
| `coverage` | `none`, `partial`, `complete` |
| `review_status` | `draft`, `review_requested`, `reviewed`, `stale` |
| `migration_readiness` | `blocked`, `conditional`, `ready` |
| `depends_on` | 관련 `area_id` 또는 `none` |
| `blocking_decisions` | 관련 `decision_id` 또는 `none` |
| `as_of_commit` | 해당 판단의 source commit |
| `next_trigger` | 다음 갱신 조건 또는 `none` |

Preparation 시작 시 다음 13개 domain을 빠짐없이 한 행씩 만든다. 일부 domain만
먼저 깊게 검토하더라도 나머지를 누락하지 않고 `candidate`와 실제 coverage 상태로
남긴다.

| `area_id` | `gameplay_area` |
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

`disposition` 변경은 관련 card와 decision 근거 없이 수행하지 않는다. `defer`와
`exclude`도 검토 결과이며 누락을 뜻하지 않는다. Material gameplay 변경이 있으면
관련 행을 `stale`로 바꾸고 재검토 전에는 `ready`로 표시하지 않는다.

## 4. Card 인터페이스

`cards/_template.md`와 모든 실제 card는 다음 front matter 필드를 정확히 사용한다.

```yaml
card_id:
title:
domain:
status: draft # draft | review_requested | reviewed | stale | frozen | superseded
coverage: partial # partial | complete
migration_readiness: blocked # blocked | conditional | ready
as_of_commit:
supersedes:
depends_on:
```

본문 섹션은 다음 순서와 제목을 정확히 사용한다.

```markdown
## Scope와 non-goals
## Gameplay rule statements
## Authoritative state 후보와 invariants
## Logical inputs, validation과 atomic effects
## Ordering, timing, numeric과 randomness 의미
## Boundary 및 acceptance cases
## Mode와 content variants
## Dependencies
## Open decisions
## References
## Readiness checklist
```

Gameplay rule statement마다 `intended`, `incidental`, `unknown`, `conflict` 중 하나를
표시한다. `observed`를 곧바로 `intended`로 승격하지 않는다. `unknown`과
`conflict`는 새 규칙을 추측하지 않고 `decisions.md`의 blocking question으로
연결한다.

`cards/core-combat-lifecycle.md`는 첫 실제 card다. Match 시작과 terminal, spawn,
movement와 breach, targeting과 direct attack, damage와 death가 결합되는 최소 core
flow를 다룬다. 이 card 하나가 관련 domain 전체의 review를 대신하지 않는다.

## 5. `decisions.md` 인터페이스

`decisions.md`는 다음 필드를 정확히 사용하는 current-state decision register다.
상태가 바뀌면 같은 decision 행을 갱신하며 Git history가 변경 이력을 보존한다.
의미가 다른 별도 결정에만 새 ID를 부여한다.

| 필드 | 값 |
|---|---|
| `decision_id` | 영구 ID, 재사용 금지 |
| `status` | `open`, `proposed`, `decided`, `deferred` |
| `domain` | 관련 candidate domain |
| `question` | owner가 결정해야 하는 기술 중립 질문 |
| `decision` | 결정 내용, 미결이면 `none` |
| `affected_cards` | 관련 card ID |
| `blocks_readiness` | `true` 또는 `false` |
| `owner` | 결정 owner |
| `as_of_commit` | 판단 기준 source commit |

결정 질문의 의미를 조용히 바꾸지 않는다. 질문 자체가 대체되면 새
`decision_id`를 만들고 영향받는 card를 `stale` 또는 `superseded`로 전환한다.

## 6. `review-ledger.md` 인터페이스

`review-ledger.md`는 domain-by-domain owner review를 기록하는 append-only
ledger다. 다음 필드를 정확히 사용한다.

| 필드 | 값 |
|---|---|
| `review_id` | 영구 ID, 재사용 금지 |
| `card_id` | 검토한 card ID |
| `card_revision` | `<card_id>@<review-candidate-commit>` 형식의 검토 문서 revision |
| `as_of_commit` | card가 설명하는 gameplay source commit |
| `from_status` | review 전 card status |
| `to_status` | review 후 card status |
| `reviewed_by` | reviewer |
| `summary` | 승인, 수정 요구 또는 보류 요약 |
| `supersedes` | 대체한 review ID 또는 `none` |

여러 domain을 한 번에 암묵적으로 승인하지 않는다. 각 domain은 coverage row와
관련 card를 연결하고 독립적으로 review를 요청한다. Review되지 않은 card는
`migration_readiness: ready`가 될 수 없다.

`review-candidate-commit`은 owner에게 최종 문구를 제시하기 직전, 정규화된 card와
decision 문서를 담아 demo 저장소에 만든 local commit이다. 같은 gameplay
`as_of_commit`을 유지한 채 문서를 여러 번 고칠 수 있으므로 source revision과
reviewed document revision을 같은 SHA로 취급하지 않는다. Review 요청 자체는
owner review event가 아니며, owner가 해당 문구를 확인하기 전에는 ledger 행을
추가하지 않는다.

## 7. 장기 preparation workflow

첫 dossier 개선에서는 `_template.md`, 전체 `coverage.md`, `decisions.md`,
`review-ledger.md`와 재검증한 `core-combat-lifecycle.md`까지만 작성한다. 나머지
domain card를 일괄 backfill하지 않는다.

1. Gameplay 결과에 영향을 주는 demo spec이 안정되면 관련 coverage row와 card를
   갱신한다.
2. 새 card는 `draft`로 시작하고 의미, dependency와 open decision이 정리되면
   `review_requested`로 바꾼다.
3. Owner는 domain 단위로 의미를 검토한다. 승인하면 `reviewed`, 관련 spec이 나중에
   바뀌면 즉시 `stale`로 전환한다.
4. Coverage가 완전해도 결정 공백이 있으면 readiness는 `blocked` 또는
   `conditional`로 유지한다.
5. 기존 card 범위의 변경은 현재 card를 갱신한다. 책임이 독립 domain으로 분리될
   때만 새 card를 만든다.
6. Presentation-only spec은 dossier 대상이 아니다.
7. Git history가 준비 중 semantic 변경 이력을 보존하므로 별도 semantic delta
   chain을 만들지 않는다. `review-ledger.md`에는 실제 owner review event만 append한다.
8. Dossier는 demo 안에서만 유지한다. 정기 snapshot, weekly bundle, exporter,
   중간 server copy와 intake ledger를 만들지 않는다.

Gameplay 의미에 영향을 주는 승인된 spec 변경 또는 사용자 요청이 있을 때만 관련
domain을 갱신한다. Dossier 준비는 server feature 계획 시점에 맞춘 just-in-time
작업이 아니라, demo 개발과 함께 장기간 단계적으로 coverage를 완성하는 작업이다.

## 8. One-time freeze와 dossier copy

다음 절차는 사용자가 migration initiative 시작을 명시적으로 승인한 뒤에만
수행한다.

1. Initiative 범위의 모든 `include` domain이 `coverage: complete`,
   `review_status: reviewed`인지 확인한다.
2. 각 included domain을 `ready` 또는 owner가 가정과 gap을 승인한 `conditional`로
   확정한다. `blocked` domain은 `defer` 또는 `exclude`로 바꾸지 않는 한 포함할 수
   없다.
3. Blocking decision을 해결하거나 accepted gap으로 owner가 명시 승인한다.
4. 하나의 clean cutover commit을 고정한다.
5. `freezes/`를 만들고
   `freezes/MIG-FREEZE-YYYYMMDD-<sha8>.md` 한 파일에 다음을 기록한다.
   - freeze ID와 cutover commit
   - included, deferred와 excluded domain
   - card ID와 frozen revision
   - dependency와 readiness
   - accepted assumption과 gap
   - open 또는 deferred decision
   - owner 승인
   - freeze 이후 변경 처리 규칙
6. Freeze 대상 card를 `frozen`으로 전환하고 review-ledger에 전이를 기록한다.
7. 다음 dossier 전체를 한 번만 복사한다.

```text
demo:   docs/production-transition/migration-dossier/
server: docs/migration-input/dreamsquad-demo/<freeze-id>/
```

8. 복사 전후 상대 경로와 file bytes가 같은지 검증한다.
9. Game Server에서는 copied dossier를 Phase 2 계약 review 입력으로만 사용한다.
   Copy가 계약 승인이나 implementation activation을 자동 수행하지 않는다.

Cutover 뒤 demo 변경은 copied dossier에 자동 동기화하거나 부분 update하지 않는다.

- Implementation 실행 전 frozen 의미의 치명적 오류가 발견되면 initiative를
  일시 중단한다. Owner가 명시적으로 승인한 re-freeze는 새 freeze ID로 이전 freeze와
  server input을 supersede하며, 변경 이유와 대체 관계를 두 freeze에 기록한다.
- Implementation 실행이 시작된 뒤의 신규·변경 기능은 현재 migration에 편입하지
  않고 일반 후속 서버 개발로 관리한다.
- Presentation-only 변경은 frozen gameplay baseline에 영향을 주지 않는다.

## 9. Game Server one-time migration 실행 계획

Frozen dossier가 복사된 뒤 Game Server는 별도의 one-time migration execution
plan을 작성한다. 이 charter와 dossier copy는 그 실행 계획이나 implementation을
자동 승인하지 않는다.

실행 계획은 다음 순서로 work package를 구성한다.

1. 공통 용어와 blocking decision
2. Time, numeric, identity와 randomness policy
3. 독립 state와 pure rule
4. Ordering과 복합 state transition
5. Logical command와 validation
6. Terminal, score와 result
7. Slice 통합과 acceptance scenario

Dependency가 없는 work package만 병렬화한다. 한 initiative 안에서 여러 work
package와 commit으로 구현할 수 있으며 단일 대형 merge를 요구하지 않는다.
Migration execution plan 승인과 실제 implementation activation은 별도 사용자
gate로 유지한다.

## 10. 검증과 완료 기준

Preparation 갱신마다 다음을 확인한다.

- 변경이 canonical dossier의 Markdown 파일에만 한정됨
- runtime, 기존 test, gameplay data, scene와 project 설정 변경이 없음
- 13개 candidate domain이 coverage에서 누락되지 않음
- coverage, card, decision과 review reference가 일치함
- `unknown`과 `conflict`를 추측으로 해소하지 않음
- domain-by-domain owner review 없이 readiness를 승격하지 않음
- repository 표준 Markdown·whitespace 검사가 통과함

Preparation 완료는 dossier가 장기 대기 가능한 상태라는 뜻일 뿐 migration
initiative나 Game Server 구현이 시작됐다는 뜻이 아니다. Commit, push와 원격
변경은 각각 별도 사용자 요청이 없는 한 이 charter 범위가 아니다.
