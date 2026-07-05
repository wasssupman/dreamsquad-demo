# 2 — BattleScaledRateManager (ECS 시간)

## 목적

`BattleSimGroup` 의 시간 진행을 `BattleTimeScale` singleton 값으로 제어한다. 정지=그룹 skip, 슬로우모=스케일된 delta.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/BattleScaledRateManager.cs`
- 신규 `Assets/_Project/Scripts/Battle/BattleTimeScale.cs` (singleton 컴포넌트, 그룹 인프라 — 특정 맥락 소속 아님)
- `BattleSimGroup.cs` (단위 1) — `OnCreate` 에서 `RateManager = new BattleScaledRateManager(...)` 부착

## 구현

`BattleTimeScale`:
```csharp
public struct BattleTimeScale : IComponentData { public float Value; }   // 기본 1, BattleBridge 가 write
```

`BattleScaledRateManager : IRateManager` — Entities 6.4 계약 (`RateUtils.cs` 확인):
- `ShouldGroupUpdate(group)` 은 false 반환까지 반복 호출됨.
- 프레임당 1회 로직:
  1. 이미 이번 프레임 업데이트했으면 `PopTime()` 하고 `false` 반환(재진입 종료).
  2. `BattleTimeScale` singleton 조회(없으면 1 취급). `scale <= 0` → **PushTime 없이 `false` 반환** (그룹 멤버 전부 skip = 완전 정지).
  3. `scale > 0` → `float dt = group.World.Time.DeltaTime * scale;` `group.World.PushTime(new TimeData(elapsedTime: group.World.Time.ElapsedTime + dt, deltaTime: dt));` 플래그 set, `true` 반환.
- `float Timestep { get; set; }` — 계약상 필요. **스케일 delta 를 Timestep 로 라우팅하지 않는다**(setter 가 ≥0.0001 클램프). TimeData 직접 push.
- singleton 조회는 `group.EntityManager` 로 `TryGetSingleton` 패턴(RateManager 는 managed, group.World 접근 가능).

## 완료 기준

- [ ] 컴파일 통과. `read_console` 에러 0.
- [ ] Play: `BattleTimeScale.Value=1` → 정상 속도(단위 1 스모크와 동일).
- [ ] `Value=0.2` → ECS 유닛 이동/공격 눈에 띄게 0.2x. `Value=0` → ECS 유닛 완전 정지, 재개 시 이어감.
- [ ] `Value=0` 동안 프로파일러상 BattleSimGroup 멤버 시스템 미실행(유휴 tick 0) 확인.
- [ ] elapsed 누적이 scaled dt 로만 진행(쿨다운 등 countdown 이 스케일 반영).

## 주의

- `BattleTimeScale` singleton 엔티티 **생성**은 단위 3(BattleBridge) 부팅 시. 단위 2 단독 테스트는 execute_code 로 임시 생성·set 하여 검증.
