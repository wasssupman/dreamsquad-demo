using Unity.Mathematics;

namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 3f — **불꽃 팽이.** host 를 도는 화염구를 수명만큼 띄운다.
    //
    // 이 스킬이 실제로 판단하는 것은 셋이다:
    //   ① 몇 개를 띄우나 — 저작이 개수를 말한다(0 이면 1개).
    //   ② 어느 각도에서 시작하나 — 균등 배치. 같은 궤도·같은 수명·같은 각속도로 돌면서
    //      **시작 각도만** 2π/n 씩 어긋난다.
    //   ③ **얼마나 빨리 도나** — 각속도 = 선속도 ÷ 반경.
    //
    // ⚠ ③ 이 이 파일에서 가장 중요한 줄이다. 저작은 «월드 속도»를 적는데, 그걸 각속도로
    // 직접 쓰면 반경을 키우는 순간 큰 원에서 갑자기 빨라진다. 나누기 하나가 「반경을 키워도
    // 도는 체감이 유지된다」는 저작의 뜻을 지킨다 — 그래서 어댑터가 아니라 여기 산다.
    public sealed class OrbitProjectileSkill : ISkill
    {
        public const int Id = 24;
        public int SkillId => Id;

        // 한 번에 띄울 수 있는 상한. 저작 실수(period 를 다른 뜻으로 적음)가 프레임을
        // 잡아먹지 않게 막는 값이지 튜닝 노브가 아니다.
        private const int MaxOrbs = 16;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            float radius = p.TileRange * ctx.TileSize;
            // 반경·수명·속도 중 하나라도 비면 「도는 척」만 하는 no-op 이다 — 아예 안 띄운다.
            if (radius <= 0f || p.Duration <= 0f || p.Speed <= 0f) return;

            int count = math.clamp(p.Period > 0 ? p.Period : 1, 1, MaxOrbs);
            var center = ctx.Position(caster.Unit);

            for (int i = 0; i < count; i++)
            {
                ctx.Emit(new SimIntent
                {
                    Kind = SimIntentKind.SpawnOrbitProjectile,
                    Source = caster.Unit,          // 위협 귀속
                    Position = center,             // 궤도 중심(발사 시점 고정)
                    Amount = p.Magnitude,          // flat — 공격력 배율 미적용(계약 10)
                    Radius = radius,
                    Speed = p.Speed / radius,      // ↑ ③
                    Phase = count <= 1 ? 0f : 2f * math.PI * i / count,
                    Duration = p.Duration,         // 수명
                    HitThreshold = p.HitThreshold, // 피격 반경 — 궤도 반경과 **다른 축**이다
                    DataIndex = p.DataIndex,
                    VisualScale = p.VisualScale,
                    // ⚠ **host 의 공격 층을 따른다.** 안 실으면 0 = 무제한이라, 지상만 때리는
                    // 유닛에 이 카드를 붙이면 그 유닛이 못 때리는 비행 적을 화염구는 때린다 —
                    // 카드가 유닛의 근본 제약을 우회하는 뒷문이 된다.
                    TargetTraversalLayers = p.TargetTraversalLayers,
                });
            }
        }
    }
}
