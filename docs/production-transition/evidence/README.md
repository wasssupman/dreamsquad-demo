# Evidence 산출물 가이드

> 상태: **Draft**
>
> 기준일: **2026-07-29**
>
> 적용 범위: 정규 프로젝트 PRD/ADR의 근거로 인용할 플레이테스트·로그·설문·인터뷰 산출물

이 디렉터리는 “기능이 있다”와 “가설이 지지된다”를 구분하기 위한 evidence 계약을 정의한다.
현재 inventory는 [데모 기준선 `BASE-009`와 `BASE-010`](../demo-baseline.md)에 기록한다.
원시 개인정보, 인증 token, 원문 음성·영상과 제한 없는 raw log는 Git 저장소에 커밋하지 않는다.

## Evidence level과 판정

| level | 의미 | 허용되는 결론 |
|---|---|---|
| `E0` | 문서에 적힌 의도·주장 | 가설 등록, 결정 이력 설명 |
| `E1` | 코드·asset·자동 테스트·정적 inspection | 구현/계측 존재, 계산 계약 |
| `E2` | 내부 기능 Play·시각 확인 | 특정 build/조건에서 동작함 |
| `E3` | 사전 protocol을 사용한 구조화 정성 테스트 | 관찰 범위에서 가설 `supported/refuted` 가능 |
| `E4` | 반복 가능한 표본과 집계 정의를 갖춘 정량 근거 | 지표 분포·cohort 비교와 반복성 판단 |

- `functional` 또는 `E2`는 재미, 이해도, 긴장감, 재방문 의도의 지지 근거로 쓰지 않는다.
- `supported`와 `refuted`는 연결된 실제 산출물이 `E3` 이상일 때만 쓴다.
- E3/E4도 영구 진리가 아니다. build, config, cohort, mode가 바뀌면 적용 범위를 다시 판정한다.
- 상충하는 evidence를 삭제하거나 평균내 숨기지 않는다. 각각의 조건을 보존하고 claim에
  `superseded_by` 또는 반대 evidence 링크를 남긴다.

## 저장 위치

Git에는 검토 가능한 **익명화·최소화된 manifest와 derived artifact**만 둔다. 권장 경로는 다음과
같다. 이 구조는 산출물이 생길 때 만들며 빈 폴더를 미리 커밋하지 않는다.

```text
evidence/
├── playtests/{study-id}/
│   ├── README.md
│   ├── observations.csv
│   └── findings.md
├── surveys/{study-id}/
│   ├── README.md
│   └── aggregate.csv
├── interviews/{study-id}/
│   ├── README.md
│   └── coded-findings.md
└── telemetry/{study-id}/
    ├── README.md
    ├── query.sql
    └── aggregate.csv
```

원본 영상·음성·자유서술 원문·전체 session log는 접근 통제된 외부 저장소에 둔다. Git의 manifest에는
opaque external record id, 보존 기한과 담당자만 기록하고 공유 URL에 token/query secret을 넣지 않는다.

## 공통 manifest

각 `{study-id}/README.md`는 다음 필드를 빠짐없이 기록한다.

```yaml
evidence_id: EVID-YYYYMMDD-NNN
study_id: short-kebab-case
status: planned | collecting | closed | superseded
evidence_level: E3 | E4
owners: [Product, Client, Server]
collected_at: YYYY-MM-DD..YYYY-MM-DD
as_of_commit: full-git-sha
build_id: immutable-build-id
client_version: string
server_version: string
protocol_version: string
content_version_or_hash: string
mode: first-match | first-return | normal | reconnect | other
environment: internal | staging | production
cohort_definition: string
participant_count: integer
inclusion_exclusion: string
linked_claim_ids:
  - LRN-PROD-...
  - VAL-PROD-...
  - PRD-IN-...
  - PRD-MET-...
  - BASE-...
  - ENG-...
  - TRN-...
  - ADR-CAND-...
protocol_path: relative-path
artifact_paths: [relative-path]
external_raw_record_ids: [opaque-id]
analysis_method: string
known_biases: string
retention_until: YYYY-MM-DD
approved_by: [role-or-pseudonymous-id]
```

결측 필드는 삭제하지 말고 `unknown` 또는 `not-applicable`과 이유를 적는다. `as_of_commit`,
build/server/content version이 없으면 다른 실행을 같은 조건으로 재현했다고 주장할 수 없다.

## 산출물별 최소 형식

### 구조화 플레이테스트

- 사전 문서에 질문, 가설, 대상 cohort, task, 시작/종료 조건, 관찰 항목과 중단 조건을 적는다.
- 첫 판·첫 재방문·일반 판을 별도 mode로 기록한다.
- 관찰 행은 최소 `participant_key`, `session_key`, `build_id`, `mode`, `task_id`,
  `started_at`, `ended_at`, `outcome`, `observer_code`, `note_code`를 가진다.
- 관찰자의 해석과 실제 행동을 분리한다. 예: `action=next_wave_pressed`와
  `interpretation=understood_risk`를 같은 필드에 쓰지 않는다.
- E3 판정에는 모집/진행 protocol, 익명화된 observation, 질문 원문, coding 기준과 finding이 모두
  필요하다. 단순 “몇 명이 재미있다고 했다” 메모는 E2를 넘지 않는다.

### Telemetry와 전투 로그

