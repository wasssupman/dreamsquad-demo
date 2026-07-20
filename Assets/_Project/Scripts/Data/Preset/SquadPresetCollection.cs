using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Data
{
    // loadout-preset-page unit 0 — 기획자가 authoring 하는 프리셋 집합. 개별 SquadPreset 은
    // 스쿼드 7유닛 + 드림캐쳐 10카드를 SO 직접 참조로 담는다(id 미저장; 적용 시 .id 를 읽는다).
    // 드림스톤은 범위 밖(유닛+드캐만). 순수 데이터 SO — MonoBehaviour/ECS 의존 없음.
    [Serializable]
    public class SquadPreset
    {
        public string presetName = "프리셋 1";   // 목록 아이템에 표시
        public DefenderUnitData[] units;          // ≤ SquadSave.SlotCount(7). SO 직접 참조
        public DreamcatcherCard[] cards;          // ≤ 라이브 deckSize(현 10). SO 직접 참조
    }

    [CreateAssetMenu(fileName = "SquadPresetCollection", menuName = "Wassup/SquadPresetCollection", order = 25)]
    public class SquadPresetCollection : ScriptableObject
    {
        public List<SquadPreset> presets = new List<SquadPreset>();
    }
}
