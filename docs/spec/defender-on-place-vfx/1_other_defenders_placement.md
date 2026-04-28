# Other 9 Defenders — Placement VFX Wiring

**작업 구분**: 1
**근거**: task 0 (Archer Water) 검증 후, 같은 spawn 경로를 다른 9 디펜더로 데이터만 채워 확장.

## 목적

Archer 외 9 디펜더가 배치 시 effect 의미에 맞는 AOE VFX 1회 재생. 코드 변경 0, 데이터 와이어링만.

## 변경 대상 (자산만)

- Modify: `Assets/_Project/Data/Defenders/Defender_Bastion.asset`
- Modify: `Assets/_Project/Data/Defenders/Defender_Bruiser.asset`
- Modify: `Assets/_Project/Data/Defenders/Defender_Cannon.asset`
- Modify: `Assets/_Project/Data/Defenders/Defender_Guardian.asset`
- Modify: `Assets/_Project/Data/Defenders/Defender_Marksman.asset`
- Modify: `Assets/_Project/Data/Defenders/Defender_Piercer.asset`
- Modify: `Assets/_Project/Data/Defenders/Defender_Ranger.asset`
- Modify: `Assets/_Project/Data/Defenders/Defender_Scout.asset`
- Modify: `Assets/_Project/Data/Defenders/Defender_Sniper.asset`

## Effect → AOE 매핑

Effect enum 참조:
```
0 None / 1 SlowPulse / 2 BoostNearbyDefenders / 3 BindNearby
4 MeleeBurst / 5 ForwardProjectile / 6 GainCost / 7 ReduceSkillCooldown
```

| Defender | effect | placement VFX | URP path | guid | root fileID | 의도 |
|---|---|---|---|---|---|---|
| Bastion | 4 MeleeBurst | FireAoeVFX | `Assets/PixPlays/ElementalAOE/FireAOE/Version_URP/FireAoeVFX.prefab` | `971aa18e6a15c0d48bf889c9d9df4fce` | `525611502235730195` | 폭발/burst |
| Bruiser | 4 MeleeBurst | FireAoeVFX | (위와 동일) | `971aa18e6a15c0d48bf889c9d9df4fce` | `525611502235730195` | 폭발/burst |
| Cannon | 4 MeleeBurst | FireAoeVFX | (위와 동일) | `971aa18e6a15c0d48bf889c9d9df4fce` | `525611502235730195` | 폭발/burst |
| Guardian | 2 BoostNearbyDefenders | EarthSlamSpikesAoeVFX | `Assets/PixPlays/ElementalAOE/EarthAOE/Version_URP/EarthSlamSpikesAoeVFX.prefab` | `c7ebe9341e13e2e448481a1a5b7a89bf` | `39267471112187546` | rally/buff |
| Marksman | 5 ForwardProjectile | WindAoeVFX | `Assets/PixPlays/ElementalAOE/WindAOE/Version_URP/WindAoeVFX.prefab` | `c91958420bcc8d647ae19c0e7caeafa6` | `972644139175097883` | swift |
| Piercer | 5 ForwardProjectile | WindAoeVFX | (위와 동일) | `c91958420bcc8d647ae19c0e7caeafa6` | `972644139175097883` | swift |
| Ranger | 7 ReduceSkillCooldown | WindAoeVFX | (위와 동일) | `c91958420bcc8d647ae19c0e7caeafa6` | `972644139175097883` | haste |
| Scout | 6 GainCost | EarthSlamSpikesAoeVFX | (Earth, 위와 동일) | `c7ebe9341e13e2e448481a1a5b7a89bf` | `39267471112187546` | treasure/coin |
| Sniper | 5 ForwardProjectile | WindAoeVFX | (Wind, 위와 동일) | `c91958420bcc8d647ae19c0e7caeafa6` | `972644139175097883` | swift |

## YAML edit 패턴

각 디펜더 자산의 `placementVfxPrefab: {fileID: 0}` 라인을 다음으로 교체:

Fire (Bastion/Bruiser/Cannon):
```yaml
  placementVfxPrefab: {fileID: 525611502235730195, guid: 971aa18e6a15c0d48bf889c9d9df4fce, type: 3}
```

Earth (Guardian/Scout):
```yaml
  placementVfxPrefab: {fileID: 39267471112187546, guid: c7ebe9341e13e2e448481a1a5b7a89bf, type: 3}
```

Wind (Marksman/Piercer/Ranger/Sniper):
```yaml
  placementVfxPrefab: {fileID: 972644139175097883, guid: c91958420bcc8d647ae19c0e7caeafa6, type: 3}
```

## VFX self-destroy

task 0 에서 확인한 대로 PixPlays AOE prefab 들의 `stopAction = 0` (None). 그러나 `BattleBridge.PlayDeploymentPresentation:1738` 의 `Destroy(go, max(deploymentDuration, 1f) + 0.25f)` 가 모두 강제 destroy 처리하므로 leak 위험 없음. 시각 길이가 1.25s 초과면 잘림 (회귀 아님).

## 검증 시나리오

BattleScene Play 에서 9 디펜더 1대씩 차례로 배치 (Draft 에서 등장 보장 필요시 deck 임시 조정):
1. Bastion/Bruiser/Cannon 배치 → Fire 폭발 VFX 재생
2. Guardian/Scout 배치 → Earth 가시 spike VFX 재생
3. Marksman/Piercer/Ranger/Sniper 배치 → Wind 회오리 VFX 재생
4. Archer 배치 → Water VFX (회귀 없음)
5. 모든 effect (BindNearby/MeleeBurst/BoostNearbyDefenders/ForwardProjectile/GainCost/ReduceSkillCooldown) 동작 정상 — 회귀 0
6. console clean

## 완료 기준

- 9 Defender 자산의 placementVfxPrefab 채움.
- BattleScene Play smoke: 9 디펜더 모두 적절한 AOE VFX 재생.
- BindNearby/MeleeBurst 등 게임 효과 회귀 없음.
- read_console Error/Warning 0.
- 시각 길이/스케일 부적합 발견 시 메모 — 후속 fine-tune 후보 (자산 자체 조정은 본 task 밖).

확인 2026-04-28 / 커밋: 2625753
