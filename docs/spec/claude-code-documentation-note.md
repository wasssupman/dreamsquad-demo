# Claude Code Documentation Note

이 프로젝트의 `docs/spec/` 문서는 구현 상세를 복제하는 장소가 아니라, 작업 계약과 세션 인계를 관리하는 장소다.

## 읽는 순서

1. `docs/spec/{feature-slug}/README.md`
   - 현재 상태
   - feature-wide 계약
   - 작업 문서 인덱스
   - 후속 후보
2. 있으면 `{N+1}_handoff_summary.md`
   - 최근 커밋 이후 무엇이 들어갔는지
   - 어떤 파일을 봐야 하는지
   - 검증 상태와 주의점
3. 실제 작업 대상 `{N}_{topic}.md`
   - 그 커밋 단위의 목적/변경 대상/구현/완료 기준

## Source Of Truth

- README: 최신 상태와 feature-wide 계약
- `{N}_{topic}.md`: 작업 단위 계약과 완료 기준
- handoff summary: 세션 인계 지도
- 코드 + git history: 구현 상세

문서는 구현 상세를 전부 따라가지 않는다. 단, 계약이 바뀌면 README 또는 관련 번호 문서를 갱신한다.

## 작성 규칙

- README 에는 상태 라인, 목표, 문서 목록, 공통 계약, 비목표/후속 후보만 둔다.
- 번호 문서는 1커밋에 가까운 작은 작업 단위로 유지한다.
- handoff 는 30~80줄로 제한한다.
- handoff 필수 섹션은 `Commit / Implemented / Key Files / Verified / Notes / Follow-up` 이다.
- diff 전체를 prose 로 다시 쓰지 않는다.
- 오래 유지될 보장 없는 추측을 사실처럼 쓰지 않는다.

## Review 반영 기준

- 코드 버그를 유발하는 계약 공백: 코드 + 테스트 + 관련 spec 갱신
- 구현과 문서의 표현 불일치: 문서 갱신
- 단순 구현 설명 요구: handoff 에 짧게 쓰거나 생략
- 미래 확장/취향 제안: 후속 후보 또는 Follow-up 으로 이동

## 한 줄 요약

계약은 문서에 남기고, 구현 상세는 코드/커밋에 둔다. handoff 는 다음 세션이 빠르게 안전한 시작점을 찾기 위한 지도다.
