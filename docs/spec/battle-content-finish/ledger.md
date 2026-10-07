# ledger — battle-content-finish 가 지우는 에셋 전수 (2026-10-07 재검증)

판정 두 방향이 일치한 것만 싣는다 — **역방향**(GUID 인바운드 0 → 추이적 고아 · basename 이 `.cs` 에 없음)과 **순방향**(씬 `BattleCoreScene` + `ProjectSettings` + `Assets/Resources` + 코드가 경로로 여는 파일 41 을 루트로 GUID 를 따라가 닿지 않음). 순방향 검사에서 A 집합 가운데 「닿는 것」은 **0** 이었다.

재현: 세션 스크래치 `guidrefs.py index` → `verify_spec.py`(역방향 · 코드 · 패키지) → `reach.py`(순방향) → `ledger.py`. 테스트가 **폴더째 스캔**하는 경로(`Data/Defenders` 등 21)는 루트로 치지 않았다 — 스캔은 검증이지 인스턴스화가 아니다.

| 묶음 | 파일 | MB | 처분 |
|---|---|---|---|
| A. `_Project` 고아 폐포 + PrimeTween Demo | 306 | 91.2 | 삭제 (단위 2) |
| B. A 가 사라지면 닿지 않게 되는 벤더 파일 | 127 | 51.1 | 삭제 (단위 2 — 벤더 재추림) |
| C. 테마 풀 밖 프롭 17 세트 + 효과 타일 2 세트 | 55 | 4.2 | **D7** 결정 |
| D. 비활성 카드 `Card_IncubusPact` 세트 | 3 | 2.05 | **D8** 결정 |
| 닿지 않지만 **남기는 것** | 10 + Spine/TMP 패키지 내부 143 + Resources 4 | — | 아래 |

## 남기는 것 (닿지 않아도)

- `Assets/Editor/SpineSettings.asset` — Spine 런타임 설정
- `Assets/_Project/Data/Dreamcatcher/DreamcatcherDeck_Default.asset` — D2 — 기본 덱 그릇으로 재사용
- `Assets/_Project/Editor/DefenderPortraits/DefenderPortraitBakeProfile.asset` — 초상 베이커 설정(에디터 도구가 경로로 연다)
- `Assets/_Project/Scripts/Data/Decks/Deck_Ford.asset` — 테스트가 경로로 연다
- `Assets/_Project/Scripts/Data/Decks/Deck_Hook.asset` — 테스트가 경로로 연다
- `Assets/_Project/Scripts/Data/Decks/Deck_Isle.asset` — 테스트가 경로로 연다
- `Assets/_Project/Scripts/Data/Decks/Deck_Spiral.asset` — 테스트가 경로로 연다
- `Assets/_Project/Scripts/Data/Decks/Deck_Twin.asset` — 테스트가 경로로 연다
- `Assets/_Project/Scripts/Data/Decks/WaveA.asset` — 테스트가 경로로 연다
- `Assets/_Project/Scripts/Data/WavePlans/WavePlan_BossTest.asset` — 테스트가 경로로 연다
- `Assets/Spine/Runtime|Editor/**` 128 · `Assets/TextMesh Pro/Shaders/**` 15 — spine-unity · TMP 패키지 내부(셰이더 · 기본 머티리얼 · 에디터 아이콘). 콘텐츠가 아니라 런타임이다.
- `Assets/Resources/RuntimeMaterials/*.mat` 4 — 경로 로드. 단위 4 가 SO 참조로 옮긴다.

## A. `_Project` 고아 폐포 + PrimeTween Demo — 306 파일 · 91.2 MB

### Plugins/PrimeTween/Demo (46 · 11.83 MB)

