using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Presentation;

// sprite-flipbook-player unit 3 — 시트 프레임 정렬 회귀 테스트.
// spec 이 지목한 실패 모드: 사전순 정렬이면 _1, _10, _11, _2 … 로 프레임이 조용히 뒤섞인다.
// 컴파일도 통과하고 경고도 없이 애니메이션만 이상해지므로 여기서 못박는다.
public class FlipbookFrameOrderTests
{
    private static List<string> Sorted(params string[] names)
    {
        var list = new List<string>(names);
        list.Sort(FlipbookFrameOrder.Compare);
        return list;
    }

    [Test]
    public void Compare_TwoDigitIndices_DoNotSortLexicographically()
    {
        // 이 테스트가 unit 3 의 존재 이유다. 사전순이면 _1, _10, _11, _2 … 가 된다.
        var sorted = Sorted("Archer_10", "Archer_2", "Archer_1", "Archer_11", "Archer_20", "Archer_3");
        Assert.That(sorted, Is.EqualTo(new[]
        {
            "Archer_1", "Archer_2", "Archer_3", "Archer_10", "Archer_11", "Archer_20"
        }));
    }

    [Test]
    public void Compare_ZeroPaddedNames_SortByNumericValue()
    {
        // 이 프로젝트의 기존 컷씬 에셋이 쓰는 형식(Archer_001 … Archer_031).
        var sorted = Sorted("Archer_031", "Archer_002", "Archer_010", "Archer_001");
        Assert.That(sorted, Is.EqualTo(new[] { "Archer_001", "Archer_002", "Archer_010", "Archer_031" }));
    }

    [Test]
    public void Compare_FullSheetOrdering_IsMonotonic()
    {
        // 0..12 을 뒤섞어 넣어도 숫자 순서가 복원되는지(두 자리 진입 경계 포함).
        var shuffled = new List<string>();
        foreach (int i in new[] { 7, 0, 12, 3, 10, 1, 9, 2, 11, 5, 4, 8, 6 })
            shuffled.Add($"sheet_{i}");
        shuffled.Sort(FlipbookFrameOrder.Compare);

        for (int i = 0; i < shuffled.Count; i++)
            Assert.That(shuffled[i], Is.EqualTo($"sheet_{i}"), $"위치 {i} 의 프레임이 어긋났다.");
    }

    [Test]
    public void Compare_NamesWithoutNumericSuffix_GoLast()
    {
        var sorted = Sorted("frame_2", "intro", "frame_1");
        Assert.That(sorted, Is.EqualTo(new[] { "frame_1", "frame_2", "intro" }));
    }

    [Test]
    public void Compare_IsAStrictWeakOrdering_SoSortIsDeterministic()
    {
        // 동률(접미사 없는 이름들)에서 ordinal 타이브레이크가 없으면 정렬 결과가 흔들린다.
        var sorted = Sorted("zulu", "alpha", "mike");
        Assert.That(sorted, Is.EqualTo(new[] { "alpha", "mike", "zulu" }));
    }

    // --- TrailingIndex 경계 ---

    [Test]
    public void TrailingIndex_ParsesTrailingDigitsOnly()
    {
        Assert.That(FlipbookFrameOrder.TrailingIndex("Archer_007"), Is.EqualTo(7));
        Assert.That(FlipbookFrameOrder.TrailingIndex("42"), Is.EqualTo(42));
        Assert.That(FlipbookFrameOrder.TrailingIndex("v2_frame_3"), Is.EqualTo(3));
    }

    [Test]
    public void TrailingIndex_NoDigits_IsMaxValue()
    {
        Assert.That(FlipbookFrameOrder.TrailingIndex("intro"), Is.EqualTo(int.MaxValue));
        Assert.That(FlipbookFrameOrder.TrailingIndex("frame_1a"), Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void TrailingIndex_NullOrEmpty_IsMaxValueWithoutThrowing()
    {
        Assert.That(FlipbookFrameOrder.TrailingIndex(null), Is.EqualTo(int.MaxValue));
        Assert.That(FlipbookFrameOrder.TrailingIndex(string.Empty), Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void TrailingIndex_OverflowingNumber_IsMaxValueWithoutThrowing()
    {
        // int 범위를 넘는 접미사에서 예외로 오소링이 끊기지 않아야 한다.
        Assert.That(FlipbookFrameOrder.TrailingIndex("frame_99999999999999"), Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void Compare_SortWithNullNames_DoesNotThrow()
    {
        var list = new List<string> { "frame_2", null, "frame_1" };
        Assert.DoesNotThrow(() => list.Sort(FlipbookFrameOrder.Compare));
    }
}
