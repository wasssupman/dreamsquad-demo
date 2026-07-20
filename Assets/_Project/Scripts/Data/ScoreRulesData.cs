using UnityEngine;

namespace Wassup.Data
{
    // battle-score-formula unit 0 — 최종 점수 산식의 배점 상수.
    //
    // 산식은 예산 소모 모델이다: 시간·스트레스 예산을 만점으로 두고 소모한 만큼
    // 깎은 뒤, 실제 처치분(AttackUnitData.killScore 합)을 더한다.
    //
    //   시간점수    = 남은시간ms × timeScorePerSecond / 1000   (패배 시 0)
    //   스트레스점수 = (한계 − 누적) × stressScorePerPoint
    //
    // 계산 자체는 ScoreMath 순수 함수가 하고, 이 SO 는 값만 공급한다(제약 6).
    [CreateAssetMenu(fileName = "ScoreRules", menuName = "Wassup/Score Rules", order = 20)]
    public class ScoreRulesData : ScriptableObject
    {
        // 180초 × 100 = 18,000 이 시간 예산의 만점이다. 다만 당기기 없이는 마지막
        // 스폰이 163.4초라 실질 상한이 ~1,660 이고, 압축할수록 커진다(README 참조).
        //
        // Range 상한은 int 오버플로 방어다: 최악 180,000ms × 이 값이 int 를 넘으면
        // 안 된다(임계 11,930). ScoreMath 가 long 을 안 쓰는 근거이기도 하다.
        [Tooltip("남은 1초당 점수. 180초 × 이 값 = 시간 예산 만점 (100 → 18,000).")]
        [Range(1, 10000)]
        public int timeScorePerSecond = 100;

        // 한계 × 이 값 = 스트레스 예산. 한계는 deck.defeatGoalReachedCount 원본값이므로
        // **둘은 곱해서 예산이 된다 — 짝으로 움직여야 한다.**
        // 한계를 바꾸면 이 값도 같이 조정해 예산 총량을 유지할 것.
        [Tooltip("남은 스트레스 1점당 점수. 유출 한계 × 이 값 = 스트레스 예산 (10 × 900 = 9,000).")]
        [Range(1, 10000)]
        public int stressScorePerPoint = 900;
    }
}
