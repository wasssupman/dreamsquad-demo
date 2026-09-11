# 2 — 소비처 11곳이 도형을 선언한다

## 목적

`AttackReach.InReach` 소비처가 **자기 `AttackState.shape` 와 side** 를 넘긴다. 획득은 합집합(`side 0`), 부가 타격은
주 대상 쪽(`side ±1`). 이 unit 이 끝나면 도형 저작이 라이브가 된다(저작은 아직 0 이라 여전히 무변).

## 소비처 — 12곳 중 11곳 (감지 제외)

| # | 지점 | side | 비고 |
|---|---|---|---|
| 1 | `AttackSystem.cs:615` 주 대상 선정 | 0 | 합집합 안 최근접. 그 결과로 `side = sign(dx)`, 0 이면 +1 (계약 4) |
| 2·4·5 | `TargetPersistence.cs:56` `KeepsLock` (focus·frontmost·방어유닛 락) | 0 | 유지 판정. 히스테리시스 `h` 는 range 에만, 도형엔 없다 |
| 3 | `AttackSystem.cs:796` 어그로 sticky | 0 | |
| 6 | RESOLVE `committedTarget` 재판정 | 0 | 주 대상 단독 |
| 7 | `AttackSystem.cs:1521` 부가 타격 pass 루프 | **±1** | 주 대상 쪽. `rankByHealth`(힐러) 분기도 동일 |
| 8·9 | `EnemyAiStateSystem.cs:110·225` guardianInRange · HasFireTarget | 0 | «멈춰도 되나» — 획득과 같은 답 |
| 10 | `HazardCastSystem.cs:133` | 0 | 캐스터는 Omni 지만 선언은 한다 |
| 11 | `PatrolAreaMath.cs:185` | 0 | 순찰병 Omni. `InCellRange` 분해 사용은 그대로 |
| — | `AggroTargeting.cs:78` `FillNearest` | 0 → ±1 | primary(`outIdx[0]`) 확정 후 `side = sign(dx)` 로 전환. Pass A/B 동일 |
| ✗ | `DetectionSystem.cs:316` | — | **원 유지**(계약 3). 인자는 `Omni` 명시 |

표기 2곳(`TilemapMapView.cs:1109` · `BattleBridge.cs:8010`)은 unit 3.

## 구현

- side 결정은 **한 곳**: 주 대상이 정해진 직후 `AttackSide.Of(dx)` 순수 함수(`dx == 0 → +1`). 부가 타격·
  `AggroTargeting`·뷰 반전(`FaceToward` 는 이미 dx 부호) 이 같은 규칙을 본다.
- `AggroTargeting.SelectTargets` 시그니처에 `shape` 추가. `count == 0` 이면 side 0, 그 뒤 primary 쪽.
  `keepFrontmostPrimary` swap 은 primary 를 바꾸지만 side 는 새 primary 로 **재계산**한다(rev 1 의 「방향이 swap 전
  primary 를 향한다」 구멍이 이 모델에선 side 재계산 한 줄로 닫힌다).
- Omni 면 게이트 분기를 건너뛴다 — 명령 수 동일.
- 손대지 않는 것: `DetectionSystem` · 스킬 layer(`EcsSkillContext`) · `MovementSystem`(정본 술어는
  `EnemyAiStateSystem` 이 본다).

## 완료 기준

- [x] `AttackShapeSelectionTests`: 공격자 + 후보 5(좌 축·우 축·우 θ 안·우 θ 밖·정확히 위 2칸).
      `Sector(90)`: 획득 후보 = 좌·우 축·우 θ 안(3) → 최근접이 우 축이면 side +1, 부가 = 우 θ 안만 · **정확히 위
      2칸은 후보 아님**. `Band(1.0)`: 위 2칸 후보 아님, 좌우 축 후보. `Omni`: 5 전부(오늘과 동일).
- [x] side 결정: `dx == 0` → +1 · 좌 최근접 → −1 · 부가 타격이 반대쪽을 절대 안 잡는다.
- [x] 가디언(AggroCapacity) 버전 동일 단언 · `AggroAoeWidthTests` · `AttackReachTests` · `RangePredicateInvariantsTests`
      무변 초록.
- [~] 적(EnemyAiState) 버전 — **unit 4 하네스로 이월.** `HasFireTarget`·`guardianInRange` 가 같은 `InReach(shape)` 를
      지나는 것은 코드로 보장되지만(같은 함수·같은 인자), 「지나간다」는 이동까지 얽혀 EditMode 픽스처가 없다.
- [~] 골든 — unit 4 로 이월(Play 세션 필요). Burst: lookup 존치 함정 무접촉 ✓.

---

### 진행 기록 — 구현 2026-09-12

- `AttackReach.SideOf(dx)` 신설(두 sim 호출처 + 뷰 규칙과 일치시키기 위해). `AttackSystem` 은 Outputs 경로 진입에서
  `hitSide` 를 한 번 계산해 pass 루프에 넘긴다. 락 유지·sticky·RESOLVE 재판정은 side 0.
- `AggroTargeting.FillNearest` — `count == 0` 이면 side 0, 그 뒤 `outIdx[0]` 쪽. Pass A/B 동일.
- ⚠ **계획과 다른 점**: 「`keepFrontmostPrimary` swap 뒤 side 를 새 primary 로 재계산한다」는 **한 줄이 아니다** —
  부가 타격이 이미 옛 primary 쪽에서 뽑혀 있어 재선정이 필요하다. 삼중 조합(가디언 × 끝을 보는 눈 × 도형)이
  희귀해 **구현하지 않고** README 후속 후보에 남긴다. 결과: 그 조합에서 부가 타격은 SelectTargets 의 primary 쪽.
- `EnemyAiStateSystem` 은 `attackLookup[enemy].shape` 를 두 호출에 넘긴다 · `PatrolFieldSystem` 은 순찰병 `AttackState.shape`
  (없으면 Omni) · `HazardCastSystem`·`DetectionSystem` 은 Omni **명시**(계약 3 · 캐스터는 사거리 0).
