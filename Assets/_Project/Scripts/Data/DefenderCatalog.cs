using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Data
{
    // outgame-scene-and-flow Unit 0 — id -> DefenderUnitData resolution for
    // save/load. Authoritative list of defender units a profile can reference.
    [CreateAssetMenu(fileName = "DefenderCatalog", menuName = "Wassup/DefenderCatalog", order = 12)]
    public class DefenderCatalog : ScriptableObject
    {
        public DefenderUnitData[] units;

        public DefenderUnitData ById(string id)
        {
            if (string.IsNullOrEmpty(id) || units == null) return null;
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] != null && units[i].id == id) return units[i];
            }
            return null;
        }

        // dreamcatcher-attach-requirement unit 5 — 카드 문안의 "{유닛명} 전용" 해석기.
        // 문안 소비처 4곳이 같은 람다를 반복하지 않게 카탈로그가 제공한다(4 호출처 =
        // 추출 기준 충족). 없는 id 는 null → 포매터가 id 문자열로 폴백한다.
        public string DisplayNameOf(string id)
        {
            var unit = ById(id);
            if (unit == null) return null;
            return string.IsNullOrEmpty(unit.displayName) ? unit.id : unit.displayName;
        }

        public IEnumerable<string> AllIds()
        {
            if (units == null) yield break;
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] != null && !string.IsNullOrEmpty(units[i].id))
                    yield return units[i].id;
            }
        }
    }
}
