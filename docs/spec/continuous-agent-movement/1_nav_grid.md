# unit 1 — `NavGrid` 프레임 뷰 + 정적 walk 마스크 단일 소유

## 목적

벽 질의의 **단일 진입점**을 세우고, 정적 walk 마스크의 소유자를 바로잡는다.

**술어는 바꾸지 않는다.** `NavGrid` 는 이 unit 에서 기존 zero-flow 술어를 그대로 감싼다. 의미 교체는 unit 2 다. 이 unit 은 **동작 불변 · 테스트 무손상** 지점에서 끊어, 다음 unit 의 실패가 술어 탓임을 분명히 한다.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Battle/Movement/NavGrid.cs`
- 수정: `Assets/_Project/Scripts/Battle/Effects/FlowFieldSingleton.cs` — `walkMask` 필드 추가
- 수정: `Assets/_Project/Scripts/Battle/Effects/DefenderFieldSingleton.cs` — `walkMask` 제거
- 수정: `Assets/_Project/Scripts/Battle/Effects/DefenderFieldSystem.cs` — 마스크를 `FlowFieldSingleton` 에서 읽음
- 수정: `Assets/_Project/Scripts/Bridge/SimFieldInstaller.cs` — Persistent 마스크 1개만 할당
- 수정: `Assets/_Project/Scripts/Battle/Movement/MovementCellTrim.cs` — `NavGrid` 조립 + 기존 시그니처를 어댑터 오버로드로 유지
- 신규: `Assets/_Project/Tests/EditMode/NavGridTests.cs`
- **직접 수정 없음**: `MovementSystem.cs` · `AggroStateSystem.cs` · `PatrolFieldSystem.cs` — `MovementCellTrim` 어댑터 오버로드가 내부에서 `NavGrid` 를 경유하므로 호출부 diff 가 0 이다 (ecs-review L2 정정). 이 셋이 `NavGrid` 를 직접 받는 형태로 바뀌는 건 unit 2~3 이다.

## 구현

### `NavGrid` — 프레임 뷰

정적 마스크와 동적 오버레이를 합친 **읽기 전용 뷰**. 저장하지 않고 프레임마다 조립한다.

```
readonly struct NavGrid {
    NativeArray<byte> staticWalk;   // 1 = walkable
    NativeArray<float2> flow;       // unit 1 한정 — 기존 zero-flow 술어 유지용. unit 2 에서 제거
    NativeHashSet<int2> blockedCells;
    bool hasObstacles;
    int2 gridSize; float tileSize; float3 origin;

    bool InBounds(int2)
    bool IsBlocked(int2)            // OOB || 정적 벽 || 동적 장애물
    void MaterializeWalkMask(NativeArray<byte> outMask)
}
```

**생성자는 plain 값만 받는다** — `FlowFieldSingleton`/`ObstacleSingleton` 을 인자로 받지 않는다. 그 타입을 알면 ECS 에 묶여 다른 아키텍처가 같은 함수를 못 쓴다. 조립은 호출자(ISystem)가 한다.

### 마스크 소유권 이관

현재 `tiles == Walk` 마스크가 `DefenderFieldSingleton.walkMask` 에 있다("goal field 가 저장하지 않는 값이라 여기서 보유"). 정적 벽은 goal field 쪽이 정본이므로 옮긴다.

⚠ **같은 `NativeArray` 를 두 싱글턴이 들면 double dispose 로 죽는다.** 반드시 한쪽만 소유한다 — `FlowFieldSingleton` 이 소유·dispose 하고 `DefenderFieldSystem` 은 읽기만 한다. `SimFieldInstaller` 는 Persistent 마스크를 1개만 할당한다(현재는 Temp `walk` + Persistent `dWalk` 2개).

⚠ **`walkMask` 를 `FlowFieldSingleton.IsCreated` 에 넣지 않는다.** `goals` 를 뺀 것과 같은 이유 — 마스크를 안 채우는 EditMode 픽스처가 `IsCreated=false` 로 뒤집혀 무관한 테스트가 무더기로 깨진다.

### 술어 우선순위 — unit 1 의 동작 불변이 걸린 지점

`IsBlocked` 는 **`flow` 가 있으면 무조건 기존 zero-flow 규칙**을 쓴다. 정적 마스크는 그것이 없을 때만 본다.

반대로(마스크 우선) 짜면 **고립된 Walk 셀**의 판정이 뒤집힌다 — 골에서 도달 불가한 Walk 셀은 flow=0 이라 지금은 벽이지만, 마스크에선 통행 가능이다. 그건 의미 변경이고 unit 2 의 몫이다. 여기서 새면 unit 을 나눈 의미가 사라진다.

한편 `DefenderFieldSystem` 의 BFS 는 **원래부터 정적 마스크**(`tiles == Walk` 사본)를 썼으므로, 공유 마스크로 바꿔도 입력이 동일하다.

unit 2 가 이 우선순위를 뒤집고 `flow`/`goals` 폴백을 통째로 제거한다.

## 완료 기준

- [x] compile 통과 (콘솔 에러 0)
- [x] EditMode **실패 0 · 기존 테스트 수정 0건** — 동작 불변의 증거. 고쳐야 했다면 술어가 샌 것이다
- [x] `DefenderFieldSingleton` 에 `walkMask` 없음 · Persistent 마스크 할당 1개
- [x] `ecs-reviewer` 통과 — CRITICAL/HIGH/MEDIUM **0건**. LOW 2건(문서 부정확)·테스트 갭 3건 반영 완료
- [x] `NavGridTests` 신설 — flow 경로 / 마스크 경로 **양쪽** 직접 커버 (ecs-review T1·T2)
- [ ] Play 스모크: 전투 진입 → 적 이동·보스 사냥·순찰병 정상, 재진입 시 콘솔 경고 0 (double dispose 회귀 확인)

## 주의

`DefenderFieldSystem` 은 `RequireForUpdate<DefenderFieldSingleton>` 만 건다. 마스크를 `FlowFieldSingleton` 에서 읽으려면 그 싱글턴 부재 시의 거동을 정해야 한다 — **두 필드는 `SimFieldInstaller` 가 항상 함께 세우므로** 실운영에선 동시 존재이지만, 합성 테스트 월드는 한쪽만 만들 수 있다. 부재 시 조용히 return 한다(현행 `!field.IsCreated` 조기 return 과 같은 성격).

---

**완료 기준 확인**: (미확인)
