# 장부 — 브리지/Entities 결합 도구 처분표 (unit 0 · 항목 6 · tools.md)

> 생성 2026-09-23. 완전성 리뷰 항목 6·순서 오류 6 의 자리. 「삭제 예정 폴더 안」= `Scripts/Battle/`·`Scripts/Bridge/` — unit 9 의 폴더 삭제에 쓸려 가므로 그 전에 처분이 끝나야 한다.

| # | 도구 | 위치 | 삭제 예정 폴더 안 | 하는 일 | 처분 | 시점 |
|---|---|---|---|---|---|---|
| 1 | `SimHarnessRunner` | `Editor/Battle/` | 아니오 | 시나리오·입력 스케줄·상태 지문(골든 몸통). `Run(BattleBridge …)` | **새 코어용 러너 신설**(`CoreHarnessRunner`) — 옛 러너는 unit 9 까지 병존(A/B) | unit 1 |
| 2 | `SimHarnessRunMenu` | `Editor/Battle/` | 아니오 | 하네스 1회 실행 메뉴 | 새 메뉴로 재작성(코어 러너 호출) | unit 1 |
| 3 | `SimGoldenMenu` | `Editor/Battle/` | 아니오 | 코퍼스 굽기·검증. 브리지 없으면 하드 실패 | 새 코어 골든 메뉴 재작성(`configHash` = `MatchDefinition`) | unit 1 |
| 4 | `SimOrderDumpMenu` | `Editor/Battle/` | 아니오 | ECS 시스템 총순서 덤프 | **은퇴**(새 코어의 순서는 코드 그 자체 — `TickPipeline` 나열) | unit 9 |
| 5 | `DetectionProbeMenu` | `Editor/Battle/` | 아니오 | 감지 반경 프로브(월드 질의) | 코어 읽기 모델 위에서 재작성 | **unit 5a**(조각 A 는 안 만들고 지나갔다) |
| 6 | `HazardDebugMenu` | `Battle/Effects/` | **예** | 존 해저드 수동 스폰 | 코어 커맨드(디버그 전용) 로 재작성 | **조각 C 앞** |
| 7 | `BlockingHazardDebugMenu` | `Battle/Effects/` | **예** | 길막 해저드 수동 스폰 | 위와 통합 재작성 | **조각 C 앞** |
| 8 | `ObstacleDebugMenu` | `Battle/Effects/` | **예** | 장애물 토글·흐름장 재빌드 확인 | 코어 `MapRuntime` 디버그 커맨드로 재작성 | **unit 5a** |
| 9 | `FatigueDebugMenu` | `Battle/Effects/` | **예** | 피로 스택 강제 | 기믹 호스트 디버그 커맨드로 재작성 | **조각 C 앞** |
| 10 | `PatrolDebugMenu` | `Battle/Movement/` | **예** | 순찰병 수동 스폰 | 코어 스폰 커맨드로 재작성 | **조각 D 앞** |
| 11 | `RelocationDebugMenu` | `Bridge/` | **예** | 재배치 강제 | `PlacementService` 디버그 커맨드로 재작성 | **조각 D 앞** |

원칙: 디버그 도구는 브리지 메서드를 부르는 대신 **코어 커맨드**(`Command.Kind = Debug*`)를 넣는다 — 그래야 하네스·리플레이에서도 같은 길을 탄다. 완전성 리뷰가 「10개」로 센 것은 `SimHarnessRunner` 를 메뉴에 포함해 셌기 때문이며 실측 파일은 11.
