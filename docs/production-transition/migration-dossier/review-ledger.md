# Legacy 이식 준비 문서 검토 이력

> 상태: **Historical · non-approving**
>
> 현재 review 정본: [`../governance/reviews.json`](../governance/reviews.json)

이 이력은 삭제하지 않는 감사 자료다. Draft 작성, 자동 점검, review request와 defer는
승인이 아니다. 현재 승인 키는 `(area_id, card_id, document_revision, source_commit)`이며
여러 area를 한 행으로 승인할 수 없다.

| review_id | area_id | card_id | document_revision | source_commit | outcome | approval | reviewed_by | summary | supersedes |
|---|---|---|---|---|---|---|---|---|---|
| `MIG-REVIEW-001` | `none` | `MIG-CARD-CORE-001` | `a7aaf9675f11210ad63cc6f714ce45f2415fcd36` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` | `deferred` | `false` | `repository-owner` | `MIG-DEC-CORE-001`~`006`을 보류했다. Gameplay 규칙, 어떤 area, Server 채택, freeze 또는 migration kickoff도 승인하지 않았다. | `none` |

새 review는 이 표에 append하지 않는다. Package card와 registry record의 exact revision/source를
대상으로 전역 `reviews.json`에 area별로 기록한다. 실제 reviewer가 없으면 placeholder approval을
만들지 않는다.
