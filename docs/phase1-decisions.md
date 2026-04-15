# Phase 1 Decisions Log

> 본 문서는 Phase 1 (드래프트) 진행 중 에이전트가 내린 기술적 결정과 근거를 한 줄씩 누적 기록한다.
> CLAUDE.md "기본 워크플로우"와 PHASE1.md 섹션 4의 자율 결정 영역에 따른다.

---

## P1-01 — 방어 유닛 풀 10종 확장

### 결정

1. **신규 7종 스탯 구성**: 10종 전체가 단순 업그레이드 관계에 놓이지 않도록 사거리·공격속도·데미지·체력을 서로 다른 축에서 튀게 설정.

| 이름 | HP | RNG | DMG | CD | 역할 |
|---|---|---|---|---|---|
| Archer (기존) | 50 | 4 | 15 | 1.5 | 중거리 낮은 DPS |
| Guardian (기존) | 80 | 1.5 | 15 | 0.3 | 근접 탱크·고 DPS |
| Cannon (기존) | 40 | 5 | 60 | 2.5 | 장거리 유리 버스트 |
| **Sniper** | 25 | 7 | 80 | 3.0 | 초장거리 유리대포 |
| **Ranger** | 40 | 5 | 10 | 0.4 | 장거리 지속 DPS |
| **Scout** | 30 | 2 | 8 | 0.25 | 근접 래피드 초저비용 |
| **Bruiser** | 100 | 1.5 | 30 | 1.2 | 근접 체력·중딜 |
| **Marksman** | 45 | 6 | 40 | 1.0 | 중장거리 안정 |
| **Piercer** | 55 | 3 | 45 | 1.5 | 중거리 단발 강타 |
| **Bastion** | 150 | 1 | 25 | 0.8 | 벽 유닛 지속 근접 |

2. **색상 구분**: 10종 각각 고유한 머티리얼 색(RGB). Scout=노랑, Bruiser=오렌지, Sniper=퍼플, Ranger=시안, Marksman=청록, Piercer=마젠타, Bastion=브라운. Archer/Guardian/Cannon은 기존 색 유지.
3. **에셋 경로 일관화**: 신규 SO와 머티리얼 모두 `Assets/_Project/Data/Defenders/`, `Assets/_Project/Data/Materials/` 하위. Phase 0에서 생긴 관례 준수.
4. **GUID 수동 지정**: 머티리얼·SO에 결정적 GUID(예: `a1b2c3d4e5f6…`) 부여하여 다른 컴퓨터에서 에셋 레퍼런스 깨지지 않도록.

---

## P1-02 — DraftSession + DraftController 골격

### 결정

5. **DraftSession = POCO**: MonoBehaviour/SO 아닌 순수 C# 클래스. 테스트가 Unity 런타임 없이 가능. DraftController(MonoBehaviour)가 소유.
6. **풀 샘플링**: Fisher-Yates 부분 셔플, `System.Random(seed)` 기반. 동일 시드 = 동일 풀 순서 보장(PHASE1 2.3 재현성 요구).
7. **시드 소스**: `Environment.TickCount ^ UnityEngine.Random`. 동일 프레임 연속 호출 시도 분기 보장. H1 비교 필요 시 외부에서 `BeginDraft(seed)` 오버로드로 고정 시드 주입 가능.
8. **DraftController 위치**: 전용 GameObject `DraftController` 루트에 배치. `GameManager` 자식으로 만들지 않음 — Instance 싱글톤화 방지 & 씬 계층 단순화.
9. **인스펙터 필드**: `catalog`(DefenderUnitData[10]), `poolSize=10`, `pickCount=7`, `battleBridge` 레퍼런스. 매직 넘버는 SerializeField 상수로 고정(PHASE1 §7 준수).

---

## P1-03 — 드래프트 UI

### 결정

