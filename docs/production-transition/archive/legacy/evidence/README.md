# Evidence 산출물 가이드

> **DORMANT · OWNER-GATED · NOT A DEMO STUDY BACKLOG.** Project owner의 명시적 transition 활성화 전에는 계측·실험·수집 작업을 시작하거나 Demo 완료를 차단하지 않는다.

> 상태: **Historical · preparatory contract**
>
> 기준일: **2026-07-29**
>
> 적용 범위: 정규 프로젝트 PRD/ADR의 근거로 인용할 플레이테스트·로그·설문·인터뷰 산출물

Evidence의 `closed/supported`만으로 current applicability를 뜻하지 않는다. Official include에
쓰려면 registry에서 해당 freeze의 ruleset/presentation/build와 대조해 `freshness: current`로
검토해야 한다. 바뀐 watch path와 연결된 evidence는 재검토 전 `stale`다.

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
ruleset_version_or_hash: string
presentation_version_or_hash: string
recording_schema_version: string | not-applicable-with-reason
authoritative_record_id: opaque-id | not-applicable-with-reason
viewer_mode: live-player | replay | spectator | diagnostic | not-applicable-with-reason
viewer_role: versioned-role-id | not-applicable-with-reason
viewpoint_subject_key: study-pseudonymous-stable-id | not-applicable-with-reason
projection_policy_version_or_hash: string | not-applicable-with-reason
viewpoint_profile_version_or_hash: string | not-applicable-with-reason
visibility_policy_version_or_hash: string | not-applicable-with-reason
source_presentation_version_or_hash: string | not-applicable-with-reason
playback_presentation_version_or_hash: string | not-applicable-with-reason
effective_delay_ms: integer | not-applicable-with-reason
correlation_contract_path: relative-path | not-applicable-with-reason
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
build/server/ruleset/presentation version이 없으면 다른 실행을 같은 조건으로 재현했다고 주장할 수
없다. Telemetry를 포함한 study는 `correlation_contract_path`에 아래 네 단계의 event 필드와 결합
규칙을 정의한 schema 또는 query를 연결한다. 순수 설문·인터뷰처럼 event correlation이 없는
study만 이유를 적은 `not-applicable`을 허용한다.

`mode`는 첫 판·재방문·재접속 같은 gameplay/session 조건이고 `viewer_mode`는 같은 match를 어느
표면에서 관찰했는지 구분한다. `viewer_role`은 해당 정책의 role ID, `viewpoint_subject_key`는
player-follow처럼 관점 대상이 있을 때 쓰는 study 범위 pseudonymous stable ID다.
`projection_policy_version_or_hash`는 적용한 viewpoint·visibility·접근·공개 시점·delay 정책
조합을 식별하며, 독립 배포되는 `viewpoint_profile_version_or_hash`와
`visibility_policy_version_or_hash`도 함께 기록한다. Replay·Spectator 비교 study에서
`authoritative_record_id`, `recording_schema_version`, viewer role·subject와 위 policy version을
`unknown`으로 둔 채 fidelity를 주장할 수 없다. `presentation_version_or_hash`는 해당 study가
직접 관찰한 Client presentation을 가리킨다. cross-mode 비교에서는 당시 live Client의
`source_presentation_version_or_hash`와 재생 Client의
`playback_presentation_version_or_hash`도 따로 기록한다. `effective_delay_ms`는 Spectator에서
Server가 적용한 실제 지연을 기록하고 다른 mode에는 이유를 적은 `not-applicable`을 사용한다.

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
  `session_key`, `match_id`, client/server/build/ruleset/presentation/protocol version을 포함한다.
- Authoritative Match Record의 semantic event에는 `authoritative_record_id`,
  `recording_schema_version`, match 안에서 유일한 `authoritative_event_id`와 `authoritative_tick`을
  포함한다. Live player·Replay·Spectator 사이의 canonical join key는
  **`(match_id, authoritative_event_id)`**다.
- Viewer Projection event에는 `viewer_projection_event_id`, canonical join key, `viewer_mode`,
  viewer role·subject, projection/viewpoint/visibility policy version, projection stream
  sequence/cursor, emit 시각과 적용된 delay를 포함한다. 해당 policy가 허용한 semantic field만
  담으며, 숨은 정보를 Client로 보낸 뒤 presentation에서 가리는 방식은 evidence 경계로 인정하지
  않는다.
- session funnel은 최소 `lobby_shown → start_requested → play_accepted → gift_started →
  placement_started → battle_started → tally_started → result_shown → lobby_returned`를 구분한다.
- 핵심 행동은 squad/deck 변경, 배치/회수, resource 보유·소비, unaffordable attempt,
  Awakening hand open/use, Next Wave, leak/stress, 승패와 server 확정 score를 연결할 수 있어야 한다.
