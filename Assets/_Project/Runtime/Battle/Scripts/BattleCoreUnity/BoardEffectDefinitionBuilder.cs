using System.Collections.Generic;
using UnityEngine;
using Somnia.Battle.BattleCore;
using Somnia.Battle.Data.Authoring;
using Somnia.Battle.Data;
using CoreDotElement = Somnia.Battle.BattleCore.Effects.DotElement;
using CoreFaction = Somnia.Battle.Skills.Faction;

namespace Somnia.Battle.BattleCoreUnity
{
    /// <summary>
    /// unit 6b — 판 위에 깔리는 것의 **저작 입력**. 모드와 같은 층의 선택 인자다(안 넘기면 셋 다 빈다).
    /// </summary>
    public struct BoardEffectAuthoring
    {
        /// <summary>
        /// 그 판에 쓰일 수 있는 존 장판 SO. **까는 자는 unit 7** 이라 오늘 라이브는 비어 있다 —
        /// 이 배열의 순서가 `MatchDefinition.Hazards` 줄 번호다.
        /// </summary>
        public HazardSO[] Hazards;

        /// <summary>
        /// 탄이 참조하지 않는 길막 SO(디버그·unit 7 생산자 전용). 탄이 참조하는 것은
        /// 탄 표에서 **자동으로** 모인다 — 호출자가 목록을 따로 넘기면 두 벌이 된다.
        /// </summary>
        public BlockingHazardSO[] ExtraBlockers;

        /// <summary>그 판의 맵 테마. 효과 타일 종류와 **개수**가 여기서 온다(옛 `theme.effectTiles` · `theme.effectTileCount`).</summary>
        public MapThemeData Theme;

        /// <summary>스테이지가 효과 타일을 끄나(옛 `MapStage.suppressEffectTiles` — 고정 셀 계측 픽스처용).</summary>
        public bool SuppressEffectTiles;
    }

    // battle-core-rebuild unit 6b — SO → 판 위에 깔리는 것의 정의표.
    //
    // `MatchDefinitionBuilder` 와 같은 규율이다: plain 수치·열거형만 읽고, 저작 어휘(옛 enum)는
    // **이름으로** 옮긴다(번호 캐스트 금지 — `HazardAuthoringMappingTests` 가 그물).
    public static class BoardEffectDefinitionBuilder
    {
        public static void Fill(MatchDefinition def, MatchViewAssets assets, in BoardEffectAuthoring board)
        {
            def.Hazards = ToHazardDefs(board.Hazards);
            // unit 6c — 줄 번호를 매긴 **같은 순회**가 뷰의 되찾기 표도 채운다(`MatchViewAssets` 규율).
            // 뷰 쪽에서 다시 모으면 두 벌이 갈려 장판·길막이 엉뚱한 프리팹으로 선다.
            var blockerAssets = assets != null ? new List<BlockingHazardSO>(4) : null;
            def.BlockingHazards = ToBlockingHazardDefs(assets != null ? assets.Projectiles : null,
                                                       board.ExtraBlockers, def.Projectiles, blockerAssets);
            if (assets != null)
            {
                assets.SetHazards(board.Hazards);
                assets.SetBlockers(blockerAssets);
            }
            FillEffectTiles(def, board.Theme, board.SuppressEffectTiles);
            // unit 8a2 행 1 — 줄을 매긴 **같은 판단**으로 그림 표도 채운다(줄 0 이면 그림 0 — 두 벌이 갈리지 않게).
            if (assets != null)
                assets.SetEffectTiles(def.EffectTiles.Length > 0 ? board.Theme.effectTiles : null,
                                      def.EffectTiles.Length > 0 ? board.Theme.effectTileMaterial : null);
        }

        // ── 존 장판 ──────────────────────────────────────────────────────────

        public static HazardDef[] ToHazardDefs(HazardSO[] src)
        {
            if (src == null || src.Length == 0) return System.Array.Empty<HazardDef>();
            var rows = new HazardDef[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var so = src[i];
                if (so == null)
                {
                    // 줄 번호가 곧 참조라 **빈 줄을 지우지 않는다** — 지우면 뒤 줄이 전부 밀린다.
                    rows[i] = new HazardDef { Id = "", Shape = -1, Effects = System.Array.Empty<HazardEffectDef>() };
                    continue;
                }
                var effects = so.effects ?? System.Array.Empty<HazardEffect>();
                var outEff = new HazardEffectDef[effects.Length];
                for (int e = 0; e < effects.Length; e++)
                {
                    var he = effects[e];
                    outEff[e] = new HazardEffectDef
                    {
                        Kind = (int)ToCoreEffectKind(he.kind),
                        // U10 — DoT 의 크기는 까는 효과 줄이 싣는다(`TryDotDamage`). 장판 줄에는 비피해 수치만.
                        Magnitude = he.kind == CcKind.DoT ? 0f : he.param1,
                        RestDuration = he.restDuration,
                        TickInterval = he.tickInterval,
                        Element = (int)ToCoreDotElement(he.element),
                        // F34 — 축은 정의표에서 열렸고 값은 **저작**이 정한다.
                        TargetFactions = (int)so.zoneTargetFactions,
                    };
                }
                rows[i] = new HazardDef
                {
                    Id = so.name,
                    Shape = (int)ToCoreShape(so.shape),
                    Radius = so.radius,
                    Lifetime = so.lifetime,
                    Effects = outEff,
                };
            }
            return rows;
        }

