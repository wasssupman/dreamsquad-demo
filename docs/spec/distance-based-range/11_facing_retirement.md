# 11 — 방향 지정(facing) 은퇴

> **사용자 결정 2026-09-01**: 「facing 기능 자체 은퇴.」

## 왜 이 spec 안인가

facing 유닛의 타겟팅은 **폭 1칸 셀 레인**(`LaneMath.IsInLane`)이다. 이 spec 의 비목표가
그 이유를 이미 적었다 — **폭 0 레인은 연속 좌표에서 측도 0**이라 거리 기반으로 옮길 수 없다.

그리고 unit 10 이 몸을 사각형으로 만들면 「폭 2 유닛의 레인은 두 열 중 어디냐」가 **기하로
답이 없는** 질문이 된다. 정수 나눗셈으로 정하면 대표 셀과 똑같은 동전 던지기가 하나 더 생긴다.

레인을 연속화하는 대신 **은퇴시킨다**(사용자 결정).

## 변경 대상

| 곳 | 처분 |
|---|---|
| `Combat/AttackSystem.cs:662` · `:976` | `laneWitness` 경로 삭제 — facing 유닛도 **최근접 타겟팅** |
| `Combat/LaneMath.cs` | 삭제 |
| `UI/DirectionAimLogic.cs` | 삭제 |
| `UI/DirectionAimController.cs` | 삭제 |
| `Bridge/BattleBridge.cs:8033-8048` | 레인 표기(`PaintLanes` 경로) 삭제 |
| `Core/TilemapMapView` | `SetPlacementRange(squareShape:true)` 조준 분기 정리 |
| `DefenderUnitData.RequiresFacing` | 은퇴. 파생원은 volley ability |
| `Data/UnitKitSummary.cs:41·105` | 「지정 방향」 문안 → 「가까운 적 쪽으로」 |

## 영향

- **머신거너·샷건너**가 다른 유닛과 같은 규칙으로 최근접 적을 쏜다. 다연발·관통은 유지 —
  발사 **명세**는 남고 **조준 규칙**만 바뀐다.
- 배치 조준 페이즈가 사라진다. 배치 = 탭/드롭 한 번.
- ⚠ **명일방주식 방향 배치 플레이가 사라진다.** 되살리려면 이 문서를 뒤집는 것이 아니라
  **연속 좌표에서 성립하는 새 어휘**(부채꼴·유닛 폭 직사각형)로 다시 설계해야 한다.

---

### 진행 기록 — 구현 2026-09-01

- 삭제: `LaneMath`(+테스트) · `DirectionAimLogic`/`DirectionAimController`(+테스트, 런타임 생성이라
  씬 배선 0) · `VolleyMachineGunnerTest`(레인 계약 그 자체라 전제와 함께 은퇴 — 다연발은
  `DirectionalVolleyIntegrationTests` 패턴 계약이 지킨다) · AttackSystem 레인 witness/오버라이드 ·
  브리지 조준 가이드 클러스터(SetAimGuide·PaintLanes·CollectLaneCells·화살표) · 뷰 aimStyle 축 ·
  `RequiresFacing` 전 계열 · facing 활성화 오버로드.
- 존치(스코프): `DeployedFacing` 컴포넌트·`ISkillContext.TryFacing`·`EmitPatternSkill` 폴백 —
  이제 아무도 안 써서 폴백(조준 대상 방향)이 유일 경로다. 문안 「지정 방향」 잔존 0(grep).
- ⚠ **Burst/소스젠 함정 4번째 재발**: `SystemAPI.GetComponentLookup<DeployedFacing>` 호출을
  OnUpdate 에서 지우자 AttackSystem 의 Burst 컴파일이 조용히 깨져 NRE(전 공격 정지, BC 에러 0,
  Burst OFF 면 통과). HEAD 복원 → 초록, 그 한 줄 복원 → 초록으로 **이분 확정**. 소비처 0 인
  lookup 한 줄을 경고 주석과 함께 존치하는 것이 워크어라운드다(AttackSystem.cs 상단).

## 완료 기준 (갱신)

- [x] `LaneMath` 참조 0. grep 으로 확인
- [x] 머신거너·샷건너가 배치 즉시 최근접 적을 쏜다 — 배치 경로 단일화(조준 페이즈 삭제) +
      `OnPlaceForwardProjectileTest` 최근접 폴백. 육안은 다음 Play 에서
- [x] 카드 문안에 「지정 방향」 잔존 0
- [x] EditMode 전건 초록(선행 실패 2건 제외). 골든 재베이크는 별도 체크포인트
