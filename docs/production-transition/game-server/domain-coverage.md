# Authoritative Domain Coverage — Game Server

> 구현 inventory나 parity evidence가 아니다. Production Server가 Demo 모습을 authoritative하게
> 재현할 때 빠뜨리지 않을 rule domain을 관리한다.

`PT-DEC-PRODUCT-001`이 open이므로 현재 모든 domain은 `decision-blocked`다.

| Domain ID | 권위 범위 | 대표 semantic outcome | 상태 | Blocking decision |
|---|---|---|---|---|
| `SRV-DOM-001` | Match lifecycle, phase, pause/terminal | Match/phase/terminal state와 reason | decision-blocked | `PT-DEC-PRODUCT-001`, `PT-DEC-SERVER-002` |
| `SRV-DOM-002` | Command, ownership, cost/cooldown와 placement/card intent | Accepted/rejected/corrected atomic outcome | decision-blocked | `PT-DEC-PRODUCT-001`, `PT-DEC-COMMON-001` |
| `SRV-DOM-003` | Map, wave, spawn, path, movement와 breach | Versioned map/wave state와 actor progression | decision-blocked | `PT-DEC-PRODUCT-001`, `PT-DEC-SERVER-001` |
| `SRV-DOM-004` | Targeting, attack, projectile, damage, heal와 death | Ordered combat state/event/result | decision-blocked | `PT-DEC-PRODUCT-001`, `PT-DEC-SERVER-001` |
| `SRV-DOM-005` | Modifier, status, stack, skill, hazard와 boss/special | Versioned effect transition과 causal outcome | decision-blocked | `PT-DEC-PRODUCT-001`, `PT-DEC-SERVER-001` |
| `SRV-DOM-006` | Logical time, total order, numeric와 gameplay RNG | Deterministic step and repeatability oracle | decision-blocked | `PT-DEC-SERVER-001` |
| `SRV-DOM-007` | Content/ruleset identity와 match pinning | Validated configuration/version | decision-blocked | `PT-DEC-COMMON-001` |
| `SRV-DOM-008` | Victory/defeat, score, ranking payload와 result finality | Authoritative terminal result and breakdown | decision-blocked | `PT-DEC-PRODUCT-001`, `PT-DEC-SERVER-002` |
| `SRV-DOM-009` | Snapshot, reconnect, replay, audit와 observability | Canonical resume/replay/audit input | decision-blocked | `PT-DEC-SERVER-002` |

Final reconciliation에서는 included domain마다 rule ID와 dependent Client surface를 연결한다.
Excluded domain은 Product owner와 사유를 기록한다. Code path, fixture와 evidence는 넣지 않는다.
