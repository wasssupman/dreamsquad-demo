using System;
using UnityEngine;

namespace Wassup.Data
{
    // battle-content-finish unit 0 — **기본 편성.** 바깥(`MatchEntryInput`)이 편성을 안 주는 판(에디터 직접 진입 · 테스트)이
    // 쓰는 유닛 · 드림스톤 · 덱이다. 바깥에서 편성을 받은 판은 이 에셋을 읽지 않는다 — 입력이 이긴다(`MatchEntry`).
    //
    // 덱은 `DreamcatcherDeck` 한 장이다(D2). 비우면(없거나 카드 0) 드라이버가 입력의 고른 덱 + 판마다 굴린 액티브로 짓는다.
    [CreateAssetMenu(menuName = "Wassup/Default Loadout", fileName = "DefaultLoadout")]
    public sealed class DefaultLoadout : ScriptableObject
    {
        [Tooltip("편성 유닛(순서 그대로). 입력이 없을 때만.")]
        public DefenderUnitData[] defenders = Array.Empty<DefenderUnitData>();
        [Tooltip("장착 드림스톤(스탯 돌 · 코스트 돌 혼재 — 가르는 것은 빌더). 입력이 없을 때만.")]
        public DreamstoneData[] dreamstones = Array.Empty<DreamstoneData>();
        [Tooltip("개발용 덱 덮어쓰기. 비우면 입력의 고른 덱 + 굴린 액티브로 짓는다.")]
        public DreamcatcherDeck deck;

        /// <summary>덱의 카드. 덱이 없거나 비어 있으면 빈 배열(null 아님).</summary>
        public DreamcatcherCard[] DeckCards
            => deck != null && deck.cards != null ? deck.cards : Array.Empty<DreamcatcherCard>();
    }
}
