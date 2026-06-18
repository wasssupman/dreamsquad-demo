# Unit 3 — 어그로 이동 (가디언으로 보행 후 겹쳐 정지)

## 목적

어그로된 적이 **자기 moveSpeed 로** 가디언 타일을 향해 보행하고, 도착하면 겹쳐 정지하게 한다. 토네이도 강제 풀이 아니다(계약 2, 3).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`

## 구현

`MovementSystem.OnUpdate` 의 엔티티 루프 초반(포탈/플로우 처리 **이전**)에 어그로 분기 추가:

```csharp
var aggroLookup = SystemAPI.GetComponentLookup<Wassup.Battle.Effects.Aggroed>(isReadOnly: true);
var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);
```

루프 안에서:

```csharp
if (aggroLookup.HasComponent(entity))
{
    var guardian = aggroLookup[entity].guardian;
    if (transformLookup.HasComponent(guardian))
    {
        float3 gpos = transformLookup[guardian].Position;
        float3 to = gpos - current; to.y = 0f;
        float dist = math.length(to);
        const float stackThreshold = 0.05f; // 겹쳐 정지 거리
        if (dist > stackThreshold)
        {
            float speedMul = modifierStatsLookup.HasComponent(entity)
                ? modifierStatsLookup[entity].moveSpeedMul : 1f;
            float step = follow.ValueRO.speed * speedMul * dt;
            transform.ValueRW.Position = (step >= dist)
                ? new float3(gpos.x, current.y, gpos.z)        // 도착 → 겹침
                : current + math.normalize(to) * step;          // 자기 속도로 보행
        }
        // dist <= threshold: 겹쳐 정지(이동 안 함). 공격은 AttackSystem 이 처리.
    }
    continue; // 플로우 필드/포탈/토네이도/goal 판정 우회
}
```

- 핵심: **flow field·tornado pull 을 타지 않는다.** 목적지=가디언, 추진=적 자신의 `PathFollowState.speed`(+moveSpeedMul). 역주행은 자연 발생.
- `EnemyAttackMovePause` 는 어그로 분기에선 무시(어그로 적은 가디언으로 계속 접근).
- 해제되면(`Aggroed` 제거) 분기 안 타고 기존 flow field 로직으로 복귀 → 출구행(계약 6).

## 완료 기준

- [ ] 컴파일 + Burst 호환.
- [ ] PlayMode/EditMode: 어그로된 적 위치가 가디언 위치로 수렴(겹침).
- [ ] 어그로 적은 출구가 아니라 가디언으로 이동(역주행 포함).
- [ ] 해제 후 적이 다시 flow field 로 출구를 향함.
- [ ] 비어그로 적 이동 회귀 없음.
