# 데모 경험의 정규 프로젝트 전환

> 상태: **Draft**
>
> 기준선: **2026-07-29 / `44c87885`**
>
> 대상: Product, Client, Server
>
> 범위: 데모 근거팩, PRD 입력, ADR 후보
>
> 비범위: 최종 기획서, 승인된 ADR, 정규 프로젝트 구현 명세

이 폴더는 현재 데모에서 확인한 구현 사실과 학습 가설을 정규 프로젝트의 PRD와 ADR을 작성할 수 있는 입력으로 번역한다. 데모 문서를 최신인 것처럼 합치는 대신, 사실·결정·가설과 그 증거 수준을 분리한다.

현재 데모의 정확한 경계는 **클라이언트 권위 전투 시뮬레이션 + Firebase 인증 + 서버 발급 토너먼트 시드·시도 + 결과·랭킹 API**다. 정규 프로젝트는 **서버가 전투 상태·판정·점수를 소유하는 온라인 게임**을 목표로 하며, 데모의 ECS 구현은 이식하지 않는다.

이 문서 묶음은 정규 프로젝트 구현을 허가하지 않는다. 이 저장소의 ECS 경계와 작업 규칙은 계속 [`CLAUDE.md`](../../CLAUDE.md)와 현재 spec을 따른다.

## 목적과 비목표

목적:

- 데모의 현재 구현과 역사 문서를 구분한 기준선을 남긴다.
- 제품 학습과 아직 검증하지 못한 재미 가설을 분리한다.
- ECS 구현에서 얻은 원칙을 non-ECS, server-authoritative 구조의 설계 질문으로 변환한다.
- 정규 프로젝트 PRD와 ADR이 출처와 미결 질문을 추적할 수 있게 한다.

비목표:

- 데모가 재미 검증을 완료했다고 선언하지 않는다.
- 서버 runtime, transport, tick rate, 수치 표현을 선택하지 않는다.
- `docs/decisions/` 또는 공식 ADR 번호를 만들지 않는다.
- 데모 코드·에셋·Unity scene 또는 현재 ECS 규칙을 변경하지 않는다.

## 읽는 순서

1. [`source-map.md`](source-map.md) — 출처별 역할, 기준일, 드리프트와 supersession
2. [`demo-baseline.md`](demo-baseline.md) — 현재 세션 흐름, 권위 경계, ECS 구조, 검증 상태
3. [`product/learning-register.md`](product/learning-register.md) — 제품 사실·결정·가설과 이전 판단
4. [`product/validation-backlog.md`](product/validation-backlog.md) — 정규 프로젝트에서 다시 검증할 실험
5. [`product/prd-inputs.md`](product/prd-inputs.md) — 기술 해법을 제외한 PRD 입력
6. [`architecture/engineering-learnings.md`](architecture/engineering-learnings.md) — 유지할 원칙과 폐기할 구현
7. [`architecture/transition-matrix.md`](architecture/transition-matrix.md) — `carry / adapt / drop / decide` 및 역할 분담
8. [`architecture/adr-candidates.md`](architecture/adr-candidates.md) — 승인 전 결정 질문
9. [`evidence/README.md`](evidence/README.md) — 이후 수집할 증거 산출물 규칙

## 문서 상태

| 문서 | 상태 | 다음 게이트 |
|---|---|---|
| `source-map.md` | Draft | Product·Client·Server가 출처 역할과 누락을 검토 |
| `demo-baseline.md` | Draft | 3개 직군 검토 후에만 `Frozen` 승격 |
| `product/*` | Draft | 플레이테스트 설계와 PRD 작성 시 갱신 |
| `architecture/*` | Draft | 기술 조사와 ADR 승인 흐름에서 갱신 |
| `evidence/README.md` | Draft | 리서치·분석 담당자가 저장·익명화 규칙 검토 |

## 공통 기록 계약

주장 단위 레코드는 다음 필드를 사용한다. 표 형식으로 줄여 쓰더라도 의미는 같아야 한다.

```yaml
id: PT-AREA-001
statement: "검증하거나 결정할 수 있는 하나의 주장"
claim_kind: fact                 # fact | decision | hypothesis
evidence_status: untested        # 아래 상태 목록 참조
evidence_level: E0               # E0 | E1 | E2 | E3 | E4
as_of: 2026-07-29
mode: demo-normal
conditions: "주장이 성립하는 모드·맵·세션 조건"
sources:
  - path: docs/spec/example/README.md
    commit: 44c87885
tests: []
evidence_artifacts: []
transfer_action: retest          # carry | adapt | retest | drop | decide
regular_project_impact: "Product·Client·Server에 미치는 영향"
next_step: "다음 검증 또는 결정"
```

