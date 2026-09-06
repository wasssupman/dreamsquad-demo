using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // enemy-detection-range unit 8 — **대상 지향 추격판**. 감지한 «그» 방어유닛까지, «내» 통행
    // 층으로 구운 dist/flow 필드(길이 = gridSize.x * y).
    //
    // ★ **이 버퍼가 존재하는 이유는 규칙의 2단계다** — 「그 적을 향해 갈 수 있는 이동 경로가
    // 있는가」. units 1~6 은 이 질문을 공용 사냥판(`DefenderFieldSingleton`)에 위임했는데,
    // 그 필드는 **다른 질문**에 답한다:
    //   - 「**아무** 방어유닛의 사격 칸까지」 — 대상이 특정되지 않는다(실측 5.0% 갈림)
    //   - 「**지상** 통행으로」 — `goalField.walkMask` 로 굽기 때문. 비행이 자기 층으로 안 물어졌다.
    // 그래서 비행 감지가 벽 위에서 조용히 죽었고, 그게 「비행은 감지 대상 밖」으로 오독됐다.
    // 이 버퍼가 붙은 뒤로 **비행은 특별 취급이 없다** — 층은 `PathFollowState.traversalLayers`
    // 에서 오고, 규칙은 층을 언급하지 않는다.
    //
    // **`AggroChaseCell` 과 형제다**(같은 `AggroChaseMath.BuildChaseField` 로 굽는다). 다른 점 둘:
    //   1. `flow` 도 같이 보관한다 — 빌더가 어차피 만들고(`BuildFromSources` 의 `tempFlow`),
    //      갖고 있으면 `MovementSystem` 의 사냥 분기가 `huntField.flow/dist` 를 **그대로 치환**하는
    //      drop-in 이 된다(평활화 포함, 하강 코드 재작성 0). 어그로는 `RecoveryDir` 로 dist 만 쓴다.
    //   2. 무효화 주체가 다르다. 어그로는 `FlowFieldRebuildSystem`(Effects)이 떼 주지만, 이쪽은
    //      **Combat 이 자기 맥락 안에서** 한다 — `DetectedTarget.chaseSignature` 와 필드의
    //      `blockedSignature` 를 비교해 다르면 다시 굽는다. Effects 가 Combat 소유 컴포넌트를
    //      건드리지 않게 하려는 것이다(맥락 경계).
    //
    // writer 는 `DetectionSystem` 하나(ECB 로 부착/제거, 자기 OnUpdate 끝에서 재생 —
    // `MovementSystem` 이 **같은 프레임**에 본다). Movement 는 RO 소비.
    [InternalBufferCapacity(0)]
    public struct DetectionChaseDist : IBufferElementData
    {
        public int dist;
    }

    [InternalBufferCapacity(0)]
    public struct DetectionChaseFlow : IBufferElementData
    {
        public float2 flow;
    }
}
