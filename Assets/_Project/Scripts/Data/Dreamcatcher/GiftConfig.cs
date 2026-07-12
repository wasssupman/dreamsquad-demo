using UnityEngine;

namespace Wassup.Data
{
    // gift-phase unit 0 — 선물 페이즈(배치 직전) 이벤트/연출 파라미터.
    // "루시드의 선물"(공용 스킬 Active 2장) vs "림의 선물"(무의식 2장) 이벤트를
    // 가중 랜덤으로 고르고, 발라트로식 셔플 연출의 각 구간 타이밍을 담는다.
    // 하드코딩 금지 — 모든 수치는 이 SO 에서. GiftPhaseView / DreamcatcherHandController 가 소비.
    public enum GiftKind { Lucid, Rim }

    [CreateAssetMenu(fileName = "GiftConfig", menuName = "Wassup/GiftConfig", order = 21)]
    public class GiftConfig : ScriptableObject
    {
        [Header("Event weights (Lucid vs Rim, 가중 랜덤)")]
        [Min(0f)] public float lucidWeight = 1f;
        [Min(0f)] public float rimWeight = 1f;

        [Header("Rim gift")]
        [Tooltip("림의 선물이 지급하는 무의식 카드 수")]
        [Min(0)] public int rimGiftCount = 2;

        [Header("Sequence timings (seconds, unscaled)")]
        public float introTextSec = 1.0f;
        public float baseCardsInSec = 0.5f;
        public float giftAppendDelaySec = 1.0f;
        public float giftAppendSec = 0.5f;
        public float shuffleSec = 2.0f;
        public float holdSec = 2.0f;
        public float flyOutSec = 0.6f;

        [Header("Test mode")]
        [Tooltip("테스트 모드에서 연출을 건너뛰고 즉시 배치로")]
        public bool fastForwardInTestMode = true;
    }
}
