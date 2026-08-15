# Transition Change Register

> **Non-export · non-blocking · separate follow-up tasks only**

Demo 개발 중 production에 중요할 수 있는 확정 규칙을 가볍게 capture하는 inbox다. Demo feature
task, spec, handoff, CI와 완료 조건에서는 이 파일을 읽거나 갱신하지 않는다.

## 운영 규칙

- 사용자가 명시한 별도 transition-maintenance 요청과 별도 commit에서만 갱신한다.
- Demo 구현 diff, fixture, screenshot, test log와 evidence를 복제하지 않는다.
- 누락과 stale을 허용한다. 자동 watch path와 freshness 계산을 두지 않는다.
- 하나의 entry는 확정된 의미 변화 하나만 담는다.
- Living rule/coverage에 반영하면 status를 `incorporated`, 무관하면 `discarded`로 바꾼다.
- Official freeze에는 이 파일을 포함하지 않고 final reconciliation에서 open entry만 처리한다.

## Entry 형식

| 필드 | 값 |
|---|---|
| Change ID | `PT-CHG-NNN` 영구 ID |
| Captured at | 날짜 |
| Audience | `common | client | game-server` 복수 가능 |
| Meaning | Production이 보존해야 할 확정 의미 1~3문장 |
| Demo source pointer | 활성 spec/owner decision/commit의 길찾기 정보 |
| Open decision | 없으면 `none`, 있으면 `PT-DEC-*` |
| Status | `captured | incorporated | discarded` |

## Entries

아직 등록된 entry가 없다. Governance 구조 변경 자체는 gameplay/experience change가 아니므로
이 register에 넣지 않는다.
