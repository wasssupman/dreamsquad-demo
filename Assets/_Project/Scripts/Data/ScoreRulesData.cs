using UnityEngine;

namespace Wassup.Data
{
    // three-minute-survival unit 3 — **배점 상수는 은퇴했다.**
    //
    // 구 산식은 예산 소모 모델이었다: 시간(초당 100)·스트레스(점당 900) 예산을 만점으로 두고
    // 소모분을 깎은 뒤 처치분을 더했다. 지금은 처치가 유일한 점수원이고 값은 적 SO 의
    // `killScore` 에서 직접 나오므로(제약 6) 이 SO 가 공급할 값이 남지 않았다.
    //
    // 타입을 지우지 않는 이유: `ScoreRules.asset` 이 디스크에 있고 BattleBridge 씬 참조가
    // 그것을 물고 있다. 타입을 지우면 씬·에셋이 missing script 로 깨진다(Unity 없이 정리 불가).
    // 필드 없는 빈 SO 로 남겨 두고, 실제 삭제는 에디터에서 에셋·씬 참조를 함께 정리할 때 한다.
    [CreateAssetMenu(fileName = "ScoreRules", menuName = "Wassup/Score Rules", order = 20)]
    public class ScoreRulesData : ScriptableObject
    {
    }
}