- Assets/Plugins/PrimeTween/Demo/Demo.unity
- Assets/Plugins/PrimeTween/Demo/Demo_URP.unity
- Assets/Plugins/PrimeTween/Demo/Scripts/Animatable.cs
- Assets/Plugins/PrimeTween/Demo/Scripts/Baggage.cs
- Assets/Plugins/PrimeTween/Demo/Scripts/CameraController.cs
- Assets/Plugins/PrimeTween/Demo/Scripts/CameraProjectionMatrixAnimation.cs
- Assets/Plugins/PrimeTween/Demo/Scripts/Demo.cs
- Assets/Plugins/PrimeTween/Demo/Scripts/DirectionalLightController.cs
- Assets/Plugins/PrimeTween/Demo/Scripts/Door.cs
- Assets/Plugins/PrimeTween/Demo/Scripts/Headlights.cs
- Assets/Plugins/PrimeTween/Demo/Scripts/HighlightableElement.cs
- Assets/Plugins/PrimeTween/Demo/Scripts/HighlightedElementController.cs
- Assets/Plugins/PrimeTween/Demo/Scripts/InputController.cs
- Assets/Plugins/PrimeTween/Demo/Scripts/JumpAnimation.cs
- Assets/Plugins/PrimeTween/Demo/Scripts/MeasureAllocations/DebugInfo.cs
- Assets/Plugins/PrimeTween/Demo/Scripts/MeasureAllocations/MeasureMemoryAllocations.cs
- Assets/Plugins/PrimeTween/Demo/Scripts/MeasureAllocations/PrimeTween.Debug.asmdef
- Assets/Plugins/PrimeTween/Demo/Scripts/PrimeTween.Demo.asmdef
- Assets/Plugins/PrimeTween/Demo/Scripts/Road.cs
- Assets/Plugins/PrimeTween/Demo/Scripts/SlidingDoor.cs
- Assets/Plugins/PrimeTween/Demo/Scripts/SqueezeAnimation.cs
- Assets/Plugins/PrimeTween/Demo/Scripts/SwipeTutorial.cs
- Assets/Plugins/PrimeTween/Demo/Scripts/TypewriterAnimatorExample.cs
- Assets/Plugins/PrimeTween/Demo/Scripts/Wheels.cs
- Assets/Plugins/PrimeTween/Demo/Stylized Cartoon Van by Fero Andezo/CREDITS.txt
- Assets/Plugins/PrimeTween/Demo/Stylized Cartoon Van by Fero Andezo/Exterior.png
- Assets/Plugins/PrimeTween/Demo/Stylized Cartoon Van by Fero Andezo/Interior.png
- Assets/Plugins/PrimeTween/Demo/Stylized Cartoon Van by Fero Andezo/Materials/Exterior.mat
- Assets/Plugins/PrimeTween/Demo/Stylized Cartoon Van by Fero Andezo/Materials/Wheel.mat
- Assets/Plugins/PrimeTween/Demo/Stylized Cartoon Van by Fero Andezo/Materials/Window.mat
- Assets/Plugins/PrimeTween/Demo/Stylized Cartoon Van by Fero Andezo/Materials/interior.mat
- Assets/Plugins/PrimeTween/Demo/Stylized Cartoon Van by Fero Andezo/Materials_URP/Exterior.mat
- Assets/Plugins/PrimeTween/Demo/Stylized Cartoon Van by Fero Andezo/Materials_URP/Wheel.mat
- Assets/Plugins/PrimeTween/Demo/Stylized Cartoon Van by Fero Andezo/Materials_URP/Window.mat
- Assets/Plugins/PrimeTween/Demo/Stylized Cartoon Van by Fero Andezo/Materials_URP/interior.mat
- Assets/Plugins/PrimeTween/Demo/Stylized Cartoon Van by Fero Andezo/Van.fbx
- Assets/Plugins/PrimeTween/Demo/Stylized Cartoon Van by Fero Andezo/Wheel.png
- Assets/Plugins/PrimeTween/Demo/Stylized Cartoon Van by Fero Andezo/Window.png
- Assets/Plugins/PrimeTween/Demo/Stylized Sand by Joao Paulo/CREDITS.txt
- Assets/Plugins/PrimeTween/Demo/Stylized Sand by Joao Paulo/Stylized_Sand_001_ambientOcclusion.jpg
- Assets/Plugins/PrimeTween/Demo/Stylized Sand by Joao Paulo/Stylized_Sand_001_basecolor.jpg
- Assets/Plugins/PrimeTween/Demo/Stylized Sand by Joao Paulo/Stylized_Sand_001_basecolor.mat
- Assets/Plugins/PrimeTween/Demo/Stylized Sand by Joao Paulo/Stylized_Sand_001_basecolor_URP.mat
- Assets/Plugins/PrimeTween/Demo/Stylized Sand by Joao Paulo/Stylized_Sand_001_height.png
- Assets/Plugins/PrimeTween/Demo/Stylized Sand by Joao Paulo/Stylized_Sand_001_normal.jpg
- Assets/Plugins/PrimeTween/Demo/Stylized Sand by Joao Paulo/Stylized_Sand_001_roughness.jpg

### _Project/Art/DreamcatcherCards (6 · 11.70 MB)

- Assets/_Project/Art/DreamcatcherCards/CasualStatCards/card_attack_power_test_01.png
- Assets/_Project/Art/DreamcatcherCards/CasualStatCards/card_attack_speed_test_01.png
- Assets/_Project/Art/DreamcatcherCards/CasualStatCards/card_cost_production_test_01.png
- Assets/_Project/Art/DreamcatcherCards/CasualStatCards/card_health_test_01.png
- Assets/_Project/Art/DreamcatcherCards/dreamcatcher_tarot_test_01.png
- Assets/_Project/Art/DreamcatcherCards/dreamcatcher_tarot_test_02_effect.png

### _Project/Art/KeyringCordShine.mat (1 · 0.00 MB)

- Assets/_Project/Art/KeyringCordShine.mat

### _Project/Art/Theme (56 · 34.84 MB)

