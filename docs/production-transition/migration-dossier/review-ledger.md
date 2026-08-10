# 이식 준비 문서 검토 이력

이 이력은 append-only다. 실제 검토 사건만 기록하며 draft 작성이나 자동 점검을
검토로 간주하지 않는다. 아직 담당자 검토가 없으므로 행은 비어 있다.
`card_revision`은 `<card_id>@<review-candidate-commit>` 형식을 사용한다.
`review-candidate-commit`은 owner에게 최종 확인을 요청한 card와 decision 문구가
담긴 demo 저장소의 local commit이다. `as_of_commit`은 card가 설명하는 gameplay
source commit으로 별도 기록한다. `from_status`와 `to_status`는 `draft`,
`review_requested`, `reviewed`, `stale`, `frozen`, `superseded` 중 하나를 사용한다.
개인 식별자 대신 역할만 `reviewed_by`에 기록한다.

| review_id | card_id | card_revision | as_of_commit | from_status | to_status | reviewed_by | summary | supersedes |
|---|---|---|---|---|---|---|---|---|
