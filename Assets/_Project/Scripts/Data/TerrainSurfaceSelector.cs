using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Wassup.Data
{
    public static class TerrainSurfaceSelector
    {
        private const int PathInfluenceRadius = 3;

        public static Texture2D SelectTexture(GeneratedMap map, MapThemeData theme, MapTileType type, int x, int y)
        {
            if (theme == null)
                return null;

            if (type == MapTileType.Walk)
            {
                var shapeTexture = GetWalkShapeTexture(theme, BoardShapeUtility.GetShapeForMask(GetWalkMask(map, x, y)));
                if (shapeTexture != null)
                    return shapeTexture;
            }

            var rules = GetRules(theme, type);
            if (rules != null && HasUsableRule(rules))
                return SelectRuleTexture(map, theme, rules, x, y);

            var variants = GetLegacyVariants(theme, type);
            if (variants != null && variants.Length > 0)
                return SelectLegacyVariant(map, theme, variants, type, x, y);

            return GetSingleTexture(theme, type);
        }

        public static Texture2D[] CollectTextures(MapThemeData theme, MapTileType type)
        {
            var textures = new List<Texture2D>();
            if (theme == null)
                return textures.ToArray();

            var rules = GetRules(theme, type);
            if (rules != null)
            {
                for (int i = 0; i < rules.Length; i++)
                {
                    var texture = rules[i] != null ? rules[i].texture : null;
                    if (texture != null && !textures.Contains(texture))
                        textures.Add(texture);
                }
            }

            var variants = GetLegacyVariants(theme, type);
            if (variants != null)
            {
                for (int i = 0; i < variants.Length; i++)
                {
                    if (variants[i] != null && !textures.Contains(variants[i]))
                        textures.Add(variants[i]);
                }
            }

            var single = GetSingleTexture(theme, type);
            if (single != null && !textures.Contains(single))
                textures.Add(single);

            if (type == MapTileType.Walk)
                AddWalkShapeTextures(theme, textures);

            return textures.ToArray();
        }

        private static Texture2D SelectRuleTexture(
            GeneratedMap map,
            MapThemeData theme,
            TerrainSurfaceVariant[] rules,
            int x,
            int y)
        {
            float macro = Coherent01(map.seed + theme.tileVariantSeedOffset + 101, x, y, theme.tileVariantNoiseScale);
            float moisture = Coherent01(map.seed + theme.tileVariantSeedOffset + 503, x, y, theme.tileVariantNoiseScale * 1.7f);
            float pathInfluence = PathInfluence(map, x, y) * math.saturate(theme.pathSurfaceInfluence);
            float edgeInfluence = EdgeInfluence(map.gridSize, x, y) * math.saturate(theme.edgeSurfaceInfluence);
            float jitter = math.lerp(0.9f, 1.1f, Hash01(x, y, map.seed + theme.tileVariantSeedOffset + 907));

            Texture2D selected = null;
            float bestScore = -1f;
            for (int i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                if (rule == null || rule.texture == null || rule.weight <= 0f)
                    continue;

                float score = rule.weight;
                score *= RangeScore(macro, rule.noiseRange);
                score *= RangeScore(moisture, rule.moistureRange);
                score *= math.lerp(1f, rule.nearPathMultiplier, pathInfluence);
                score *= math.lerp(1f, rule.edgeMultiplier, edgeInfluence);
                score *= jitter;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                selected = rule.texture;
            }

            return selected;
        }

        public static Texture2D SelectRuleTexture(
            BoardVisualPlan plan,
            MapThemeData theme,
            TerrainSurfaceVariant[] rules,
            int x,
            int y)
        {
            if (plan == null || theme == null || rules == null)
                return null;

            var cell = plan.CellAt(new int2(x, y));
            float macro = HashTo01(cell.surfaceNoiseHash);
            float moisture = HashTo01(cell.surfaceNoiseHash ^ 0x9e3779b9u);
            float pathInfluence = cell.HasPathProximity ? math.saturate(1f - cell.pathProximity / 3f) * math.saturate(theme.pathSurfaceInfluence) : 0f;
            float edgeInfluence = math.saturate(1f - cell.borderProximity / (float)math.max(1, math.min(plan.gridSize.x, plan.gridSize.y) / 3)) * math.saturate(theme.edgeSurfaceInfluence);
            float jitter = math.lerp(0.9f, 1.1f, HashTo01(cell.surfaceNoiseHash ^ 0x85ebca6bu));

            Texture2D selected = null;
            float bestScore = -1f;
            for (int i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                if (rule == null || rule.texture == null || rule.weight <= 0f)
                    continue;

                float score = rule.weight;
                score *= RangeScore(macro, rule.noiseRange);
                score *= RangeScore(moisture, rule.moistureRange);
                score *= math.lerp(1f, rule.nearPathMultiplier, pathInfluence);
                score *= math.lerp(1f, rule.edgeMultiplier, edgeInfluence);
                score *= jitter;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                selected = rule.texture;
            }

            return selected;
        }

        private static Texture2D SelectLegacyVariant(
            GeneratedMap map,
            MapThemeData theme,
            Texture2D[] variants,
            MapTileType type,
            int x,
            int y)
        {
            int usableCount = 0;
            for (int i = 0; i < variants.Length; i++)
            {
                if (variants[i] != null)
                    usableCount++;
            }

            if (usableCount == 0)
                return GetSingleTexture(theme, type);

            float scale = math.max(0.01f, theme.tileVariantNoiseScale);
            float jitter = math.saturate(theme.tileVariantJitter);
            int typeOffset = ((int)type + 1) * 37;
            float noise = Coherent01(map.seed + theme.tileVariantSeedOffset + typeOffset, x, y, scale);
            float hash = Hash01(x, y, map.seed + theme.tileVariantSeedOffset + typeOffset);
            float value = math.frac(noise * 1.618f + hash * jitter);
            int target = math.clamp((int)math.floor(value * usableCount), 0, usableCount - 1);

            for (int i = 0; i < variants.Length; i++)
            {
                if (variants[i] == null)
                    continue;

                if (target == 0)
                    return variants[i];

                target--;
            }

            return GetSingleTexture(theme, type);
        }

        private static float Coherent01(int seed, int x, int y, float scale)
        {
            scale = math.max(0.01f, scale);
            float ox = (seed & 0xffff) * 0.017f;
            float oy = ((seed >> 8) & 0xffff) * 0.019f;
            float low = Mathf.PerlinNoise((x + ox) * scale, (y + oy) * scale);
            float high = Mathf.PerlinNoise((x + ox * 0.37f) * scale * 2.9f, (y + oy * 0.41f) * scale * 2.9f);
            return math.saturate(low * 0.72f + high * 0.28f);
        }

        private static float RangeScore(float value, Vector2 range)
        {
            float min = math.min(range.x, range.y);
            float max = math.max(range.x, range.y);
            if (max <= min)
                return 1f;

            if (value >= min && value <= max)
                return 1f;

            float distance = value < min ? min - value : value - max;
            float falloff = math.max(0.08f, (max - min) * 0.5f);
            return math.saturate(1f - distance / falloff);
        }

        private static float PathInfluence(GeneratedMap map, int x, int y)
        {
            if (!map.IsCreated)
                return 0f;

            int best = PathInfluenceRadius + 1;
            for (int dy = -PathInfluenceRadius; dy <= PathInfluenceRadius; dy++)
            for (int dx = -PathInfluenceRadius; dx <= PathInfluenceRadius; dx++)
            {
                int distance = math.abs(dx) + math.abs(dy);
                if (distance == 0 || distance >= best || distance > PathInfluenceRadius)
                    continue;

                int cx = x + dx;
                int cy = y + dy;
                if (cx < 0 || cy < 0 || cx >= map.gridSize.x || cy >= map.gridSize.y)
                    continue;

                if (map.TileAt(new int2(cx, cy)) == MapTileType.Walk)
                    best = distance;
            }

            if (best > PathInfluenceRadius)
                return 0f;

            return 1f - (best - 1) / (float)PathInfluenceRadius;
        }

        private static float EdgeInfluence(int2 gridSize, int x, int y)
        {
            int maxDistance = math.max(1, math.min(gridSize.x, gridSize.y) / 3);
            int distance = math.min(math.min(x, y), math.min(gridSize.x - 1 - x, gridSize.y - 1 - y));
            return 1f - math.saturate(distance / (float)maxDistance);
        }

        private static bool HasUsableRule(TerrainSurfaceVariant[] rules)
        {
            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i] != null && rules[i].texture != null && rules[i].weight > 0f)
                    return true;
            }

            return false;
        }

        private static TerrainSurfaceVariant[] GetRules(MapThemeData theme, MapTileType type)
        {
            return type switch
            {
                MapTileType.Place => theme.placeSurfaceRules,
                MapTileType.Walk => theme.walkSurfaceRules,
                MapTileType.Env => theme.envSurfaceRules,
                MapTileType.Deco => theme.decoSurfaceRules,
                _ => null
            };
        }

        private static Texture2D[] GetLegacyVariants(MapThemeData theme, MapTileType type)
        {
            return type switch
            {
                MapTileType.Place => theme.placeTileVariants,
                MapTileType.Walk => theme.walkTileVariants,
                MapTileType.Env => theme.envTileVariants,
                MapTileType.Deco => theme.decoTileVariants,
                _ => null
            };
        }

        private static Texture2D GetSingleTexture(MapThemeData theme, MapTileType type)
        {
            return type switch
            {
                MapTileType.Place => theme.placeTileTexture,
                MapTileType.Walk => theme.walkTileTexture,
                MapTileType.Env => theme.envTileTexture,
                MapTileType.Deco => theme.decoTileTexture,
                _ => null
            };
        }

        private static byte GetWalkMask(GeneratedMap map, int x, int y)
        {
            if (!map.IsCreated)
                return 0;

            int width = map.gridSize.x;
            int height = map.gridSize.y;
            byte mask = 0;
            if (y + 1 < height && map.TileAt(new int2(x, y + 1)) == MapTileType.Walk) mask |= 1;
            if (x + 1 < width && map.TileAt(new int2(x + 1, y)) == MapTileType.Walk) mask |= 2;
            if (y - 1 >= 0 && map.TileAt(new int2(x, y - 1)) == MapTileType.Walk) mask |= 4;
            if (x - 1 >= 0 && map.TileAt(new int2(x - 1, y)) == MapTileType.Walk) mask |= 8;
            return mask;
        }

        private static float HashTo01(uint hash)
            => (hash & 0x00ffffff) / 16777215f;

        private static void AddWalkShapeTextures(MapThemeData theme, List<Texture2D> textures)
        {
            AddTexture(theme.walkSingleTexture, textures);
            AddTexture(theme.walkStraightNSTexture, textures);
            AddTexture(theme.walkStraightEWTexture, textures);
            AddTexture(theme.walkCornerTexture, textures);
            AddTexture(theme.walkEndTexture, textures);
            AddTexture(theme.walkTJunctionTexture, textures);
            AddTexture(theme.walkCrossTexture, textures);
        }

        private static void AddTexture(Texture2D texture, List<Texture2D> textures)
        {
            if (texture != null && !textures.Contains(texture))
                textures.Add(texture);
        }

        private static Texture2D GetWalkShapeTexture(MapThemeData theme, BoardShapeType shape)
        {
            return shape switch
            {
                BoardShapeType.Isolated => theme.walkSingleTexture,
                BoardShapeType.EndN => theme.walkEndTexture,
                BoardShapeType.EndE => theme.walkEndTexture,
                BoardShapeType.EndS => theme.walkEndTexture,
                BoardShapeType.EndW => theme.walkEndTexture,
                BoardShapeType.StraightNS => theme.walkStraightNSTexture,
                BoardShapeType.StraightEW => theme.walkStraightEWTexture,
                BoardShapeType.OuterCornerNE => theme.walkCornerTexture,
                BoardShapeType.OuterCornerNW => theme.walkCornerTexture,
                BoardShapeType.OuterCornerSE => theme.walkCornerTexture,
                BoardShapeType.OuterCornerSW => theme.walkCornerTexture,
                BoardShapeType.TJunctionN => theme.walkTJunctionTexture,
                BoardShapeType.TJunctionE => theme.walkTJunctionTexture,
                BoardShapeType.TJunctionS => theme.walkTJunctionTexture,
                BoardShapeType.TJunctionW => theme.walkTJunctionTexture,
                BoardShapeType.Cross => theme.walkCrossTexture,
                _ => null
            };
        }

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)seed;
                h ^= (uint)(x * 374761393);
                h ^= (uint)(y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h & 0x00ffffff) / 16777215f;
            }
        }
    }
}