- Assets/_Project/Art/Theme/BG.png
- Assets/_Project/Art/Theme/MapStage_Hello.prefab
- Assets/_Project/Art/Theme/bot-left.png
- Assets/_Project/Art/Theme/bot-right.png
- Assets/_Project/Art/Theme/bot.png
- Assets/_Project/Art/Theme/building/M_Building_Floor.mat
- Assets/_Project/Art/Theme/building/MapStage_Building.prefab
- Assets/_Project/Art/Theme/building/future.png
- Assets/_Project/Art/Theme/building/future_bg.png
- Assets/_Project/Art/Theme/building/future_far.png
- Assets/_Project/Art/Theme/building/future_floor.png
- Assets/_Project/Art/Theme/building/future_floor2.png
- Assets/_Project/Art/Theme/building/future_prop1.png
- Assets/_Project/Art/Theme/building/future_prop2.png
- Assets/_Project/Art/Theme/building/future_prop3.png
- Assets/_Project/Art/Theme/building/future_prop4.png
- Assets/_Project/Art/Theme/building/future_prop5.png
- Assets/_Project/Art/Theme/building/future_prop6.png
- Assets/_Project/Art/Theme/deco1.png
- Assets/_Project/Art/Theme/deco2.png
- Assets/_Project/Art/Theme/deco3.png
- Assets/_Project/Art/Theme/deco4.png
- Assets/_Project/Art/Theme/forest/forest_env_surface_atlas_v2.png
- Assets/_Project/Art/Theme/forest/forest_place_surface_atlas_v2.png
- Assets/_Project/Art/Theme/forest/forest_transition_decal_atlas_v2.png
- Assets/_Project/Art/Theme/forest/tile_deco.png
- Assets/_Project/Art/Theme/forest/tile_env.png
- Assets/_Project/Art/Theme/forest/tile_forest_biggrass2.png
- Assets/_Project/Art/Theme/forest/tile_forest_env_detail_patch_v2.png
- Assets/_Project/Art/Theme/forest/tile_forest_grass1.png
- Assets/_Project/Art/Theme/forest/tile_forest_grass2.png
- Assets/_Project/Art/Theme/forest/tile_forest_place_edge.png
- Assets/_Project/Art/Theme/forest/tile_forest_place_grass_edge.png
- Assets/_Project/Art/Theme/forest/tile_forest_place_inner_corner.png
- Assets/_Project/Art/Theme/forest/tile_forest_place_outer_corner.png
- Assets/_Project/Art/Theme/forest/tile_forest_smallgrass1.png
- Assets/_Project/Art/Theme/forest/tile_forest_walk_corner.png
- Assets/_Project/Art/Theme/forest/tile_forest_walk_cross.png
- Assets/_Project/Art/Theme/forest/tile_forest_walk_end.png
- Assets/_Project/Art/Theme/forest/tile_forest_walk_straight_ew.png
- Assets/_Project/Art/Theme/forest/tile_forest_walk_straight_ns.png
- Assets/_Project/Art/Theme/forest/tile_forest_walk_t_junction.png
- Assets/_Project/Art/Theme/forest/tile_place.png
- Assets/_Project/Art/Theme/forest/tile_place_variant_a.png
- Assets/_Project/Art/Theme/forest/tile_place_variant_b.png
- Assets/_Project/Art/Theme/forest/tile_place_variant_c.png
- Assets/_Project/Art/Theme/forest/tile_place_variant_d.png
- Assets/_Project/Art/Theme/forest/tile_walk.png
- Assets/_Project/Art/Theme/street/image 2626.png
- Assets/_Project/Art/Theme/subway/06.png
- Assets/_Project/Art/Theme/subway/07.png
- Assets/_Project/Art/Theme/subway/prop5.png
- Assets/_Project/Art/Theme/top1.png
- Assets/_Project/Art/Theme/top2.png
- Assets/_Project/Art/Theme/top3-sky.png
- Assets/_Project/Art/Theme/레이어 12.png

### _Project/Art/TileShadowReceive.mat (1 · 0.00 MB)

- Assets/_Project/Art/TileShadowReceive.mat

### _Project/Art/dissolve_noise.png (1 · 0.02 MB)

- Assets/_Project/Art/dissolve_noise.png

### _Project/Audio/BattleBgm.mp3 (1 · 0.96 MB)

- Assets/_Project/Audio/BattleBgm.mp3

### _Project/Audio/DeployVoice (16 · 0.40 MB)

- Assets/_Project/Audio/DeployVoice/Deploy_Archer.mp3
- Assets/_Project/Audio/DeployVoice/Deploy_Artillery.mp3
- Assets/_Project/Audio/DeployVoice/Deploy_Bastion.mp3
- Assets/_Project/Audio/DeployVoice/Deploy_BlockingCaster.mp3
- Assets/_Project/Audio/DeployVoice/Deploy_Bruiser.mp3
- Assets/_Project/Audio/DeployVoice/Deploy_Cannon.mp3
- Assets/_Project/Audio/DeployVoice/Deploy_FireCaster.mp3
- Assets/_Project/Audio/DeployVoice/Deploy_Guardian.mp3
- Assets/_Project/Audio/DeployVoice/Deploy_Healer.mp3
- Assets/_Project/Audio/DeployVoice/Deploy_IceCaster.mp3
- Assets/_Project/Audio/DeployVoice/Deploy_Marksman.mp3
- Assets/_Project/Audio/DeployVoice/Deploy_Piercer.mp3
- Assets/_Project/Audio/DeployVoice/Deploy_PoisonCaster.mp3
- Assets/_Project/Audio/DeployVoice/Deploy_Ranger.mp3
- Assets/_Project/Audio/DeployVoice/Deploy_Scout.mp3
- Assets/_Project/Audio/DeployVoice/Deploy_Sniper.mp3

### _Project/Audio/GimmickRevealTakes (3 · 0.05 MB)

- Assets/_Project/Audio/GimmickRevealTakes/Take1_stamp_shimmer.mp3
- Assets/_Project/Audio/GimmickRevealTakes/Take2_subthump_sparkle.mp3
- Assets/_Project/Audio/GimmickRevealTakes/Take3_whoosh_bell.mp3

