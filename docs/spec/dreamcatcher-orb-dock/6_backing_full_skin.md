# 6 · backing 제거 + 100 가득 + 죽은 유닛 스킨

## 목적

사용자 피드백 3건 반영: (1) 피규어 뒤 단색 세로 backing 이 "스프라이트를 강제로 늘린" 인상
→ 제거. (2) 게이지 100 에서 항아리가 피규어로 **가득** 차도록. (3) 피규어 생김새 = **죽은
유닛과 동일한 스킨**(대표 나이트메어 고정 → 실제 처치된 적/디펜더).

## 변경 대상

- `AwakeningGaugeView.cs` — `_fill` Image(단색 backing) 제거, `maxFigures` 44, `OnAwakeningGainedAt`
  가 죽은 유닛 시각 데이터 수신 → 비행 고스트/피규어 스킨 소스로 전달.
- `JarFigurePile.cs` — `SpawnAtTop(ISpineUnitVisualData)` 오버로드(활성 피규어 re-skin).
- `SpineFigureBuilder.cs` — `Reskin(sg, data)`(동결 미니어처 스킨 교체, 스켈레톤 일치 시에만).
- `BattleBridge.cs` — `_enemyTypeByEntity` 등록부(스폰 기록 / 킬 드레인 조회+제거 / teardown Clear).
- `BattleBridge.Dreamcatcher.cs` — `EnemyKilledAwakening` → `Action<int,Vector3,ISpineUnitVisualData>`.
- `DreamcatcherHandController.cs` — `AwakeningGainedAt` 위드닝 + `GainAwakening` 3-arg(디펜더
  사망도 `DefenderUnitData`=`ISpineUnitVisualData` 전달).
- `BountyMarkTest.cs` — 위드닝 시그니처 반영.

## 구현

- **backing 제거**: BuildCanvas 의 Fill GameObject 생성 삭제 + Refresh 의 fillAmount 갱신 삭제.
  피규어 더미가 유일한 채움(잔량 힌트는 개수).
- **100 가득**: `maxFigures 16→44`. 순수 물리 시뮬(interior 116×190, radius 11)로 FillHeight
  측정: 44 → 97%(오버플로우 없음), 50 → 113%(넘침). 44 확정. 씬 배선값도 44 로 동기.
- **죽은 유닛 스킨**: **모든 적이 한 스켈레톤 공유**(`ee98f82…` Casual Character, 스킨만 상이).
  브리지가 `Entity→AttackUnitData` 등록부만 유지하면 **ECS 이벤트 변경 없이** 킬 시점에 죽은
  적 데이터 해결(파괴된 Entity 값도 키 비교 유효, 역참조 안 함). 이벤트 relay 에 `ISpineUnitVisualData`
  동봉 → 뷰가 비행 고스트/도착 피규어를 `SpineFigureBuilder.Reskin` 으로 그 스킨으로 교체.
  스켈레톤 불일치(예: 디펜더 rig 다름)면 Reskin 이 스킵해 대표 스킨 유지(스킨 미존재 예외 회피).

## 완료 기준

- **compile** 그린. EditMode 회귀 없음.
- **re-skin 증명**(오프스크린): Setup(보스)→Reskin(Runner)→Reskin(Tanker) 가 각각 고유 파츠
  스킨 조합으로 교체(예외 없음, 스켈레톤 동일).
- **fill 증명**(순수 시뮬): 44 피규어 정착 시 FillHeight 97%.
- **미완(육안)**: 실전 랜드스케이프 배틀에서 backing 제거·100 가득·적별 스킨 체감(덱은 로비→배틀
  플로우 필요라 오프스크린/직접 Play 로는 딜/덱 미로드 — 사용자 Play 확인).

## 완료 확인
(대기)
