# D2 — Registry와 검토 계약

> **DORMANT · OWNER-GATED · NON-ACTIONABLE HISTORY.** 현재 Demo의 spec·작업 큐·검증 gate가 아니며 Project owner의 명시적 transition 활성화 전에는 실행·갱신하지 않는다.

## 목적

많은 전환 문서를 사람이 다시 분류하지 않도록 package, freshness, owner, review와
dependency를 기계 검증 가능한 하나의 registry로 묶는다.

## 변경 대상

- `docs/production-transition/governance/registry.json`
- `docs/production-transition/governance/reviews.json`
- `docs/production-transition/governance/decisions.json`
- 기존 baseline과 `migration-dossier/` 상태 표기

## 구현

- 모든 exportable record에 `id`, `package`, `owner`, `consumer`, `required_reviewers`,
  `as_of_commit`, `watch_paths`, `freshness`, `review_status`, `depends_on`,
  `blocking_decisions`, `implementation_wave`, `execution_stage`, `target_path`를 둔다.
- 공식 gate용 `completeness`와 `readiness`, package closure용 `source_path`,
  `references`, 검토 분리용 `areas`를 추가한다.
- review 승인 키는 `(area_id, card_id, document_revision, source_commit)`이다. 한 card가
  여러 area를 다루면 area마다 독립 승인이 필요하다.
- `conditional`, `provisional`, accepted gap은 preparation 메모로 남길 수 있지만 공식
  include gate를 통과하지 않는다.
- Record `blocking_decisions`와 decision `affected_records`는 양방향으로 같은 edge를
  선언해야 하며 unknown ID나 한쪽짜리 edge를 허용하지 않는다.
- `44c87885` baseline과 기존 `MIG-REVIEW-001`은 historical/stale/non-approving으로
  보존한다. 현행 onboarding sequence와 충돌하는 claim을 drift record로 등록한다.

## 완료 기준

- [x] legacy 문서가 새 정본보다 높은 권위를 주장하지 않는다.
- [x] 포함 record의 엄격 gate를 JSON으로 판정할 수 있다.
- [x] 다중-area card를 하나의 review로 승인할 수 없다.
- [x] watch path와 source commit으로 stale 여부를 다시 계산할 수 있다.