### _Project/Data/Config (2 · 0.00 MB)

- Assets/_Project/Data/Config/RelocationSettings.asset
- Assets/_Project/Data/Config/ScoreRules.asset

### _Project/Data/Enemies (3 · 0.01 MB)

- Assets/_Project/Data/Enemies/Enemy_WaypointAir.asset
- Assets/_Project/Data/Enemies/Enemy_WaypointBasic.asset
- Assets/_Project/Data/Enemies/Enemy_WaypointBasicAlt.asset

### _Project/Data/Flipbook (1 · 0.00 MB)

- Assets/_Project/Data/Flipbook/Test/Flipbook_idle.asset

### _Project/Data/Maps (1 · 0.00 MB)

- Assets/_Project/Data/Maps/MapDocument_MovementStress.asset

### _Project/Data/Materials (3 · 0.01 MB)

- Assets/_Project/Data/Materials/HazardVisual_Fire_Mat.mat
- Assets/_Project/Data/Materials/HazardVisual_Ice_Mat.mat
- Assets/_Project/Data/Materials/HazardVisual_Poison_Mat.mat

### _Project/Data/Projectiles (49 · 0.06 MB)

- Assets/_Project/Data/Projectiles/Projectile_Arrow.asset
- Assets/_Project/Data/Projectiles/Projectile_Arrow02_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Arrow03_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Arrow04_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Arrow05_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Arrow06_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Arrow07_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Arrow08_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Arrow09_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Arrow10_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Arrow11_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Arrow14_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Arrow15_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Arrow16_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Arrow17_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Arrow18_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Arrow19_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Arrow20_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Arrow21_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Arrow22_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Axe01_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Axe02_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Axe03_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_BlinkPuff.asset
- Assets/_Project/Data/Projectiles/Projectile_Bolt.asset
- Assets/_Project/Data/Projectiles/Projectile_CannonBall.asset
- Assets/_Project/Data/Projectiles/Projectile_Card02_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Card03_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Card04_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_CardsThrow01_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Cylinder01_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Cylinder02_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Cylinder03_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Cylinder04_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_ExplosiveBullet02_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_ExplosiveBullet03_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_ExplosiveBullet_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Rock02_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Rock03_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Rock_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_RotatingSpheres02_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_RotatingSpheres03_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_RotatingSpheres04_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Shard02_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Shard03_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Shard_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Shuriken02_GA.asset
- Assets/_Project/Data/Projectiles/Projectile_Sniper_Crimson.asset
- Assets/_Project/Data/Projectiles/Projectile_WhipPulse.asset

### _Project/Data/Sprites (1 · 0.00 MB)

- Assets/_Project/Data/Sprites/Sprite_Dot.png

### _Project/Data/Structures (3 · 0.00 MB)

- Assets/_Project/Data/Structures/Structure_EnemyCore.asset
- Assets/_Project/Data/Structures/Structure_EnemyHeart.asset
- Assets/_Project/Data/Structures/Structure_TestInstinct.asset

### _Project/Data/Theme (1 · 0.00 MB)

- Assets/_Project/Data/Theme/forest/struct_spawn_arch.asset

### _Project/Fonts/DamageNumber Outline Mat.mat (1 · 0.00 MB)

- Assets/_Project/Fonts/DamageNumber Outline Mat.mat

### _Project/Fonts/Score Outline Mat.mat (1 · 0.00 MB)

- Assets/_Project/Fonts/Score Outline Mat.mat

### _Project/Generated/Enemies (3 · 4.30 MB)

- Assets/_Project/Generated/Enemies/ConceptSources/Enemy_Needler_Source.png
- Assets/_Project/Generated/Enemies/ConceptSources/Enemy_Rootcaster_Source.png
- Assets/_Project/Generated/Enemies/ConceptSources/Enemy_Runner_Source.png

### _Project/Generated/Projectiles (12 · 2.09 MB)

- Assets/_Project/Generated/Projectiles/Textures/fire_var0.png
- Assets/_Project/Generated/Projectiles/Textures/fire_var1.png
- Assets/_Project/Generated/Projectiles/Textures/fire_var2.png
- Assets/_Project/Generated/Projectiles/Textures/stone_var0.png
- Assets/_Project/Generated/Projectiles/Textures/stone_var1.png
- Assets/_Project/Generated/Projectiles/Textures/stone_var2.png
- Assets/_Project/Generated/Projectiles/Textures/water_var0.png
- Assets/_Project/Generated/Projectiles/Textures/water_var1.png
- Assets/_Project/Generated/Projectiles/Textures/water_var2.png
- Assets/_Project/Generated/Projectiles/Textures/wind_var0.png
- Assets/_Project/Generated/Projectiles/Textures/wind_var1.png
- Assets/_Project/Generated/Projectiles/Textures/wind_var2.png

### _Project/Generated/Props (1 · 1.43 MB)

- Assets/_Project/Generated/Props/ConceptSources/concept_prop_sheet_alpha.png

### _Project/Materials/terrain.mat (1 · 0.00 MB)

- Assets/_Project/Materials/terrain.mat

### _Project/Prefabs/Characters (2 · 0.01 MB)

