using UnityEngine;

namespace Wassup.UI
{
    // dreamcatcher-orb-dock unit 8 — 각성치를 «드림캐쳐 몇 회분» 으로 읽는 순수 계산.
    //
    // 항아리 독은 회차를 선으로 그리지 않는다(사용자 결정: 칸 구분 없음). 대신 회차가
    // 오르는 그 순간에만 짧게 터지므로, «언제 올랐나» 를 판정할 단위가 필요하다.
    // 화폐는 여전히 점(0~100)이고 카드 비용도 점이다 — 여기서 회차는 연출 트리거의
    // 기준일 뿐 화면 숫자를 대체하지 않는다.
    //
    // 값을 순수하게 결정하고 소비처(뷰)가 해석한다(TRD 아키텍처 중립 로직 규칙).
    public static class AwakeningCharge
    {
        // 한 회분 = 가장 싼 카드 코스트. 0/음수 코스트는 «비용 없음» 이라 무시한다.
        // 셋 다 유효하지 않으면 0 = 회 개념이 성립하지 않음(호출처는 연출을 끈다).
        public static int UnitCost(int costSquad, int costUnit, int costActive)
        {
            int min = 0;
            if (costSquad > 0) min = costSquad;
            if (costUnit > 0 && (min == 0 || costUnit < min)) min = costUnit;
            if (costActive > 0 && (min == 0 || costActive < min)) min = costActive;
            return min;
        }

        // 지금 보유량으로 낼 수 있는 회수. unitCost 가 0 이면 판정 불가 → 0.
        public static int CountOf(int gauge, int unitCost)
        {
            if (unitCost <= 0) return 0;
            return Mathf.Max(0, gauge) / unitCost;
        }
    }
}
