# 0. 미커밋 자산 분리 (사전 정리)

## 목적

세션 시작 시점에 Codex 가 미커밋 상태로 남긴 자산을 본 spec 과 무관한 부분과 분리한다. 본 spec 작업 중 우연히 forest tile/surface 변경이 묶여 들어가면 안 된다.

## 변경 대상

### 본 spec 으로 흡수 (Codex 가 만든 컨셉 프랍 6종 — EdgeProp 으로 재사용)

```
Assets/_Project/Data/Theme/forest/prop_concept_arcane_lantern_1_2.asset (+ .meta)
Assets/_Project/Data/Theme/forest/prop_concept_cannon_turret_2_1.asset (+ .meta)
Assets/_Project/Data/Theme/forest/prop_concept_coil_machine_1_1.asset (+ .meta)
Assets/_Project/Data/Theme/forest/prop_concept_crystal_node_1_1.asset (+ .meta)
Assets/_Project/Data/Theme/forest/prop_concept_runic_portal_2_2.asset (+ .meta)
Assets/_Project/Data/Theme/forest/prop_concept_stone_altar_2_2.asset (+ .meta)
Assets/_Project/Prefabs/Props/forest/prop_concept_*.prefab (+ .meta)  // 6개
Assets/_Project/Generated/Props/Textures/prop_concept_*.png (+ .meta)  // 6개
Assets/_Project/Generated/Props/ConceptSources/concept_prop_sheet_alpha.png (+ .meta)
Assets/_Project/Generated/Props.meta
Assets/_Project/Generated/Props/ConceptSources.meta
Assets/_Project/Generated/Props/Textures.meta
```

### 본 spec 과 분리 (별도 커밋 또는 stash)

```
Assets/_Project/Map/Theme/forest/forest.asset                        // tile texture/surface rule 갱신
Assets/_Project/Data/Theme/forest/prop_style_*.asset (14개)          // SO 필드 튜닝
Assets/_Project/Data/Theme/forest/forest 의 새 tile texture (있다면)
Assets/TextMesh Pro/Resources/Fonts & Materials/...                  // 무관
Assets/_Project/VFX/Materials/Heal_Applied_Mat.mat                   // 무관
ProjectSettings/EditorSettings.asset                                  // 무관
Assets/Plugins/, Assets/Plugins.meta                                  // 무관
Assets/Screenshots/                                                   // 본 작업의 reference
Assets/_Project/Scenes/BattleScene.unity                              // 본 spec 3 단계에서 wiring 시 다시 변경
```

## 구현

1. `git status` 로 미커밋 목록 점검.
2. 본 spec 흡수 대상 파일만 staging:
   ```
   git add Assets/_Project/Data/Theme/forest/prop_concept_*.asset
   git add Assets/_Project/Data/Theme/forest/prop_concept_*.asset.meta
   git add Assets/_Project/Prefabs/Props/forest/prop_concept_*.prefab
   git add Assets/_Project/Prefabs/Props/forest/prop_concept_*.prefab.meta
   git add Assets/_Project/Generated/Props
   git add Assets/_Project/Generated/Props.meta
   ```
3. `git diff --cached --stat` 로 흡수 범위만 staged 됐는지 확인.
4. 분리 대상은 staging 하지 않는다. 작업 마지막에 사용자에게 "별도 커밋으로 분리할지 / stash 로 보관할지" 결정 위임.
5. 본 spec 의 1~6 작업 단위는 staged + working tree 위에서 진행한다.

## 완료 기준

- `git diff --cached --stat` 출력에 prop_concept_* 6종 + Generated/Props 만 있고 forest.asset / prop_style_* / 무관 파일은 없다.
- 분리 대상 파일들은 working tree 에 그대로 dirty 상태로 남아있다 (덮어쓰지 않음).
- 0 단계 자체는 별도 커밋을 만들지 않는다. staging 만 정리.

## 산출

- staging area 정리만. 신규 커밋 없음. 신규 파일 작성 없음.