- Assets/_Project/Prefabs/Characters/SpriteCharacter.prefab
- Assets/_Project/Prefabs/Characters/SpriteCharacter_Test.prefab

### _Project/Prefabs/Hazards (1 · 0.00 MB)

- Assets/_Project/Prefabs/Hazards/BlockingHazard_RockPlaceholder.mat

### _Project/Scripts/Data (8 · 0.02 MB)

- Assets/_Project/Scripts/Data/Decks/Deck_SiegeTest.asset
- Assets/_Project/Scripts/Data/Decks/Deck_WaypointLab.asset
- Assets/_Project/Scripts/Data/Decks/WaveB.asset
- Assets/_Project/Scripts/Data/WavePlans/WavePlan_DragonTest.asset
- Assets/_Project/Scripts/Data/WavePlans/WavePlan_JjangssenTest.asset
- Assets/_Project/Scripts/Data/WavePlans/WavePlan_MamemoTest.asset
- Assets/_Project/Scripts/Data/WavePlans/WavePlan_Sample.asset
- Assets/_Project/Scripts/Data/WavePlans/WavePlan_SlimeTest.asset

### _Project/Shaders/Backdrop_Unlit.shader (1 · 0.00 MB)

- Assets/_Project/Shaders/Backdrop_Unlit.shader

### _Project/Shaders/Tile_ShadowReceive.shader (1 · 0.00 MB)

- Assets/_Project/Shaders/Tile_ShadowReceive.shader

### _Project/Shaders/UICordShine.shader (1 · 0.00 MB)

- Assets/_Project/Shaders/UICordShine.shader

### _Project/Shaders/UI_Additive.shader (1 · 0.00 MB)

- Assets/_Project/Shaders/UI_Additive.shader

### _Project/Spine/CH3.png (1 · 0.21 MB)

- Assets/_Project/Spine/CH3.png

### _Project/Spine/CH3_Atlas.asset (1 · 0.00 MB)

- Assets/_Project/Spine/CH3_Atlas.asset

### _Project/Spine/CH3_Material.mat (1 · 0.00 MB)

- Assets/_Project/Spine/CH3_Material.mat

### _Project/Spine/CH3_SkeletonData.asset (1 · 0.00 MB)

- Assets/_Project/Spine/CH3_SkeletonData.asset

### _Project/Spine/Doll.png (1 · 0.01 MB)

- Assets/_Project/Spine/Doll.png

### _Project/Spine/Doll_Atlas.asset (1 · 0.00 MB)

- Assets/_Project/Spine/Doll_Atlas.asset

### _Project/Spine/Doll_Material.mat (1 · 0.00 MB)

- Assets/_Project/Spine/Doll_Material.mat

### _Project/Sprites/Keyring (2 · 0.02 MB)

- Assets/_Project/Sprites/Keyring/keyring_cord.png
- Assets/_Project/Sprites/Keyring/keyring_ring.png

### _Project/Sprites/Unit (1 · 1.25 MB)

- Assets/_Project/Sprites/Unit/Test/idle.png

### _Project/VFX/MalphiteHitEarth.prefab (1 · 2.02 MB)

- Assets/_Project/VFX/MalphiteHitEarth.prefab

### _Project/VFX/Materials (10 · 0.04 MB)

- Assets/_Project/VFX/Materials/Meteor_Mat.mat
- Assets/_Project/VFX/Materials/Placement_Mat.mat
- Assets/_Project/VFX/Materials/Portal_BeamMat.mat
- Assets/_Project/VFX/Materials/Portal_Mat.mat
- Assets/_Project/VFX/Materials/Skybox_Game.mat
- Assets/_Project/VFX/Materials/Tornado_Mat.mat
- Assets/_Project/VFX/Materials/VFX_Dissolve_Mat.mat
- Assets/_Project/VFX/Materials/VFX_Glow_Mat.mat
- Assets/_Project/VFX/Materials/VFX_Uber_Water.mat
- Assets/_Project/VFX/Materials/VFX_Uber_Wind.mat

### _Project/VFX/Meteor_Falling_SKELETON.prefab (1 · 0.25 MB)

- Assets/_Project/VFX/Meteor_Falling_SKELETON.prefab

### _Project/VFX/Projectiles (40 · 18.64 MB)

- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow02.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow03.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow04.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow05.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow06.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow07.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow08.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow09.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow10.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow11.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow14.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow15.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow16.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow17.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow18.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow19.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow20.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow21.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow22.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Axe01.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Axe02.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Axe03.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Card02.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Card03.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Card04.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_CardsThrow01.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Cylinder01.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Cylinder02.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Cylinder03.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Cylinder04.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_ExplosiveBullet01.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_ExplosiveBullet02.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_ExplosiveBullet03.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Rock03.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_RotatingSpheres02.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_RotatingSpheres03.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_RotatingSpheres04.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Shard02.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Shard03.prefab
- Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Shuriken02.prefab

### _Project/VFX/Score Additive.mat (1 · 0.00 MB)

- Assets/_Project/VFX/Score Additive.mat

### _Project/VFX/ShotgunPelletFireball.prefab (1 · 0.13 MB)

- Assets/_Project/VFX/ShotgunPelletFireball.prefab

### _Project/VFX/Textures (6 · 0.02 MB)

