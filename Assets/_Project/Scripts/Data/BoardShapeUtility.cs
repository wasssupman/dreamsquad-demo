namespace Wassup.Data
{
    public static class BoardShapeUtility
    {
        public static BoardShapeType GetShapeForMask(int mask)
        {
            mask &= 15;

            bool n = (mask & 1) != 0;
            bool e = (mask & 2) != 0;
            bool s = (mask & 4) != 0;
            bool w = (mask & 8) != 0;
            int count = (n ? 1 : 0) + (e ? 1 : 0) + (s ? 1 : 0) + (w ? 1 : 0);

            if (count == 0) return BoardShapeType.Isolated;
            if (count == 4) return BoardShapeType.Cross;

            if (count == 1)
            {
                if (n) return BoardShapeType.EndN;
                if (e) return BoardShapeType.EndE;
                if (s) return BoardShapeType.EndS;
                return BoardShapeType.EndW;
            }

            if (count == 2)
            {
                if (n && s) return BoardShapeType.StraightNS;
                if (e && w) return BoardShapeType.StraightEW;
                if (n && e) return BoardShapeType.OuterCornerNE;
                if (n && w) return BoardShapeType.OuterCornerNW;
                if (s && e) return BoardShapeType.OuterCornerSE;
                return BoardShapeType.OuterCornerSW;
            }

            if (!n) return BoardShapeType.TJunctionS;
            if (!e) return BoardShapeType.TJunctionW;
            if (!s) return BoardShapeType.TJunctionN;
            return BoardShapeType.TJunctionE;
        }

        public static byte GetInnerCornerMask(byte mask8)
        {
            bool n = (mask8 & 1) != 0;
            bool e = (mask8 & 2) != 0;
            bool s = (mask8 & 4) != 0;
            bool w = (mask8 & 8) != 0;
            bool ne = (mask8 & 16) != 0;
            bool se = (mask8 & 32) != 0;
            bool sw = (mask8 & 64) != 0;
            bool nw = (mask8 & 128) != 0;

            byte inner = 0;
            if (n && e && !ne) inner |= 1;
            if (s && e && !se) inner |= 2;
            if (s && w && !sw) inner |= 4;
            if (n && w && !nw) inner |= 8;
            return inner;
        }
    }
}
