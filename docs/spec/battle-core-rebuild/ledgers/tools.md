# 장부 — 브리지/Entities 결합 도구 처분표 (unit 0 · 항목 6 · tools.md)

> 생성 2026-09-23. 완전성 리뷰 항목 6·순서 오류 6 의 자리. 「삭제 예정 폴더 안」= `Scripts/Battle/`·`Scripts/Bridge/` — unit 9 의 폴더 삭제에 쓸려 가므로 그 전에 처분이 끝나야 한다.

| # | 도구 | 위치 | 삭제 예정 폴더 안 | 하는 일 | 처분 | 시점 |
|---|---|---|---|---|---|---|
| 1 | `SimHarnessRunner` | `Editor/Battle/` | 아니오 | 시나리오·입력 스케줄·상태 지문(골든 몸통). `Run(BattleBridge …)` | **새 코어용 러너 신설**(`CoreHarnessRunner`) — 옛 러너는 unit 9 까지 병존(A/B) | unit 1 |
| 2 | `SimHarnessRunMenu` | `Editor/Battle/` | 아니오 | 하네스 1회 실행 메뉴 | 새 메뉴로 재작성(코어 러너 호출) | unit 1 |
| 3 | `SimGoldenMenu` | `Editor/Battle/` | 아니오 | 코퍼스 굽기·검증. 브리지 없으면 하드 실패 | 새 코어 골든 메뉴 재작성(`configHash` = `MatchDefinition`) | unit 1 |
| 4 | `SimOrderDumpMenu` | `Editor/Battle/` | 아니오 | ECS 시스템 총순서 덤프 | **은퇴**(새 코어의 순서는 코드 그 자체 — `TickPipeline` 나열) | unit 9 |
| 5 | `DetectionProbeMenu` | `Editor/Battle/` | 아니오 | 감지 반경 프로브(월드 질의) | **완료** — `Editor/BattleCore/CoreDetectionProbeMenu.cs`. 옛 것은 고정 스텝 하네스를 새로 돌려 «감지를 넣기 전 기준선»을 쟀고 그 답은 이미 나왔다. 새 것은 **지금 돌고 있는 판을 읽어** 적마다 (반경·사냥·관성·막힘·억제·표식쿨·최근접 방어유닛)을 찍는다 — 「적이 왜 안 쫓아오나」의 네 원인(반경 밖·억제 창·통행 층·관성)이 화면에서 구분되지 않기 때문 | unit 5a |
| 6 | `HazardDebugMenu` | `Battle/Effects/` | **예** | 존 해저드 수동 스폰 | **완료** — `Editor/BattleCore/CoreHazardDebugMenu.cs`(존·길막 통합). `DebugSpawnHazard`(17) 커맨드 · 정의표 줄(`BattleDriver._hazards`)로 깐다 · 「왜 안 걸렸나」 네 원인(자격·진영/통행층·반경·병합 키) 프로브 | unit 6c |
| 7 | `BlockingHazardDebugMenu` | `Battle/Effects/` | **예** | 길막 해저드 수동 스폰 | **완료** — 6행과 같은 메뉴. `DebugSpawnBlocker`(18) · 가까운 칸부터 코어가 받을 때까지 시도(거절 사유는 코어 `BlockerSpawn`) | unit 6c |
| 8 | `ObstacleDebugMenu` | `Battle/Effects/` | **예** | 장애물 토글·흐름장 재빌드 확인 | **완료** — `Editor/BattleCore/CoreObstacleDebugMenu.cs`. `CommandKind.DebugSetObstacle` 커맨드를 넣고 막힌 칸 수 + 흐름장 지문을 같이 찍는다(「토글은 됐는데 길이 안 바뀐」 경우가 이 도구의 존재 이유) | unit 5a |
| 9 | `FatigueDebugMenu` | `Battle/Effects/` | **예** | 피로 스택 강제 | **완료** — `Editor/BattleCore/CoreGimmickDebugMenu.cs`. `DebugSetStack`(21, 피로·열기) · `DebugSpawnPickup`(19) · `DebugDropResignation`(20) + 셈판 찍기. ⚠ 픽업·사직서·열기는 **그 기믹이 뽑힌 판**에서만 받는다(`GimmickInactive` — 기본 모드는 기믹 0) | unit 6c |
| 10 | `PatrolDebugMenu` | `Battle/Movement/` | **예** | 순찰병 수동 스폰 | 코어 스폰 커맨드로 재작성 | **조각 D 앞** |
| 11 | `RelocationDebugMenu` | `Bridge/` | **예** | 재배치 강제 | `PlacementService` 디버그 커맨드로 재작성 | **조각 D 앞** |

원칙: 디버그 도구는 브리지 메서드를 부르는 대신 **코어 커맨드**(`Command.Kind = Debug*`)를 넣는다 — 그래야 하네스·리플레이에서도 같은 길을 탄다. 완전성 리뷰가 「10개」로 센 것은 `SimHarnessRunner` 를 메뉴에 포함해 셌기 때문이며 실측 파일은 11.
