# 1 — 시즌 분리 + 랜덤 배정 + 주입 소스 스왑

## 목적

기믹을 시즌에서 떼어내고, `BattleConfig.gimmickPool` 에서 매치당 1개를 **결정론적으로** 배정해 BattleBridge 에 주입한다. 배정 소스가 `SeasonData.gimmick` → `GameManager.AssignedGimmick` 로 바뀐다. 이 유닛 종료 시 기믹이 BattleConfig 로부터 정상 주입되고 on/off 가 동작한다(dormant 창 없음).

## 변경 대상

- `Assets/_Project/Scripts/Data/Season/SeasonData.cs` — `gimmick` 필드 **제거**.
- `Assets/_Project/Scripts/Data/Gimmick/GimmickData.cs` — 클래스 주석의 "SeasonData.gimmick 슬롯용" 문구 정정(BattleConfig.gimmickPool).
- `Assets/_Project/Scripts/Core/MatchSeed.cs` — `GimmickSalt` + `DeriveGimmickSeed`.
- (신규) `Assets/_Project/Scripts/Core/GimmickSelection.cs` — 순수 `PickIndex`.
- (신규) `Assets/_Project/Tests/EditMode/GimmickSelectionTests.cs`.
- (신규, 여유되면) `Assets/_Project/Tests/PlayMode/GimmickAssignmentTest.cs` — assign→inject 스모크.
- `Assets/_Project/Scripts/Core/GameManager.cs` — `battleConfig` 필드 + 배정 + 주입 + `AssignedGimmick`.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SetAssignedGimmick` + **3개 소비 지점 소스 스왑**. **(공유 파일 — 자기 hunk 만 선별 스테이징, 타 세션 마커 확인)**
- `Assets/_Project/Scenes/BattleScene.unity` — `GameManager.battleConfig` 배선(에셋은 unit 0 에서 생성됨).

## 구현

1. **SeasonData**: `public GimmickData gimmick;` 라인 삭제(주석 포함). 시즌은 맵 테마 전담. 제거 시 `season_overwork.asset` 의 해당 프로퍼티는 재직렬화에서 자동 드롭 — 잔여 참조 없음 확인.
2. **MatchSeed**: 기존 `Derive*Seed`/`Mix` 는 **`int` 반환**이므로 미러로 `const int GimmickSalt = <고유값>` + `public static int DeriveGimmickSeed(int matchSeed)` 추가(반환 int). PickIndex 호출 시 `(uint)` 캐스트.
3. **GimmickSelection**(순수, `Wassup.Core`):
   ```csharp
   public static int PickIndex(int poolCount, uint seed)
   {
       if (poolCount <= 0) return -1;
       var rng = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);
       return (int)(rng.NextUInt() % (uint)poolCount);
   }
   ```
   결정론 계약: 같은 (count, seed) → 같은 index. `seed==0` 방어(Random 은 0 금지).
4. **GameManager**:
   - `[SerializeField] private BattleConfig battleConfig;`
   - `public GimmickData AssignedGimmick { get; private set; }`
   - `Start()` 의 `EnsureMatchSeed()` **직후**, TestMode/squad/draft 분기 **이전**에 `AssignGimmick()` 호출.
   - `AssignGimmick()`: `battleConfig != null && battleConfig.gimmickEnabled && pool!=null && pool.Length>0` 이면 `idx = GimmickSelection.PickIndex(pool.Length, (uint)MatchSeed.DeriveGimmickSeed(MatchSeed))`, `AssignedGimmick = pool[idx]`; 아니면 `null`. `battleBridge?.SetAssignedGimmick(AssignedGimmick)`. `Debug.Log($"[GameManager] gimmick={(AssignedGimmick!=null?AssignedGimmick.gimmickId:\"none\")}")`.
   - **empty-pool 경고**: `gimmickEnabled==true && (pool==null || pool.Length==0)` 이면 `Debug.LogWarning`(off 와 결과는 같지만 오설정 신호). 
   - **Restart 시맨틱**: `AssignedGimmick`/`_assignedGimmick` 는 매치 내 유지(씬 리로드 없는 mid-match Restart 는 초기 배정 유지 = 결정론적, 의도됨). `AssignGimmick` 은 매치 진입 Start 1회만.
5. **BattleBridge** (자기 hunk 만 — 아래 **3곳 모두** 스왑, 하나라도 놓치면 컴파일 실패):
   - 필드 `private GimmickData _assignedGimmick;` + `public void SetAssignedGimmick(GimmickData g) => _assignedGimmick = g;`
   - **(a) `CreateGimmickConfigIfActive()` (~L4164)**: `season?.gimmick is OverworkGimmickData od` → `_assignedGimmick is OverworkGimmickData od`. 시즌 읽기·로그의 season 언급 제거.
   - **(b) `BuildPickupSpawnState()` (~L646-647)**: `var season=...; if (!(season?.gimmick is OverworkGimmickData)) return;` → `if (!(_assignedGimmick is OverworkGimmickData)) return;`. season 지역변수 제거.
   - **(c) 디버그 로그 (~L3878)**: `SeasonRuntime.Active?.gimmick?.gimmickId` → `_assignedGimmick?.gimmickId`(season 부분만 교체, 나머지 로그 유지).
6. **씬 배선**: `BattleScene` 의 GameManager 컴포넌트에 `battleConfig = BattleConfig.asset` 주입.

## 완료 기준

- [ ] 컴파일 통과, 콘솔 에러 0. `season.gimmick`/`SeasonRuntime.Active.gimmick` 잔존 참조 0(grep 확인).
- [ ] `GimmickSelectionTests` — 같은 seed→동일 index / count≤0 → -1. (분포 테스트는 Random 재검이라 생략.) EditMode green.
- [ ] (여유되면) PlayMode 스모크: enabled → `OverworkGimmickConfig` 싱글턴 존재, `gimmickEnabled=false` → 부재. 무거운 씬 스캐폴딩이 병목이면 우선순위 낮춤(CLAUDE.md 테스트 지침).
- [ ] Play: 콘솔 `gimmick=<id>` + `OverworkGimmickConfig 주입` + `PickupSpawnState built` 로그. 피로도/레드불 기존대로 발동.
- [ ] `BattleConfig.gimmickEnabled=false` → `gimmick=none`, config·픽업 스폰 미주입, 클린 forest. 다시 true 복구.
- [ ] `debugFixedMatchSeed` 고정 시 재실행마다 같은 기믹(현재 pool 1개라 항상 동일 — 로그로 확인).
- [ ] BattleBridge 커밋에 타 세션 마커(ModifierOrigin/Empowered/Dreamcatcher 등) 미포함.
