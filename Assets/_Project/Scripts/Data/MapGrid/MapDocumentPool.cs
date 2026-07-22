using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Data.MapGrid
{
    /// <summary>
    /// 인게임 플레이에 랜덤 등장할 (맵, 공격 덱) 인코운터 풀(random-map-pool spec).
    /// 엔트리 = MapDocument + AttackDeck 쌍. 맵과 덱은 항상 같은 인덱스로 함께 선택되어
    /// "맵마다 고정된 적 패턴"을 만든다. 선택 로직은 순수 함수 <see cref="MapPoolSelect"/> 소유.
    /// </summary>
    [CreateAssetMenu(fileName = "MapDocumentPool", menuName = "Wassup/Map/MapDocumentPool", order = 2)]
    public class MapDocumentPool : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("배틀필드 레이아웃.")]
            public MapDocument document;

            [Tooltip("이 맵과 함께 도는 공격 덱(적 웨이브 패턴). null 이면 BattleBridge 의 레거시 deck 폴백.")]
            public Wassup.Data.AttackDeck deck;
        }

        [SerializeField] private List<Entry> entries = new();

        public int Count => entries?.Count ?? 0;

        public Entry Get(int index) => entries[index];
    }
}
