using UnityEngine;

namespace Wassup.Presentation
{
    // sprite-flipbook-player unit 5 — NxM 균일 격자 시트의 셀 사각형을 정하는 순수 계산.
    //
    // FlipbookFrameOrder 와 같은 이유로 런타임 어셈블리에 있다 — 오소링(에디터)에서만 호출되지만
    // Wassup.Tests.EditMode 가 참조할 수 있는 위치가 여기뿐이다. 순수 정수 연산이라 런타임 비용 0.
    //
    // 이 계산의 함정은 좌표계다: 사람은 시트를 왼쪽 **위**부터 읽는데 텍스처 원점은 왼쪽 **아래**다.
    // 변환을 호출측에 두면 프레임이 행 단위로 뒤집힌 채 각 행 안에서는 순서가 맞아,
    // 애니메이션이 대충 돌긴 하는 형태로 조용히 어긋난다(컴파일도 경고도 통과한다).
    public static class FlipbookSheetGrid
    {
        public static int CellCount(int columns, int rows) =>
            columns <= 0 || rows <= 0 ? 0 : columns * rows;

        // 셀 크기 = 텍스처를 정확히 나눈 몫. 나머지가 남으면 **실패**다.
        // 내림으로 진행하면 마지막 열·행에 잘린 띠가 남고 그 띠가 프레임에 섞여 들어간다 —
        // 경고로 넘길 종류가 아니라 입력이 격자가 아니라는 뜻이다.
        public static bool TryCellSize(int textureWidth, int textureHeight, int columns, int rows,
            out int cellWidth, out int cellHeight)
        {
            cellWidth = 0;
            cellHeight = 0;

            if (columns <= 0 || rows <= 0) return false;
            if (textureWidth <= 0 || textureHeight <= 0) return false;
            if (textureWidth % columns != 0 || textureHeight % rows != 0) return false;

            cellWidth = textureWidth / columns;
            cellHeight = textureHeight / rows;
            return true;
        }

        // index 번째 셀의 텍스처 좌표 사각형. 순서는 Unity 슬라이서와 같은 "왼→오, 위→아래".
        // 범위 밖 index 는 크기 0 사각형 — 호출측은 CellCount 로 순회하므로 여기 닿으면 호출측 버그다.
        // 예외를 던지지 않는 이유: 오소링 버튼 한 번이 예외로 끊기면 이미 바꾼 임포터 상태가 어중간하게 남는다.
        public static RectInt CellRect(int index, int columns, int rows, int cellWidth, int cellHeight)
        {
            if (index < 0 || index >= CellCount(columns, rows)) return default;
            if (cellWidth <= 0 || cellHeight <= 0) return default;

            int column = index % columns;
            int row = index / columns;

            // 텍스처 원점이 왼쪽 아래라 행 번호를 뒤집는다. 첫 프레임(row 0)이 시트의 맨 윗줄이다.
            int y = (rows - 1 - row) * cellHeight;
            return new RectInt(column * cellWidth, y, cellWidth, cellHeight);
        }
    }
}