- Assets/_Project/VFX/Textures/DamageNumbers/damage_number_halftone_dense_256.png
- Assets/_Project/VFX/Textures/DamageNumbers/damage_number_halftone_sparse_256.png
- Assets/_Project/VFX/Textures/DamageNumbers/damage_number_impact_spark_64.png
- Assets/_Project/VFX/Textures/ScoreGlow.png
- Assets/_Project/VFX/Textures/ScoreShine.png
- Assets/_Project/VFX/Textures/ScoreSpark.png

### _Project/VFX/Tornado_SKELETON.prefab (1 · 0.37 MB)

- Assets/_Project/VFX/Tornado_SKELETON.prefab

### _Project/VFX/WeaponTrailPreset_Lightning.asset (1 · 0.00 MB)

- Assets/_Project/VFX/WeaponTrailPreset_Lightning.asset

### _Project/VFX/WeaponTrail_Slash_Lightning.prefab (1 · 0.00 MB)

- Assets/_Project/VFX/WeaponTrail_Slash_Lightning.prefab

### _Project/스나이퍼.png (1 · 0.10 MB)

- Assets/_Project/스나이퍼.png

### _Project/실드.png (1 · 0.11 MB)

- Assets/_Project/실드.png

### _Project/파이터.png (1 · 0.09 MB)

- Assets/_Project/파이터.png

### _Project/힐러.png (1 · 0.11 MB)

- Assets/_Project/힐러.png

## B. 벤더 재추림 — 127 파일 · 51.1 MB

### GabrielAguiarProductions/Shaders (1 · 0.17 MB)

- Assets/GabrielAguiarProductions/Shaders/MasterScroll01_Unlit.shadergraph

### GabrielAguiarProductions/Textures (14 · 7.33 MB)

- Assets/GabrielAguiarProductions/Textures/Circle002.png
- Assets/GabrielAguiarProductions/Textures/Gradients/Gradient03.png
- Assets/GabrielAguiarProductions/Textures/Gradients/Gradient06.png
- Assets/GabrielAguiarProductions/Textures/Gradients/Gradient_01.tga
- Assets/GabrielAguiarProductions/Textures/Gradients/Gradient_greyscale.PNG
- Assets/GabrielAguiarProductions/Textures/Impact02.png
- Assets/GabrielAguiarProductions/Textures/Noise05.png
- Assets/GabrielAguiarProductions/Textures/Noises/Noise09.png
- Assets/GabrielAguiarProductions/Textures/Noises/Noise10.png
- Assets/GabrielAguiarProductions/Textures/SnowFlake01.png
- Assets/GabrielAguiarProductions/Textures/Spritesheets/Cards01_3x2.png
- Assets/GabrielAguiarProductions/Textures/Toxic01.png
- Assets/GabrielAguiarProductions/Textures/Trails/StylizedTrail09.png
- Assets/GabrielAguiarProductions/Textures/Trails/StylizedTrail16.png

### GabrielAguiarProductions/UniqueProjectilesVol_4 (83 · 26.11 MB)

- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/ArrowMat2_MasterAdd.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Axe_Mul.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Card01_AB.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Circle001_MasterAdd.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Circle04_MasterAB.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/ExplosiveBulletProjectile_Pre.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/GalaxyMat03_GalaxyShader.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/GalaxyMat04_GalaxyShader.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/GalaxyMat05_GalaxyShader.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/GalaxyMat07_GalaxyShader.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/GalaxyMat08_GalaxyShader.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/GalaxyMat10_GalaxyShader.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Impact02_MasterAdd.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Orb02_MasterAB.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Orb02_MasterAdd.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/SnowFlake01_Add2.5.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Toxic01_MasterAdd.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Trail04Bright_TrailShader1.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Trail04Bright_TrailShader2.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Trail04Bright_TrailShader3.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Trail04Bright_Trailshader5.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Trail04Bright_Trailshader6.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Trail04Dark_TrailShader2.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Trail04Dark_TrailShader3.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Trail04Dark_TrailShader4.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Trail04Dark_TrailShader5.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Trail05Bright_TrailShader1.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Trail05Bright_TrailShader2.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Trail05Bright_TrailShader3.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Trail05Dark_TrailShader1.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Trail05Dark_TrailShader2.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Trail05Dark_TrailShader3.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Trail07Bright_MasterAdd.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Trail10Bright_MasterAdd.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/Trail11Bright_MasterAdd.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/shard02_Add.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Materials/shard03_Add.mat
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Models/Arrow02.fbx
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Models/AsheArrow01.fbx
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Models/Axe01.fbx
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Models/ExplosiveBullet.fbx
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Models/Rod01.fbx
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Hits/vfx_Hit_Cylinder04.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Hits/vfx_Hit_RotatingSpheres03.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Arrow02.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Arrow03.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Arrow04.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Arrow05.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Arrow06.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Arrow07.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Arrow08.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Arrow09.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Arrow10.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Arrow11.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Arrow14.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Arrow15.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Arrow16.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Arrow17.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Arrow18.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Arrow19.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Arrow21.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Arrow22.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Axe01.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Axe02.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Axe03.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Card02.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Card03.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Card04.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Cylinder01.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Cylinder02.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Cylinder03.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Cylinder04.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_ExplosiveBullet02.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_ExplosiveBullet03.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Rock02.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Rock03.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_RotatingSpheres02.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_RotatingSpheres03.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_RotatingSpheres04.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Shard01.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Shard02.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Shard03.prefab
- Assets/GabrielAguiarProductions/UniqueProjectilesVol_4/Prefabs/Muzzles/vfx_Muzzle_Shuriken02.prefab

