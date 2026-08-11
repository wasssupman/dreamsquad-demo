# Transition Governance

> 상태: **preparing**
>
> 이 폴더는 official package가 아니라 세 package를 조립·검증하는 control plane이다.

## 정본

| 파일 | 역할 |
|---|---|
| [`registry.json`](registry.json) | record, package, freshness, dependency와 target inventory |
| [`reviews.json`](reviews.json) | area/revision/source별 실제 owner review 사건 |
| [`decisions.json`](decisions.json) | gameplay blocking decision 상태 |
| [`manifest.schema.json`](manifest.schema.json) | 미래 immutable freeze manifest 형식 |
| [`export-charter.md`](export-charter.md) | `references/governance/transition-charter.md`로 이동해도 link가 깨지지 않는 축약 계약 |

모든 JSON은 UTF-8, 정렬 가능한 stable ID와 repository-relative `/` 경로를 쓴다. Registry가
Markdown의 상태 문구와 다르면 registry를 우선하되, validator가 불일치를 오류로 보고해야
한다. 실제 owner review나 gameplay 결정을 자동화·추정해 만들지 않는다.

## 상태 의미

Registry의 `package`는 `shared | client | game-server | references`이다. 앞의 세 값만
consumer package이고, `references`는 같은 snapshot 안에서 manifest·governance closure를
전달하는 구획이다. Client는 `shared+client+references`, Game Server는
`shared+game-server+references`를 같은 freeze ID로 받는다.

- `freshness`: `current | stale | historical`
- `review_status`: `draft | review_requested | reviewed | historical`
- `disposition`: `candidate | include | defer | exclude | reference`
- `completeness`: `none | partial | complete`
- `readiness`: `blocked | provisional | ready`

Preparation mode는 incomplete/stale/blocked를 허용하지만 정확히 드러내야 한다. Cutover mode의
include record는 `complete/current/reviewed/ready`만 허용한다.
`blocking_decisions`와 각 decision의 `affected_records`는 양방향 exact mapping이며 unknown
record/decision이나 한쪽만 있는 edge는 preparation에서도 구조 오류다.

## Review key

```text
(area_id, card_id, document_revision, source_commit)
```

`required_reviewers`의 각 role이 모든 `areas`에 exact approval을 남겨야 한다. 작성자는 같은
revision의 독립 reviewer가 될 수 없다. `legacy_reviews`는 감사 이력이며 승인이 아니다.

## Hash와 closure

- `sha256`은 `source_path` bytes의 lowercase SHA-256이다.
- Preparation은 worktree bytes를 읽지만 strict cutover에서는 모든 selected `source_path`가
  `candidate_source_commit`의 tracked blob이고 현재 읽은 bytes와 정확히 같아야 한다.
- 상위 `.gitattributes`는 이 subtree의 text checkout을 LF로 고정한다. Validator의 raw-byte
  비교는 Windows `core.autocrlf`에서도 같은 commit이 같은 package bytes를 만들도록 한다.
- `references`와 `depends_on`은 transitive closure에서 빠지면 안 된다.
- 각 record가 소비되는 target마다 dependency도 같은 target inventory에 있어야 한다.
- Markdown local link는 source 위치가 아니라 이동 뒤 `target_path`를 기준으로 해석한다.
- `target_path`는 freeze snapshot 내부 상대 경로이며 첫 segment가 `package`와 같아야 한다.
  `..`, absolute path, package 간 중복을 금지한다.
- dry-run은 canonical path order로 Client와 Server inventory를 만든다. Shared file list와
  aggregate hash는 양쪽에서 같아야 한다.
- Registry의 destination template은 정본에 고정된 두 경로와 정확히 일치해야 한다.
  dry-run은 하나의 deterministic sentinel freeze ID를 두 경로에 치환하고,
  `manifest.schema.json`과 같은 필드와 package 구획을 가진 `dry_run_manifest`를 계산한다.
- Manifest의 각 file은 `record_id`를 가지며 `governance_attestation.records/reviews/decisions`가
  source/watch provenance, gate metadata, implementation wave/stage, exact review tuple과
  관련 decision을 canonical order로 보존한다. Live registry 자체를 복사하거나
  self-hash하지 않는다.

## 금지

- preparation 도중 `freezes/` 또는 production `docs/migration-input/` 생성
- placeholder review/decision으로 strict gate 우회
- official freeze 뒤 registry를 고쳐 새 Demo export 생성
