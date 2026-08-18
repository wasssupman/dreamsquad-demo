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

            [Tooltip("이 맵과 함께 도는 공격 덱. null 이면 BattleBridge 의 레거시 deck 폴백.")]
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
        // MapStage 인스펙터 "Dev 엔트리 등록" 버튼 전용 — 본편/devEntries 어디에도 없는 스테이지만 추가.
        public bool EditorRegisterDevStage(MapStage stage)
        {
            if (stage == null) return false;
            foreach (var e in entries) if (e.stage == stage) return false;
            foreach (var e in devEntries) if (e.stage == stage) return false;
            devEntries.Add(new Entry { stage = stage, deck = null });   // deck null = 레거시 deck 폴백 계약
            return true;
        }
#endif
    }
}
