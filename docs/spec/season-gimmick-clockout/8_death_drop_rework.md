# 8. 룰1 재설계 — 강제 퇴근 폐기 → 사망 시 사직서 드랍

## 목적

룰 1 을 재설계한다. 기존 "전투 시작 10초 후 배치 유닛 **강제 퇴근(사망)**" 은 배치한 유닛이 플레이어 의사와 무관하게 사라져 감성이 불합리하다는 평가(사용자). 이를 폐기하고, **배치 유닛이 (원인 불문) 사망하면 그 배치 타일에 사직서를 드랍**한다. 룰 2(사직서 5장 → 메테오)는 불변.

## 변경 대상

- **삭제**: `Battle/Effects/ClockOutSystem.cs` · `Battle/Effects/ClockOutTimer.cs` · `Battle/BattleRunning.cs` · `Battle/Effects/ClockOutRefundEvent.cs` · `Battle/Effects/ClockOutRefundEventsSingleton.cs` (각 `.meta` 포함)
- **신규**: `Battle/Effects/ResignationDropSystem.cs`
- `Data/Gimmick/ClockOutGimmickData.cs` — `clockOutSeconds` · `clockOutCostRefund` 필드 제거
- `Battle/Effects/ClockOutGimmickConfig.cs` — `clockOutSeconds` · `costRefund` 필드 제거
- `Bridge/BattleBridge.cs` — `BattleRunning` 배관(필드/push/teardown) + 환급 채널(필드/생성/dispose/teardown/drain) + config 주입의 `clockOutSeconds`/`costRefund` 제거. **추가 버그픽스**: `DestroyBattleEntities()` 에 `Resignation` 엔티티 파괴 추가(기존 teardown 누락 — 로비 재진입 시 사직서 잔존)
- `Data/Gimmick/Gimmick_ClockOut.asset` — `clockOutSeconds`/`clockOutCostRefund` 라인 제거 + description 갱신
- `CLAUDE.md` — NativeQueue 채널 20→19 (`ClockOutRefundEventsSingleton` 은퇴)
- `docs/reference/object-pipeline-map.md` — 스폰 진입점 `ClockOutSystem` → `ResignationDropSystem`

## 구현

1. **`ResignationDropSystem`**(Effects, Burst, BattleSimGroup): `RequireForUpdate<ClockOutGimmickConfig>` self-gate. `[UpdateAfter(DamageApplicationSystem)]` + `[UpdateAfter(HealthDeathSystem)]` + `[UpdateBefore(UnitLifecycleSystem)]` — DeadTag 가 붙은 뒤, 파괴 전에 관찰. `WithAll<DeadTag, DefenderUnitTag>` + `RefRO<DefenderTile>` 쿼리로 죽은 defender 의 배치 셀에 `Resignation { cell }` 스폰(ECB).
2. **중복 드랍 없음**: DeadTag 는 사망 프레임에 붙고 같은 프레임 `UnitLifecycleSystem` 이 엔티티를 파괴한다. 관측 창은 1프레임뿐이라 defender 당 사직서 1장.
3. **running-gate 불필요**: 사망은 전투 중에만 발생(배치 페이즈엔 전투 없음). `BattleRunning` 싱글턴은 이 시스템의 유일 소비자였던 `ClockOutSystem` 과 함께 폐기.
4. **코스트 환급 폐기**(사용자 결정): 환급은 "강제로 사라진 유닛" 보상이었다. 강제 퇴근이 없어졌으므로 채널(`ClockOutRefundEvent`/`ClockOutRefundEventsSingleton`/`DrainClockOutRefundEvents`)과 SO 필드 전부 제거. 매 사망마다 코스트를 돌려주면 경제가 크게 느슨해짐.
5. **맥락 경계 유지**: `Resignation`(Effects 소유)은 Effects 시스템이 생성. `DefenderTile`(Units 소유)은 RO 읽기만. 사망 경로(Health 쓰기·DeadTag·파괴)는 Units 그대로.

## 완료 기준

- compile 0 에러(Unity 재컴파일). — **확인 2026-07-21**: force 재컴파일 후 read_console error 0.
- Play(ClockOut 기믹): 배치 유닛이 **전투 중 사망**하면 그 타일에 사직서(흰 종이)가 남는다. 10초 강제 퇴근은 더 이상 없다(가만히 둔 유닛은 사라지지 않음). 사직서 5장 → 메테오 3발(적만)은 그대로. 코스트 환급 없음.
- gimmick=null / 다른 기믹 매치에서 사직서 드랍 미발생(self-gate).
- **로비 재진입 무누수**: 사직서가 남은 채 전투→로비→전투 재진입 시, 이전 판 사직서(뷰+엔티티)가 다시 나타나지 않는다(`DestroyBattleEntities` 에 `Resignation` 추가).

확인 2026-07-21 — Unity 재컴파일 CS 에러 0. 사용자 Play 통과: 사망 드랍 동작 + 로비 재진입 시 사직서 무누수. 번아웃 rename("불금은 없습니다!") 동반.
