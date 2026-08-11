# production-transition-governance — Foundation + Pilot

> 상태: **Foundation 작업 계획 승인 · D0 완료 · D1 대기 — 2026-08-11**
>
> 범위: 문서·정적 검증기만. Runtime, Unity serialized asset, API, package,
> `ProjectSettings`, 실제 production import는 변경하지 않는다.

## 검증 질문

변경이 잦은 Demo에서 Client와 Game Server가 각자 필요한 의미를 계속 축적하면서도,
미래의 단 한 번인 freeze/export와 이후의 다단계 production 구현을 혼동하지 않는가?

## 상위 목표

- `docs/production-transition/README.md`를 준비 과정의 전역 정본으로 만든다.
- 공식 산출물을 `shared`, `client`, `game-server`로 분리한다.
- freshness, review, decision, dependency와 package closure를 구조적으로 검증한다.
- `unit-lifecycle` 한 조각으로 end-to-end 준비 절차만 증명한다.
- 두 production 저장소의 기존 문서는 non-authoritative preparation으로 정합한다.

## 작업 단위

| 순서 | 문서 | 작업 | 상태 |
|---|---|---|---|
| D0 | `README.md` | 분산 spec, unit·비범위·승인 gate 고정 | 완료 |
| D1 | [0_global_cutover_contract.md](0_global_cutover_contract.md) | 전역 정본·package·one-time cutover | 대기 |
| D2 | [1_registry_and_review_contract.md](1_registry_and_review_contract.md) | registry·review·decision gate | 대기 |
| D3 | [2_static_validator.md](2_static_validator.md) | preparation/cutover 검증기 | 대기 |
| C1/S1 | [3_target_intake_alignment.md](3_target_intake_alignment.md) | Client·Server 준비 문서 정합 | 대기 |
| D4 | [4_unit_lifecycle_pilot.md](4_unit_lifecycle_pilot.md) | 세 패키지 연결 pilot | 대기 |
| D5 | `5_handoff_summary.md` | 검증·장기 갱신 인계 | 대기 |

의존 순서는 `D0 → D1 → D2 → (D3, C1, S1) → D4 → D5`다. 문서 번호는
Demo 저장소의 구현 단위를 나타내며 Client·Server 변경은 각 저장소 정책을 따른다.

## Unit review와 commit gate

| Unit | 필수 검토 | commit gate |
|---|---|---|
| D0–D2 | Product owner, Demo transition steward, Client·Game Server tech owner | 전역 계약과 schema 승인 뒤 Demo 저장소의 분리 commit 후보 |
| D3 | Demo transition steward, independent reviewer | positive/negative fixture와 read-only 보장 확인 뒤 분리 commit 후보 |
| C1 | Client tech owner | Client 저장소에서 별도 사용자 승인 전 commit/push 금지 |
| S1 | Game Server tech owner | Game Server 저장소에서 별도 사용자 승인 전 commit/push 금지 |
| D4 | Product owner, 양쪽 tech owner, independent reviewer | area별 exact review와 미결 blocker 확인 뒤 분리 commit 후보 |
| D5 | Demo transition steward, independent reviewer | 앞 unit 검증 결과를 재현한 뒤 분리 commit 후보 |

2026-08-11 사용자 결정으로 Foundation preparation 구조는 승인됐다. Pilot gameplay
`PT-DEC-UL-001..003`은 모두 `open`으로 보류한다. 이 delivery-level 승인은 위 역할별 exact
review나 official cutover 승인을 대신하지 않는다. 따라서 `reviews.json`을 추정해 채우거나
pilot record를 `reviewed`, `ready`, `include`로 승격하지 않으며, 세 저장소 commit/push도
별도 범위·저장소 승인이 있기 전에는 수행하지 않는다.

## 비범위

- 13개 Game Server domain 전체 backfill
- runtime, Unity serialized asset, API, wire DTO, package와 `ProjectSettings` 변경
- live production protocol 결정
- official freeze publication 또는 production `docs/migration-input/` 생성

## Feature-wide 계약

- 공식 freeze는 하나의 immutable `freeze_id`와 byte set을 발행하는 사건 한 번이다.
- Client는 `shared+client`, Game Server는 `shared+game-server`를 같은 freeze에서 받는다.
- 중단된 import는 같은 ID와 같은 bytes만 재개한다. 새 freeze/re-export는 없다.
- 준비 중 candidate는 반복 검증·폐기할 수 있으며 공식 freeze로 세지 않는다.
- 포함 record는 `complete + current + reviewed + ready`이고 blocking decision이 모두
  `decided`여야 한다.
- Shared에는 기술 중립 의미와 ordering만 둔다. Unity/ECS, wire DTO, 인증과 transport는
  production ADR 범위다.
- 13개 Server domain은 일괄 backfill하지 않는다. 변경된 spec과 연결된 domain만 하나씩
  갱신한다.

## 후속 후보

- Product가 transition을 결정할 때 production-v1 include/exclude 목록 확정
- 실제 freeze publication과 양쪽 production receipt 생성
- 각 production 저장소의 별도 구현 wave 계획·승인