- Client 입력에서 시작해 게임 결과에 영향을 주고 Server가 수락한 행동은 하나의 opaque
  `correlation_id`로 다음 네 단계를 연결한다. 같은 match 안에서 ID가 유일해야 하며 PII, 인증
  token 또는 원본 user id를 인코딩하지 않는다.

    1. client `command/input` event: 같은 `correlation_id`, client command/input event ID, 행동
       의도와 client 관측 시각을 기록한다. client가 계산한 결과나 prediction은 별도 비권위 필드로만
       기록한다.
    2. server `authoritative event/tick`: `(match_id, authoritative_event_id)`, authoritative tick,
       적용 가능한 경우 같은 `correlation_id`를 기록하고 수락된 command가 만든 실제 상태
       전이·결과를 보존한다.
    3. server `viewer projection event`: `viewer_projection_event_id`, canonical join key,
       `viewer_mode`, viewer role·subject, 적용한 policy version, stream sequence/cursor와 emit
       시각·delay를 기록한다.
    4. client `presentation event`: canonical join key와 `viewer_projection_event_id`,
       presentation event ID, 적용 가능한 경우 같은 `correlation_id`, 표시 시각과
       표시·정정·억제·중복 제거 결과를 기록한다.
- 거절된 command는 `correlation_id`와 별도 `command_decision_id`로 수락 여부·거절 이유를
  audit하고 canonical progression 밖에 둔다. 거절 자체가 별도의 authoritative gameplay event로
  정의되지 않았다면 `authoritative_event_id`를 만들거나 canonical Replay에 삽입하지 않는다.
- timer·AI·gameplay RNG·spawn처럼 Client 입력 없이 Server에서 발생하는 event에 가짜
  `correlation_id`를 만들지 않는다. 이 event들은 canonical join key로 projection·presentation과
  연결한다.
- server의 실제 결과, viewer별 projection과 사용자가 본 presentation은 서로 다른 event/필드로
  보존한다. Client presentation이 누락되거나 정정된 경우에도 server 값을 덮어쓰지 않는다.
  하나의 authoritative event는 권한·관점·policy별 `0..N` projection event를 만들고, 각 projection
  event는 `0..N` presentation event를 만들 수 있다. 이 fan-out을 여러 authoritative 결과로
  중복 집계하지 않고 canonical join key로 묶는다.
- 결과가 확정되기 전의 Client prediction event는 `viewer_mode: diagnostic`의 비권위 trace로만
  보존할 수 있다. 거절·정정된 prediction을 Authoritative Match Record나 canonical Replay
  progression에 삽입하지 않는다.
- E4에는 query 또는 분석 script, 집계 단위, denominator, bot/test exclusion, timezone,
  중복 제거와 결측 처리 규칙, 표본 크기와 관측 기간이 필요하다.
- 저장소에는 원시 전체 log 대신 재현 가능한 query와 비식별 aggregate를 둔다. 작은 cell로
  개인을 역추정할 수 있으면 bucket을 합치거나 suppress한다.

#### Replay·관전 fidelity

Authoritative Match Record, Viewer Projection, 실제 network delivery trace와 Client presentation
trace는 서로 다른 artifact로 보존한다. canonical Replay는 confirmed Server progression에서
생성하며 당시 local prediction, reversal, packet arrival timing이나 camera를 정본처럼 복원하지
않는다. `player-visible authoritative perspective`는 해당 player에게 권한이 있었던 authoritative
semantic event를 뜻하며 실제 화면 녹화인 `as-seen presentation trace`와 구분한다.

- `simulation_fidelity`: authoritative tick 순서, 상태 전이, stable ID, gameplay RNG 결과,
  승패·점수와 progression signature의 일치를 측정한다.
- `observation_fidelity`: 같은 viewpoint·visibility policy에서 보이는 semantic event의
  누락·추가·순서 오류와 숨은 정보의 조기 노출을 측정한다.
- `presentation_fidelity`: 핵심 단서 표시와 인과 이해를 측정한다. camera·interpolation·cosmetic
  RNG·VFX/SFX의 pixel/frame 동일성은 기본 성공 기준으로 사용하지 않는다.

Spectator가 도입되면 Replay와 같은 Viewer Projection 계약을 사용하고 `effective_delay_ms`와
접근 policy를 함께 기록한다. 동일한 `authoritative_record_id`와 projection policy를 사용한 완료
Replay와 Spectator stream은 canonical join key의 semantic event 순서에 수렴해야 한다. pause,
speed, seek와 rewind는 비권위 playback cursor/read model을 바꿀 수 있으며 Replay-control
telemetry로 별도 기록한다. 이를 Server tick, 원 경기 gameplay event 또는 score·reward 같은 권위
결과가 다시 발생한 것으로 집계하지 않는다.

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
