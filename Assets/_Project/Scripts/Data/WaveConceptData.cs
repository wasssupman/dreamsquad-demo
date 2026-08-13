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

        // wave-pull-revival unit 2 — 블록 **가운데** 웨이브에 slots 위로 **끼어드는** 편성.
        //
        // 왜 필요한가: 같은 성격이 3연속이면 블록 안에서 «당길까»가 무차별 판단이 된다.
        // 두 번째나 세 번째나 오는 게 똑같으니 고민할 것이 없다. 가운데에 다른 것이 끼면
        // 「지금 당기면 벌떼 위에 저격수가 얹힌다」가 계산 대상이 된다.
        //
        // **교체가 아니라 삽입이다.** 교체하면 가운데 웨이브에 블록의 성격이 통째로 사라져
        // 「배우고 → 대응하고 → 겨우 버티고」의 압력 상승이 끊긴다. 수량 총량은 곡선이 그대로
        // 소유하므로, 슬롯이 하나 늘면 그 웨이브는 같은 총량을 더 잘게 나눠 받는다.
        //
        // **비어 있으면 변주 없음**(3웨이브 동일) — 5종 중 일부만 저작해도 무회귀다.
        // 첫 웨이브는 성격을 가르치는 자리라 순수해야 하고 마지막은 그 성격의 시험대라,
        // 가운데가 유일하게 비어 있는 칸이다. conceptHoldWaves < 3 이면 적용되지 않는다.
        //
        // ⚠ 이 슬롯의 `laneGroup` 은 **입구를 새로 뽑지 않는다.** 블록이 이미 확정한 배정을
        // 물려받는다(같은 laneGroup 이 본 편성에 있으면 그 입구, 없으면 본 편성의 입구를
        // 순서대로 재사용). 입구까지 흔들면 「이쪽을 보강하자」는 결정이 보상받지 못한다.
        [Tooltip("블록 가운데 웨이브에 추가로 끼는 편성(교체 아님). 비어 있으면 변주 없음. 입구는 블록 배정을 물려받는다.")]
        public WaveConceptSlot[] variantSlots = Array.Empty<WaveConceptSlot>();

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
