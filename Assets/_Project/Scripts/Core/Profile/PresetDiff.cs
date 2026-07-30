using System.Collections.Generic;

namespace Wassup.Core
{
    // page-local-presets unit 1 — "작업본이 저장본과 다른가" 를 순수하게 판정한다.
    //
    // 왜 변이마다 세우는 dirty 플래그가 아닌가: 유닛을 뺐다 **같은 자리에** 되넣으면 내용이
    // 동일하므로 dirty 는 꺼져야 한다. 플래그 방식은 이때 거짓말을 하고, 없어도 될 미저장
    // 경고 팝업을 띄운다. 그래서 매번 내용을 비교한다(7/4칸·10여장 규모라 비용은 무의미).
    //
    // Unity 타입 의존 0 — 입력은 string / IReadOnlyList<string> 과 프리셋 POCO 뿐이라
    // EditMode 에서 직접 구동된다(제약 10).
    public static class PresetDiff
    {
        // 슬롯 기반 프리셋(스쿼드): 이름 + 유닛 7칸 + 스톤 4칸을 **위치까지** 비교한다.
        // 슬롯 위치는 의미를 갖는다(헤더 스트립 표시 순서) — 집합 비교가 아니다.
        public static bool IsSquadDirty(
            string workingName, IReadOnlyList<string> workingUnits, IReadOnlyList<string> workingStones,
            SquadPreset stored)
        {
            if (stored == null)
                return !IsNameEmpty(workingName)
                    || !AllSlotsEmpty(workingUnits) || !AllSlotsEmpty(workingStones);

            if (!SameText(workingName, stored.name)) return true;
            if (SlotsDiffer(workingUnits, stored.unitIds, SquadPreset.SlotCount)) return true;
            if (SlotsDiffer(workingStones, stored.stoneIds, SquadPreset.StoneSlotCount)) return true;
            return false;
        }

        // 가변 길이 프리셋(드림캐쳐): 이름 + 카드 순서열. 고정 칸이 없으므로 빈 항목을
        // 걷어낸 뒤(DreamcatcherPreset.NormalizeCards 와 같은 규율) 순서열을 비교한다.
        // 같은 카드 집합이라도 순서가 다르면 dirty 다 — 덱 순서는 손패 사이클 순서다.
        public static bool IsDeckDirty(
            string workingName, IReadOnlyList<string> workingCards, DreamcatcherPreset stored)
        {
            if (stored == null)
                return !IsNameEmpty(workingName) || NonEmptyCount(workingCards) > 0;

            if (!SameText(workingName, stored.name)) return true;

            var storedCards = stored.cardIds;
            int wi = 0, si = 0;
            while (true)
            {
                string w = NextNonEmpty(workingCards, ref wi);
                string s = NextNonEmpty(storedCards, ref si);
                if (w == null && s == null) return false;   // 둘 다 소진 = 동일
                if (w == null || s == null) return true;    // 길이 차이
                if (w != s) return true;
            }
        }

        // ---- helpers ------------------------------------------------------

        // 빈칸의 두 표현을 같은 값으로 본다. 작업본은 ""(NormalizeSlots 규약), 저장본은
        // JSON 왕복 후 null 일 수 있어(JsonUtility 가 부분 JSON 에서 컬렉션 항목을 null 로
        // 남기는 경로) 정규화 없이는 로드 직후 무조건 dirty 로 뜬다.
        private static string Norm(string s) => s ?? "";

        private static bool SameText(string a, string b) => Norm(a) == Norm(b);

        private static bool IsNameEmpty(string name) => string.IsNullOrEmpty(name);

        // 인덱스를 벗어난 칸은 ""(빈칸)으로 취급 — 길이가 달라도 내용이 같으면 동일하다.
        private static string SlotAt(IReadOnlyList<string> list, int i)
            => (list != null && i < list.Count) ? Norm(list[i]) : "";

        private static bool SlotsDiffer(IReadOnlyList<string> working, IReadOnlyList<string> stored, int count)
        {
            for (int i = 0; i < count; i++)
                if (SlotAt(working, i) != SlotAt(stored, i)) return true;
            return false;
        }

        private static bool AllSlotsEmpty(IReadOnlyList<string> list)
        {
            if (list == null) return true;
            for (int i = 0; i < list.Count; i++)
                if (!string.IsNullOrEmpty(list[i])) return false;
            return true;
        }

        private static int NonEmptyCount(IReadOnlyList<string> list)
        {
            if (list == null) return 0;
            int n = 0;
            for (int i = 0; i < list.Count; i++)
                if (!string.IsNullOrEmpty(list[i])) n++;
            return n;
        }

        // cursor 를 전진시키며 다음 비어있지 않은 항목을 돌려준다. 소진되면 null.
        private static string NextNonEmpty(IReadOnlyList<string> list, ref int cursor)
        {
            if (list == null) return null;
            while (cursor < list.Count)
            {
                var v = list[cursor++];
                if (!string.IsNullOrEmpty(v)) return v;
            }
            return null;
        }
    }
}