        /// <summary>
        /// skill-data-table 1b(U10) — 그 장판 SO 의 DoT 피해(DoT 하위 효과의 `param1` · 없으면 0). 효과 줄 피해 칸은 하나라
        /// DoT 가 둘 이상이면 false. 빌더(`SpawnHazard` 효과)와 디버그 메뉴가 같은 값을 읽는다.
        /// </summary>
        public static bool TryDotDamage(HazardSO so, out float damage)
        {
            damage = 0f;
            if (so?.effects == null) return true;
            int n = 0;
            foreach (var he in so.effects)
                if (he.kind == CcKind.DoT) { damage = he.param1; n++; }
            return n <= 1;
        }

        public static HazardShapeKind ToCoreShape(HazardShape authored)
        {
            switch (authored)
            {
                case HazardShape.SingleCell: return HazardShapeKind.SingleCell;
                case HazardShape.Square3x3: return HazardShapeKind.Square3x3;
                case HazardShape.RadiusSquare: return HazardShapeKind.RadiusSquare;
                default:
                    Debug.LogError($"[BoardEffectDefinitionBuilder] 모르는 장판 모양({authored}) — 한 칸으로 접는다.");
                    return HazardShapeKind.SingleCell;
            }
        }

        public static HazardEffectKind ToCoreEffectKind(CcKind authored)
        {
            switch (authored)
            {
                case CcKind.Slow: return HazardEffectKind.Slow;
                case CcKind.Impulse: return HazardEffectKind.Impulse;
                case CcKind.DoT: return HazardEffectKind.DoT;
                case CcKind.Stun: return HazardEffectKind.Stun;
                case CcKind.Sleep: return HazardEffectKind.Sleep;
                default:
                    Debug.LogError($"[BoardEffectDefinitionBuilder] 모르는 장판 효과({authored}) — 감속으로 접는다.");
                    return HazardEffectKind.Slow;
            }
        }

        public static CoreDotElement ToCoreDotElement(DotElement authored)
        {
            switch (authored)
            {
                case DotElement.None: return CoreDotElement.None;
                case DotElement.Bleed: return CoreDotElement.Bleed;
                case DotElement.Fire: return CoreDotElement.Fire;
                case DotElement.Ice: return CoreDotElement.Ice;
                case DotElement.Poison: return CoreDotElement.Poison;
                default:
                    Debug.LogError($"[BoardEffectDefinitionBuilder] 모르는 원소({authored}) — 원소 없음으로 접는다.");
                    return CoreDotElement.None;
            }
        }

        // ── 길막 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 탄 표에서 길막 참조를 모아 줄을 만든다(탄 하나당 줄 하나 · `SpawnedByProjectile`).
        /// 탄이 안 가리키는 추가 길막은 그 뒤에 `-1` 로 붙는다.
        ///
        /// ⚠ **F12** — 폭발 피해를 저작했는데 폭발 탄이 표에 없으면 **loud 하게 거절**하고 폭발
        /// 저작을 통째로 버린다(피해 0). 옛 전투는 경고만 내고 0번 탄 비주얼을 한 프레임 빌렸다.
        /// </summary>
        public static BlockingHazardDef[] ToBlockingHazardDefs(IReadOnlyList<ProjectileData> projectiles,
                                                               BlockingHazardSO[] extra,
                                                               ProjectileDef[] projectileRows,
                                                               List<BlockingHazardSO> assetsOut = null)
        {
            var rows = new List<BlockingHazardDef>(4);
            if (projectiles != null)
                for (int i = 0; i < projectiles.Count; i++)
                {
                    var blocker = projectiles[i] != null ? projectiles[i].spawnBlocker : null;
                    if (blocker == null) continue;
                    var row = ToBlockingHazardDef(blocker, projectileRows);
                    row.SpawnedByProjectile = i;
                    rows.Add(row);
                    assetsOut?.Add(blocker);
                }
            if (extra != null)
                for (int i = 0; i < extra.Length; i++)
                    if (extra[i] != null)
                    {
                        rows.Add(ToBlockingHazardDef(extra[i], projectileRows));
                        assetsOut?.Add(extra[i]);
                    }
            return rows.Count == 0 ? System.Array.Empty<BlockingHazardDef>() : rows.ToArray();
        }

