using System.Globalization;
using System.Text;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 6b — **판 위의 칸 하나가 그 위에 선 유닛에게 주는 것.**
    //
    // 옛 `EffectTileData` 의 수치 부분이다(오버레이 타일은 뷰가 갖는다).
    //
    // ⚠ **개체가 없다.** 효과 타일은 칸이라 스폰도 소멸도 없고, 배치·퇴근 **엣지에서만**
    // 일이 일어난다. 그래서 파이프라인 표의 「생성」·「매 프레임」 칸이 N/A 다.
    //
    // ⚠ 같은 `(Stat, Op)` 를 두 줄 저작하면 **마지막만 남는다** — 병합 키가 같아 뒤 줄이
    // 앞 줄을 갱신하기 때문이다(옛 저작 규칙 그대로).
    public struct EffectTileEntryDef
    {
        /// <summary>`Effects.StatKind` 의 int 값.</summary>
        public int Stat;

        /// <summary>
        /// `Effects.CombineOp` 의 int 값. **저작한 연산자를 그대로 쓴다** — 값으로 버킷을
        /// 고르는 중앙 헬퍼(`ModifierAuthoring.FromMultiplier`)를 지나지 않는다.
        /// `RegenPerSec` 는 기본이 0 이라 가산이어야 하고, 그 선택은 타일이 한다.
        /// </summary>
        public int Op;

        public float Magnitude;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv, string prefix)
            => MatchDefinition.Put(sb, prefix,
                Stat.ToString(inv) + "," + Op.ToString(inv) + "," + Magnitude.ToString("R", inv));
    }

    public struct EffectTileDef
    {
        /// <summary>저작 자산 이름.</summary>
        public string Id;

        public EffectTileEntryDef[] Entries;

        public int EntryCount => Entries != null ? Entries.Length : 0;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "id", Id);
            for (int i = 0; i < EntryCount; i++)
                Entries[i].Canonicalize(sb, inv, "entry" + i.ToString(inv));
        }
    }
}
