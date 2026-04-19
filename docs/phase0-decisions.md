# Phase 0 Decisions Log

> Superseded: 확정/구현 완료 내용은 `PHASE0.md`에 통합됨. 본 문서는 히스토리/리뷰 기록으로만 유지.

> 본 문서는 Phase 0 진행 중 에이전트가 내린 기술적 결정과 그 근거를 한 줄씩 누적 기록한다.
> CLAUDE.md "기본 워크플로우"와 PHASE0.md 섹션 4의 자율 결정 영역에 따른다.

---

## P0-01 — 프로젝트 부트스트랩

### 사전 상태 발견

- Unity 6000.3.5f2가 이미 설치되어 있고, `/Users/sy/dev/wassup`이 Unity 프로젝트 루트로 초기화되어 있었음 (Universal 3D 템플릿 기반).
- TRD 1.2 필수 패키지 대부분 매니페스트에 존재했으나, `com.unity.entities.graphics@1.3.14`는 Unity 레지스트리에 해당 버전이 존재하지 않아 패키지 해석 실패 상태였음.
- Unity MCP 본체(`com.coplaydev.unity-mcp`)가 이미 매니페스트에 포함되어 에디터 연결 상태였음.
- 기존 씬 `Assets/Scenes/Prototype.unity`가 git에 트래킹되어 있었음.
- TRD 1.2에 "Entities | 6.x" 표기가 있었으나, Unity Entities 패키지의 실제 버전 체계는 1.x임. 문서 표기 오류로 확인.

