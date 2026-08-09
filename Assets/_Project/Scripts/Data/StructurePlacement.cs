using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Battle.Units;

namespace Wassup.Data
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

        public static bool IsCore(Faction faction) => ((int)faction & Factions.AnyCore) != 0;
        public static bool IsInstinct(Faction faction) => ((int)faction & Factions.AnyInstinct) != 0;
    }

    // battle-structures unit 3 — 모드는 **파생**이다. 저작 enum 을 두면 «공성인데 적 마음
    // 없음» 이라는 표현 불가능해야 할 상태가 생긴다(README §모드 판정).
    public enum MapMode : byte { Invasion, Siege, Invalid }

    // 저작 규칙. 페인터와 테스트가 **같은 함수**를 본다 — 페인터에 인라인하면 규칙이
    // 에디터 어셈블리에 갇혀 검증할 수 없고, 두 벌로 갈리면 «툴은 통과인데 런타임이 거부»
    // 가 난다.
    public static class StructureAuthoringRules
    {
        public static MapMode DeriveMode(int enemyCoreCount)
            => enemyCoreCount == 0 ? MapMode.Invasion
             : enemyCoreCount == 1 ? MapMode.Siege
             : MapMode.Invalid;

        // 방어 마음은 goals[] 로 저작한다(현행 승계) — defenderGoalCount 는 그 개수다.
        public static void ValidateMode(
            int enemyCoreCount, int defenderGoalCount, int spawnCount, List<string> errors)
        {
            switch (DeriveMode(enemyCoreCount))
            {
                case MapMode.Invalid:
                    errors.Add($"적 마음 {enemyCoreCount}개 — 공성 맵의 마음은 진영당 1개다");
                    break;
                case MapMode.Siege:
                    if (defenderGoalCount != 1)
                        errors.Add($"공성 맵은 방어 마음 정확히 1 (현재 {defenderGoalCount}) — 멀티골 금지");
                    if (spawnCount > 0)
                        errors.Add($"공성 맵은 spawns 저작 금지 (현재 {spawnCount}) — 적 마음 셀이 파생으로 채운다");
                    break;
                default:
                    if (spawnCount < 1 || spawnCount > 4)
                        errors.Add($"스폰 {spawnCount}개 (1~4 필요)");
                    if (defenderGoalCount < 1 || defenderGoalCount > 4)
                        errors.Add($"골 {defenderGoalCount}개 (1~4 필요)");
                    break;
            }
        }
    }
}
