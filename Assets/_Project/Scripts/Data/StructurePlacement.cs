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

        // 거점 저작 자체의 정합성 — 금지 조합·격자 경계·footprint 겹침.
        // 투트랙 리뷰 M-a: 이 세 규칙이 페인터(에디터 어셈블리)에만 있으면 런타임·테스트가
        // 볼 수 없고, structures 는 [SerializeField] 라 **인스펙터로 페인터를 우회**해 찍을 수
        // 있다. 특히 (Defender, Core) 금지는 «골이 두 벌» 재발을 막는 유일한 게이트다 —
        // 페인터와 MapDocument.OnValidate 양쪽이 이 함수를 부른다.
        // tiles(선택) — 주면 **적 마음 셀의 Walk 검사**가 켜진다(unit 6). 적 마음은 파생
        // 스폰이 되는데(공성 모드), 스폰이 Walk 가 아니면 연결성 BFS 가 도달 못 해 런타임
        // hard-fail 한다. 기존 «스폰이 Walk 아님» 검사는 저작 스폰 목록만 봐서 파생 스폰을
        // 못 잡는다.
        public static void ValidateStructures(
            IReadOnlyList<StructureEntry> structures, int width, int height, List<string> errors,
            IReadOnlyList<MapTileType> tiles = null)
        {
            if (structures == null) return;
            var occupied = new Dictionary<UnityEngine.Vector2Int, int>();
            for (int i = 0; i < structures.Count; i++)
            {
                var s = structures[i];
                if (s.data == null) { errors.Add($"거점 {s.cell} 의 StructureData 가 비었다"); continue; }

                // 리뷰 A-L3 — 중립은 비트만 예약이다(계약 9: 생산자·소비자 0). 어떤 마스크도
                // 중립 비트를 포함하지 않아 «못 죽이는데 쏘는 블로커» 가 된다. 페인터는
                // 방어/적 2택만 노출하지만 인스펙터 저작이 가능하므로 여기서 거절한다.
                if (s.side == StructureSide.Neutral)
                { errors.Add($"거점 {s.cell}: 중립 거점은 저작 불가 — 계약 9(중립은 비트만 예약)"); continue; }

                // 방어 마음의 정본은 goals[] 다 — 두 벌이 되는 것을 여기서 막는다.
                if (s.side == StructureSide.Defender && s.data.kind == StructureKind.Core)
                    errors.Add($"거점 {s.cell}: 방어 마음은 Goal 브러시로 저작한다(골이 두 벌이 되는 것을 막는다)");

                var faction = StructurePlacements.DeriveFaction(s.side, s.data.kind);
                int half = StructurePlacements.FootprintOf(faction) / 2;
                if (s.cell.x - half < 0 || s.cell.x + half >= width
                    || s.cell.y - half < 0 || s.cell.y + half >= height)
                    errors.Add($"거점 {s.cell}({s.data.kind}) 이 격자를 벗어난다 (반경 {half}, 격자 {width}×{height})");

                // 적 마음 → 파생 스폰 = 마음의 하단(y−1)·상단(y+1) 셀(siege-lane-spawn unit 0,
                // MapDocumentBuilder 와 같은 규칙). 스폰 클러스터(마음·하단·상단) 3셀이 Walk 가
                // 아니면 연결성 BFS 가 도달 못 해 런타임 hard-fail. 상/하단이 격자 밖이면 파생
                // 자체가 불가능하다 — 이건 타일과 무관한 기하 규칙이라 tiles 없이도 검사한다.
                if (faction == Wassup.Battle.Units.Faction.EnemyCore)
                {
                    if (s.cell.y - 1 < 0 || s.cell.y + 1 >= height)
                        errors.Add($"적 마음 {s.cell} 의 상/하단이 격자를 벗어난다 — 파생 스폰(마음 y±1)이 불가능하다");
                    else if (tiles != null && tiles.Count == width * height
                        && s.cell.x >= 0 && s.cell.x < width)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int sy = s.cell.y + dy;
                            if (tiles[sy * width + s.cell.x] == MapTileType.Walk) continue;
                            errors.Add(dy == 0
                                ? $"적 마음 {s.cell} 이 Walk 셀이 아니다 — 스폰 클러스터(마음·하단·상단)는 Walk 여야 한다"
                                : $"적 마음 {s.cell} 의 {(dy < 0 ? "하단" : "상단")} ({s.cell.x},{sy}) 이 Walk 가 아니다 — 파생 스폰 셀이다");
                        }
                    }
                }

                // 리뷰 M-8 — 아군 사격 저작 함정. SO 의 targetFactions 는 진영을 모르고
                // (기본 DefenderUnit = 적 본능용 포탑), 진영은 배치가 정한다 — 그래서
                // «방어/중립 본능 + 방어유닛 마스크» 조합이 저작으로 가능해진다. 여기서 잡는다.
                if (s.data.kind == StructureKind.Instinct && s.side != StructureSide.Enemy
                    && s.data.attackDamage > 0f
                    && ((int)s.data.targetFactions & (int)Wassup.Battle.Units.Faction.DefenderUnit) != 0)
                    errors.Add($"거점 {s.cell}: {s.side} 본능이 방어유닛을 노린다(SO targetFactions) — 아군 사격. 편에 맞는 마스크의 SO 를 물려라");

                // footprint 겹침 — 3×3 본능끼리, 또는 본능이 다른 거점을 덮는 경우.
                for (int dy = -half; dy <= half; dy++)
                    for (int dx = -half; dx <= half; dx++)
                    {
                        var c = new UnityEngine.Vector2Int(s.cell.x + dx, s.cell.y + dy);
                        if (occupied.TryGetValue(c, out int other))
                            errors.Add($"거점 {s.cell} 이 거점 {structures[other].cell} 과 {c} 에서 겹친다");
                        else occupied[c] = i;
                    }
            }
        }

        // 적 마음 개수 = 모드의 유일한 입력. 페인터·OnValidate 가 공유한다.
        public static int CountEnemyCores(IReadOnlyList<StructureEntry> structures)
        {
            if (structures == null) return 0;
            int n = 0;
            for (int i = 0; i < structures.Count; i++)
                if (structures[i].data != null
                    && structures[i].side == StructureSide.Enemy
                    && structures[i].data.kind == StructureKind.Core) n++;
            return n;
        }

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
