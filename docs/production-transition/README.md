# Demo → Production Transition — Owner-gated Dormant 자료

> 상태: **dormant — Project owner의 명시적 활성화 전에는 transition/freeze/import 미시작**
>
> 적용 범위: production-transition 준비 자료에만 해당한다. 현재 Demo 정본이 아니다.
>
> 2026-07-29 / `44c87885` 기준 자료와 기존 Game Server dossier는
> **historical · stale · preparatory**다. 현재 gameplay나 production 계약으로 승인되지 않았다.

## Demo firewall — 절대 규칙

- Demo가 유일한 upstream이다. 현재 Demo의 설계·구현·검증은 `CLAUDE.md`, 활성
  `docs/spec/{feature-slug}/`, TRD/PRD의 적용 가능한 계약과 코드·에셋·테스트를 따른다.
- 이 subtree는 Project owner가 현재 요청에서 production-transition 작업을 명시적으로
  활성화한 경우에만 agent가 읽거나 갱신·검증한다. 최근 커밋, stale 상태, watch path 변화는
  활성화가 아니다.
- Demo와 불일치하면 이 자료를 stale로 남긴다. Demo를 고치거나 Demo 작업을 지연하지 않는다.
  Transition freshness/review/decision/verifier는 Demo 완료·검증·커밋 gate가 아니다.
- Freeze, cutover, production import와 후속 wave의 시점·범위는 Project owner만 결정한다.
  명시적 활성화 전에는 agent 작업 큐나 후속 후보로 취급하지 않는다.

## 목적

계속 바뀌는 single-player Demo에서 production 이식에 필요할 수 있는 사실, 미결 결정과
증거를 미리 보관한다. Demo 진행 중에는 동기 갱신 의무가 없으며 stale 누적을 정상으로
허용한다. Project owner가 별도로 전환을 활성화한 뒤에만 당시 Demo 정본과 재대조한다.

이 문서는 현재 Demo 구현을 바꾸는 gameplay spec도, production protocol/API를 승인하는
문서도 아니다.

## 이번 foundation 범위

포함:

- 전역 freeze/export 계약과 package 분리
- 구조화 registry, review, decision와 freshness 계약
- preparation/cutover 정적 검증기
- Client·Game Server의 기존 준비 문서 정합
- `unit-lifecycle` 한 조각의 문서·fixture pilot

비범위:

- 13개 Game Server domain 전체 backfill
- runtime, network, API, DTO, asset, Unity scene/prefab, package, `ProjectSettings` 변경
- 실제 `freezes/<freeze-id>/` publication 또는 production import
- production gameplay/protocol/architecture 승인과 구현 activation

## 권위 계층

1. 이 README: 전역 transition lifecycle, package와 owner의 정본
2. [`governance/registry.json`](governance/registry.json): record 상태와 package inventory 정본
3. [`governance/decisions.json`](governance/decisions.json)과
   [`governance/reviews.json`](governance/reviews.json): decision/review 사건 정본
4. [`shared/`](shared/README.md), [`client/`](client/README.md),
   [`game-server/`](game-server/README.md): 소비자별 card와 package index
5. 기존 `product/`, `architecture/`, `evidence/`, `migration-dossier/`, baseline과 source map:
   registry가 current로 승격하기 전까지 historical/preparatory reference

아래 계층의 오래된 lifecycle, `Frozen`, `conditional`, accepted gap, re-freeze 문구가 이
정본과 충돌하면 이 정본을 따른다. 충돌 문구는 공식 gate나 예외 승인의 근거가 아니다.

## 공식 consumer package와 control plane

| Package | 소비자 | 담는 것 | 담지 않는 것 |
|---|---|---|---|
| `shared` | Client + Game Server | stable ID, command intent, authoritative state/event/result 의미, ordering, 기술 중립 invariant | Unity/ECS 타입, wire DTO, 인증, transport, serializer, 최종 protocol |
| `client` | Client | 사용자 입력 UX, pending/rejected/corrected 표현, projection, cue, reconnect/resync 연출, stable ID→asset/catalog mapping | gameplay 판정, canonical state mutation, score/terminal 권위 |
| `game-server` | Game Server | canonical rules/config/state, command validation, atomic outcome, time/tick/RNG/score 요구와 서버 acceptance | Client presentation timing, prefab/VFX/UI 구현, wire 선택 |

