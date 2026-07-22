# 2 — 코드 cut-over + flat 필드 삭제 + 테스트 갱신

## 목적

모든 소비처를 abilities 경로로 전환하고 flat 25필드를 삭제한다. 이 커밋으로 재구조화 완성 —
bake 결과는 전과 동일(계약 6).

## 변경 대상 (소비처 전수 — 2026-07-22 grep 실측)

| 파일 | 현재 읽는 것 | 전환 |
|---|---|---|
| `Bridge/BattleBridge.cs` | bake 4곳(`shotCount>1`→Volley, `hazardCastEnabled`→Hazard, `shieldCastCooldown>0`→Shield, `bombLandingTiles>0`→Bomb) + `SetAimGuide`(`bombLandingTiles`) | `GetAbility<T>() != null` 게이트 + ability 필드 복사. SetAimGuide 는 `GetAbility<BombThrowAbility>()` |
| `Battle/Combat/AttackSystem.cs` | (무변경 — baked 컴포넌트만 읽음) | N/A 확인만 |
| `UI/DirectionAimController.cs` | `bombLandingTiles`·`attackRange` | `GetAbility<BombThrowAbility>()` 분기 |
| `UI/DefenderDragPlacementController.cs` | `directionalAttack`(aim 진입) | `unit.RequiresFacing` |
| `UI/DefenderSelector.cs` | `directionalAttack`(튜토리얼 슬롯 선호) | `RequiresFacing` |
| `UI/Tutorial/FirstSessionTutorialController.cs` | `directionalAttack`(aim 대기 스텝) | `RequiresFacing` |
| `Data/UnitKitSummary.cs` | `directionalAttack`·`shotCount`·`hazardCastEnabled`(자동 요약문) | ability 조회로 재작성 |
| `Data/DefenderUnitData.cs` | — | **flat 25필드 삭제**(volley 4 · hazard 8 · shield 4 · bomb 9) |
| `Tests/EditMode/DirectionalVolleyIntegrationTests.cs` | flat 필드로 SO 구성 | ability 에셋 CreateInstance 구성 |
| `Tests/EditMode/UnitKitSummaryTests.cs` | 동일 | 동일 |
| 유닛 `.asset` 7개 | orphan flat 키 | 삭제된 필드의 YAML 키 정리(선택 — Unity 는 무시하나 위생) |

## 구현

- bake 게이트 의미 보존: `shotCount>1` → `GetAbility<DirectionalVolleyAbility>() is {} v && v.shotCount>1`
  (RequiresFacing 과 별개 — DeployedFacing bake 는 기존 `ActivateDeployedDefender` 경로 그대로).
- `DefenderSelector` 의 "non-directional 선호"·튜토리얼 aim 스텝은 `RequiresFacing` 로 의미 불변.
- `UnitKitSummary`: `GetAbility<DirectionalVolleyAbility>()`(연발 문구)·`GetAbility<HazardCastAbility>()`
  기준으로 동일 문구 생성 — 문구 텍스트 무변경.
- 시트 임포터/익스포터(`unit-stat-spreadsheet-schema`)는 flat 능력필드를 애초에 계약 제외라 무변경 — 확인만.

## 완료 기준

- [ ] compile 0 · 전체 EditMode green(갱신된 volley/summary 테스트 포함, 기존 skip 2 제외 0 fail).
- [ ] Play 스모크(7유닛): 머신거너 방향 볼리 · 캐스터 4종 해저드 · 실드셔틀 실드 · 폭탄맨 조준/폭탄 — 전부 재구조화 전과 동일 동작, 콘솔 0.
- [ ] `docs/reference/object-pipeline-map.md` Defender 데이터 SO 앵커 갱신(같은 커밋 또는 종료 커밋).
