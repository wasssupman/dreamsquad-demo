using UnityEngine;

namespace Somnia.Battle.Data.BattleView
{
    // battle-core-rebuild unit 5a — 드림캐쳐 발동 연출 노브. 옛 브리지의
    // `dcProcImpactMinIntervalSec` 하나의 새 주인.
    //
    // 자산 하나에 필드 하나인 것이 어색해 보이지만, 「브리지에 얹혀 있던 값」을 기능별
    // 주인에게 돌려주는 것이 이 unit 의 일이다. 다른 주인의 자산에 끼워 넣으면 다음 사람이
    // 「드림캐쳐 값이 왜 캐릭터 설정에 있나」를 묻게 된다. 소비자는 unit 7 에서 선다.
    [CreateAssetMenu(menuName = "Somnia/Battle/BattleView/DC Visual Config", fileName = "DcVisualConfig")]
    public sealed class DcVisualConfig : ScriptableObject
    {
        [Tooltip("한 호스트의 발동 임팩트 최소 간격(초). 연타를 코얼레스한다 — 발동 사실 자체는 매번 알린다.")]
        [SerializeField, Min(0f)] private float procImpactMinIntervalSec = 0.25f;

        public float ProcImpactMinIntervalSec => procImpactMinIntervalSec;
    }
}