        public static BlockingHazardDef ToBlockingHazardDef(BlockingHazardSO so, ProjectileDef[] projectileRows)
        {
            var d = BlockingHazardDef.Default();
            d.Id = so.name;
            d.MaxHealth = so.maxHp;
            d.Shape = (int)ToCoreShape(so.shape);
            d.DecayPerSec = so.healthDecayPerSec;
            d.ExplodeTileRange = so.explodeTileRange;
            d.ExplodeTargetCap = so.explodeTargetCap;

            if (so.explodeDamage > 0f)
            {
                if (so.explodeProjectile == null)
                {
                    // F12 — 옛 fail-silent 의 정확한 조건(탄 미배선). 경고가 아니라 **거절**이다.
                    Debug.LogError($"[BoardEffectDefinitionBuilder] 길막 '{so.name}' 은 폭발 피해 {so.explodeDamage} 를 "
                                   + "저작했는데 폭발 탄이 미배선이다 — 폭발 저작을 버린다(F12).");
                }
                else
                {
                    // U10 — 폭발 **피해**는 세우는 효과 줄이 싣는다(`BindPattern`). 이 줄은 폭발 탄·반경만.
                    // 폭발 탄이 **공격 표에 이미 있으면** 그 줄이다. 없으면 -1 로 남는다 — 폭발의 발사와
                    // 그 탄의 표 편입은 사망 seam 과 함께 unit 7 이 연다(그 전엔 폭발이 안 난다).
                    d.ExplodeProjectileDefIndex = IndexOfProjectile(projectileRows, so.explodeProjectile);
                    // unit 7d — 탄이 세우는 길막의 폭발 탄은 전투 빌더가 표에 편입한다. 그래도 없다면(탄 밖에서만 세우는 길막 —
                    // 디버그 목록 등) 조용히 안 터지지 않게 말한다.
                    if (d.ExplodeProjectileDefIndex < 0)
                        Debug.LogError($"[BoardEffectDefinitionBuilder] 길막 '{so.name}' 의 폭발 탄 '{so.explodeProjectile.name}' 이 "
                                       + "이 판의 탄 표에 없다 — 부서져도 안 터진다.");
                }
            }
            return d;
        }

        // 탄 표의 줄은 저작 id 로 찾는다(`ProjectileDef.Id` = `ProjectileData.id`).
        private static int IndexOfProjectile(ProjectileDef[] rows, ProjectileData asset)
        {
            if (asset == null || rows == null || string.IsNullOrEmpty(asset.id)) return -1;
            for (int i = 0; i < rows.Length; i++)
                if (rows[i].Id == asset.id) return i;
            return -1;
        }

        // ── 효과 타일 ────────────────────────────────────────────────────────

        /// <summary>
        /// 테마의 효과 타일 종류와 개수를 싣는다. **종류가 비거나 개수가 0 이면 둘 다 0** 이다
        /// (옛 가드: `effectTiles.Length > 0 && effectTileCount > 0`). 스테이지가 끄면 0.
        ///
        /// ⚠ 개수를 싣지 않으면 뽑기(`PlacementService`)가 한 칸도 안 뽑는다 — 새 코어에서 라이브
        /// 효과 타일 3칸이 0 이 되던 결함(드리프트 감사)이 이 함수가 없어서였다.
        /// </summary>
        public static void FillEffectTiles(MatchDefinition def, MapThemeData theme, bool suppressed)
        {
            def.EffectTiles = System.Array.Empty<EffectTileDef>();
            def.EffectTileCount = 0;
            if (suppressed || theme == null || theme.effectTiles == null) return;
            if (theme.effectTiles.Length == 0 || theme.effectTileCount <= 0) return;

            var rows = new EffectTileDef[theme.effectTiles.Length];
            for (int i = 0; i < rows.Length; i++) rows[i] = ToEffectTileDef(theme.effectTiles[i]);
            def.EffectTiles = rows;
            def.EffectTileCount = theme.effectTileCount;
        }

        public static EffectTileDef ToEffectTileDef(EffectTileData data)
        {
            if (data == null) return new EffectTileDef { Id = "", Entries = System.Array.Empty<EffectTileEntryDef>() };
            var src = data.effects ?? System.Array.Empty<EffectTileEntry>();
            var entries = new EffectTileEntryDef[src.Length];
            for (int i = 0; i < src.Length; i++)
                entries[i] = new EffectTileEntryDef
                {
                    Stat = (int)CombatDefinitionBuilder.ToCoreStat(src[i].stat),
                    Op = (int)CombatDefinitionBuilder.ToCoreOp(src[i].op),
                    Magnitude = src[i].magnitude,
                };
            return new EffectTileDef { Id = string.IsNullOrEmpty(data.id) ? data.name : data.id, Entries = entries };
        }
    }
}
