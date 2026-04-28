# Defender On-Place VFX Spec

**작성일**: 2026-04-28
**상태**: 완료 2026-04-28 (10 디펜더 placement 모두 와이어)
**목표**: 디펜더 배치 시점의 on-place 효과(`placementVfxPrefab`)에 실제 VFX 자산을 와이어링한다. task 0 에서 Archer (`BindNearby` → Water) 검증, task 1 에서 다른 9 디펜더에 effect→AOE 매핑 적용.

## 배경

- BattleBridge.cs:1735-1742 가 이미 `unitData.placementVfxPrefab != null` 이면 Instantiate, null 이면 `vfxSpawner.SpawnPlacementRing` fallback 으로 동작. **코드 경로는 완성됨**.
- 모든 10 디펜더의 `placementVfxPrefab = null` 인 상태로 배포되어, 현재는 항상 fallback ring 만 보임.
- 이 spec 은 Archer 1대 자산 와이어링 + 시각 검증으로 시스템이 의도대로 동작하는지 확인한다.

## 구현 문서 목록

| 작업 구분 | 문서 | 목적 |
|---|---|---|
| 0 | `0_archer_water_bind.md` | Archer 의 BindNearby 효과에 WaterAOE prefab 와이어링 + Play 검증 |
| 1 | `1_other_defenders_placement.md` | 다른 9 디펜더 placement VFX 와이어링 (effect→AOE 매핑) |

## 공통 원칙

- 디펜더 데이터 자산만 수정. 코드 변경 없음 (이미 완성된 spawn 경로 활용).
- VFX 자산은 PixPlays URP 변종 사용 (프로젝트 렌더 파이프라인이 URP 17.3).
- 자체 `Assets/_Project/VFX/Placement_SKELETON.prefab` 은 별도 톤 통일 작업으로 분리. 이번 spec 에서 건드리지 않음.
- VFX 의 lifetime 은 prefab 자체 ParticleSystem 의 duration 에 의존 (자체 종료). BattleBridge 는 spawn 후 destroy 책임을 갖지 않는다 (자체 ParticleSystem 의 `stopAction = Destroy` 또는 별도 self-destroy 컴포넌트 필요).

## 후속 후보 (이번 spec 밖)

- **자체 톤 일관 VFX**: PixPlays 자산 대신 자체 제작 톤으로 통일 (`Placement_SKELETON.prefab` 완성).
- **VFX self-destroy 컴포넌트 표준화**: PixPlays prefab 들의 `stopAction = 0` (None) 을 보완하는 표준 컴포넌트. 현재는 `BattleBridge.PlayDeploymentPresentation:1738` 의 `Destroy(go, max(deploymentDuration,1f)+0.25f)` 강제 destroy 로 우회 중.
- **VFX 시각 길이/스케일 fine-tune**: 1.25s 초과 시 잘림 + 디펜더 크기 대비 VFX 스케일 조정.
- **effect 별 VFX 톤 다양화**: 현재 같은 effect 디펜더가 같은 VFX 사용 (Bastion/Bruiser/Cannon 모두 Fire). 디펜더별 시각 차별화 후속.