10. **프리팹 무도입 방침**: 카드·카운터·확정 버튼 UI 전체를 `DraftView` 런타임 빌드로 처리. Phase 1 에셋 추가 0건. 스크립트 하나로 전체 UI 수명주기 제어.
11. **레이아웃**: 2행 × 5열 `GridLayoutGroup`. 카드 셀 280×260, 스페이싱 16. 카드 상단 40px "swatch"가 머티리얼 색 블록으로 정체성 시각화.
12. **픽 피드백**: 배경색 전환(녹색=픽, 회색=미픽). 카드 클릭 = 토글(PHASE1 §4 자율 결정).
13. **카운터·확정**: 카운터 "n/7" + Confirm 버튼. `IsFull`일 때만 interactable=true. 7 미만에서는 클릭 불가 (반복 방지).
14. **Canvas 전용 GameObject**: `DraftView`가 자신의 Canvas/CanvasScaler/GraphicRaycaster를 AddComponent로 부착. 씬에 별도 `DraftCanvas` 프리팹/오브젝트 필요 없음.

---

## P1-04 — DraftSession → BattleBridge 주입

### 결정

15. **API**: `BattleBridge.SetDefenderPool(DefenderUnitData[] pool)` + `DefenderPool` 읽기 전용 노출. null/빈 배열 시 인스펙터 폴백 사용 가능(Phase 0 호환).
16. **확정 시 흐름**: `DraftController.TryConfirm` → `battleBridge.SetDefenderPool(picked) → battleBridge.StartBattle() → DraftConfirmed` 이벤트. Start Order: 풀 주입이 반드시 StartBattle 이전.
17. **GameManager 흐름 변경**: `OnEnable`에서 `logger.StartSession()`만 호출. `BattleBridge.StartBattle()`은 `DraftController.TryConfirm`이 담당 → 드래프트 전 배치 불가(PHASE1 §3.1 P1-04 요구).
18. **이벤트 구독 타이밍**: `GameManager.BeginDraft`를 `OnEnable`에서 호출하면 `DraftView.OnEnable` 구독 전에 이벤트가 발화해 UI가 뜨지 않는 레이스가 발생. → `Start()`로 이관(Start는 모든 OnEnable 이후 실행). 실 플레이에서 검증 완료.

---

## P1-05 — 재시작 (같은 픽 유지)

### 결정

19. **기존 `BattleBridge.RestartBattle` 재사용**: `SetDefenderPool`이 Confirm 시 쓴 값이 그대로 남아 있으므로, Restart 경로가 `defenderPool`을 변경하지 않는 한 같은 7종으로 재진행. 추가 코드 불필요 — Phase 0 RestartBattle 로직(로그 롤 + Teardown + StartBattle)이 그대로 맞아떨어짐.
20. **검증**: `execute_code`로 confirm 후 Restart 직후 `bb.DefenderPool` 동일·`running=true`·teardown 후 `DefenderUnitTag` 엔티티 0건 확인.

---

## P1-06 — 재시작 (다른 픽으로 재도전)

### 결정

21. **`ResultScreen.RedraftRequested` 이벤트 신설**: `RestartRequested`와 대칭. `redraftButton` SerializeField 추가. Awake에서 AddListener, OnDestroy에서 RemoveListener.
22. **`BattleBridge.OnRedraftRequested` 핸들러**: 로그 롤 → `TeardownCurrentBattle` → ResultScreen Hide → `_running=false`, `_resultShown=false` → `draftController.BeginDraft()`. `StartBattle`은 호출하지 않음(Confirm 시점까지 유예).
23. **Redraft 버튼 생성**: 기존 Restart 버튼을 `Instantiate`로 복제, 레이블을 "REDRAFT"로 변경, 위치를 Restart 버튼 아래 120px 오프셋.
24. **동작 검증**: 1차 풀/시드 기록 → 재드래프트 시 새 시드·새 풀 생성·picked=0·DraftView 재활성 확인.

---

## P1-07 — 로깅 확장 (DraftRecord)

### 결정

