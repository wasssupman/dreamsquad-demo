# 3 · CH2 → 스나이퍼 · CH3 → 실드셔틀 저작 + Play 확인

## 목적

신규 고유 리그 두 종을 각 유닛에 꽂아 공유 대기 규칙의 Spine 첫 소비자로 세운다. **코드 0** — 고유 리그 관용구(CH1 선례) 그대로.

## 리그 실측 (2026-09-17 · spine-unity 4.3.102 로드 확인)

| | CH2 (스나이퍼) | CH3 (실드셔틀) | CH1 (소환사 · 선례) |
|---|---|---|---|
| Export | 4.3.26 | 4.3.26 | 4.3.26 |
| 크기 | 595×611 | 1198×1071 | 423×506 |
| 애니 | `attack`(1.67) `attack-Loop`(1.67) `drop`(1.67) `Idle`(2.67) `Idle2`(3.33) | `attack`(0.33) `attack2`(0.8) `drop`(0.67) `idle1`(1.33) `idle2`(1.33) `idle3`(2.5) | `attack1~3` `drop` `idle` `idle2` `idle3` |
| 스킨 | default | default | default |
| 앵커 본 후보 | `Muzzle` · `weapon` | `weapon` | — |
| 없는 것 | death · drag · walk | death · drag · walk | death |

## 저작 값

| 필드 | 스나이퍼 | 실드셔틀 |
|---|---|---|
| `skeletonDataAsset` | `CH2_SkeletonData` | `CH3_SkeletonData` |
| `spineSkinName` · `partSkins` · `slotColors` | 비움 | 비움 |
| `idleAnimation`(기본 루프) | `Idle` | **`idle2`**(사용자 지정) |
| `idleBreaks`(N초마다 한 번) | `[Idle2]` | `[idle1, idle3]` |
| `idleBreakInterval` | (1, 3) | (1, 3) |
| `attackAnimation` | `attack` | `attack` |
| `deployAnimation` | `drop` | `drop` |
| `dragAnimation` · `deathAnimation` · `walkAnimation` | 비움(폴백: drag→idle · death→즉시 파괴) | 동일 |
| `castAnchorBone` | `Muzzle` | 비움(오프셋 폴백) |
| `spineVisualScale` 초기값 | 0.624 × 506/611 ≈ **0.52** | 0.624 × 506/1071 ≈ **0.30** (CH1 높이 기준 · Play 에서 조정) |
| facing | Play 에서 확인 → 오른쪽을 보면 `SkeletonFlipX` 모디파이어 부착 | 동일 |

`attack-Loop`(CH2)·`attack2`(CH3)는 연결하지 않는다(사용자 결정).

⚠ `deploymentDuration` 은 각 유닛 현재값 유지 — `drop` 길이(1.67s / 0.67s)와 다르면 배치 모션이 잘리거나 남는다. Play 에서 보고 사용자 결정으로.

## 완료 기준

- 두 유닛이 판에 서고(Spine 뷰) 대기 순환이 돈다(CH2: Idle 루프 → N초 → Idle2 한 바퀴 → 루프 · CH3: idle2 루프 → N초 → idle1/idle3 한 바퀴 → 루프). 공격·배치(drop)·픽킹 정상.
- 방향이 규약(왼쪽)과 맞고, 크기가 옆 유닛과 눈높이.
- `UnitVisualDataValidator` 경고 0(파츠 비움).
- 사용자 Play 육안 확인 후 커밋 해시 기록.
