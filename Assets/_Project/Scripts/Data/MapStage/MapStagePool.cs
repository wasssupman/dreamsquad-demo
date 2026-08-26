using System;
using System.Collections.Generic;
using UnityEngine;
using Wassup.Core;

namespace Wassup.Data
{
    // map-diorama-stage unit 2 — 스테이지 인코운터 풀. MapDocumentPool 의 구조 승계:
    // 엔트리 = (MapStage 프리팹, AttackDeck, WavePlanAsset) — 맵과 덱·플랜은 같은 인덱스로
    // 잠긴다("맵마다 고정된 적 패턴"). 선택 로직은 순수 함수 MapPoolSelect 재사용.
    // WarnOnSiegeCoreHpMismatch 는 승계하지 않는다 — 이 브랜치에서 공성/거점 비가용(README 계약 11).
    [CreateAssetMenu(fileName = "MapStagePool", menuName = "Wassup/Map/MapStagePool", order = 3)]
    public class MapStagePool : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("디오라마 스테이지 프리팹 (루트에 MapStage). 인스턴스가 곧 비주얼이다.")]
            public MapStage stage;

            [Tooltip("이 맵과 함께 도는 공격 덱. null 이면 BattleBridge 인스펙터의 deck 필드(BattleScene: Deck_Duel)로 폴백 — 그 폴백은 «기본 덱»이지 맵 전용 패턴이 아니다.")]
            public AttackDeck deck;

            [Tooltip("저작 웨이브 플랜(튜토리얼·스크립트 인카운터). null = 덱의 생성 웨이브.")]
            public WavePlanAsset plan;
        }

        [SerializeField] private List<Entry> entries = new();

        // dev 전용 슬롯 — 시드 선택(seed % Count)에 절대 미포함. 진입은 DevMapOverride 인덱스뿐.
        [SerializeField] private List<Entry> devEntries = new();

        public int Count => entries?.Count ?? 0;

        public Entry Get(int index) => entries[index];

        public int DevCount => devEntries?.Count ?? 0;

        public Entry GetDev(int index) => devEntries[index];

#if UNITY_EDITOR
        // US-004b — 기존 dev 엔트리의 덱/플랜 짝을 갱신 (구 문서 풀의 «맵마다 그 맵의 적 패턴» 승계).
        public bool EditorSetDevPairing(MapStage stage, AttackDeck deck, WavePlanAsset plan)
        {
            for (int i = 0; i < devEntries.Count; i++)
                if (devEntries[i].stage == stage)
                {
                    devEntries[i] = new Entry { stage = stage, deck = deck, plan = plan };
                    return true;
                }
            return false;
        }

        // unit 11 — 라이브 엔트리 upsert(생성기 재실행용). 같은 **이름**의 라이브 엔트리가 있으면 그 자리의 참조를
        // 갱신하고(SaveAsPrefabAsset 이 fileID 를 새로 매길 수 있어 참조 동일성 대신 이름으로 찾는다), dev 에 같은
        // 이름이 있으면 승격(dev 에서 제거). 없으면 insertIndex 에 삽입. 끊어진(null) 라이브 슬롯은 제거한다 —
        // 그대로 두면 StagePoolBuildabilityTests 가 «빈 스테이지 슬롯»으로 막는다.
        public bool EditorUpsertLiveEntry(MapStage stage, AttackDeck deck, WavePlanAsset plan, int insertIndex)
        {
            if (stage == null) return false;
            bool changed = entries.RemoveAll(e => e.stage == null) > 0;
            var entry = new Entry { stage = stage, deck = deck, plan = plan };
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].stage.name == stage.name)
                {
                    bool same = entries[i].stage == stage && entries[i].deck == deck && entries[i].plan == plan;
                    entries[i] = entry;
                    return changed || !same;
                }
            for (int i = devEntries.Count - 1; i >= 0; i--)
                if (devEntries[i].stage != null && devEntries[i].stage.name == stage.name) devEntries.RemoveAt(i);
            entries.Insert(Mathf.Clamp(insertIndex, 0, entries.Count), entry);
            return true;
        }

        // MapStage 인스펙터 "Dev 엔트리 등록" 버튼 전용 — 본편/devEntries 어디에도 없는 스테이지만 추가.
        // 덱은 라이브 0번 엔트리의 덱을 물려받는다 — deck null 로 두면 BattleScene 의 폴백 덱으로
        // 떨어지는데, 그 폴백이 현행 덱과 다른 시절(WaveA: 생성기 v2·컨셉 없음)이 있었다. 새 맵은
        // «지금 라이브가 도는 규칙»으로 바로 Play 되는 게 등록 버튼의 목적이다.
        public bool EditorRegisterDevStage(MapStage stage)
        {
            if (stage == null) return false;
            foreach (var e in entries) if (e.stage == stage) return false;
            foreach (var e in devEntries) if (e.stage == stage) return false;
            var deck = entries.Count > 0 ? entries[0].deck : null;
            devEntries.Add(new Entry { stage = stage, deck = deck });
            return true;
        }
#endif
    }
}