25. **BattleLogSchema 확장**: `DraftRecord { List<string> pool, List<string> picked, int seed }` 신규. `BattleLogEntry.draft` 필드 추가.
26. **phase 필드 기본값 "phase1"**: Phase 진입 전환 표기. 이전 로그는 "phase0"이었으며, Phase 2에서는 여기를 "phase2"로 바꿀 것.
27. **`BattleLogger.SetDraft` 시그니처**: DraftRecord를 받아 내부 필드에 **복사**(외부 리스트 재사용 방지). null/no-op 안전.
28. **호출 시점**: `DraftController.TryConfirm` 안에서 `BattleBridge.SetDefenderPool` 직전에 `logger.SetDraft(...)`. BeginDraft 시점이 아닌 Confirm 시점 — 미확정 드래프트는 로그에 남기지 않는 방침.
29. **pool/picked 표기법**: SO `displayName`을 사용(PHASE1 §4 자율 결정: displayName / GUID / asset path 중 택1). 검증 시 사람 가독성이 우선. Phase 1 종료 후 분석 파이프라인에서 GUID 필요하면 병행 기록 검토.
30. **실제 로그 샘플** (`session-20260415-164719-a0330a2f.json`): `phase=phase1`, `draft.pool=10`, `draft.picked=7`, `seed=-2071281404`, 배치 유닛 이름도 picked 집합에서 나옴 — 전체 플로우 적재 확인.

---

## P1-08 — EditMode 테스트 확장

### 결정

31. **DraftSessionTests.cs**: 7건 추가 — pool 크기/중복 없음, 동일 시드 → 동일 풀, Toggle add/remove, 7 초과 픽 거부, 풀 외 유닛 거부, Reset이 picks 초기화, PickedArray 순서 보존.
32. **테스트 SO 제작**: `ScriptableObject.CreateInstance<DefenderUnitData>()` — 에셋 없이 인스턴스 10개 생성. TearDown에서 `DestroyImmediate`로 누수 방지.
33. **회귀**: 기존 MovementSystemTests(3) + UnitLifecycleSystemTests(3) + 신규 DraftSessionTests(7) = **13/13 pass**, 1.50s. `run_tests` MCP 경유 확인.

---

## P1-09 — Phase 0 회귀 체크

### 결정

34. **체크 결과**: MapView 활성, 드래프트 Confirm 후 _running=true, 배치→엔티티 생성, RestartBattle→teardown→정상 재시작, 재시작 후 배치 가능, 로그 파일(`GameLogs/session-*.json`) 생성 모두 확인.
35. **MCP 자동 검증의 한계**: Unity Editor 포커스 없을 때 Update가 안 돌아 DEFEAT 자연 발생까지 기다리기 어려움 → `rs.ShowDefeat()` 직접 호출 + 이벤트 핸들러 경로 점검으로 우회. DEFEAT 실측은 P0 Decision 17처럼 사용자 포커스 하에 추가 확인 권장.
36. **Phase 1이 깨뜨린 Phase 0 기능 없음**: BattleBridge/ECS 시스템은 무변경, 로깅은 기능 추가만, Input/ResultScreen 동작 유지.

---

## P1-10 — Android 실기기 검증

### 상태

- **미완** — 실기기 필요. 에이전트가 에디터에서 할 수 있는 모든 검증은 통과.
- **후속 절차**: Build Target을 Android로 전환 → IL2CPP + ARM64 설정 점검 → APK 빌드 → 실기기 설치 → 드래프트 UI 터치, 배치 터치, Restart·Redraft 터치 확인 → `Application.persistentDataPath/GameLogs/` 로그 존재 확인.
- **Input**: `PlacementInput`과 드래프트 카드 UI 모두 `UnityEngine.InputSystem.Pointer`/UGUI EventSystem 기반이라 터치 자동 대응. 추가 분기 불필요.

---

## Phase 1 전용 아키텍처 검증 (PHASE1 §3.2)

- ECS 시스템 무변경 → Phase 0 경계 유지
- `BattleBridge` 여전히 유일 ECS 창구 (`SetDefenderPool`은 데이터 주입만, 내부에서 EntityManager 접근 안 함)
- `DraftSession` / `DraftController` / `DraftView`는 전부 MonoBehaviour 레이어 — ECS 오염 없음
- 신규 7 방어 유닛 전부 SO 기반 — 하드코딩 스탯 0건
- `DraftController` 정적 Instance 없음 — `GameManager`가 유일 싱글톤 유지
- Assembly Definition 여전히 `Wassup.Runtime` + `Wassup.Tests.EditMode` 2개 체제

---

## Phase 1 종료 선언 조건 (현 상태)

- 기능 이진 체크 P1-01 ~ P1-09 **완료**
- P1-10 **실기기 검증은 사용자 실물 디바이스 필요**
- 주관 평가 게이트(§3.3)는 외부 플레이어 3~5명 반복 플레이 이후 진행
