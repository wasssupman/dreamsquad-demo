using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.EditorTools
{
    // map-diorama-stage unit 11 — Duel 스테이지 절차 조립기 (Street 제작방식: 바닥 Plane + 스프라이트 프랍 + 마커).
    // 판형 정본 = main 현행 MapDocument_Duel(23×10 열린 마당). 아트는 Street 에셋 placeholder — Duel 전용
    // 아트가 오면 아래 경로 상수만 바꾼다. 멱등: 재실행 시 프리팹을 통째로 다시 쓴다(풀 참조는 Register 가 갱신).
    public static class MapStageDuelGenerator
    {
        public const string PrefabPath = "Assets/_Project/Art/Theme/duel/MapStage_Duel.prefab";
        const string ProfilePath = "Assets/_Project/Scenes/BattleScene/Duel.asset";
        const string ProfileSourcePath = "Assets/_Project/Scenes/BattleScene/Street.asset";
        const string PoolPath = "Assets/_Project/Data/Maps/MapStagePool.asset";
        const string DeckPath = "Assets/_Project/Scripts/Data/Decks/Deck_Duel.asset";

        // Street 에셋 (placeholder)
        const string FloorMat = "Assets/_Project/Art/Theme/street/M_Street_Floor.mat";
        const string SpriteMat = "Assets/_Project/Art/SpriteShadowCaster.mat";
        const string Backdrop = "Assets/_Project/Art/Theme/street/image 2614.png";
        const string WallTile = "Assets/_Project/Art/Theme/street/image 2622.png";
        const string PropTall = "Assets/_Project/Art/Theme/street/image 2623.png";
        const string PropLow = "Assets/_Project/Art/Theme/street/image 2624.png";
        const string PropWide = "Assets/_Project/Art/Theme/street/image 2625.png";

        const string GuardInstinct = "Assets/_Project/Data/Structures/Structure_GuardInstinct.asset";
        const string WatchInstinct = "Assets/_Project/Data/Structures/Structure_WatchInstinct.asset";
        const string EnemyHeart = "Assets/_Project/Data/Structures/Structure_EnemyHeart.asset";

        const int W = 23, H = 10;
        const float OriginY = 0.19f;          // Street 와 같은 발바닥 높이(바닥 Plane 은 y 0)
        const float Tilt = 30f;               // Street 프랍 틸트(카메라 pitch 55 에 맞춘 저작값)
        static readonly Color BackdropTint = new Color(0.5377f, 0.5377f, 0.5377f, 1f);

        [MenuItem("Window/Wassup/Map Stage/Generate Duel Stage")]
        public static void GenerateMenu() => Debug.Log(Generate());

        public static string Generate()
        {
            var floorMat = Load<Material>(FloorMat);
            var spriteMat = Load<Material>(SpriteMat);
            var guard = Load<StructureData>(GuardInstinct);
            var watch = Load<StructureData>(WatchInstinct);
            var heart = Load<StructureData>(EnemyHeart);
            var profile = EnsureProfile();

            EnsureFolder("Assets/_Project/Art/Theme/duel");
            var root = new GameObject("MapStage_Duel");
            try
            {
                var stage = root.AddComponent<MapStage>();
                stage.playAreaCells = new Vector2Int(W, H);
                stage.gridOriginLocal = new Vector3(0f, OriginY, 0f);
                stage.previewTileSize = 1f;
                stage.suppressEffectTiles = false;

                // ── 바닥: 본판 + 좌/우/전방 확장 (Street 패턴 — 16:9 가장자리 공백 방지) ──
                Floor(root.transform, "Floor", floorMat, new Vector3(W * 0.5f, 0f, H * 0.5f), new Vector3(2.4f, 1f, 1.5f));
                Floor(root.transform, "Floor_L", floorMat, new Vector3(W * 0.5f - 24f, 0f, H * 0.5f), new Vector3(2.4f, 1f, 1.5f));
                Floor(root.transform, "Floor_R", floorMat, new Vector3(W * 0.5f + 24f, 0f, H * 0.5f), new Vector3(2.4f, 1f, 1.5f));
                Floor(root.transform, "Floor_Front", floorMat, new Vector3(W * 0.5f, -2.61f, -10f), new Vector3(8.7f, 1f, 1.5f));

                // ── 배경/장식 (placeholder — Street 스프라이트) ──
                var backdrop = SpriteProp(root.transform, "Backdrop", Load<Sprite>(Backdrop), spriteMat, BackdropTint);
                backdrop.transform.localPosition = new Vector3(W * 0.5f, 2.84f, H + 12.6f);
                backdrop.transform.localScale = new Vector3(6.5457f, 4.8831f, 1f);
                GroundedSprite(root.transform, "Deco_0", Load<Sprite>(PropWide), spriteMat, 2.0f, 2.5f, H + 0.3f);
                GroundedSprite(root.transform, "Deco_1", Load<Sprite>(PropTall), spriteMat, 7.5f, 2.5f, H + 0.3f);
                GroundedSprite(root.transform, "Deco_2", Load<Sprite>(PropLow), spriteMat, 13f, 2.5f, H + 0.3f);
                GroundedSprite(root.transform, "Deco_3", Load<Sprite>(PropTall), spriteMat, 18.5f, 2.5f, H + 0.3f);   // flipX 금지 — 간판 텍스트가 뒤집힌다
                GroundedSprite(root.transform, "Deco_4", Load<Sprite>(PropWide), spriteMat, 22.5f, 2.5f, H + 0.3f);

                // ── 중앙 분리대 x=11: y0 · y3~6 · y9 (통로 y1~2, y7~8) ──
                var wall = Load<Sprite>(WallTile);
                Divider(root, "divider_s", new Vector2Int(11, 0), 1, wall, spriteMat);
                Divider(root, "divider_c", new Vector2Int(11, 3), 4, wall, spriteMat);
                Divider(root, "divider_n", new Vector2Int(11, 9), 1, wall, spriteMat);

                // ── 적 진영 배치 금지 x=17..22 (main placeMask 04 구역) ──
                Host(root, "enemy_zone", new Vector2Int(17, 0)).AddComponent<PlacementBlockZone>().size = new Vector2Int(6, 10);

                // ── 적 마음 자리 (20,4) — 계약 11: 사이 아님, 장식 + 배치 금지 1칸 ──
                var heartHost = Host(root, "enemy_heart", new Vector2Int(20, 4));
                heartHost.AddComponent<PlacementBlockZone>().size = Vector2Int.one;
                if (heart != null && heart.viewPrefab != null)
                {
                    var view = (GameObject)PrefabUtility.InstantiatePrefab(heart.viewPrefab);
                    view.transform.SetParent(heartHost.transform, false);
                    view.transform.localScale *= heart.viewScale;
                }

                // ── 마커 ──
                Host(root, "spawn0", new Vector2Int(20, 3)).AddComponent<SpawnMarker>().laneIndex = 0;
                Host(root, "spawn1", new Vector2Int(20, 5)).AddComponent<SpawnMarker>().laneIndex = 1;
                Host(root, "goal", new Vector2Int(2, 4)).AddComponent<GoalMarker>();
                Host(root, "bonus_portal_0", new Vector2Int(11, 2)).AddComponent<BonusSpawnMarker>();
                Host(root, "bonus_portal_1", new Vector2Int(11, 7)).AddComponent<BonusSpawnMarker>();
                Instinct(root, "instinct_ally_a", new Vector2Int(4, 2), StructureSide.Defender, guard);
                Instinct(root, "instinct_ally_b", new Vector2Int(4, 7), StructureSide.Defender, guard);
                Instinct(root, "instinct_enemy_a", new Vector2Int(18, 2), StructureSide.Enemy, watch);
                Instinct(root, "instinct_enemy_b", new Vector2Int(18, 7), StructureSide.Enemy, watch);

                // ── 포스트 볼륨 (브리지 PushStagePostVolume 이 스테이지 수명으로 넘긴다) ──
                var post = new GameObject("Post");
                post.transform.SetParent(root.transform, false);
                var volume = post.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 0;
                volume.sharedProfile = profile;

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                string reg = RegisterLive(prefab.GetComponent<MapStage>());
                return $"OK|Duel stage {W}x{H} → {PrefabPath} | {reg}";
            }
            finally { Object.DestroyImmediate(root); }
        }

        // 풀 entries[0] = Duel + Deck_Duel (fallback0/직접 Play = Duel — main 과 동일). 같은 이름의 엔트리가 있으면 참조만 갱신.
        static string RegisterLive(MapStage stage)
        {
            var pool = Load<MapStagePool>(PoolPath);
            var deck = Load<AttackDeck>(DeckPath);
            if (pool == null || stage == null) return "pool/stage 없음";
            bool changed = pool.EditorUpsertLiveEntry(stage, deck, null, insertIndex: 0);
            if (changed) { EditorUtility.SetDirty(pool); AssetDatabase.SaveAssets(); }
            return changed ? "pool entries[0] = Duel(Deck_Duel)" : "pool 변경 없음";
        }

        static VolumeProfile EnsureProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (existing != null) return existing;
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfileSourcePath) == null) return null;
            AssetDatabase.CopyAsset(ProfileSourcePath, ProfilePath);
            return AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) Debug.LogWarning($"[MapStageDuelGenerator] 에셋 없음: {path}");
            return a;
        }

        static void Floor(Transform parent, string name, Material mat, Vector3 pos, Vector3 scale)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = name;
            plane.transform.SetParent(parent, false);
            plane.transform.localPosition = pos;
            plane.transform.localScale = scale;
            if (mat != null) plane.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static GameObject SpriteProp(Transform parent, string name, Sprite sprite, Material mat, Color tint, bool flipX = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(Tilt, 0f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (mat != null) sr.sharedMaterial = mat;
            sr.color = tint;
            sr.flipX = flipX;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            return go;
        }

        // 틸트된 스프라이트의 아랫변이 (x, 바닥 y0, frontZ) 에 닿게 놓는다.
        static GameObject GroundedSprite(Transform parent, string name, Sprite sprite, Material mat, float x, float scale, float frontZ, bool flipX = false)
        {
            var go = SpriteProp(parent, name, sprite, mat, Color.white, flipX);
            go.transform.localScale = Vector3.one * scale;
            float h = (sprite != null ? sprite.bounds.size.y : 1f) * scale;
            float c = Mathf.Cos(Tilt * Mathf.Deg2Rad), s = Mathf.Sin(Tilt * Mathf.Deg2Rad);
            go.transform.localPosition = new Vector3(x, 0.5f * h * c - 0.05f, frontZ + 0.5f * h * s);
            return go;
        }

        // 분리대: footprint 호스트 1개(1×n) + 셀마다 벽 타일 스프라이트.
        static void Divider(GameObject root, string name, Vector2Int cell, int length, Sprite tile, Material mat)
        {
            var host = Host(root, name, cell);
            var fp = host.AddComponent<PropFootprint>();
            fp.size = new Vector2Int(1, length);
            fp.anchorOffset = Vector2Int.zero;
            for (int i = 0; i < length; i++)
            {
                // 호스트는 셀 중심(+0.5)·발바닥 높이(OriginY)에 있다 — 스프라이트 아랫변을 셀 앞변(z −0.5)·바닥(y 0)에.
                var go = GroundedSprite(host.transform, $"tile_{i}", tile, mat, 0f, 1.3f, -0.5f + i + 0.15f);
                go.transform.localPosition += new Vector3(0f, -OriginY, 0f);
                go.GetComponent<SpriteRenderer>().sortingOrder = 100 - i;
            }
        }

        static void Instinct(GameObject root, string name, Vector2Int cell, StructureSide side, StructureData data)
        {
            var m = Host(root, name, cell).AddComponent<StructureMarker>();
            m.side = side;
            m.data = data;
        }

        static GameObject Host(GameObject root, string name, Vector2Int cell)
        {
            var stage = root.GetComponent<MapStage>();
            var host = new GameObject(name);
            host.transform.SetParent(root.transform, false);
            host.transform.localPosition = stage.gridOriginLocal + new Vector3(cell.x + 0.5f, 0f, cell.y + 0.5f);
            return host;
        }
    }
}
