using System.Collections.Generic;
using UnityEngine;
using Wassup.Data;

namespace Wassup.UI
{
    // tournament-history-deck-view unit 2 — deckInfo 페이로드의 id 목록을 화면이 바로
    // 그릴 수 있는 표시 항목으로 바꾸는 순수 변환. MonoBehaviour 밖에 두어 견고성
    // 계약(어떤 데이터가 와도 그린다)을 EditMode 로 고정한다.
    //
    // **미해석 id 를 버리지 않는 것이 핵심이다.** 남의 덱을 보는 화면이라, 내 빌드의
    // 카탈로그가 모르는 id(리네임·삭제·신규 유닛)가 정상적으로 들어온다. 버리면 7명
    // 스쿼드가 5명으로 보이고, 그게 그 사람의 실제 덱인지 내 카탈로그가 뒤처진 건지
    // 화면에서 구분되지 않는다.
    public static class DeckInfoDisplay
    {
        public readonly struct Item
        {
            public readonly string Id;
            public readonly string Name;   // 미해석이면 id 그대로
            public readonly Sprite Art;    // null 가능 — 뷰가 플레이스홀더를 그린다
            public readonly string Sub;    // 등급/희귀도 한 줄
            public readonly string Body;   // 설명 (없을 수 있음)
            public readonly bool Resolved; // 카탈로그가 아는 id 인가

            public Item(string id, string name, Sprite art, string sub, string body, bool resolved)
            {
                Id = id;
                Name = name;
                Art = art;
                Sub = sub;
                Body = body;
                Resolved = resolved;
            }
        }

        // 한 탭 안의 묶음(스쿼드 탭 = 유닛 + 드림스톤).
        public readonly struct Section
        {
            public readonly string Title;
            public readonly List<Item> Items;

            public Section(string title, List<Item> items)
            {
                Title = title;
                Items = items ?? new List<Item>();
            }
        }

        public static List<Item> Units(IReadOnlyList<string> ids, DefenderCatalog catalog)
        {
            var items = NewList(ids);
            if (ids == null) return items;
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (string.IsNullOrWhiteSpace(id)) continue;
                var unit = catalog != null ? catalog.ById(id) : null;
                items.Add(unit == null
                    ? Unresolved(id)
                    : new Item(id, Named(unit.displayName, id), unit.portrait,
                        unit.rarity.ToString(), unit.desc, true));
            }
            return items;
        }

        public static List<Item> Stones(IReadOnlyList<string> ids, DreamstoneCatalog catalog)
        {
            var items = NewList(ids);
            if (ids == null) return items;
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (string.IsNullOrWhiteSpace(id)) continue;
                var stone = catalog != null ? catalog.ById(id) : null;
                items.Add(stone == null
                    ? Unresolved(id)
                    : new Item(id, Named(stone.displayName, id), stone.icon,
                        stone.grade.ToString(), "", true));
            }
            return items;
        }

        public static List<Item> Cards(IReadOnlyList<string> ids, DreamcatcherCardCatalog catalog)
        {
            var items = NewList(ids);
            if (ids == null) return items;
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (string.IsNullOrWhiteSpace(id)) continue;
                var card = catalog != null ? catalog.ById(id) : null;
                items.Add(card == null
                    ? Unresolved(id)
                    : new Item(id, Named(card.displayName, id), card.art, "", card.description, true));
            }
            return items;
        }

        // 카탈로그가 모르는 id — 슬롯은 남기고 id 를 그대로 보여준다. 화면에서 "이건
        // 내 빌드가 모르는 것"임이 드러나는 편이 조용히 사라지는 것보다 낫다.
        private static Item Unresolved(string id)
            => new Item(id, id, null, "미확인", "", false);

        // 카탈로그에 있는데 표시명이 비어 있는 경우까지 방어 — 이름 없는 빈 칸이 되면
        // 무엇인지 알 길이 없다.
        private static string Named(string displayName, string id)
            => string.IsNullOrWhiteSpace(displayName) ? id : displayName;

        private static List<Item> NewList(IReadOnlyList<string> ids)
            => new List<Item>(ids != null ? ids.Count : 0);
    }
}