공식 consumer package는 위 세 개뿐이다. [`governance/`](governance/README.md)는 registry,
review/decision, manifest schema와 validator를 소유하는 Demo 내부 control plane이지 네 번째
package가 아니다. Freeze의 `references/`는 세 package가 인용하는 정본·evidence와 필요한
governance snapshot을 전달하는 closure partition이며 독립 consumer package가 아니다.

`shared`는 양쪽 production 저장소에 byte-identical하게 복제한다. 이번 foundation은 live
production protocol의 최종 정본을 만들지 않는다. wire/auth/transport는 기존 production ADR
gate가 결정한다.

## Owner와 승인 책임

| 역할 | 책임 |
|---|---|
| Product owner | production-v1 include/exclude와 gameplay 의미 결정 |
| Demo transition steward | registry, freshness, dependency closure, package 조립과 dry-run |
| Client tech owner | Client card 검토와 미래 Client import receipt |
| Game Server tech owner | Game Server card 검토와 미래 Server import receipt |
| 두 tech owner | Shared card 공동 검토 |
| Independent reviewer | 자신이 작성하지 않은 revision의 schema, closure와 receipt 감사 |

작성자는 자신의 revision에 대한 독립 승인자가 될 수 없다. 실제 사람의 검토가 없으면
`reviewed`나 `approved`를 기록하지 않는다.

## Transition lifecycle

```text
dormant -> preparing -> cutover_candidate -> cutover_in_progress -> cutover_complete
                           |                     |
                           +-> discard           +-> resume same freeze ID + same bytes only
```

- `dormant`: 선행 준비 자료만 보관한다. Demo 변경을 추적·차단하지 않고 agent가 transition
  후속을 제안하지 않는다. Project owner의 현재 요청상 명시적 활성화만 다음 상태 진입 근거다.
- `preparing`: Demo 안에서 record를 추가·수정하고 반복 검증한다.
- `cutover_candidate`: Product가 범위를 제안하고 clean source commit에서 temp dry-run한다.
  오류가 있으면 candidate를 폐기한다. 이것은 freeze가 아니다.
- `cutover_in_progress`: 정확히 한 번의 official publication이 `freeze_id`, source commit과
  모든 bytes/hash를 고정한다. 이 순간부터 새 freeze/re-export는 금지다.
- `cutover_complete`: Client와 Game Server receipt가 동일 manifest와 Shared hash를 확인했다.

두 target으로의 fan-out은 한 logical transition event다. 기계적 중단 때문에 한 target의
copy가 끝나지 않았다면 같은 freeze ID와 byte set만 재개할 수 있다. 새 candidate를 만들거나
bytes를 고치는 것은 재개가 아니다.

공식 freeze 뒤 의미 오류는 frozen Demo package를 바꾸지 않는다. 각 production 저장소에서
errata, ADR 또는 일반 change control로 처리한다. Demo re-freeze, 부분 update와 후속 import는
허용하지 않는다.

`docs/production-transition/.gitattributes`는 이 전환 source subtree의 text bytes를 LF로
고정한다. 따라서 Windows `core.autocrlf` 설정과 무관하게 clean checkout의 selected artifact가
candidate commit blob과 byte-identical해야 한다는 strict gate를 재현할 수 있다.

## Registry record 계약

Registry top-level은 `schema_version`, `transition_state`, `candidate_source_commit`,
`official_freeze`와 아래 두 exact destination template을 가진다. Validator는 template의
오타·Client/Server swap을 실패시키고 dry-run freeze ID를 두 destination에 동일하게 확장한다.

```text
somnia-client/docs/migration-input/dreamsquad-demo/<freeze-id>/
somnia-game-server/docs/migration-input/dreamsquad-demo/<freeze-id>/
```

각 transferable record는 최소한 다음 필드를 가진다.

```text
id, package, source_path, target_path, owner, consumer,
required_reviewers, as_of_commit, document_revision, watch_paths,
freshness, review_status, disposition, completeness, readiness,
depends_on, blocking_decisions, areas, references, sha256,
implementation_wave, execution_stage, cutover_blocking
```

