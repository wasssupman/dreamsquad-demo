using System.Globalization;
using System.Text;
using Unity.Mathematics;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 6b — **판 위에 깔리는 존 장판의 저작.**
    //
    // 옛 `HazardSO` 의 수치 부분이다(프리팹·비주얼은 뷰가 갖는다 — 계약 6).
    // 「무엇이 언제 이것을 까는가」는 unit 7 이고, 여기 있는 것은 **물건과 그 규칙**뿐이다.
    //
    // ⚠ 필드를 더하면 `Canonicalize` 도 같이 고친다. 안 고치면 「장판을 바꿨는데 해시가
    // 그대로」라는 조용한 실패가 된다.

    /// <summary>
    /// 존이 덮는 모양. 옛 `Wassup.Battle.Effects.HazardShape` 의 **미러**이고 이름·번호가
    /// 같아야 한다 — 어셈블리가 갈려 컴파일러가 못 잡는 자리라 `BuilderEnumPinTests` 가 그물이다.
    /// </summary>
    public enum HazardShapeKind : byte
    {
        SingleCell = 0,
        Square3x3 = 1,
        RadiusSquare = 2,
    }

    /// <summary>
    /// 존이 거는 효과의 **저작 토큰**. 옛 `Wassup.Battle.Effects.CcKind` 의 미러이고
    /// 이름·번호가 같아야 한다(같은 그물).
    ///
    /// ⚠ **토큰이지 런타임 슬롯이 아니다.** `Slow` 는 이동속도 모디파이어로, `DoT` 는
    /// 지속 피해 슬롯으로 간다(6a 구현 9). 군중 제어 슬롯으로 가는 것은 셋뿐이다.
    /// </summary>
    public enum HazardEffectKind : byte
    {
        Slow = 0,
        Impulse = 1,
        DoT = 2,
        Stun = 3,
        Sleep = 4,
    }

    // 존이 거는 효과 한 줄.
    public struct HazardEffectDef
    {
        /// <summary>`HazardEffectKind` 의 int 값.</summary>
        public int Kind;

        /// <summary>
        /// 크기. `Slow` = 이동속도 배율 · `DoT` = 틱당 피해(주기 0 이면 DPS) ·
        /// 군중 제어 = 세기(오늘 소비처 없음 — 지속만 쓴다).
        /// </summary>
        public float Magnitude;

        /// <summary>
        /// **「나가면 이만큼 뒤에 꺼진다」**(F17). 총 지속이 아니다 — 위에 서 있는 동안
        /// 매 틱 이 값으로 갱신되므로, 총 지속으로 읽으면 장판이 즉시 꺼진 것처럼 보인다.
        /// 장판 자체의 총 수명은 `HazardDef.Lifetime` 이 따로 든다.
        /// </summary>
        public float RestDuration;

        /// <summary>`DoT` 전용 — 이산 틱 간격(초). 0 이면 연속(`Magnitude` = DPS).</summary>
        public float TickInterval;

        /// <summary>`DoT` 전용 — `Effects.DotElement` 의 int 값. **출처는 저작하지 않는다**(F16).</summary>
        public int Element;

        /// <summary>
        /// 이 효과가 거는 대상의 진영 비트(`Faction`). **0 = 아무에게도 안 건다.**
        ///
        /// 옛 전투는 `Faction.EnemyUnit` **하드 게이트**였고 CLAUDE.md 제약 8 이 그것을
        /// 「축 없이 하드코딩해 둔 같은 실수의 반대편」으로 명시 지목했다(F34). 축은 여기서
        /// 열리고, 빌더가 오늘의 값(적만)을 실으므로 **판은 안 바뀐다.**
        /// </summary>
        public int TargetFactions;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv, string prefix)
            => MatchDefinition.Put(sb, prefix,
                Kind.ToString(inv) + ","
                + Magnitude.ToString("R", inv) + ","
                + RestDuration.ToString("R", inv) + ","
                + TickInterval.ToString("R", inv) + ","
                + Element.ToString(inv) + ","
                + TargetFactions.ToString(inv));
    }

    // 존 장판 한 종류.
    public struct HazardDef
    {
        /// <summary>저작 자산 이름. 어느 줄이 어느 에셋인지를 로그·해시가 말하게 한다.</summary>
        public string Id;

        /// <summary>`HazardShapeKind` 의 int 값.</summary>
        public int Shape;

        /// <summary>`RadiusSquare` 전용 저작 반경(칸). 다른 모양은 안 읽는다.</summary>
        public int Radius;

        /// <summary>총 수명(초). 이 시간이 지나면 개체가 사라진다.</summary>
        public float Lifetime;

        public HazardEffectDef[] Effects;

        public int EffectCount => Effects != null ? Effects.Length : 0;

        /// <summary>
        /// 멤버십 판정의 반경(칸). **저작 모양이 정한다** — `HazardShapeMath` 가 그 유일한 규칙이다.
        /// 음수면 존 효과가 없다(F18).
        /// </summary>
        public int RadiusTiles => HazardShapeMath.RadiusTiles((HazardShapeKind)Shape, Radius);

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "id", Id);
            MatchDefinition.Put(sb, "shape", Shape, inv);
            MatchDefinition.Put(sb, "radius", Radius, inv);
            MatchDefinition.Put(sb, "lifetime", Lifetime, inv);
            for (int i = 0; i < EffectCount; i++)
                Effects[i].Canonicalize(sb, inv, "eff" + i.ToString(inv));
        }
    }

    // 저작 모양 → 멤버십 반경. **한 곳에서만 한다.**
    //
    // 옛 전투는 이 매핑이 스포너(`EffectSpawner.SpawnHazard`) 안에 인라인돼 있었고, 셀
    // 샘플러(`HazardShapeSampler`)가 같은 규칙을 **따로** 들고 있었다. 셀 해시가 은퇴하면서
    // (distance-based-range unit 19) 샘플러 쪽은 죽었고, 남은 규칙이 이것 하나다.
    public static class HazardShapeMath
    {
        /// <summary>「존 효과가 없다」의 센티널(F18). **0 으로 바꾸면 한 칸 존이 전부 켜진다.**</summary>
        public const int NoZone = -1;

        public static int RadiusTiles(HazardShapeKind shape, int authoredRadius)
        {
            switch (shape)
            {
                case HazardShapeKind.SingleCell: return 0;
                case HazardShapeKind.Square3x3: return 1;
                case HazardShapeKind.RadiusSquare: return math.max(1, authoredRadius);
                default: return NoZone;
            }
        }
    }
}
