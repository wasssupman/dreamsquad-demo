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
| 10 | `PatrolDebugMenu` | `Battle/Movement/` | **예** | 순찰병 수동 스폰 | **완료** — `Editor/BattleCore/CoreSummonDebugMenu.cs`. `DebugSummonPatrol`(24) — 소환사와 **같은 조립 자리**(`CombatPhase.SpawnPatrol`)라 구역·이동·공격이 진짜 소환물과 같고 소환사만 없다(연쇄 소멸 없음). 앵커 = 배치된 첫 방어유닛 칸(옛 메뉴와 같이 커서를 안 쓴다) · 반경 2/4 · 구역 찍기. 짝 도구 `CoreTriggerDebugMenu`(`DebugFireBinding` 25 — 규칙 목록 · 강제 발화 · 「왜 안 터졌나」 4원인, 판정은 코어 `BindingDiagnosis`) | unit 7d |
| 11 | `RelocationDebugMenu` | `Bridge/` | **예** | 재배치 강제 | **은퇴** — 재배치(유닛 이동)는 이식 제외(`defender-clock-out/0` 이 진입구를 껐고 퇴근이 대신한다 · 7d 「이식 제외」). 구동할 기능이 없다 | unit 7d |
| 12 | `ReachDebugGizmos` | `Presentation/` | 아니오(단 퇴역 목록 3번 묶음) | 씬 뷰 기즈모로 브리지 전용 `BattleBridge.DebugCollectReachSpheres` 의 도달 구(사거리 + 몸)를 그린다. 어느 씬·프리팹에도 붙어 있지 않다(런타임 부착 0) | **은퇴(에이전트 판정)** — 옛 spec 결정의 인용이 아니라 **대체물이 근거**다: 새 코어의 도달 자는 `CoreMapOverlay.PaintRange`(링 = 사거리 + 내 몸)가 그리고, 수치 정합은 `AttackReachParityTests`(20,000건 불일치 0)가 증언했다. 8c 가 누락을 찾아 이 행을 더했다 | unit 9 |

원칙: 디버그 도구는 브리지 메서드를 부르는 대신 **코어 커맨드**(`Command.Kind = Debug*`)를 넣는다 — 그래야 하네스·리플레이에서도 같은 길을 탄다. 완전성 리뷰가 「10개」로 센 것은 `SimHarnessRunner` 를 메뉴에 포함해 셌기 때문이며 실측 파일은 11.

> **7d 에서 도구 11행 전건 처분 완료**(완료 9 · 은퇴 1 · 예정 1 = 4행 `SimOrderDumpMenu` 은퇴는 unit 9).
> **8c 에서 누락 1행(12 `ReachDebugGizmos`)을 더했다** — 11행 표가 `Presentation/` 을 안 훑었다. 12행 처분: 완료 9 · 은퇴 2 · 예정 1(4행 · 12행은 unit 9 가 `retire-set.md` 대로 지운다).