- `as_of_commit`은 의미를 관찰한 immutable Demo source commit이다.
- `package`는 `shared | client | game-server | references`다. `references`는 위에서 정의한
  delivery partition이며 공식 consumer package 수를 늘리지 않는다.
- `document_revision`은 검토한 문서 bytes의 revision이며 source commit과 별개다.
- `watch_paths`가 `as_of_commit` 이후 바뀌면 record는 재검토 전까지 `stale`다.
- `target_path`는 freeze snapshot 기준 상대 경로이며 첫 segment가 record의 `package`와
  정확히 같아야 한다. 절대 경로, `..`, 중복과 partition 탈출을 금지한다.
- `references`는 normative dependency의 transitive closure에 포함돼야 한다.
- `implementation_wave`는 production 구현 순서일 뿐 freeze readiness가 아니다.

기존 문서의 자유 형식 claim은 registry에 등록되고 current로 검토되기 전에는 공식 export
대상이 아니다.

## Review와 decision 계약

Review 승인 키는 다음 4-tuple이다.

```text
(area_id, card_id, document_revision, source_commit)
```

한 card가 여러 area를 다루면 required reviewer가 area별로 각각 승인해야 한다. 다른 area,
다른 document revision 또는 다른 source commit의 승인을 암묵적으로 상속하지 않는다.
기존 `MIG-REVIEW-001`은 review request/defer 이력일 뿐 승인 행이 아니다.

Gameplay blocker는 [`governance/decisions.json`](governance/decisions.json)의 영구 ID로
연결한다. `open`, `proposed`, `deferred`, `provisional`, `conditional`과 accepted gap은
preparation 기록에는 허용하지만 공식 include gate를 해제하지 않는다. Blocker를 해제하는
유일한 상태는 owner가 근거와 함께 기록한 `decided`다.
Record의 `blocking_decisions`와 decision의 `affected_records`는 양방향으로 정확히
일치해야 하며, 어느 쪽에도 알 수 없는 ID를 둘 수 없다.

## 공식 include gate

`disposition: include`인 모든 record는 동시에 다음을 만족해야 한다.

- `completeness: complete`
- `freshness: current`
- `review_status: reviewed`
- `readiness: ready`
- 모든 dependency와 normative reference가 include closure 안에 존재
- 각 consumer의 virtual target inventory에서 dependency와 Markdown local link가 모두 해소
- 모든 `areas`에 exact review key와 required reviewer 승인 존재
- 모든 `blocking_decisions`가 `decided`
- 실제 source file SHA-256과 registry 값 일치
- 모든 selected source artifact가 `candidate_source_commit`의 tracked blob으로 존재하고
  worktree에서 읽은 bytes와 byte-identical
- unique/contained `target_path`

하나라도 어기면 strict cutover 검증은 실패한다. 미결 record를 조용히 `defer`/`exclude`해서
이미 승인한 include dependency를 끊을 수 없다. Product가 production-v1 범위를 잠글 때
exclude한 기능은 이후 일반 production 작업이며 두 번째 Demo import 사유가 아니다.

## 검증 stage

| Stage | 무엇을 검증하는가 | 막는 gate |
|---|---|---|
| `demo-pre-freeze` | Demo 의미, freshness, schema, evidence와 package closure | candidate/freeze |
| `production-client-wave` | projection, correction/reconnect UX, Client vertical slice | 지정 Client wave |
| `production-server-wave` | authoritative simulation, ordering, command/result, Server vertical slice | 지정 Server wave |
| `production-release` | 통합 신뢰성, replay/운영/제품 기준 | release |

`VAL-PROD-008/009`처럼 server-authoritative vertical slice가 필요한 검증은 frozen input으로
요구·protocol·목표 문턱만 전달한다. 결과는 production-side evidence로 쌓고 Demo freeze
완료 조건이나 재-export 사유로 쓰지 않는다.

## 미래 freeze layout과 destination

공식 cutover 때만 다음 immutable snapshot을 만든다.

```text
freezes/<freeze-id>/
  manifest.json
  shared/
  client/
  game-server/
  references/
```

Destination은 다음으로 고정한다.