- event에는 `event_name`, `event_version`, `occurred_at`, pseudonymous `participant_key`,
  `session_key`, `match_id`, client/server/build/content/protocol version을 포함한다.
- session funnel은 최소 `lobby_shown → start_requested → play_accepted → gift_started →
  placement_started → battle_started → tally_started → result_shown → lobby_returned`를 구분한다.
- 핵심 행동은 squad/deck 변경, 배치/회수, resource 보유·소비, unaffordable attempt,
  Awakening hand open/use, Next Wave, leak/stress, 승패와 server 확정 score를 연결할 수 있어야 한다.
- server와 client event는 공통 match id와 correlation id로 결합하되, client 제출값과 server
  authoritative 값을 별도 필드로 보존한다.
- E4에는 query 또는 분석 script, 집계 단위, denominator, bot/test exclusion, timezone,
  중복 제거와 결측 처리 규칙, 표본 크기와 관측 기간이 필요하다.
- 저장소에는 원시 전체 log 대신 재현 가능한 query와 비식별 aggregate를 둔다. 작은 cell로
  개인을 역추정할 수 있으면 bucket을 합치거나 suppress한다.

### 설문

- 질문 문구, 응답 척도, 표시 순서, 필수 여부와 조사 시점을 version 관리한다.
- 자유서술 원문은 외부 제한 저장소에 두고, Git에는 비식별 code와 aggregate만 둔다.
- 응답률의 denominator와 무응답/중도 이탈을 함께 기록한다.
- 편의 표본의 만족도 수치를 전체 사용자 선호로 일반화하지 않는다.

### 인터뷰

- interview guide, 대상 조건, 진행자, 길이, 녹음·전사 동의 여부를 기록한다.
- Git에는 participant 이름이나 원문 transcript 대신 pseudonymous key, coded observation,
  짧은 비식별 paraphrase와 finding을 둔다.
- 패배 귀인 같은 질문은 먼저 open question으로 받고, 점수 축이나 기능명을 제시한 유도 질문은
  별도 표시한다.
- theme의 근거 participant 수와 반례를 함께 기록한다.

## 익명화와 보안

다음 항목은 Git에 커밋하지 않는다.

- 이름, 이메일, 전화번호, 조직·기기 식별자, IP, 위치, 원본 Firebase UID/userId 등 직접·간접 PII.
- Firebase `idToken`/`refreshToken`, `Authorization` header, cookie, session token, API secret,
  signed URL과 credential이 든 request/response dump.
- 원본 음성·영상·화면 녹화, 전체 자유서술·transcript, 접근 통제되지 않은 raw battle log.
- token이나 PII가 섞일 수 있는 server debug dump와 complete `debug` payload 원문.

`participant_key`는 study별 pseudonym을 사용한다. 변환 salt/key와 원본 대응표는 Git 밖의 제한
저장소에 분리하고, 여러 study를 가로질러 같은 사람을 추적할 필요가 없다면 매번 새 key를 만든다.
삭제 요청과 retention 만료 시 원본과 대응표를 삭제할 수 있도록 담당자와 위치를 manifest에 남긴다.

## Claim 연결 계약

Evidence를 인용하는 claim은 다음 필드를 모두 유지한다.

```yaml
claim_id: DOMAIN-NNN
claim_kind: fact | decision | hypothesis
evidence_status: untested | instrumented | functional | internal-observed | supported | refuted | superseded
evidence_level: E0 | E1 | E2 | E3 | E4
as_of: YYYY-MM-DD
mode_and_conditions: string
evidence:
  - evidence_id: EVID-YYYYMMDD-NNN
    artifact: relative-path
    commit_or_build: immutable-id
transfer_action: carry | adapt | retest | drop | decide
production_impact: string
next_validation_or_decision: string
```

한 claim이 여러 evidence를 참조하면 가장 높은 level만 적고 낮은 level을 숨기지 않는다.
반대로 구현 경로만 있는 claim을 E3로 올리지 않는다. PRD가 작성되면 해당 product input과
evidence를 연결하고, ADR이 승인되면 후보의 decision driver에 동일 evidence id를 연결한다.

## 현재 inventory

### EVID-INV-001 — 저장소의 구조화 evidence 공백

- `claim_kind`: `fact`
- `evidence_status`: `instrumented`
- `evidence_level`: `E1`
- `as_of`: `2026-07-29 @ 44c87885`
- 적용 모드·조건: Git 추적 파일과 현재 `BattleLogger` 구현.
- 주장: client log 생성·snapshot 전송 기능은 있으나, 이 기준 커밋에는 위 manifest를 만족하는
  playtest, telemetry aggregate, survey 또는 interview evidence artifact가 없다.
- 근거:
  - [BattleLogger.cs](../../../Assets/_Project/Scripts/Logging/BattleLogger.cs).
  - [Tournament play/report spec](../../spec/tournament-play-report/README.md).
  - 기준 커밋의 tracked file inventory.
- `transfer_action`: `adapt`
- 정규 프로젝트 영향: 재미 검증 완료를 선언할 수 없으며, 첫 E3/E4 study부터 이 계약으로
  provenance와 개인정보 경계를 확보해야 한다.
- 다음 검증·결정: validation backlog의 최우선 실험에 `study-id`를 부여하고, raw storage owner와
  retention을 정한 뒤 수집을 시작한다.
