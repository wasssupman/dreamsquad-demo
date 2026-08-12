namespace Wassup.Presentation
{
    // summon-patrol-defender unit 10 — idle 변형 선택. 아키텍처 중립이라 순수 함수로 둔다
    // (제약 10 판정 기준: 분기가 있고 회귀 테스트 가치가 있다).
    //
    // 난수를 이 안에서 뽑지 않고 **roll 을 받는다**. 호출측이 UnityEngine.Random 을 주므로
    // 테스트는 결정론적으로 모든 분기를 겨눌 수 있고, sim 난수(waveSeed)와 섞일 여지도 없다.
    public static class UnitAnimationChoice
    {
        // 다음 idle 변형 인덱스. 직전과 같은 것을 연속으로 뽑지 않는다 —
        // 3종을 저작해 두고 같은 게 두 번 이어 나오면 "랜덤"이 아니라 "안 바뀜"으로 읽힌다.
        //
        // count <= 0  → -1 (변형 없음: 호출측이 단일 idle 로 폴백)
        // count == 1  → 0  (선택지가 하나면 연속 회피가 불가능하다 — 그대로 반복)
        // current 가 범위 밖(-1 = 최초 진입)이면 회피 없이 그냥 뽑는다.
        //
        // roll 은 [0,1) 을 기대한다. 1.0 이 들어와도 인덱스가 넘치지 않게 클램프한다
        // (Random.value 는 1.0 을 포함한다 — 이 한 줄이 없으면 드물게 IndexOutOfRange).
        public static int ChooseNext(int count, int current, float roll)
        {
            if (count <= 0) return -1;
            if (count == 1) return 0;

            if (roll < 0f) roll = 0f;

            if (current < 0 || current >= count)
            {
                int any = (int)(roll * count);
                return any >= count ? count - 1 : any;
            }

            // 직전 것을 후보에서 뺀 (count-1) 중에서 뽑고, current 이상이면 한 칸 밀어
            // current 를 건너뛴다. 남은 후보에 균등하다.
            int pick = (int)(roll * (count - 1));
            if (pick >= count - 1) pick = count - 2;
            return pick >= current ? pick + 1 : pick;
        }
    }
}