```text
Client:      somnia-client/docs/migration-input/dreamsquad-demo/<freeze-id>/
Game Server: somnia-game-server/docs/migration-input/dreamsquad-demo/<freeze-id>/
```

Client snapshot은 `manifest + shared + client + 필요한 references`, Game Server snapshot은
`manifest + shared + game-server + 필요한 references`를 받는다. 각 receipt는 freeze ID,
aggregate hash, Shared hash, 상대 경로와 byte count를 기록한다. Preparation 동안 Demo나
production 저장소 어느 쪽에도 official freeze/input 디렉터리를 만들지 않는다.

Validator의 in-memory dry-run manifest는 [`manifest.schema.json`](governance/manifest.schema.json)과
같은 필드·partition·destination shape를 사용한다. Deterministic sentinel ID와 timestamp는
검증용일 뿐 official publication이 아니며 production destination에 쓰지 않는다.
Link 검증은 source 위치가 아니라 이동 뒤 `target_path` 위치를 기준으로 수행한다.
각 file entry는 stable record ID를 보존하고, `governance_attestation`은 included record의
source/watch provenance, gate metadata, implementation wave/stage, exact review tuple과 관련
decision을 canonical 배열로 고정한다. 따라서 두 production 저장소는 live Demo registry
없이 frozen manifest bytes만으로 승인과 후속 실행 위치를 재감사한다.
Preparation은 uncommitted candidate bytes를 허용하지만 strict cutover는 selected artifact를
candidate Git commit에 결합한다. 형식만 맞는 SHA, untracked source와 dirty byte mismatch는
publication 전에 실패한다.

## Owner 활성화 시 reconciliation 참고

아래 절차는 Project owner가 production-transition을 명시적으로 활성화한 별도 작업에서만
수행한다. 평상시 Demo 작업의 완료 기준이나 후속 목록이 아니다.

1. 활성화 시점의 Demo 정본과 registry `watch_paths`를 한 번에 대조한다.
2. 누적 drift가 있는 record를 stale로 판정하고 필요한 source commit, claim, fixture만 갱신한다.
3. owner가 정한 범위의 package card와 decision만 재검토한다.
4. exact area/revision/source key로 owner review를 기록하고 owner-authorized dry-run을 수행한다.

Demo 작업 중에는 이 reconciliation을 선제 수행하지 않는다. 등록되지 않은 문서와 새 Demo
spec은 transition 후보로 자동 승격되지 않는다.

## Historical 자료와 알려진 drift

- [`demo-baseline.md`](demo-baseline.md), [`source-map.md`](source-map.md),
  [`product/`](product/), [`architecture/`](architecture/), [`evidence/`](evidence/README.md)은
  2026-07-29 snapshot에서 출발한 유용한 조사 자료지만 현재는 stale/preparatory다.
- 옛 onboarding claim은 첫 복귀에 Squad와 Dreamcatcher를 함께 지목한다. 현행
  [`outgame-tutorial`](../spec/outgame-tutorial/README.md)은 `B1 → B2 → C → E`, 독립 `D`다.
  이 충돌은 registry의 explicit drift record로 남기며 옛 문구를 current처럼 export하지 않는다.
- [`migration-dossier/`](migration-dossier/README.md)는 기존 13-domain Game Server 조사
  자료다. `game-server` package의 legacy input이며 전역 freeze charter가 아니다.

## Owner-authorized 실행과 확인

다음 명령은 현재 사용자 요청에 Project owner의 명시적 transition 지시가 있을 때만 사용한다.
플래그가 없으면 verifier는 registry나 Git을 읽지 않고 `SKIP`한다.

```text
python tools/verify_production_transition.py prepare --project-owner-authorized
python tools/verify_production_transition.py cutover --project-owner-authorized
python -m unittest tools.test_verify_production_transition
```

Foundation+pilot 자료의 owner-authorized 기대 결과는 dormant preparation 구조 검증 통과,
`CUTOVER_STATE` 및 unresolved gameplay decision 때문에 strict cutover 실패다. 이 실패는
foundation 실패가 아니라 현재 dormant 상태의 owner 전용 역사적 참고이며, cutover 실행·후속 작업 지시가 아니다.
