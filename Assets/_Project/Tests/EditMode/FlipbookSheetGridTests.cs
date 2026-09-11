using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Presentation;

// sprite-flipbook-player unit 5 — NxM 격자 슬라이스 회귀 테스트.
// spec 이 지목한 실패 모드 2개를 못박는다:
//   (1) 좌표계 뒤집힘 — 사람은 왼쪽 위부터 읽고 텍스처 원점은 왼쪽 아래다. 틀리면 행 단위로
//       뒤집힌 채 각 행 안에서는 순서가 맞아 "대충 도는" 형태로 조용히 어긋난다.
//   (2) 나머지 삼키기 — 나누어떨어지지 않는 격자를 내림으로 진행하면 잘린 띠가 프레임에 섞인다.
public class FlipbookSheetGridTests
{
    [Test]
    public void TryCellSize_DividesEvenly_ReturnsQuotient()
    {
        Assert.IsTrue(FlipbookSheetGrid.TryCellSize(1024, 1024, 4, 4, out int w, out int h));
        Assert.AreEqual(256, w);
        Assert.AreEqual(256, h);
    }

    [Test]
    public void TryCellSize_NonSquareGrid_AllowsNonSquareCells()
    {
        // 가로 6칸 · 세로 2칸. 셀이 정사각형일 필요는 없다.
        Assert.IsTrue(FlipbookSheetGrid.TryCellSize(960, 240, 6, 2, out int w, out int h));
        Assert.AreEqual(160, w);
        Assert.AreEqual(120, h);
    }

    [Test]
    public void TryCellSize_NotDivisible_FailsInsteadOfFlooring()
    {
        // 이 테스트가 unit 5 의 존재 이유 절반이다. 1024/5 = 204.8 을 204 로 내리면
        // 마지막 열에 4px 띠가 남고 그게 프레임에 섞여 들어간다.
        Assert.IsFalse(FlipbookSheetGrid.TryCellSize(1024, 1024, 5, 4, out int w, out int h));
        Assert.AreEqual(0, w);
        Assert.AreEqual(0, h);
    }

    [Test]
    public void TryCellSize_InvalidInput_Fails()
    {
        Assert.IsFalse(FlipbookSheetGrid.TryCellSize(1024, 1024, 0, 4, out _, out _));
        Assert.IsFalse(FlipbookSheetGrid.TryCellSize(1024, 1024, 4, -1, out _, out _));
        Assert.IsFalse(FlipbookSheetGrid.TryCellSize(0, 1024, 4, 4, out _, out _));
        Assert.IsFalse(FlipbookSheetGrid.TryCellSize(1024, 0, 4, 4, out _, out _));
    }

    [Test]
    public void CellCount_InvalidGrid_IsZero()
    {
        Assert.AreEqual(12, FlipbookSheetGrid.CellCount(4, 3));
        Assert.AreEqual(0, FlipbookSheetGrid.CellCount(0, 3));
        Assert.AreEqual(0, FlipbookSheetGrid.CellCount(4, -2));
    }

    [Test]
    public void CellRect_FirstFrame_IsTopLeftOfSheet()
    {
        // 텍스처 원점은 왼쪽 아래다. 맨 윗줄의 y 는 (rows-1)*cellHeight — 0 이 아니다.
        var first = FlipbookSheetGrid.CellRect(0, 4, 3, 64, 64);
        Assert.AreEqual(new RectInt(0, 128, 64, 64), first);
    }

    [Test]
    public void CellRect_LastFrame_IsBottomRightOfSheet()
    {
        var last = FlipbookSheetGrid.CellRect(11, 4, 3, 64, 64);
        Assert.AreEqual(new RectInt(192, 0, 64, 64), last);
    }

    [Test]
    public void CellRect_AdvancesLeftToRightThenDown()
    {
        // 첫 행 4칸은 y 가 같고 x 만 증가 → 다음 프레임에서 x 가 0 으로 돌아가고 y 가 한 칸 내려간다.
        var row0 = new List<RectInt>();
        for (int i = 0; i < 4; i++) row0.Add(FlipbookSheetGrid.CellRect(i, 4, 3, 64, 64));

        Assert.That(row0.TrueForAll(r => r.y == 128));
        Assert.AreEqual(new[] { 0, 64, 128, 192 }, row0.ConvertAll(r => r.x).ToArray());

        var row1First = FlipbookSheetGrid.CellRect(4, 4, 3, 64, 64);
        Assert.AreEqual(0, row1First.x);
        Assert.AreEqual(64, row1First.y);
    }

    [Test]
    public void CellRect_SingleRow_StaysAtOrigin()
    {
        var only = FlipbookSheetGrid.CellRect(2, 5, 1, 32, 48);
        Assert.AreEqual(new RectInt(64, 0, 32, 48), only);
    }

    [Test]
    public void CellRect_OutOfRangeOrInvalidCell_IsEmpty()
    {
        Assert.AreEqual(default(RectInt), FlipbookSheetGrid.CellRect(-1, 4, 3, 64, 64));
        Assert.AreEqual(default(RectInt), FlipbookSheetGrid.CellRect(12, 4, 3, 64, 64));
        Assert.AreEqual(default(RectInt), FlipbookSheetGrid.CellRect(0, 4, 3, 0, 64));
    }

    [Test]
    public void CellRects_TileTheSheetExactlyOnce()
    {
        // 겹침·틈·바깥 삐져나감을 한 번에 잡는다. 행 뒤집기 자체는 여기서 안 걸린다 —
        // 뒤집힌 격자도 시트를 정확히 덮기 때문(실측: 뒤집기 mutation 에서 이 테스트만 초록).
        // 그건 위 세 좌표 테스트의 몫이고, 여기는 셀 크기·개수 쪽 실수를 받는다.
        const int columns = 4, rows = 3, cell = 64;
        int width = columns * cell, height = rows * cell;

        var covered = new HashSet<Vector2Int>();
        for (int i = 0; i < FlipbookSheetGrid.CellCount(columns, rows); i++)
        {
            var rect = FlipbookSheetGrid.CellRect(i, columns, rows, cell, cell);
            Assert.That(rect.x >= 0 && rect.xMax <= width, $"프레임 {i} 이 가로 범위를 벗어났다: {rect}");
            Assert.That(rect.y >= 0 && rect.yMax <= height, $"프레임 {i} 이 세로 범위를 벗어났다: {rect}");
            Assert.IsTrue(covered.Add(new Vector2Int(rect.x, rect.y)), $"프레임 {i} 이 다른 프레임과 겹친다: {rect}");
        }

        Assert.AreEqual(columns * rows, covered.Count);
    }
}
