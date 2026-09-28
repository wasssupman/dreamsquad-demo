using System.Globalization;
using System.Text;
using Unity.Mathematics;
using Wassup.BattleCore.Map;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 6b — **길을 막는 설치물의 저작.**
    //
    // 옛 `BlockingHazardSO` 의 수치 부분이다. 개체 자체는 unit 3 이 이미 세웠고
    // (`UnitKind.BlockingHazard` · `TickProjectilePhase.ResolveSpawnBlocker`) 이 표가 여는 것은
    // **수명과 부서짐**이다.
    //
    // ⚠ **문은 「부서짐」 하나다.** 시한 만료 경로는 옛 unit 7 에서 이미 은퇴했다 —
    // 시한은 두 번째 죽음 경로를 만들어 폭발을 시계 사건으로 바꿨고, 그래서 남은 시간을
    // 알리는 별도 장치(퓨즈 틴트)가 필요했다. 노후화는 **그냥 피해**라서 죽음도 폭발도
    // 한 문으로 나가고, 남은 시간은 이미 있는 체력 바가 그대로 말한다.
    public struct BlockingHazardDef
    {
        /// <summary>저작 자산 이름.</summary>
        public string Id;

        public float MaxHealth;

        /// <summary>
        /// `HazardShapeKind` 의 int 값 — **막는 칸의 모양**. 옛 `BlockingHazardSO.shape` 이고
        /// 옛 스포너는 반경 1 로 샘플링했다(`RadiusSquare` 도 3×3). 그 매핑이 `SpanRadius` 다.
        /// </summary>
        public int Shape;

        /// <summary>
        /// 중심에서 막는 칸까지의 반경(칸). 한 칸 → 0 · 3×3 → 1. 모르는 모양은 0(자기 칸만) —
        /// 존과 달리 «효과 없음» 센티널이 없다: 길막은 서면 적어도 자기 칸은 막는다.
        /// </summary>
        public int SpanRadius => math.max(0, HazardShapeMath.RadiusTiles((HazardShapeKind)Shape, 1));

        /// <summary>
        /// 몸 반경(타일). 제약 13 의 «대상의 몸» 항이 이 값을 받는다. **막는 칸에서 파생한다**
        /// (거점과 같은 규칙 = 점유의 내접원) — 저작 필드가 따로 있으면 3×3 바위가 한 칸 몸으로
        /// 거짓말할 수 있다.
        /// </summary>
        public float BodyRadius => StructureSize.BodyRadius(SpanRadius * 2 + 1);

        /// <summary>
        /// 초당 스스로 닳는 체력. 0 = 안 닳음.
        ///
        /// ⚠ **저작 감각은 「체력 ÷ 이 값 = 아무도 안 때렸을 때의 수명(초)」다**(F11).
        /// 옛 전투는 이 관계가 SO 주석에만 있었고, 모르면 저작자가 두 값을 따로 굴려
        /// 수명이 통째로 달라진다.
        /// </summary>
        public float DecayPerSec;

        // skill-data-table 1b(U10) — 폭발 **피해**는 여기 없다. 세운 탄의 피해(= 그 명세를 쓰는 효과 줄 `EffectDef.Damage`)를
        // 설치물이 들고(`Unit.BlockerExplodeDamage`) 부서질 때 낸다. 0 = 폭발 없음. 이 줄은 모양·반경·폭발 탄만.

        /// <summary>폭발 반경(칸).</summary>
        public int ExplodeTileRange;

        /// <summary>가까운 순 최대 타격 수. 0 = 무제한.</summary>
        public int ExplodeTargetCap;

        /// <summary>
        /// 폭발을 해결할 즉발 탄(`MatchDefinition.Projectiles` 인덱스). **-1 = 미배선.**
        ///
        /// ⚠ 0 이 유효 인덱스라 여기만 센티널이 필요하다(F12). 옛 전투는 폭발 피해를
        /// 저작했는데 탄이 미배선이면 인덱스가 0 으로 떨어져 **0번 탄의 비주얼이 한 프레임
        /// 번쩍였다** — 경고만 내고 차단하지 않는 fail-silent 였다. 새 코어는 빌더가
        /// **loud 하게 거절**하고 폭발 저작을 통째로 버린다.
        /// </summary>
        public int ExplodeProjectileDefIndex;

        /// <summary>
        /// 이 길막을 세우는 탄(`MatchDefinition.Projectiles` 인덱스). -1 = 탄이 안 세운다(디버그·
        /// unit 7 생산자 전용).
        ///
        /// 왜 탄 쪽이 아니라 여기인가: 옛 저작은 탄 SO 가 길막 SO 를 참조했다. 정의표에서는 그
        /// 참조를 **이 줄의 역참조**로 편다 — 탄 표는 전투 빌더가 매기고 길막 표는 이 unit 의
        /// 빌더가 매기므로, 둘을 잇는 번호는 뒤에 매기는 쪽이 들고 있어야 한 번에 확정된다.
        /// 같은 길막을 탄 둘이 쓰면 **줄이 둘**이 된다(값은 같다).
        /// </summary>
        public int SpawnedByProjectile;

        public static BlockingHazardDef Default() => new BlockingHazardDef
        {
            Id = "",
            MaxHealth = 0f,
            Shape = (int)HazardShapeKind.SingleCell,
            ExplodeTileRange = 1,
            ExplodeProjectileDefIndex = -1,
            SpawnedByProjectile = -1,
        };

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "id", Id);
            MatchDefinition.Put(sb, "maxHealth", MaxHealth, inv);
            MatchDefinition.Put(sb, "shape", Shape, inv);
            MatchDefinition.Put(sb, "decayPerSec", DecayPerSec, inv);
            MatchDefinition.Put(sb, "explodeTileRange", ExplodeTileRange, inv);
            MatchDefinition.Put(sb, "explodeTargetCap", ExplodeTargetCap, inv);
            MatchDefinition.Put(sb, "explodeProjectileDef", ExplodeProjectileDefIndex, inv);
            MatchDefinition.Put(sb, "spawnedByProjectile", SpawnedByProjectile, inv);
        }
    }
}
