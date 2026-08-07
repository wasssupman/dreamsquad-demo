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

        // map-painter-tool unit 5 — dev 전용 슬롯. 시드 선택(seed % Count)에 절대 미포함 —
        // Count 가 entries 만 세므로 토너먼트/랜덤 맵 결정론은 byte-identical. 진입 경로는
        // DevMapOverride 인덱스(풀 뒤 이어붙은 슬롯)뿐이다. 페인터 Bake 가 신규 문서를 자동 등록한다.
        [SerializeField] private List<Entry> devEntries = new();

        public int Count => entries?.Count ?? 0;

        public Entry Get(int index) => entries[index];

        public int DevCount => devEntries?.Count ?? 0;

        public Entry GetDev(int index) => devEntries[index];

#if UNITY_EDITOR
        // 페인터 Bake 전용(에디터) — 풀 본편/devEntries 어디에도 없는 문서만 dev 슬롯에 추가.
        public bool EditorRegisterDevDocument(MapDocument doc)
        {
            if (doc == null) return false;
            foreach (var e in entries) if (e.document == doc) return false;
            foreach (var e in devEntries) if (e.document == doc) return false;
            devEntries.Add(new Entry { document = doc, deck = null });   // deck null = 레거시 deck 폴백 계약
            return true;
        }
#endif
    }
}
