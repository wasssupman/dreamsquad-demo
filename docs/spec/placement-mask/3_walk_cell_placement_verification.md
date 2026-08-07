# 3. Walk 셀 배치 검증 (B-1)

## 목적

마스크가 Walk 셀을 포함할 때 "적·유닛 행동 불변, 위치 제약만 해제"(B-1)가 실제로 성립하는지 검증한다. 코드 변경은 원칙적으로 0 — 이 유닛은 "배치칸 = 벽(비-walkable)" 암묵 전제 위에 선 시스템 4곳의 **재검토·검증**이 본체다. 재검토 중 결함이 나오면 최소 수정으로 좁힌다(기능 확장 금지).

## 변경 대상

- 검증용 맵: 기존 MapDocument 1종 사본에 Walk 셀 포함 마스크 저작 (`Assets/_Project/Data/Maps/` — **풀 미등록 필수**: 풀 등록 시 `seed % poolCount` 매핑이 바뀌어 토너먼트 맵 결정론 오염)
- **진입 경로 2종** (임의 비-풀 문서를 로드하는 dev override 는 현재 없다 — `BattleBridge.cs:1036` "풀이 유일 소스"):
  - PlayMode 테스트: `BattleBridgeDraftMapTests.AddPoolEntry`(reflection 으로 `MapDocumentPool.entries` 임시 주입, `BattleBridgeDraftMapTests.cs:92~99`) 패턴 재사용
  - 육안 Play: `endlessEncounter.document` 임시 배선(`DevMapOverride.Endless` 경유, `BattleBridge.cs:1041~1047`) — **씬 저장 금지**(in-memory 배선만, SaveScene 은 WIP 를 베이크)
- `Assets/_Project/Tests/EditMode/PlacementMaskLivePathTests.cs` — 자동 검증(라이브 경로)
- (결함 발견 시에만) 해당 시스템 최소 수정

## 재검토 6곳 (전제: 배치 유닛 셀은 벽)

| 시스템 | 전제 | Walk 셀 유닛일 때 기대 |
|---|---|---|
| `DefenderFieldSystem`(:62) | 유닛 주변 Chebyshev ≤R 디스크의 walkable 셀을 BFS 소스로 수집, 자기 셀은 `dx==0&&dy==0` 제외(`FlowFieldBuilder.cs:143`) | 자기 셀이 소스에서 빠져도 주변 walkable 소스로 필드 성립 — 보스 사냥 동작 확인 |
| `AggroChaseMath`(:42) | `CollectDefenderSources` 공유 — 위와 동일 전제 | 어그로 추격 경로 정상(위와 동일 결론) 확인 |
| `HealthThresholdSystem`(:275) | 착지 셀이 배치칸(비-walkable)이면 인접 walkable 로 스냅 | Walk 셀은 스냅 불발(no-op) — 착지 위치 불변 확인 |
| `PatrolAnchor`(:11) | 거점 = 유닛 근처 walk 셀(자기 셀은 벽) | `TryGetNearestWalkCell` 이 자기 셀 즉시 반환 → 거점=자기 셀. 순찰 정상 확인 |
| 보스/궁극 도약 착지(`BlinkMath`) | `TryFindLandingCell` 은 r=0 부터 — desired 가 walkable·연결이면 그 셀 반환 | Walk 셀 유닛이면 보스가 **유닛 셀 위로** 착지(기존 Place 유닛은 인접 스냅). B-1 겹침 수용 범위 — 명시적으로 수용하고 육안만 확인 |
| `FindNearestPathDirection`(`BattleBridge.cs:4346~`) — 전방 배치기(`ApplyForwardOnPlaceProjectile`) 방향 | "최근접 Walk 타일 방향"으로 발사 — 배치 셀이 비-Walk 전제 | 자기 셀이 Walk 면 d2=0 자기 자신이 최근접 → zero-길이 가드로 **고정 +x 발사** = 관측 가능 결함. **자기 셀 제외 1줄 최소 수정**(최근접 '타' Walk 셀 방향)으로 좁혀 고친다 |

## Play 검증 시나리오

1. 경로(Walk) 셀이 하이라이트에 포함되고, D&D 배치·재배치·탭 배치가 성사된다.
2. 그 위로 적이 **그대로 통과**한다(겹침 수용) — 경로·속도·유출 판정 불변(walkMask 무변경 assert).
3. 어그로(가디언 히트) 시 대치·추격 정상. 근접 적이 경로 위 유닛과 교전 정상.
4. 보스 웨이브: 방어유닛 사냥 필드가 Walk 셀 유닛에게도 수렴.

## 주석 갱신 (B-1 이후 거짓이 되는 주석)

- `PatrolAnchor.cs:11~14` — "방어유닛은 MapTileType.Place 에만 놓이고"
- `BattleBridge.cs:5829~5831` — 배치칸=벽 전제 서술
- `FlowFieldBuilder.cs:143` — "방어유닛 자신의 셀(Place=벽)"

## 완료 기준

- 자동 테스트(EditMode, 라이브 경로 `mapPool → BuildMapForBattle`): Walk 셀 mask=1 배치 성사 + Place 셀 mask=0 거부 + **tiles 불변**(walkMask 의 유일한 파생원이라 통행 불변의 메커니즘 축 — 도달 시간 e2e 는 아래 육안 Play 가 담당. 결정론 sim 에서 tiles 동일 ⇒ 경로 동일).
- 위 Play 시나리오 4종 육안 확인(에디터), 콘솔 에러 0.
- `FindNearestPathDirection` 자기 셀 제외 수정 + 전방 배치기 발사 방향 육안 확인.
- 재검토 표 6행 각각에 확인 결과 한 줄 기록(이 파일에 추기) + 주석 3곳 갱신.
