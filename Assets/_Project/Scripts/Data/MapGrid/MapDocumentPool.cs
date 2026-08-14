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

            // tutorial-map — 이 맵이 «저작 웨이브»로 도는 경우의 플랜. null = 덱의 생성 웨이브(기본).
            //
            // 왜 덱이 아니라 엔트리가 드나: 플랜은 **맵과 한 몸**이다(튜토리얼 10웨이브는 그
            // 맵의 지형·배치를 전제로 짜인다). 덱에 달면 다른 맵에 그 덱을 붙이는 순간 플랜이
            // 따라가 무의미해진다. 엔트리는 (문서, 덱)을 이미 한 쌍으로 쥔 유일한 자리다.
            [Tooltip("저작 웨이브 플랜(튜토리얼·스크립트 인카운터). null = 덱의 생성 웨이브.")]
            public Wassup.Data.WavePlanAsset plan;
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
        // battle-structures unit 10 — 공성 맵의 «두 마음 동일 체력» 저작 규율.
        //
        // 방어 마음의 체력은 **덱**(goalStabilityMax)이 정하고 적 마음은 **SO**(health)가
        // 정한다. 두 값을 다 아는 자리는 (문서, 덱) 쌍을 쥔 이 엔트리뿐이다 — 문서는 덱을
        // 모르고 덱은 문서를 모른다. 그래서 대칭 검증이 여기 있다.
        //
        // **에러가 아니라 경고**다: 비대칭 체력도 난이도 저작일 수 있다. 알리는 것은 «3분
        // 만료 절대값 비교가 한쪽에 유리해진다» 는 사실뿐이다.
        // deck == null 이면 레거시 deck 폴백이라 여기서 알 수 없다 — 건너뛴다.
        private void OnValidate()
        {
            WarnOnSiegeCoreHpMismatch(entries, nameof(entries));
            WarnOnSiegeCoreHpMismatch(devEntries, nameof(devEntries));
        }

        private void WarnOnSiegeCoreHpMismatch(List<Entry> list, string label)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e.document == null || e.deck == null) continue;
                var structures = e.document.Structures;
                if (structures == null) continue;

                for (int s = 0; s < structures.Count; s++)
                {
                    var entry = structures[s];
                    if (entry.data == null) continue;
                    if (Wassup.Data.StructurePlacements.DeriveFaction(entry.side, entry.data.kind)
                        != Wassup.Battle.Units.Faction.EnemyCore) continue;

                    int enemyHp = Mathf.RoundToInt(entry.data.health);
                    int defenderHp = Mathf.Max(1, e.deck.goalStabilityMax);
                    if (enemyHp == defenderHp) continue;

                    Debug.LogWarning(
                        $"[MapDocumentPool] {label}[{i}] '{e.document.name}': 두 마음의 체력이 다르다 — "
                        + $"방어 {defenderHp}(덱 '{e.deck.name}'.goalStabilityMax) vs 적 {enemyHp}"
                        + $"('{entry.data.name}'.health). 3분 만료 시 절대값 비교라 한쪽이 유리하다.", this);
                }
            }
        }

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
