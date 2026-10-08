using UnityEngine;

namespace Somnia.Battle.Data.BattleView
{
    // battle-core-rebuild unit 5a — 마음(HUD) 연출 노브. 옛 브리지의 `heart*` 6 ·
    // `coreBurst*` 2 · `goalOverheadHeight` 의 새 주인.
    //
    // ⚠ **규칙은 하나도 없다.** 스트레스·붕괴·유출은 코어 `HeartMeter` 의 것이고, 여기 값은
    // 「그 숫자를 화면이 어떻게 뛰게 하나」뿐이다. 박동 속도가 판정을 바꾸는 날이 오면 그
    // 값은 이 자산이 아니라 정의표로 간다.
    [CreateAssetMenu(menuName = "Somnia/Battle/BattleView/Heart HUD Config", fileName = "HeartHudConfig")]
    public sealed class HeartHudConfig : ScriptableObject
    {
        [Header("박동")]
        [Tooltip("스트레스 0 에서의 분당 박동 수.")]
        [SerializeField, Min(20f)] private float restBpm = 52f;

        [Tooltip("스트레스 최대에서의 분당 박동 수.")]
        [SerializeField, Min(20f)] private float maxBpm = 168f;

        [Tooltip("박동 한 번의 수축 깊이(0 = 안 뛴다).")]
        [SerializeField, Range(0f, 0.9f)] private float beatDepth = 0.5f;

        [Header("바 펀치")]
        [Tooltip("스트레스가 오른 순간 바가 커지는 깊이.")]
        [SerializeField, Range(0f, 1f)] private float barPunchDepth = 0.35f;

        [Tooltip("펀치가 최대가 되는 한 번의 상승폭.")]
        [SerializeField, Min(0.1f)] private float barPunchFullRise = 4f;

        [Tooltip("펀치가 사그라드는 초당 비율.")]
        [SerializeField, Min(0.1f)] private float barPunchDecayPerSec = 3.2f;

        [Header("붕괴 순간")]
        [Tooltip("마음이 무너진 순간 화면을 붙드는 시간(초).")]
        [SerializeField, Min(0f)] private float coreBurstHoldSec = 1.25f;

        [Tooltip("그 동안의 배틀 도메인 시간 배율. 코어 dt 는 안 바뀐다 — 틱 발행률이다.")]
        [SerializeField, Range(0.05f, 1f)] private float coreBurstTimeScale = 0.3f;

        [Header("골 오버헤드")]
        [Tooltip("골 안정도 바가 뜨는 높이(월드).")]
        [SerializeField] private float goalOverheadHeight = 1.1f;

        public float RestBpm => restBpm;
        public float MaxBpm => maxBpm;
        public float BeatDepth => beatDepth;
        public float BarPunchDepth => barPunchDepth;
        public float BarPunchFullRise => barPunchFullRise;
        public float BarPunchDecayPerSec => barPunchDecayPerSec;
        public float CoreBurstHoldSec => coreBurstHoldSec;
        public float CoreBurstTimeScale => coreBurstTimeScale;
        public float GoalOverheadHeight => goalOverheadHeight;
    }
}
