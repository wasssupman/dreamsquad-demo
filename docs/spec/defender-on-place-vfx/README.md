# Defender On-Place VFX Spec

**작성일**: 2026-04-28
**상태**: 진행 중 (task 0 완료 / task 1 추가)
**목표**: 디펜더 배치 시점의 on-place 효과(`placementVfxPrefab`)에 실제 VFX 자산을 와이어링한다. 이번 spec 은 Archer (`BindNearby`) 1대만. 다른 9 디펜더는 후속 후보.

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

- **다른 9 디펜더 placementVfxPrefab 와이어링**: effect 별 자산 매핑 (`SlowPulse / BoostNearbyDefenders / MeleeBurst / ForwardProjectile / GainCost / ReduceSkillCooldown`). archer 와 같은 패턴으로 1대씩 검증 후 확장.
- **자체 톤 일관 VFX**: PixPlays 자산 대신 자체 제작 톤으로 통일 (`Placement_SKELETON.prefab` 완성).
- **VFX self-destroy 컴포넌트 표준화**: 모든 placement VFX prefab 이 자체 종료를 보장하는 ScriptableObject 또는 component 표준 마련.
- **무기 anchor 추출 (`projectile-cast-and-anchor` spec)**: 이번 spec 의 이후 작업 항목.