규칙:

- 한 레코드에는 한 종류의 주장만 둔다. 구현 사실과 재미 가설을 한 문장으로 합치지 않는다.
- 모든 현재 사실에는 코드·에셋·테스트·최신 spec 중 최소 하나를 연결한다.
- `as_of`와 적용 모드·조건을 생략하지 않는다.
- 충돌하는 과거 문서는 조용히 병합하지 않고 `superseded_by`를 기록한다.
- 모든 값과 산식은 별도 근거가 없으면 “데모 기준값”이다. 정규 프로젝트 요구사항으로 자동 승격하지 않는다.

### `claim_kind`

| 값 | 의미 |
|---|---|
| `fact` | 기준 시점에 구현·테스트·관찰로 확인할 수 있는 상태 |
| `decision` | 데모에서 의도적으로 선택한 규칙이나 범위 |
| `hypothesis` | 플레이어 반응, 재미, 운영 효과처럼 추가 검증이 필요한 예상 |

### `evidence_status`

| 값 | 의미 |
|---|---|
| `untested` | 검증 활동 또는 계측이 없음 |
| `instrumented` | 측정 경로는 있으나 해석 가능한 결과셋이 없음 |
| `functional` | 구현·자동 검증 또는 기능 Play로 동작을 확인 |
| `internal-observed` | 비구조화된 내부 관찰이 있으나 일반화할 수 없음 |
| `supported` | E3 이상 증거가 사전 정의된 기준을 지지 |
| `refuted` | E3 이상 증거가 사전 정의된 기준을 반박 |
| `superseded` | 후속 결정·구현·문서가 이 기록을 대체 |

`functional` 또는 E2는 기능이 작동한다는 뜻일 뿐 재미 가설의 지지 근거가 아니다. `supported`와 `refuted`는 실제 E3 이상 산출물을 연결할 때만 사용한다.

### `evidence_level`

| 수준 | 의미 | 허용되는 결론 |
|---|---|---|
| E0 | 문서에 적힌 주장·의도 | 질문과 역사 확인 |
| E1 | 구현·에셋·자동 검증 | 기능·계약의 존재 확인 |
| E2 | 내부 기능 Play | 배선·조작성·시각 동작의 제한적 확인 |
| E3 | 구조화된 정성 플레이테스트 | 표본과 조건 안에서 가설 지지·반박 |
| E4 | 반복 정량 근거 | 정의된 모집단·버전 안에서 재현된 효과 |

## 진실원 우선순위

주장 종류에 따라 우선순위가 다르다.

구현 사실:

1. 현재 코드·에셋·자동 테스트
2. 활성 spec의 `README.md`와 작업 단위 계약
3. handoff summary
4. 과거 PRD·TRD·prototype·milestone

제품 효과:

1. 익명화된 구조화 플레이테스트·로그 분석 산출물
2. 사전 등록한 가설·성공 기준
3. 내부 기능 Play와 구현
4. 과거 기획 의도

코드는 기능이 존재함을 증명할 수 있지만 재미를 증명하지 않는다. 서버 저장소가 이 근거팩의 범위에 없으므로 서버의 내부 검증 여부는 클라이언트 계약만으로 추정하지 않는다.

## 수명주기와 승격

```text
Draft → Reviewed → Frozen → Exported 또는 Superseded
```

- 1차 작성물은 모두 `Draft`다.
- `demo-baseline.md`는 Product·Client·Server 검토가 끝난 기준 스냅샷만 `Frozen`으로 승격한다.
- 정규 프로젝트 PRD가 작성되면 `product/prd-inputs.md`를 `Superseded`로 바꾸고 최종 PRD를 연결한다.
- `ADR-CAND-###`가 승인되면 후보를 `Superseded`로 바꾸고 별도 공식 ADR을 연결한다. 후보 번호를 공식 ADR 번호로 재사용하지 않는다.
- 기준선 이후 데모가 바뀌면 Frozen 문서를 덮어쓰지 않고 새 기준일의 문서 또는 변경 기록을 만든다.
