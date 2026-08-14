# Production Client Implementation Waves

> Official frozen input 검증과 별도 Client implementation activation 뒤에만 실행한다.

| Wave | Outcome | 핵심 작업 | Exit gate |
|---|---|---|---|
| `C0` Intake와 authority mapping | Imported rule/coverage를 Somnia Client 정책에 매핑 | Manifest/receipt 검증, accepted ADR 충돌 확인, included surface와 open decision 확정 | 현지 plan 승인; runtime 변경 전 gate |
| `C1` Session·projection 기반 | Live/recorded input을 받을 비권위 projection 경계 | Common semantic version, ordered apply, snapshot replacement, lifecycle/disposal | Fixture-free contract tests와 architecture review |
| `C2` 입력과 correction | 대표 intent의 pending/accept/reject/correct UX | Input orchestration, idempotency correlation, gap/resync/reconnect state | 대표 입력 정상·거절·정정 acceptance |
| `C3` Core battle presentation | Spawn-to-terminal vertical slice의 화면 재현 | Actor lifecycle, combat cues, HUD/result, catalog fallback | Server-authoritative end-to-end slice |
| `C4` Full included experience | Included surface와 content catalog 완성 | Outgame/phase/card/hazard/boss/score surface, localization/accessibility | Experience map included row 전부 충족 |
| `C5` Reliability와 release | 모바일·네트워크·replay 품질 | Reconnect/replay, performance, Android/iOS device, observability | Release validation matrix와 Product acceptance |

뒤 wave는 앞 wave의 public type을 transition 문서가 선결하지 않는다. 각 wave는 Somnia Client의
task router와 validation matrix에 따라 별도 task plan을 만든다.