### Hovl Studio/Epic Sword Slash Effects System (1 · 0.24 MB)

- Assets/Hovl Studio/Epic Sword Slash Effects System/Prefabs/SlashLightningTrails.prefab

### Hovl Studio/HSFiles (8 · 1.16 MB)

- Assets/Hovl Studio/HSFiles/Materials/Glow1cg.mat
- Assets/Hovl Studio/HSFiles/Materials/Path4Slash.mat
- Assets/Hovl Studio/HSFiles/Materials/Trail113cc.mat
- Assets/Hovl Studio/HSFiles/Shaders/HS_Blend_CG.shadergraph
- Assets/Hovl Studio/HSFiles/Textures/Glow1.png
- Assets/Hovl Studio/HSFiles/Textures/Noise32.png
- Assets/Hovl Studio/HSFiles/Textures/Path4.png
- Assets/Hovl Studio/HSFiles/Textures/Trail113.png

### KayKit/Packs (4 · 0.06 MB)

- Assets/KayKit/Packs/KayKit - Platformer Pack (for Unity)/Models/neutral/structure_A.fbx
- Assets/KayKit/Packs/KayKit - Platformer Pack (for Unity)/Models/red/platform_arrow_2x2x1_red.fbx
- Assets/KayKit/Packs/KayKit - Platformer Pack (for Unity)/Prefabs/neutral/structure_A.prefab
- Assets/KayKit/Packs/KayKit - Platformer Pack (for Unity)/Prefabs/red/platform_arrow_2x2x1_red.prefab

### PixPlays/Components (4 · 1.57 MB)

- Assets/PixPlays/Components/Components_URP/SharedMaterials/WindBlastLinesMat.mat
- Assets/PixPlays/Components/Components_URP/SharedMaterials/WindBlastShockwaveMat.mat
- Assets/PixPlays/Components/Meshes/GroundShatterMesh.fbx
- Assets/PixPlays/Components/Textures/T_Lu_Noise_09.png

### PixPlays/ElementalBlastVFX (5 · 13.23 MB)

- Assets/PixPlays/ElementalBlastVFX/EarthBlast/Animations/Animation Clip_GroundShatterAnim_033.anim
- Assets/PixPlays/ElementalBlastVFX/EarthBlast/Animations/EarthBlastTimeline.playable
- Assets/PixPlays/ElementalBlastVFX/EarthBlast/Animations/GroundShatterAnim.controller
- Assets/PixPlays/ElementalBlastVFX/EarthBlast/Version_URP/Materials/GroundMat.mat
- Assets/PixPlays/ElementalBlastVFX/EarthBlast/Version_URP/Materials/ShockwaveMAt.mat

### PixPlays/ElementalProjectiles (7 · 1.28 MB)

- Assets/PixPlays/ElementalProjectiles/Fireball/Version_BuiltIn/FireballProjectile/Materials/FireTrailMat.mat
- Assets/PixPlays/ElementalProjectiles/Fireball/Version_URP/FireballCast/FireballCast.prefab
- Assets/PixPlays/ElementalProjectiles/Windbullet/Version_URP/WindbulletHit/WindbulletHit.prefab
- Assets/PixPlays/ElementalProjectiles/Windbullet/Version_URP/WindbulletProjectile/Materials/WindBodyTrailsInnerMat.mat
- Assets/PixPlays/ElementalProjectiles/Windbullet/Version_URP/WindbulletProjectile/Materials/WindBodyTrailsOuterMat.mat
- Assets/PixPlays/ElementalProjectiles/Windbullet/Version_URP/WindbulletProjectile/Materials/WindTrailMat.mat
- Assets/PixPlays/ElementalProjectiles/Windbullet/Version_URP/WindbulletProjectile/WindBulletProjectile.prefab

## C. 테마 풀 밖 프롭 · 효과 타일 (D7) — 55 파일 · 4.2 MB

### _Project/Art/EffectTiles (2 · 0.00 MB)

- Assets/_Project/Art/EffectTiles/ET_Fragile.png
- Assets/_Project/Art/EffectTiles/ET_GlassCannon.png

### _Project/Art/Theme (8 · 1.13 MB)

- Assets/_Project/Art/Theme/forest/prop_dummy_leaf_1_1.png
- Assets/_Project/Art/Theme/forest/prop_dummy_shrub_1_2.png
- Assets/_Project/Art/Theme/forest/prop_style_bush_1_1.png
- Assets/_Project/Art/Theme/forest/prop_style_crystal_cluster_1_1.png
- Assets/_Project/Art/Theme/forest/prop_style_mushroom_1_1.png
- Assets/_Project/Art/Theme/forest/prop_style_round_tree_1_1.png
- Assets/_Project/Art/Theme/forest/prop_style_stone_lantern_1_2.png
- Assets/_Project/Art/Theme/forest/prop_style_stone_shrine_3_3.png

