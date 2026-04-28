# Archer Water Bind VFX

**작업 구분**: 0

## 목적

Archer 디펜더 배치 시 BindNearby effect 와 함께 WaterAOE VFX 가 1회 재생되도록 한다. 코드 변경 없이 자산 와이어링만.

## 변경 대상

- Modify: `Assets/_Project/Data/Defenders/Defender_Archer.asset`
  - 필드: `placementVfxPrefab` (현재 fileID:0) → `Assets/PixPlays/ElementalAOE/WaterAOE/Version_URP/WaterAoeVFX.prefab`
  - GUID: `12bfc3638c321cb498765554bb7194eb`, type: 3 (PrefabImporter)

## 구현

UnityMCP `manage_asset` 또는 직접 YAML edit 으로 ScriptableObject 필드 업데이트:

```yaml
  placementVfxPrefab: {fileID: 12bfc3638c321cb498765554bb7194eb00000000, guid: 12bfc3638c321cb498765554bb7194eb, type: 3}
```

(fileID 는 prefab root, type=3 은 prefab 참조. 다른 디펜더의 prefab 참조 형식과 동일하게 작성.)

## VFX self-destroy 보장

WaterAoeVFX prefab 이 자체 종료하는지 확인 후 wire:

1. prefab 의 root `ParticleSystem.main.stopAction` 이 `Destroy` 인지 확인.
2. 또는 prefab root 에 `AutoDestroyParticle` (또는 동등 동작) 컴포넌트가 attach 되어 있는지 확인.
3. 둘 다 없으면 본 spec 에서 수정하지 않고 후속 후보 ("VFX self-destroy 컴포넌트 표준화") 로 이관, 임시로 wire 하되 Play smoke 시 누수 여부 관찰.

## Play 검증 시나리오

1. Editor Play → BattleScene 진입.
2. Draft → Archer 카드 deploy.
3. Archer 배치 셀 위치에 WaterAOE 파티클이 1회 재생되는 것을 시각 확인.
4. BindNearby 효과 (적 슬로우/속박) 동작은 기존과 동일하게 유지.
5. 다른 디펜더 (예: Cannon) 는 fallback `vfxSpawner.SpawnPlacementRing` 그대로 (회귀 없음).

## 완료 기준

- Defender_Archer.asset 의 `placementVfxPrefab` 이 WaterAoeVFX URP prefab 으로 채워짐.
- Editor Play smoke: archer 배치 시 water VFX 가 보이고 자체 종료.
- 다른 디펜더 배치는 회귀 없음 (fallback ring 동작).
- Console Error/Warning 0.
- VFX self-destroy 가 prefab 자체에서 보장되지 않으면 메모 남기고 후속 spec 으로 이관 (이 task 는 자산 와이어링만).
