using System.Globalization;
using System.Text;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 4 — 드림캐쳐 카드와 기믹의 **정의표 줄**.
    //
    // 이 unit 이 카드에서 갖는 것은 **자원**뿐이다: 큐 · 손패 · 각성 게이지 · 부착 등록부 ·
    // 쿨다운. 「그 카드가 무엇을 하는가」(바인딩·페이로드·발동)는 unit 7 의 것이고, 그래서
    // 여기에 효과 필드가 하나도 없다 — 있으면 다음 사람이 그것을 읽어 실행하려 든다.

    public enum CardKind : byte
    {
        /// <summary>방어유닛에 붙는다. 쓰면 **풀에서 이탈**하고, 숙주가 판을 떠날 때만 돌아온다(D17).</summary>
        Attach = 0,
        /// <summary>즉시 시전한다. 성공하면 값을 치르고 **덱 뒤로 재활용**된다(D17).</summary>
        Active = 1,
    }

    public struct CardDef
    {
        public string Id;
        public CardKind Kind;

        /// <summary>각성에서 깎는 값. 카드 **종류가 아니라 이 카드**가 정한다(D15).</summary>
        public int Cost;

        /// <summary>액티브 재사용 대기(초). 0 = 대기 없음(K1 「기록 자체가 없으면 준비된 것」).</summary>
        public float CooldownSeconds;

        /// <summary>
        /// 「인수인계」 선언. 이 카드가 붙은 유닛을 **플레이어가 퇴근**시키면 그 유닛의 나머지
        /// 카드가 부착 순서 그대로 큐 **맨 앞**으로 온다(D9).
        ///
        /// ⚠ 옛 전투는 이 판정을 두 곳(`DeclaresRetireRecall` ↔ 브리지 부착 화이트리스트)에
        /// 두었고, 한쪽만 넓히면 「붙는데 무효」 또는 「검증 없이 발동」이 됐다(D10 · 중복 2).
        /// 여기 **한 칸**이 그 두 판정을 접은 자리다 — 저작이 곧 판정이다.
        /// </summary>
        public bool DeclaresRetireRecall;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "id", Id);
            MatchDefinition.Put(sb, "kind", (int)Kind, inv);
            MatchDefinition.Put(sb, "cost", Cost, inv);
            MatchDefinition.Put(sb, "cooldownSeconds", CooldownSeconds, inv);
            MatchDefinition.Put(sb, "retireRecall", DeclaresRetireRecall ? 1 : 0, inv);
        }
    }

    // 시즌 기믹 한 줄. 이 unit 은 **고르기만** 한다(바인딩 부착은 unit 7).
    public struct GimmickDef
    {
        public string Id;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
            => MatchDefinition.Put(sb, "id", Id);
    }
}