### _Project/Data/EffectTiles (2 · 0.00 MB)

- Assets/_Project/Data/EffectTiles/effect_tile_fragile.asset
- Assets/_Project/Data/EffectTiles/effect_tile_glass_cannon.asset

### _Project/Data/Props (1 · 0.00 MB)

- Assets/_Project/Data/Props/prop_prototype_1_1.asset

### _Project/Data/Sprites (1 · 0.00 MB)

- Assets/_Project/Data/Sprites/Sprite_Diamond.png

### _Project/Data/Theme (16 · 0.02 MB)

- Assets/_Project/Data/Theme/forest/prop_concept_arcane_lantern_1_2.asset
- Assets/_Project/Data/Theme/forest/prop_concept_cannon_turret_2_1.asset
- Assets/_Project/Data/Theme/forest/prop_concept_coil_machine_1_1.asset
- Assets/_Project/Data/Theme/forest/prop_concept_crystal_node_1_1.asset
- Assets/_Project/Data/Theme/forest/prop_concept_runic_portal_2_2.asset
- Assets/_Project/Data/Theme/forest/prop_concept_stone_altar_2_2.asset
- Assets/_Project/Data/Theme/forest/prop_dummy_leaf_1_1.asset
- Assets/_Project/Data/Theme/forest/prop_dummy_shrub_1_2.asset
- Assets/_Project/Data/Theme/forest/prop_edge_forest_mossy_boulder_2_1.asset
- Assets/_Project/Data/Theme/forest/prop_edge_forest_pine_cluster_2_2.asset
- Assets/_Project/Data/Theme/forest/prop_style_bush_1_1.asset
- Assets/_Project/Data/Theme/forest/prop_style_crystal_cluster_1_1.asset
- Assets/_Project/Data/Theme/forest/prop_style_mushroom_1_1.asset
- Assets/_Project/Data/Theme/forest/prop_style_round_tree_1_1.asset
- Assets/_Project/Data/Theme/forest/prop_style_stone_lantern_1_2.asset
- Assets/_Project/Data/Theme/forest/prop_style_stone_shrine_3_3.asset

### _Project/Generated/Props (8 · 2.94 MB)

- Assets/_Project/Generated/Props/Textures/prop_concept_arcane_lantern_1_2.png
- Assets/_Project/Generated/Props/Textures/prop_concept_cannon_turret_2_1.png
- Assets/_Project/Generated/Props/Textures/prop_concept_coil_machine_1_1.png
- Assets/_Project/Generated/Props/Textures/prop_concept_crystal_node_1_1.png
- Assets/_Project/Generated/Props/Textures/prop_concept_runic_portal_2_2.png
- Assets/_Project/Generated/Props/Textures/prop_concept_stone_altar_2_2.png
- Assets/_Project/Generated/Props/Textures/prop_edge_forest_mossy_boulder_2_1.png
- Assets/_Project/Generated/Props/Textures/prop_edge_forest_pine_cluster_2_2.png

### _Project/Prefabs/Props (17 · 0.13 MB)

- Assets/_Project/Prefabs/Props/forest/prop_concept_arcane_lantern_1_2.prefab
- Assets/_Project/Prefabs/Props/forest/prop_concept_cannon_turret_2_1.prefab
- Assets/_Project/Prefabs/Props/forest/prop_concept_coil_machine_1_1.prefab
- Assets/_Project/Prefabs/Props/forest/prop_concept_crystal_node_1_1.prefab
- Assets/_Project/Prefabs/Props/forest/prop_concept_runic_portal_2_2.prefab
- Assets/_Project/Prefabs/Props/forest/prop_concept_stone_altar_2_2.prefab
- Assets/_Project/Prefabs/Props/forest/prop_dummy_leaf_1_1.prefab
- Assets/_Project/Prefabs/Props/forest/prop_dummy_shrub_1_2.prefab
- Assets/_Project/Prefabs/Props/forest/prop_edge_forest_mossy_boulder_2_1.prefab
- Assets/_Project/Prefabs/Props/forest/prop_edge_forest_pine_cluster_2_2.prefab
- Assets/_Project/Prefabs/Props/forest/prop_style_bush_1_1.prefab
- Assets/_Project/Prefabs/Props/forest/prop_style_crystal_cluster_1_1.prefab
- Assets/_Project/Prefabs/Props/forest/prop_style_mushroom_1_1.prefab
- Assets/_Project/Prefabs/Props/forest/prop_style_round_tree_1_1.prefab
- Assets/_Project/Prefabs/Props/forest/prop_style_stone_lantern_1_2.prefab
- Assets/_Project/Prefabs/Props/forest/prop_style_stone_shrine_3_3.prefab
- Assets/_Project/Prefabs/Props/prop_prototype_1_1.prefab

## D. 비활성 카드 (D8) — 3 파일 · 2.1 MB

### _Project/Art/DreamcatcherCards (1 · 2.05 MB)

- Assets/_Project/Art/DreamcatcherCards/dreamcatcher_card_27.png

### _Project/Data/Dreamcatcher (1 · 0.00 MB)

- Assets/_Project/Data/Dreamcatcher/Card_IncubusPact.asset

### _Project/Data/Effects (1 · 0.00 MB)

- Assets/_Project/Data/Effects/Effect_sub_incubus_pact.asset

