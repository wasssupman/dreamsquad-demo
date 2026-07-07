using System;
using UnityEngine;

namespace Wassup.Data
{
    // unit-parts-appearance 0 — 파츠 조합 외형의 슬롯 단위 틴트 항목.
    // 최종 색 = skeleton 색(R/G/B/A: 피격 틴트·사망 페이드) × slot 색 (PMA 곱연산 독립).
    [Serializable]
    public struct SpineSlotColor
    {
        public string slotName;
        public Color color;
    }
}
