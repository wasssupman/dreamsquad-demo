# Production Transition Policy

> 상태: **dormant · owner-gated · living policy**

## 고정 원칙

1. Demo는 유일한 upstream이고 transition 자료는 dormant downstream이다.
2. Official consumer는 Production Client와 Production Game Server 두 개뿐이다.
3. `common`은 두 bundle에 동일 bytes로 들어가며 독립 delivery나 adoption 대상이 아니다.
4. Production 저장소의 현행 정책, accepted ADR과 roadmap이 imported 문서보다 우선한다.
5. Runtime API, DTO, wire protocol과 기술 제품 선택은 production에서 결정한다.
6. Official Demo approval, freeze와 양쪽 이동은 각각 정확히 한 번만 일어난다.

## 역할

| 역할 | 책임 | 할 수 없는 것 |
|---|---|---|
| Project owner | 세 승인 역할 배정, transition 활성화·중단 | 한 번의 승인으로 여러 사건 충족, 이미 기록된 사건의 위임 소급 변경 |
| `game-spec-approver` | gameplay 의미와 production-v1 `approved_scope` 승인 | freeze attestation, transfer 완료 승인 |
| `demo-freeze-attestor` | 승인과 동일한 revision, manifest와 common/client/server hash 동결 | gameplay scope 변경, transfer 완료 승인 |
| `coordinated-transfer-attestor` | Client와 Game Server receipt를 모두 확인하고 전역 이동 완료 승인 | 한쪽 receipt만으로 완료 선언, production 구현 activation |
| Transition steward | 별도 maintenance, reconciliation, bundle 조립, dry-run과 receipt 수집 | Demo 변경 요구, 승인 추정, production 구현 activation |
| Client tech owner | Client 규칙·coverage와 현지 정책 적합성 검토, receipt 확인 | gameplay 결과 판정, Game Server package 변경 |
| Game Server tech owner | Server 규칙·coverage와 현지 정책 적합성 검토, receipt 확인 | Client presentation 결정, Client package 변경 |
| Independent reviewer | 자신이 작성하지 않은 revision의 partition·link·schema·hash 감사 | 자신의 revision 승인 |

Project owner의 역할 위임은 `role`, `assignee`, `assigned_by_project_owner`, `assigned_at`,
`assignment_reference`를 기록한다. 모든 사건은 `approver`, `acting_role`과 당시 위임 증거를 포함하며
`acting_role == role`, `approver == assignee`, `assigned_at < approved_at`이어야 한다. Product owner나
repository owner라는 직함만으로 위임을 추정하지 않는다.

한 사람이 세 역할을 모두 맡을 수 있지만 event ID와 승인 시각이 서로 다르고 predecessor가 정확한
사건 세 개를 각각 승인해야 한다. 한 사건이나 승인 reference를 여러 lifecycle 단계로 간주할 수 없다.
역할 재배정은 아직 일어나지 않은 미래 사건에만 적용하며 기존 event와 그 위임 증거는 바꾸지 않는다.

## Delegated one-shot 사건

| 사건 | 선행 조건 | 고정하는 것 |
|---|---|---|
| `demo-approved` (`game-spec-approver`) | coverage scope와 blocking decision 정리, consumer tech review, dry-run 성공 | Demo revision, Demo content hash, 승인 범위와 근거 |
| `demo-frozen` (`demo-freeze-attestor`) | 정확히 한 `demo-approved`, 이후 Demo content 변화 없음 | 같은 Demo revision, freeze ID, manifest/common/client/server hash |
| `transfer-completed` (`coordinated-transfer-attestor`) | 정확히 한 `demo-frozen`, 두 target receipt 검증 | 같은 freeze ID, 두 destination과 receipt hash |

각 사건은 source-side event v2인 `governance/schemas/event.schema.json`을 따르는 별도 감사 파일이다.
감사 파일은 freeze payload나 consumer bundle에 포함하지 않는다.
`demo-approved`는 chain head이므로 `predecessor_event_id`가 없고, 뒤의 두 사건만 직전 사건 ID를
기록한다. 사건을 건너뛰거나 반복할 수 없으며 predecessor ID와 동일 revision/freeze ID를 검증한다.

Canonical schema identity는 event
`https://somnia.local/schemas/dreamsquad-transition-event-v2.json` (`schema_version: "2.0"`),
manifest `https://somnia.local/schemas/dreamsquad-transition-manifest-v1.json`, receipt
`https://somnia.local/schemas/dreamsquad-transition-receipt-v1.json` (둘 다 `schema_version: "1.0"`)이다.
Consumer는 이 ID와 field semantics를 참조하며 독자 source 계약으로 확장하거나 재정의하지 않는다.

## Demo 영향 0

- Transition maintenance는 명시적인 별도 요청·task·commit으로만 수행한다.
- Demo spec/handoff/backlog에 transition 후속을 만들지 않는다.
- Demo CI, hook, feature verifier, build/test 완료 조건에서 transition source나 freshness를 읽지 않는다.
- Transition 작업은 Demo code, asset, scene, package, ProjectSettings와 test를 바꾸지 않는다.
- Demo가 바뀌어도 transition 문서는 자동 stale 처리하거나 동기 갱신하지 않는다.

방화벽 자체의 정적 감사는 별도 governance 작업에서만 수행하며 정상 Demo feature 완료를
차단하는 CI gate로 연결하지 않는다.

## Freeze와 이후

`demo-frozen`은 manifest, `common`, `client`, `game-server`와 policy bytes를 immutable하게
고정한다. `receipts/`는 manifest inventory에 속하지 않는 append-only 감사 영역이며 각 consumer
receipt를 정확히 한 번 추가할 수 있다. `transfer-completed` 이후에는 receipt를 포함한 audit tree
전체를 바꾸지 않는다.

복사 장애는 동일 manifest와 bytes로만 재개한다. 의미 오류, 누락 또는 새 요구가 발견되면 각
production 저장소의 errata/ADR/change control로 처리한다. Demo audit copy, freeze ID와 receipt는
보존하지만 working transition 문서를 다시 export하지 않는다.
