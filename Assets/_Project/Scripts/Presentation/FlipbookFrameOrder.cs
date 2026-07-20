namespace Wassup.Presentation
{
    // sprite-flipbook-player unit 3 — 시트 서브스프라이트의 프레임 순서를 정하는 순수 비교.
    //
    // 오소링(에디터)에서만 호출되지만 런타임 어셈블리에 둔다 — EditMode 테스트 어셈블리가
    // 참조할 수 있는 유일한 위치이기 때문. spec 이 이 정렬을 unit 3 의 핵심이자 회귀 방지
    // 대상으로 지목했으므로(사전순이면 프레임이 조용히 뒤섞인다) 테스트 가능성이 배치를 결정했다.
    // 순수 문자열→정수라 런타임 의존·비용이 없다.
    public static class FlipbookFrameOrder
    {
        // 이름 끝의 연속 숫자를 정수로. 접미사가 없거나 파싱 불가(자릿수 초과 등)면 int.MaxValue 로 뒤에 보낸다.
        public static int TrailingIndex(string name)
        {
            if (string.IsNullOrEmpty(name)) return int.MaxValue;

            int start = name.Length;
            while (start > 0 && char.IsDigit(name[start - 1])) start--;
            if (start == name.Length) return int.MaxValue;

            return int.TryParse(name.Substring(start), out int value) ? value : int.MaxValue;
        }

        // 프레임 순서 비교. 사전순으로 두면 _1, _10, _11, _2 … 가 되어 애니메이션만 조용히 어긋난다
        // (컴파일도 통과하고 경고도 없다). 숫자 접미사를 정수로 비교하고, 동률은 ordinal 로 안정화한다.
        public static int Compare(string a, string b)
        {
            int indexA = TrailingIndex(a);
            int indexB = TrailingIndex(b);
            if (indexA != indexB) return indexA.CompareTo(indexB);

            return string.CompareOrdinal(a, b);
        }
    }
}
