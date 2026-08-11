# 게임플레이 결정 등록부

> 상태: **Historical · stale · preparatory**
>
> 근거 기준: `2d35df0680ce97d29b78101120cb9fae63c5a8ad`

이 문서는 legacy dossier 카드가 의존했던 gameplay 결정과 질문의 역사적
등록부다. 현재 decision 정본은 [`../governance/decisions.json`](../governance/decisions.json)이다.
`proposed`는 활성 spec에서 확인한 source-backed 규칙을 Demo의 intended
gameplay 의미로 승인할지 owner 확인이 끝나지 않았다는 뜻이다. `open`은 현재
근거만으로 답할 수 없는 실제 product decision이다. `decided`만 readiness 해제
근거로 사용할 수 있다. 같은 질문의 상태가 바뀌면 해당 행을 갱신하며 Git
history가 변경 이력을 보존한다. 의미가 다른 별도 결정에만 새 ID를 부여한다.
`deferred`는 규칙을 승인하거나 반려한 것이 아니라 후속 owner review에서
재확인해야 하는 미결 상태다. 이때 `decision`은 `none`이고 readiness 차단은
유지한다.

| decision_id | status | domain | question | decision | affected_cards | blocks_readiness | owner | as_of_commit |
|---|---|---|---|---|---|---|---|---|
| `MIG-DEC-CORE-001` | `deferred` | Targeting과 attack | Direct-attack range 경계 포함과 이동 종료·공격 가능성의 동일 격자 거리 의미를 Demo의 intended gameplay 의미로 승인하는가? | `none` | `MIG-CARD-CORE-001` | `true` | `owner` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` |
| `MIG-DEC-CORE-002` | `deferred` | Targeting과 attack | 공격 시작, 판정 지연과 다음 attack readiness의 관계를 Demo의 intended gameplay 의미로 승인하는가? | `none` | `MIG-CARD-CORE-001` | `true` | `owner` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` |
| `MIG-DEC-CORE-003` | `deferred` | Targeting과 attack | Engagement 상태를 attack 시작 조건으로 쓰되 readiness 진행은 그 상태 밖에서도 계속되는 규칙을 Demo의 intended gameplay 의미로 승인하는가? | `none` | `MIG-CARD-CORE-001` | `true` | `owner` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` |
| `MIG-DEC-CORE-004` | `deferred` | Damage, heal과 death | Lethal removal과 endpoint breach를 서로 다른 outcome으로 취급하는 규칙을 Demo의 intended gameplay 의미로 승인하는가? | `none` | `MIG-CARD-CORE-001` | `true` | `owner` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` |
| `MIG-DEC-CORE-005` | `deferred` | Spawn과 wave | Pending arrival와 active attacker가 모두 없으면 current wave interval clear이고, future wave까지 없을 때만 final all-clear인 규칙을 Demo의 intended gameplay 의미로 승인하는가? | `none` | `MIG-CARD-CORE-001` | `true` | `owner` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` |
| `MIG-DEC-CORE-006` | `deferred` | Match lifecycle와 terminal | Main mode의 breach-limit defeat, final all-clear victory와 timer survival victory를 Demo의 intended gameplay 의미로 승인하는가? | `none` | `MIG-CARD-CORE-001` | `true` | `owner` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` |
| `MIG-DEC-CORE-007` | `open` | Match lifecycle와 terminal | Terminal outcome 확정 뒤 어떤 gameplay state가 더 변할 수 있는가? | `none` | `MIG-CARD-CORE-001` | `true` | `owner` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` |
| `MIG-DEC-CORE-008` | `open` | Mode와 content rule | Endless variant는 timer만으로 끝나는가, early all-clear도 허용하는가? | `none` | `MIG-CARD-CORE-001` | `true` | `owner` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` |
| `MIG-DEC-CORE-009` | `open` | Spawn과 wave | 새로 spawn된 actor는 언제 처음 행동할 수 있는가? | `none` | `MIG-CARD-CORE-001` | `true` | `owner` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` |
| `MIG-DEC-CORE-010` | `open` | Unit movement와 breach | 같은 logical step에 lethal damage와 endpoint 도달이 모두 성립하면 무엇이 우선하는가? | `none` | `MIG-CARD-CORE-001` | `true` | `owner` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` |
| `MIG-DEC-CORE-011` | `open` | Targeting과 attack | Spawned attacker가 행동 eligibility를 얻었을 때 첫 attack readiness는 어떤 상태인가? | `none` | `MIG-CARD-CORE-001` | `true` | `owner` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` |
| `MIG-DEC-CORE-012` | `open` | Match lifecycle와 terminal | 여러 terminal 조건이 같은 logical step에 성립하면 어떤 단일 outcome을 선택하는가? | `none` | `MIG-CARD-CORE-001` | `true` | `owner` | `2d35df0680ce97d29b78101120cb9fae63c5a8ad` |
