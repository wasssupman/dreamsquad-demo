# Demo Experience Coverage Map — Client

> 이 표는 구현 inventory나 parity evidence가 아니다. Freeze 시 관찰 가능한 Demo 경험을
> Production Client 책임으로 빠뜨리지 않기 위한 coverage map이다.

`PT-DEC-PRODUCT-001`이 open이므로 현재 모든 surface는 `decision-blocked`다.

| Surface ID | 관찰 가능한 경험 | Client production 책임 | 상태 | Blocking decision |
|---|---|---|---|---|
| `CLI-SURF-001` | 로그인·로비·편성·덱 준비 후 전투 진입 | 화면 흐름, 입력, loading/error feedback, presentation catalog | decision-blocked | `PT-DEC-PRODUCT-001` |
| `CLI-SURF-002` | 경기 규칙/기믹 인지와 선물·배치 전 phase | Phase projection, reveal/deal presentation, skip/confirm UX | decision-blocked | `PT-DEC-PRODUCT-001` |
| `CLI-SURF-003` | 유닛 배치·재배치·취소와 타일/범위 preview | Intent 생성, pending/accept/reject/correct feedback, non-authoritative preview | decision-blocked | `PT-DEC-PRODUCT-001`, `PT-DEC-CLIENT-001` |
| `CLI-SURF-004` | 드림캐쳐 선택·조준·사용, 자원/HUD, Next Wave | Intent UX, authoritative resource/phase projection과 correction | decision-blocked | `PT-DEC-PRODUCT-001`, `PT-DEC-CLIENT-001` |
| `CLI-SURF-005` | Spawn·이동·공격·투사체·피격·상태·사망·해저드·보스 | Actor projection, ordered state/event cue, catalog mapping과 dedupe | decision-blocked | `PT-DEC-PRODUCT-001` |
| `CLI-SURF-006` | 점수 집계·승패·랭킹·재도전/로비 복귀 | Authoritative result 표시, tally presentation과 다음 행동 UX | decision-blocked | `PT-DEC-PRODUCT-001` |
| `CLI-SURF-007` | 지연·중복·거절·정정·연결 중단·복귀 | Pending/correction/resync/reconnect UX와 진단 가능한 fallback | decision-blocked | `PT-DEC-CLIENT-001`, `PT-DEC-SERVER-002` |
| `CLI-SURF-008` | Replay가 포함될 경우 동일 경기의 인과 재현 | Viewer projection, playback controls, visibility와 cue policy | decision-blocked | `PT-DEC-PRODUCT-001`, `PT-DEC-SERVER-002` |

Final reconciliation에서는 각 row를 `included` 또는 `excluded`로 확정하고, included row의
대표 입력·state·cue·failure UX를 같은 ID 아래 짧게 보완한다. 코드/asset 목록과 screenshot
evidence는 이 문서에 넣지 않는다.