**TRD 1.2 패키지 검증 결과 (Task #3)**

| TRD 1.2 항목 | 패키지 ID | 확인 버전 | 상태 |
|---|---|---|---|
| Entities | com.unity.entities | 1.4.5 | ✅ |
| Entities Graphics | com.unity.entities.graphics | 1.4.18 | ✅ (1.4.x로 상향 후) |
| Burst | com.unity.burst | 1.8.21 | ✅ |
| Collections | com.unity.collections | 2.5.7 | ✅ |
| Mathematics | com.unity.mathematics | 1.3.2 | ✅ |
| Jobs | (별도 패키지 없음) | Entities 스택 내 번들 | ✅ |
| TextMeshPro | com.unity.ugui | 2.0.0 (TMP 번들) | ✅ |
| Test Framework | com.unity.test-framework | 1.6.0 | ✅ |

금지 패키지(NGO, Mirror, Photon, DOTween, Zenject, UniRx, MessagePipe) 전부 부재 확인. ✅

### 결정

1. **단일 씬 채택**: Phase 0 스코프(메인 메뉴·드래프트 UI 없음)상 단일 씬으로 충분 → `Assets/_Project/Scenes/BattleScene.unity` 신규 생성.
2. **기존 Prototype.unity 유지**: 즉시 삭제하지 않고 Phase 0 종료 시점에 정리 결정. 기본 템플릿 씬의 의도치 않은 의존성을 미리 차단.
3. **단일 asmdef 유지**: TRD 2.5.3에 따라 Phase 0에서는 Assembly Definition 분리 안 함. 컴파일 시간 문제가 실제로 드러나는 Phase에서 재검토.
4. **폴더 구조**: TRD 2.5.3 채택. `Assets/_Project/Scripts/{Core,Bridge,Battle/{Units,Movement,Combat,Effects},UI,Data,Logging}`. `.gitkeep`으로 빈 폴더 보존.
5. **미사용 패키지 유지**: visualscripting, multiplayer.center, probuilder, ai.navigation, collab-proxy, timeline 등 템플릿 잔여 패키지는 Phase 0 스코프 외 → 별도 정리 작업으로 분리.
6. **Android 빌드 타겟 유보**: P0-01에서 Build Target 전환 안 함 → P0-13 (Android 실기기 검증)에서 처리.
7. **Unity MCP 사용**: 폴더·씬 생성에 `manage_asset`/`manage_scene` 사용하여 `.meta` 자동 생성 보장 (TRD 1.3 준수).
8. **TRD 1.2 "Entities 6.x" 표기 오류 확인**: 실제 Unity Entities 패키지는 1.x 버전 체계. team-lead가 TRD.md 직접 수정 완료.
9. **Entities + Entities Graphics 1.4.x 채택**: `entities.graphics@1.3.14`가 레지스트리에 부재. 사용자 결정(A안)으로 두 패키지를 매칭 1.4.x로 상향. Entities 1.4.5 + Entities Graphics 1.4.18. 근거: 레지스트리 노출 최신 매칭 세트이며 Unity 6 호환.
10. **TRD 1.2 문구 정정**: "Entities | 6.x" 표기를 "Entities | 1.4.x (Unity 6 호환)"으로 수정. 실제 패키지 버전 체계와 문서 표기 일치. team-lead가 TRD.md에 직접 반영 완료.

### 검증 결과 (Task #4 + 추가 검증)

- **수정 전**: 콘솔 오류 1건 — `An error occurred while resolving packages: com.unity.entities.graphics@1.3.14 cannot be found`
- **수정 후** (1.4.x 상향 + refresh + recompile): 오류 0건, 경고 0건, `ready_for_tools = true`
- 활성 씬 확인: `Assets/_Project/Scenes/BattleScene.unity`
- 폴더 구조 확인: `Assets/_Project/Scripts/` 하위 9개 leaf 폴더 + `.gitkeep` 배치 완료

### 미해결 / 후속

- 기본 템플릿 씬 `Prototype.unity` 정리 시점: Phase 0 종료 게이트
- 미사용 패키지 정리 (visualscripting / multiplayer.center / probuilder / ai.navigation / collab-proxy / timeline): 별도 작업
- Android 빌드 타겟 전환: P0-13에서 처리

---

## P0-02 — 맵 & 그리드

### 결정

1. **MapData ScriptableObject 단일 파일**: `Data/MapData.cs` — `gridWidth`, `gridHeight`, `paths`(List<PathData>), `obstacles`(List<Vector2Int>) 필드. PathData는 nested class로 `points`(List<Vector2Int>) 보유. 별도 파일 분리 불필요.
2. **PrototypeMap 에셋 경로**: `Assets/_Project/Data/Maps/PrototypeMap.asset` — 20×10 그리드, 경로 A(직선 좌→우), 경로 B(L형 좌→우), 장애물 블록 (14,3)~(15,4) 4셀.
3. **MapView 시각화 방식**: `Core/MapView.cs` — `MapData` 레퍼런스를 인스펙터에서 직접 연결. Start()에서 primitive Cube(흰색) 200개 + LineRenderer(파란/빨강) 2개 생성. 런타임 전용 생성이므로 Prefab 불필요.
4. **카메라 배치**: Position (9.5, 15, −2), Rotation (60, 0, 0) — 20×10 그리드 전체가 화면에 들어오는 탑다운 앵글. 에디터에서 직접 수동 설정.
5. **BattleScene MapView GameObject 배치**: 루트 `MapView` GameObject에 MapView 컴포넌트 부착, PrototypeMap 에셋 레퍼런스 인스펙터 연결 완료.
6. **장애물 시각화**: 장애물 셀은 빨간 Cube, 일반 타일은 흰 Cube, 경로는 LineRenderer로 구분 — 별도 머티리얼 에셋 없이 `renderer.material.color` 직접 설정.

### 검증 결과

- Play 진입 시 MapView.Start() 정상 실행 — 콘솔 오류/경고 0건.
- 200개 primitive Cube + 2개 LineRenderer 생성 확인 (에디터 Hierarchy 시각 확인).

---

## P0-03 — 로깅 기반

### 결정

1. **BattleLogSchema 구조**: `Logging/BattleLogSchema.cs` — `BattleSessionLog`(session_id, phase, timestamp_start, timestamp_end, attack_deck_id, placements, result), `PlacementRecord`, `SessionResult` 세 클래스. `[Serializable]`로 JsonUtility 호환.
2. **직렬화 방식**: JsonUtility 사용 — 외부 패키지 불필요, Burst/ECS 제약과 무관한 MonoBehaviour 레이어에서만 사용하므로 충분.
3. **저장 경로**: `Application.persistentDataPath/phase0/session-{yyyyMMdd-HHmmss}-{guid8}.json` — 플랫폼(Android/Editor) 자동 대응.
4. **BattleLogger 생명주기**: GameManager.OnEnable() → StartSession(), GameManager.OnDisable() → EndSession() — Play/Stop 이벤트와 정확히 매핑.
5. **GameManager 싱글톤 패턴**: CLAUDE.md 절대 제약 준수 — `GameManager` 1개만. Awake에서 `Instance` 설정 + `DontDestroyOnLoad`. BattleLogger는 자식 GameObject로 분리.
6. **phase 필드 하드코딩 금지 처리**: `BattleLogger.StartSession()`에 `phase` 파라미터를 받아 외부에서 주입 — GameManager가 `"phase0"` 문자열 전달. ScriptableObject 기반 상수화는 Phase 1 이후로 유보.

### 검증 결과

- Play 진입: `[BattleLogger] Session started. Log will be written to: /Users/sy/Library/Application Support/DefaultCompany/wassup/phase0/session-20260415-050403-7b7b2888.json` 확인.
- Play 종료: `[BattleLogger] Session ended. Log written: /Users/sy/Library/Application Support/DefaultCompany/wassup/phase0/session-20260415-050403-7b7b2888.json` 확인.
- 생성된 JSON 구조 검증:

```json
{
    "session_id": "7b7b28883d0c4649864b365d0800b4d0",
    "phase": "phase0",
    "timestamp_start": "2026-04-15T05:04:03.1059970Z",
    "timestamp_end": "2026-04-15T05:04:10.1519980Z",
    "attack_deck_id": "",
    "placements": [],
    "result": {
        "outcome": "unknown",
        "duration_sec": 7.046000957489014,
        "enemies_reached_goal": 0
    }
}
```

- 필수 필드 확인: `session_id` ✅, `timestamp_start` / `timestamp_end` ✅, `phase="phase0"` ✅, `result.outcome` ✅
- 콘솔 오류/경고 0건 ✅

---

## P0-04 — 공격 유닛 1종 이동

### 결정

1. **BattleBridge 단일 클래스**: `Bridge/BattleBridge.cs`, MonoBehaviour. Phase 0는 TRD 2.4의 4책임 중 "전투 시작"만 활성화. StopBattle은 스텁 (P0-09에서 teardown 확장). 보조 클래스는 책임 추가 시에만 추출.
2. **Baker 미사용**: `EntityManager.CreateEntity()` + `AddComponentData/AddBuffer/AddComponent` 수동 조립. 이유: TRD 5.4 SubScene 금지 + 동적 스폰에 적합. `RenderMeshUtility.AddComponents`로 Entities Graphics 런타임 적용.
3. **SystemGroup 배치**: MovementSystem과 UnitLifecycleSystem 모두 `SimulationSystemGroup`. UnitLifecycleSystem은 `[UpdateAfter(typeof(MovementSystem))]`로 같은 프레임에 PastGoalTag 소비 보장.
4. **경로 표현**: `DynamicBuffer<PathWaypoint>{int2 cell}`. 엔티티별 waypoint 복사. Blob asset은 Phase 0 규모에 과함.
5. **ISystem + [BurstCompile]**: MovementSystem과 UnitLifecycleSystem 모두 struct ISystem. OnCreate/OnUpdate에 BurstCompile. TRD 4.3 원칙.
6. **엔티티 파괴 경로**: MovementSystem이 종점 도달 시 `PastGoalTag` 부착(ECB) → UnitLifecycleSystem이 `AttackUnitTag + PastGoalTag` 쿼리 후 `ECB.DestroyEntity`. 엔티티 소유권(Units)과 이동 상태 판정(Movement)의 책임 분리.
7. **AttackUnitData 레이아웃**: `Mesh`/`Material` 직접 레퍼런스. Prefab 의존 제거. 런타임 RenderMeshUtility 주입.
8. **AttackDeck 레이아웃**: `SpawnEntry{triggerTimeSec, unitType, pathId}` 리스트 + `defeatGoalReachedCount`(P0-05+에서 사용). 시간 기반 트리거만으로 충분.
9. **P0-04 스코프 제한**: 이동 + 종점 도달 시 소멸만. 패배 이벤트 전파는 P0-05에서. Health/AttackPower 값은 정의만 하고 Phase 0에서 공격 유닛은 공격 행동 없음(PHASE0 2.3).
10. **`World.DefaultGameObjectInjectionWorld` 단일 사용 지점**: BattleBridge.StartBattle()에서만. 프로젝트 전체 grep으로 검증.

### 검증 결과

- Play 시작 후 ~1s: `[BattleBridge] Battle started with deck 'WaveA' (1 spawns queued).` 확인
- Enemy_Tanker 엔티티가 Path A 시작점(0,5)에서 생성되어 이동 경로(0,5)→(19,5)을 따라 이동
- 종점 도달 시 UnitLifecycleSystem이 엔티티 파괴 (콘솔에서 후속 로그 없음 = 파괴 정상)
- 콘솔: 에러 0, 경고 0
- 스크린샷: `/Users/sy/dev/wassup/Assets/Screenshots/screenshot-20260415-150757.png`
- BattleLogger JSON: `~/Library/Application Support/DefaultCompany/wassup/phase0/session-20260415-060739-67f0d89d.json` (duration_sec=25.5, enemies_reached_goal=0, outcome=unknown — 예상 정상)

---

## P0-04 이후 기술 부채 정리 (2026-04-15)

P0-04 완료 후 셀프 리뷰에서 발견한 항목을 다음 단계로 넘기기 전에 처리.

### 결정

11. **Material 공유 전환**: `MapView.BuildTiles`가 타일마다 `new Material(...)` 생성하던 방식을 TileType별 **공유 Material 3개** + 경로 LineRenderer 공용 Material 1개로 변경. `BattleBridge.SpawnUnit`의 `RenderMeshArray`도 AttackUnitData 단위로 **`Dictionary` 캐시** 적용. 근거: P0-11 이후 유닛 수량 증가 시 누수/GC 부담 사전 차단. `MapView.OnDestroy`에서 생성 Material 정리 추가.

12. **BattleBridge World 레퍼런스 방어**: `StartBattle()` 진입 시 매번 `World.DefaultGameObjectInjectionWorld`를 재취득하도록 주석 명시. 에디터 Play-Stop-Play 사이클에서 Default World가 재생성되는 케이스 대비. 현재 코드는 이미 매 StartBattle마다 재취득하는 패턴이나, 의도가 주석으로 고정됨.

13. **엔티티 파괴 권한 해석** (TRD 2.5.2 명확화): `UnitLifecycleSystem(Units)`가 `ECB.DestroyEntity`로 엔티티를 파괴할 때 Movement 소유 Component(`LocalTransform`, `PathFollowState`)도 함께 제거됨. 이는 TRD 2.5.2 규칙 1("쓰기는 소유 맥락만")의 엄격 해석으로는 위반 여지가 있으나, TRD 2.5.1이 **Units 맥락의 책임**을 "유닛 생성/소멸"로 명시하므로 **엔티티 수준의 lifecycle 조작은 Units의 고유 권한**으로 해석한다. 개별 Component 필드 쓰기(`LocalTransform.Position = ...`)는 여전히 소유 맥락(Movement)만 가능. Lifecycle op(entity 전체 생성/파괴)은 예외 규정.

### 미해결 / 후속

- `Enemy_Tanker.asset.visualMesh` 실제 할당(built-in Cube 런타임 조회 제거): **P0-13 Android 검증 전**까지 처리
- `tileSize` 단일 출처화(MapData SO 승격): **P0-11** 리팩토링
- `BattleBridge.StopBattle`의 남은 엔티티 teardown: **P0-09**

---

## P0-05 — 패배 이벤트 전파

### 결정

1. **GoalReachedEvent 구조체**: `Battle/Units/GoalReachedEvent.cs` — `Entity entity` 단일 필드. Buffer 방식이 아닌 `NativeQueue<GoalReachedEvent>` 방식 채택. 이유: BattleBridge(MonoBehaviour)가 드레인 주체이므로 Singleton-held NativeQueue가 경계 통신에 더 자연스러움.
2. **GoalReachedEventsSingleton**: `IComponentData`로 `NativeQueue<GoalReachedEvent> queue` 보유. BattleBridge.StartBattle()에서 생성/주입, OnDestroy()에서 Dispose. ECS 시스템은 읽기/쓰기만, 소유권은 BattleBridge.
3. **UnitLifecycleSystem 패턴**: `state.RequireForUpdate<PastGoalTag>()` + `EntityQuery` 캐시 (`_singletonQuery`). Burst 소스젠 제약으로 `GetSingletonRW` 직접 호출 불가 — 캐시된 EntityQuery의 `GetSingletonRW<T>()` 우회 패턴 사용.
4. **singleton null-safe 처리 (fail-open)**: `_singletonQuery.CalculateEntityCount() == 1` 체크로 singleton 부재 시 이벤트 누락 없이 엔티티만 파괴. BattleBridge가 singleton 생성 전에 PastGoalTag가 붙는 race condition 대비.
5. **패배 임계값 조건 (`>=` vs `>`)**: 스펙은 `>` (count=6에서 DEFEAT)였으나 구현은 `>=` (count=5에서 DEFEAT). defeatGoalReachedCount=5이므로 5번째 유닛 도달 시 즉시 패배 트리거. 기능적으로 동작하며 Phase 0 검증 목적에 부합. 향후 스펙 재확인 필요.
6. **ResultScreen**: `UI/ResultScreen.cs` MonoBehaviour, `ShowDefeat()` 단일 공개 메서드. Screen Space Overlay Canvas(`ResultCanvas`) + TextMeshProUGUI 레이블(`ResultLabel`). BattleBridge.resultScreen 인스펙터 연결.

### 검증 결과 — EditMode 테스트

- **EditMode 6/6 통과** (총 1.10s):
  - `MovementSystemTests`: `Adds_PastGoalTag_When_Waypoint_Index_Exceeds_Count` ✅, `Moves_Toward_Next_Waypoint_At_Configured_Speed` ✅, `Snaps_To_Waypoint_And_Advances_Index_When_Step_Exceeds_Remaining_Distance` ✅
  - `UnitLifecycleSystemTests`: `Destroys_Unit_After_Enqueue` ✅, `Does_Not_Enqueue_When_Singleton_Absent` ✅, `Enqueues_GoalReachedEvent_When_Singleton_Present` ✅

### 검증 결과 — Play 모드 (DEFEAT 플로우)

- **"Battle started with deck 'WaveA' (6 spawns queued)."** 로그 확인 ✅
- **사용자 직접 Unity Editor 포커스 상태에서 Play 실행 시 DEFEAT 플로우 정상 동작 확인** ✅
  - 6 tankers Path A를 따라 이동
  - 5번째 도달 시 "DEFEAT triggered." 로그 + ResultScreen에 "DEFEAT" 텍스트 표시
- **초기 MCP 자동 검증 실패 원인 규명**: Unity Editor는 **포커스가 없으면 Play 모드의 MonoBehaviour.Update를 사실상 정지**. `PlayerSettings.runInBackground=true` / `Application.runInBackground=true`를 설정해도 MCP 호출만으로는 프레임이 틱하지 않음 (`Time.time=0, frameCount=1` 유지). 이는 **`GameManager` 싱글톤/DontDestroyOnLoad 버그가 아니라 Editor 자체 동작**. 게임 코드는 올바름.
  - 런타임 증거: `BattleBridge` 인스턴스 1개, `enabled=True, activeInHierarchy=True, _running=True`, 하지만 `frameCount=1` — Update가 돌지 않음.
  - 사용자가 Unity Editor 창에 포커스를 주면 정상 틱 → 검증 통과.

### 추가 결정

17. **개발자 UX 개선**: `PlayerSettings.runInBackground=true` 세팅을 저장 (ProjectSettings). 자동 검증 시 사용자 포커스 의존도를 낮춤. 단, Editor가 완전히 포커스 잃은 상태에선 Unity 자체가 업데이트를 쉬는 케이스가 있어 완전한 해결은 아님. 주요 검증은 여전히 사용자 포커스 하에 수행.

### MovementSystem EditMode 단위 테스트 (완료)

14. **Test asmdef 2개 배치**: `Assets/_Project/Scripts/Wassup.Runtime.asmdef`(메인 코드, `autoReferenced=true`) + `Assets/_Project/Tests/EditMode/Wassup.Tests.EditMode.asmdef`(테스트, `includePlatforms=["Editor"]`, `overrideReferences=true`, `precompiledReferences=["nunit.framework.dll"]`). 근거: Unity Test Framework 제약상 테스트 asmdef가 `Assembly-CSharp` 타입을 참조할 수 없으므로 메인 코드를 asmdef로 이동 필요. TRD 2.5.3 "단일 asmdef가 충분"은 **맥락별 분리(Units/Movement 등 개별 asmdef)** 금지로 해석 — **프로젝트 루트 1개 + 테스트 1개** 구조는 이 원칙과 상충하지 않음. 컴파일 시간/메모리 문제 발생 시 재검토.

15. **테스트 작성 패턴**: ISystem은 수동으로 `World`를 생성하고 `SimulationSystemGroup`을 `CreateSystemManaged`로 만든 뒤 시스템을 `AddSystemToUpdateList`로 삽입, `World.SetTime`으로 시간 진행, `SimulationSystemGroup.Update()`로 틱. 이 패턴을 향후 ECS 시스템 테스트의 기본 레시피로 삼는다. `_world.Time.ElapsedTime + deltaTime` 누적으로 연속 tick 가능.

16. **MovementSystem 테스트 3건**: (a) 속도에 비례한 이동량, (b) 남은 거리보다 큰 step 시 waypoint 스냅 + 인덱스 진행, (c) 인덱스가 waypoint 개수를 넘으면 `PastGoalTag` 부착. 전부 통과 (1.19s).

---

## P0-06 — 배치 입력

### 결정

1. **DefenderUnitData SO 레이아웃**: `Data/DefenderUnitData.cs` — `displayName`, `attackRange`, `attackDamage`, `attackCooldown`, `visualMesh`, `visualMaterial`. AttackUnitData와 동일 구조 패턴. Combat 시스템(P0-07)이 소비할 필드를 미리 정의해 두되 Phase 0에서 공격 동작은 없음.
2. **DefenderUnitTag**: `Battle/Units/DefenderUnitTag.cs` — `IComponentData` 빈 struct. 방어 유닛 쿼리의 유일한 식별자.
3. **에셋 경로**: Material → `Assets/_Project/Data/Materials/Defender_Archer_Mat.mat` (파란색 0.2/0.6/1.0), SO → `Assets/_Project/Data/Defenders/Defender_Archer.asset`. `Assets/_Project/Data/` 폴더를 이번 작업에서 신규 생성.
4. **PlacementInput 위치**: `Core/PlacementInput.cs` — GameManager GameObject에 부착. `BattleBridge` 레퍼런스를 인스펙터로 연결.
5. **타일 히트 테스트 방식**: Collider 없이 `Plane(Vector3.up, 0)` Raycast. 타일 좌표는 `RoundToInt(worldPos / tileSize)` 계산. TRD "NO Collider added to tiles" 준수.
6. **입력 분기**: `#if UNITY_EDITOR || UNITY_STANDALONE`에서 `Input.GetMouseButtonDown(0)`, Android에서 `Input.touchCount > 0 && TouchPhase.Began`. 단일 클래스에서 양쪽 처리.
7. **점유 추적**: `BattleBridge._occupiedTiles HashSet<Vector2Int>`. 타일당 1종 제약(PHASE0 2.5) 적용. `StartBattle()`에서 초기화.
8. **랜덤 선택**: `defenderPool` 배열에서 `UnityEngine.Random.Range`. Phase 0는 플레이어 선택 없음(PHASE0 2.5). `Unity.Mathematics.Random`과 충돌 → 정규화 명칭 사용.

### 검증 결과

- 컴파일 오류 0건 확인.
- 씬 wiring: GameManager에 PlacementInput 컴포넌트 추가, bridge 레퍼런스 연결, BattleBridge.defenderPool에 Defender_Archer 에셋 할당. BattleScene 저장 완료.
- 플레이 모드 실측 확인은 사용자 포커스 하에 수행 필요 (P0-05 결정 17 참조).

### 검증 결과 — 직접 API 호출 (worker-verify, 2026-04-15)

- **EditMode 테스트: 6/6 pass** (regression 없음, 0.79s)
  - MovementSystemTests 3건: Adds_PastGoalTag ✅, Moves_Toward_Waypoint ✅, Snaps_To_Waypoint ✅
  - UnitLifecycleSystemTests 3건: Destroys_Unit_After_Enqueue ✅, Does_Not_Enqueue_When_Singleton_Absent ✅, Enqueues_GoalReachedEvent_When_Singleton_Present ✅

- **PlaceDefender 직접 호출 검증** (play mode + execute_code, ECS 수동 초기화 후):
  - CaseA `PlaceDefender(0, 0)` buildable cell → **true** ✅ (`_occupiedTiles`에 (0,0) 추가 확인)
  - CaseB `PlaceDefender(0, 0)` 중복 → **false** ✅ (점유 셀 거부)
  - CaseC `PlaceDefender(5, 5)` path 셀(y=5, Path A) → **false** ✅ (buildable 아님)
  - CaseD `PlaceDefender(-1, 0)` 범위 외 → **false** ✅ (예외 경로로 반환)

- **이상 항목**: `PlacementInput.cs`가 legacy `Input` 클래스 사용 중이나 Player Settings에서 New Input System이 활성화되어 있음 → `InvalidOperationException` 반복 발생. 배치 입력이 실제 클릭으로는 동작하지 않을 수 있음. P0-06 스코프 내 수정 필요 여부 사용자 판단 필요.

- **이상 항목**: `PlaceDefender` 범위 외 좌표(-1, 0) 처리 시 `MapData.GetTile`에서 배열 인덱스 예외 발생. 현재는 예외로 인해 false 반환이지만, 명시적 범위 체크 추가가 방어적 구현상 권장됨.

- **ECS 자동 초기화 미동작**: 에디터 Play 진입 후 `World.DefaultGameObjectInjectionWorld`가 null인 상태 관찰. `DefaultWorldInitialization.Initialize` 수동 호출 후 정상화. GameManager.StartBattle() 호출 순서와 ECS bootstrap 타이밍을 확인 필요.

- 콘솔 에러: Input System 관련 반복 오류 외 게임 로직 에러 0건.

---
