# waypoint-routing — 구현 인계

## Commit

- `1535f7c4` — unit 0 저작 축
- `737b5807` — unit 1 슬롯 축 2차원화
- `e49833fd` — unit 2 순서 관리 순수 함수
- `5862f67d` — unit 3 웨이포인트 추종 활성화
- `ede9fc73` — unit 7 스웜별 경로 가이드
- `258c96ec` — unit 4 Air 통행·비주얼·타겟층과 검증 콘텐츠
- `c7db37b0` — unit 4 완료 상태·검증·handoff 마감

## Implemented

- 맵의 N개 웨이포인트 경로를 적 SO의 `waypointPathIndex`로 선택한다.
- 이동은 기존 flow field를 재사용하고, 웨이포인트 순서만 `WaypointFollow`가 소유한다.
- `PlacementLayer.Air`는 모든 타일에 열리며 지상 장애물 오버레이를 무시한다.
- Air 적의 분리 이동과 어그로 추격도 자기 통행층 NavGrid를 읽는다.
- `flightLift`는 sim 위치를 바꾸지 않고 기존 뷰 lift 파이프라인만 사용한다.
- 기존 방어유닛은 Path 전용, 대공사수는 Path와 Air를 모두 공격한다.
- 타겟층은 직접 공격뿐 아니라 투사체·재조준·튕김·스윕·광역·장판까지 전달된다.
- Skimmer는 단일 타겟·피해 10·0.2초 공격 주기, 대공사수는 피해 7·0.2초 공격 주기다.
- 스폰 가이드는 같은 웨이브에서도 `스웜 × 실제 레인`별로 각 경로를 예고한다.

## Key Files

- `docs/spec/waypoint-flight-enemy/README.md`
- `Assets/_Project/Scripts/Battle/Movement/WaypointFollow.cs`
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`
- `Assets/_Project/Scripts/Data/PlacementLayer.cs`
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Scripts/Battle/Combat/EnemyAiStateSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Data/Enemies/Enemy_Skimmer.asset`
- `Assets/_Project/Data/Defenders/Defender_AntiAir.asset`

## Verified

- unit 4 초기 전체 EditMode: 2,150건 통과, 기존 Ignore 3건.
- 타겟층 집중 검증: 최초 14/14, 최종 리뷰 회귀 포함 18/18 통과.
- WaypointLab PlayMode 최종 2/2 통과: 순서 통과·차단 대조·lift·런타임 타겟층 베이크.
- 사용자 Play 확인: 경로 가이드, Air 차단 무시, lift·체력바, 일반 방어 회피, 대공사수의 지상·공중 공격, Skimmer 단일 고속 공격.
- 최종 Unity 콘솔 에러 0, 관련 diff check 통과.

## Notes

- `BattleBridge`가 Mono↔ECS의 유일한 창구인 경계는 유지했다.
- Combat/Effects는 Movement 소유 `PathFollowState.traversalLayers`를 읽기만 한다.
- 새 시스템·이벤트 큐·인터페이스는 추가하지 않았다.
- 타겟층 `0`은 적 공격·플레이어 스킬·통행층 없는 구조물의 레거시 무필터 값이다.
- `EnemyAiStateSystem`도 AttackSystem과 같은 층 마스크를 읽는다. 빠뜨리면 Path 전용 순찰병이 Air 적만 보고 `Engaging`으로 멈춘다.
- `HazardEffect.targetTraversalLayers`는 복사본에만 쓰는 `[NonSerialized]` 런타임 스냅샷이다.
- Air 우선 타겟팅은 없다. 대공사수의 Path·Air 후보는 기존 거리순으로 동등하게 경쟁한다.

## Resume

1. `README.md`의 상태·feature-wide 계약을 먼저 읽는다.
2. 다음 미완료 작업은 `5_painter_and_maps.md` 하나다. unit 5 구현은 아직 시작하지 않았다.
3. `MapPainterWindow.cs`의 기존 저장/bake 흐름과 unit 0의 `WaypointPathValidation`을 찾아 그대로 재사용한다.
4. 경로 선택·추가/삭제·순서 클릭·마지막 점 삭제·오버레이만 만든다. 재정렬 UI나 새 검증 규칙은 추가하지 않는다.
5. 맵은 2~3장까지만 저작하고, 저장→재로드 왕복과 맵별 Play 확인 뒤 사용자 체감 승인을 받는다.

## Follow-up

- 다음 작업 단위는 `5_painter_and_maps.md`: 경로 페인터와 맵 2~3장 저작.
- 나머지 맵, 대공사수 고유 아트·최종 밸런스, Air 우선/추가 피해는 README 후속 후보다.
- 비방향 defender Entity/Cell 투사체 패턴이 실제 콘텐츠에 들어올 때 emitter 후보에도 타겟층 필터를 추가한다.
