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

        // 2026-08-17 — 신규 프로필이 받는 **시작 스쿼드**(저작). 비어 있으면 예전처럼
        // `units` 앞에서부터 슬롯 수만큼 집어온다.
        //
        // 이 필드를 만든 이유: 시작 스쿼드가 「카탈로그 배열 순서」라는 **암묵 규칙**이었다.
        // 유닛을 추가하거나 순서를 바꾸면 신규 유저의 첫 편성이 조용히 달라지는데, 그 둘은
        // 아무 관계가 없다(카탈로그 순서는 목록 UI 의 순서다). 어느 유닛을 줄지는 코드가
        // 아니라 저작이 정한다 — `EnsureDefaultStones` 의 authored 배열과 같은 정책이다.
        //
        // 「비어 있을 때만 시드」 규칙은 그대로다. 플레이어가 채운 스쿼드는 덮지 않는다.
        public DefenderUnitData[] defaultSquadUnits;

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
