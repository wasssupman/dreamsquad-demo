# 12 — 레거시 스테이지 은퇴 (KayKit 조립 프리팹 11종 삭제)

## 목적

사용자 결정(2026-08-26): 라이브 3장 + Duel 외 «나머지는 버린다» — Fixture 포함. `Assets/_Project/Prefabs/Maps/`
의 KayKit 조립 스테이지는 프로토타입 검증용이었고, 이제 스테이지는 `Art/Theme/{theme}/MapStage_*.prefab` 하나의
제작방식으로 통일된다. 이름으로 판을 pin 하던 PlayMode 테스트는 남는 판으로 재지정하거나 판형 전제가 사라진 것은
사유를 적어 Ignore 한다(삭제가 아니라 판정 기록).

## 변경 대상

- 삭제 `Assets/_Project/Prefabs/Maps/` 전체 — Coil·Duel(12×8 옛 것)·DuelClassic·Fixture·Ford·Isle·MovementLab·Pilot·Serpent·Tutorial·Zig (+ .meta, 폴더 .meta). 다른 에셋에서의 참조 0(전수 grep — 풀만 Fixture 를 가리켰고 unit 11 이 교체)
- `MapStageDummyGenerator.cs` → **`MapStageAuthoringTools.cs`** 로 개명·축소: KayKit 의존(`GeneratePilot`·`GenerateDuelClassic`·`BlockerRect`·`PlaceGround`·`Load`·`Marker`·`Blocker`) 제거, 사용자 스테이지 저작 도구(`AuthorBonusPortals`·`AuthorSpawnsAndGoal`·`Host`·`Portal`)만 유지
- `RalphEditorTasks.cs` — `unit5_pilot`·`duel_classic`·`unit9_portals` 케이스 제거
- PlayMode 테스트
  - `BattleBridgeTestAccess.DefaultMap` `"Serpent"` → **`"Street"`** — 기본판 8개 테스트의 전제(«반격할 거점이 없다», 유닛 간 사거리 계측)를 보존하는 판은 거점 없는 Street 다. Duel 은 본능 4기가 서 있어 `Whirlpot_TakesNoDamage_WhenNothingCanHitBack` 류가 거짓이 된다
  - `DioramaStagePlayTests` — Fixture → Duel. 차단 셀 = 분리대 6칸, 전진 방향 = 골(−x). 본능 4기 스폰 단언 추가(unit 10 라이브 검증)
  - `SpawnGuideMatchesWalkTest.Coil_*` · `WaypointRoutingLiveTest.RoutedMap_*("Coil","Zig")` · `TutorialDevSlot_*` — `[Ignore]` + 사유(«routed-lane / 저작 플랜 스테이지가 풀에 없다 — 재활성화 시 dev 스테이지 저작»)
  - `WaypointRoutingLiveTest.SiegeDevSlot_*` `Values("Duel","Ford","Isle")` → `Values("Duel")`
  - `StructureLivePlayTest` — Ignore 유지, 사유를 «StructureMarker 는 들어왔고(unit 10) Test/SiegeTest 스테이지 미저작» 으로 갱신
- 문서: `map-stage-authoring.md`(이름 충돌 목록·주의·절차 예시 경로), `object-pipeline-map.md`(저작 경로), README(계약 11·후속 후보·상태), `8_handoff_summary.md`

## 구현

1. 삭제는 `git rm` 으로(.meta 짝 동반). Unity 가 열려 있으면 Refresh 가 폴더 삭제를 반영한다.
2. `DevMapOverride.Index`(PlayerPrefs) 가 옛 인덱스를 가리켜도 브리지가 `[0, Count+DevCount−1]` 로 클램프한다 — 별도 마이그레이션 없음.
3. 덱 에셋(`Deck_Serpent` 등)은 **건드리지 않는다** — EditMode `WaveConceptAuthoringTests`/`LiveDeckBossAuthoringTests` 가 이름으로 순회한다. 덱 정리는 별도 결정.

## 완료 기준

- [ ] `Prefabs/Maps` 부재 · EditMode 두 lane green(신규 실패 0)
- [x] PlayMode: `DioramaStagePlayTests`·`BonusWavePullTest`·`WaypointRoutingLiveTest.SiegeDevSlot(Duel)` green (2026-08-26)
- [ ] 기본판 4파일 — 11/14 통과. `Stun_FreezesEnemiesInRange`·`Whirlpot_WalksIn_ThenEngages` 는 Serpent 판형 가정(원점 스캔·+5타일 텔레포트)이 Street 에서 깨짐 → 하네스 수선 또는 테스트 전용 dev 마당(handoff 참조), 사용자 결정 대기
- [ ] 문서 3곳에서 `Prefabs/Maps` 경로·은퇴 맵 이름이 «현재 존재» 로 읽히는 문장 0
