# Transition Decision Register

> Dormant preparation의 미결 Product/기술 질문만 기록한다. 결정 이력과 구현 task log를
> 섞지 않으며 Demo 완료를 차단하지 않는다.

## 상태

`open | proposed | decided | excluded`

Official freeze의 included scope에 연결된 `blocks_freeze: true` decision은 모두 `decided`여야 한다.

| Decision ID | Consumer | 질문 | Owner | 상태 | Blocks freeze | 결정/재개 조건 |
|---|---|---|---|---|---|---|
| `PT-DEC-PRODUCT-001` | common | Approved Demo 경험 중 production-v1에 포함·제외할 surface는 무엇인가? | Product owner | open | true | Final reconciliation에서 두 coverage map과 함께 확정 |
| `PT-DEC-COMMON-001` | common | Intent/result/snapshot/event를 어떤 production protocol과 version policy로 전달할 것인가? | Client + Server tech owners | open | false | Production ADR에서 결정; semantic rule에는 wire를 넣지 않음 |
| `PT-DEC-CLIENT-001` | client | 대표 입력별 pending·rejected·corrected UX와 허용 반응시간은 무엇인가? | Product + Client tech owner | open | true | Included input surface별 acceptance 확정 |
| `PT-DEC-SERVER-001` | game-server | Authoritative tick, numeric/rounding과 gameplay RNG policy는 무엇인가? | Product + Game Server tech owner | open | true | First deterministic slice 전에 production ADR로 결정 |
| `PT-DEC-SERVER-002` | game-server | Reconnect, terminal finality와 replay/audit 보존 범위는 무엇인가? | Product + Game Server tech owner | open | true | Included lifecycle 범위와 함께 결정 |

새 질문은 의미가 다를 때만 새 ID를 만든다. 질문을 조용히 바꾸거나 Product 결정을 tech owner가
대신하지 않는다. 결정 결과는 짧은 의미와 production ADR/owner reference만 남긴다.
