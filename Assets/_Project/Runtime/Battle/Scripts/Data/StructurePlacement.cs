using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Somnia.Battle.Skills;

namespace Somnia.Battle.Data
{
    // battle-structures unit 3 — 거점의 두 저작 축.
    //
    // 진영(Faction 교차 비트)을 저작에 직접 노출하지 않는 이유: 그러면 DefenderUnit 처럼
    // **거점이 아닌 비트**를 찍을 수 있고, 그건 표현되면 안 되는 상태다. 편·종류 두 축만
    // 저작하고 교차 비트는 파생한다 — 모드 enum 을 기각하고 «적 마음 유무» 에서 파생시킨
    // 것과 같은 판단이다(README §모드 판정).
    public enum StructureKind : byte { Core, Instinct }
    public enum StructureSide : byte { Defender, Enemy, Neutral }

    // MapDocument 직렬화 엔트리(관리 참조 포함). 저작의 정본.
    [Serializable]
    public struct StructureEntry
    {
        public Vector2Int cell;
        public StructureSide side;
        public StructureData data;
    }

    // 런타임 unmanaged 투영. GeneratedMap 이 싣는 것은 이 두 값뿐이다 — 마스크 파생·
    // 연결성·모드 판정은 셀과 진영만 본다. 스탯(체력·프랍·공격)은 SO 에 남고 브리지가
    // 문서에서 읽는다(unit 4).
    public struct StructurePlacement
    {
        public int2 cell;
        public Faction faction;
    }

    public static class StructurePlacements
    {
        // v1 footprint — 마음 1×1 · 본능 3×3 (README 계약 6). 임의 footprint 는 후속 후보.
        public const int CoreFootprint = 1;
        public const int InstinctFootprint = 3;
        // instinct-content unit 1 — 「적대적 본능의 배치 배제 여유」(구 9×9, 이후 값 0)는
        // 술어·분기까지 **삭제**됐다. 본능은 자기 footprint 만 차지하는 건물이고, 배치 배제도
        // 통행 차단도 그 이상 갖지 않는다(사용자 결정 2026-08-12). 되살릴 일이 있으면
        // «여유 ≥ 사거리 = 아무도 못 쏘는 포탑» 검산부터 하고 축을 새로 세운다.

        // 편 × 종류 → 교차 비트. 거점 아닌 비트는 이 함수에서 나올 수 없다.
        public static Faction DeriveFaction(StructureSide side, StructureKind kind)
        {
            if (kind == StructureKind.Core)
            {
                switch (side)
                {
                    case StructureSide.Defender: return Faction.DefenderCore;
                    case StructureSide.Enemy: return Faction.EnemyCore;
                    default: return Faction.NeutralCore;
                }
            }
            switch (side)
            {
                case StructureSide.Defender: return Faction.DefenderInstinct;
                case StructureSide.Enemy: return Faction.EnemyInstinct;
                default: return Faction.NeutralInstinct;
            }
        }

        // 종류는 교차 비트가 이미 인코딩한다 — footprint 도 거기서 파생한다.
        // 1축 교차 비트 결정이 값을 돌려받는 자리(별도 kind 필드를 안 싣는 근거).
        public static int FootprintOf(Faction faction)
            => ((int)faction & Factions.AnyInstinct) != 0 ? InstinctFootprint : CoreFootprint;

        // distance-based-range unit 20 리뷰 H-1 — **거점의 몸 반경.** 판정 bake(`HitRadius`)와
        // 그림자(지름 = 2r)가 여기 하나를 읽는다. 종전엔 두 소비처가 같은 수를 **다른 모양**으로
        // 적어(한쪽 `FootprintOf × 0.5`, 다른 쪽 `FootprintOf` 를 지름으로) 형제로 보이지 않았고,
        // 묶는 것이 주석 하나뿐이었다. 방어유닛 규칙(가로/2)과는 여전히 별개 식이다 —
        // 거점 footprint 는 단일 int(정사각)이라 수치가 동치일 뿐이고, 직사각 거점이 생기는 날
        // 두 식을 합칠지 결정한다.
        public static float BodyRadiusOf(Faction faction) => FootprintOf(faction) * 0.5f;

        // siege-lane-spawn unit 0/1 — 공성 파생 스폰 = 마음 셀 + 이 오프셋들.
        // **배열 순서가 곧 레인 번호다**: [하단(y−1), 상단(y+1)] = lane 0, 1.
        // 빌더(파생)·OnValidate(레인 검증)·테스트가 같은 배열을 봐야 순서 규칙이 두 벌로
        // 갈리지 않는다 — 갈리면 레인별 spawnRoutes 가 서로 바뀐다.
        public static readonly UnityEngine.Vector2Int[] SiegeSpawnOffsets =
        {
            new UnityEngine.Vector2Int(0, -1),
            new UnityEngine.Vector2Int(0,  1),
        };

        public static bool IsCore(Faction faction) => ((int)faction & Factions.AnyCore) != 0;
        public static bool IsInstinct(Faction faction) => ((int)faction & Factions.AnyInstinct) != 0;

    }
}
