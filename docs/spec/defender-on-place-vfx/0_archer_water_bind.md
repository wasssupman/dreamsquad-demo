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

확인 결과: WaterAoeVFX 의 모든 `ParticleSystem.main.stopAction = 0` (None) — prefab 자체 종료 보장 안 됨. 그러나 **`BattleBridge.PlayDeploymentPresentation:1738` 가 이미 `Destroy(go, max(deploymentDuration, 1f) + 0.25f)` 로 1.25s (Archer 기준) 후 강제 destroy** 하므로 GameObject leak 위험은 없다. 시각 길이가 1.25s 이내인지만 Play smoke 에서 시각 확인 필요. 길면 잘림 (기능 회귀 아님, 시각 fidelity 만 후속 조정 후보).

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

확인 일자: 2026-04-28 — Editor Play smoke: Archer 배치 시 WaterAOE 재생, BindNearby 회귀 없음, 다른 디펜더 fallback ring 정상, console clean. 커밋: (아래 참조)
