using System;
using UnityEngine;

namespace Wassup.Data
{
    // wave-concept-blocks unit 0 — 슬롯의 고도 축.
    //
    // PlacementLayer 비트마스크를 그대로 쓰지 않는다: 마스크는 `Ground|Air` 같은 조합을
    // 허용하는데 그 의미(둘 다 허용? 둘 다 필요?)가 정의되지 않는다. 2값 enum 이 의도를
    // 못 박는다. 판정은 AttackUnitData.EffectiveTraversalLayers 를 읽는다.
    //
    // `Any` 를 두지 않는 것이 계약이다 — 기본이 Ground 여야 「평소」가 비행을 뽑아
    // 웨이브 1~3 에 «대공 없이 막을 수 없는 적»을 내놓는 사고가 구조적으로 막힌다.
    // 비행은 altitude=Air 를 명시한 컨셉만 받는다.
    public enum SlotAltitude
    {
        Ground = 0,
        Air = 1,
    }

    // wave-concept-blocks unit 0 — 한 스웜(= 한 WaveSpawnGroup 이 될 것)의 저작 명세.
    // 필드가 3개인 것은 의도다. share(비중)·triggerOffsetSec(시차)·minLaneCount·cohesion(동행)은
    // rev 2 에서 걷어냈다 — 각각 미사용 / 저작 뭉침으로 불필요 / slots 파생 / 코어 로직 침범.
    [Serializable]
    public class WaveConceptSlot
    {
        [Tooltip("같은 값 = 같은 lane. -1 = 무지정(전 lane 분산). 실제 lane 인덱스는 waveSeed 가 고른다.")]
        public int laneGroup = -1;

        [Tooltip("이 슬롯이 뽑을 적의 성질. None = 무필터. 속도 폭이 좁은 class 를 골라야 스웜이 뭉친다.")]
        public EnemyClass classFilter = EnemyClass.None;

        [Tooltip("지상/공중. 기본 Ground — 비행은 명시적으로 Air 를 고른 슬롯만 받는다.")]
        public SlotAltitude altitude = SlotAltitude.Ground;
    }

    // wave-concept-blocks unit 0 — 웨이브 «블록»(기본 3웨이브)의 편성 규칙.
    //
    // 컨셉은 웨이브가 아니라 **블록**의 속성이다. 블록 안에서 컨셉과 lane 배정이 고정되고
    // 수량만 ExponentialWaveTotal 곡선을 따라 오른다 — «배우고 → 대응하고 → 겨우 버티고»
    // 다음 컨셉이 온다. 웨이브당 12~18초라 매 웨이브 바뀌면 반응할 창이 없다.
    [CreateAssetMenu(fileName = "WaveConcept", menuName = "Wassup/WaveConcept", order = 12)]
    public class WaveConceptData : ScriptableObject
    {
        public string id = "concept";

        [Tooltip("브리핑 스트립·다음웨이브 도크에 그대로 표시되는 라벨. 코드에 문자열을 두지 않는다.")]
        public string displayName = "컨셉";

        [Tooltip("룰렛 가중치. 0 이하면 후보에서 제외.")]
        [Min(0f)] public float weight = 1f;

        [Tooltip("등장 게이트. **블록의 첫 웨이브 번호** 기준으로 판정한다.")]
        [Min(1)] public int minWaveNumber = 1;

        // 수량 배율. ExponentialWaveTotal 은 **개체 수**를 내므로 성질을 통일하면 난이도가
        // 성질에 끌려간다(Runner 20hp × 19 = 380 vs Tanker 100hp × 19 = 1,900 — 5배).
        [Tooltip("수량 배율. 곡선 총량에 곱한다. 성질이 단단할수록 낮게.")]
        [Min(0.05f)] public float countMul = 1f;

        public WaveConceptSlot[] slots = Array.Empty<WaveConceptSlot>();

        // 이 컨셉이 요구하는 lane 수 = distinct laneGroup(>=0) 개수.
        //
        // **파생값이지 저작값이 아니다.** 별도 minLaneCount 필드를 두면 저작값과 파생값이
        // 갈릴 수 있고, 갈리면 «저작은 2를 요구하는데 슬롯은 3 lane 을 쓰는» 컨셉이 게이트를
        // 통과한다. 계산은 슬롯이 최대 몇 개 안 되므로 O(n²) 로 충분하다.
        public int RequiredLaneCount
        {
            get
            {
                if (slots == null) return 0;
                int count = 0;
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    if (slot == null || slot.laneGroup < 0) continue;
                    bool seen = false;
                    for (int j = 0; j < i; j++)
                    {
                        var prev = slots[j];
                        if (prev != null && prev.laneGroup == slot.laneGroup) { seen = true; break; }
                    }
                    if (!seen) count++;
                }
                return count;
            }
        }

        // 실질 슬롯 수(null 제외). 수량 분배의 하한이 이 값이다.
        public int EffectiveSlotCount
        {
            get
            {
                if (slots == null) return 0;
                int count = 0;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] != null) count++;
                return count;
            }
        }
    }
}
