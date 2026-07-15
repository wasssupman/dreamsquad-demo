# 9. 기믹 2분할 + 번아웃 전용 VFX (후속)

> gimmick-match-integration 이후 후속 작업. 그 spec 계약 9("pool=Overwork 1개, 새 기믹 종류 신설 금지")를
> **의도적으로 초과**한다 — 매치당 랜덤 배정에 다양성을 주기 위해 야근 기믹을 두 룰 단위로 쪼갠다.

## 목적

1. **기믹 2분할**: `OverworkGimmickData`(피로도→번아웃 + 레드불→라스트런 두 룰 묶음)를
   **`BurnoutGimmickData`(G1)** 와 **`RedBullGimmickData`(G2)** 두 독립 기믹으로 분리.
   `BattleConfig.gimmickPool = [Burnout, RedBull]` → 매치당 하나만 랜덤 배정(상호배타).
2. **레드불 등장 빈도↑**: `Gimmick_RedBull.asset` `redbullSpawnInterval` 5→3.
3. **번아웃 전용 VFX**: 번아웃 상태(피로 5스택 임계) 유닛 머리 위에 **먹구름+번개** 지속 연출.

## 변경 대상

- 데이터 분할: `Data/Gimmick/{BurnoutGimmickData,RedBullGimmickData}.cs` (신규, `OverworkGimmickData` 대체·삭제),
  SO `Data/Gimmick/{Gimmick_Burnout,Gimmick_RedBull}.asset` (`Gimmick_Overwork` 대체·삭제)
- config 분할: `Battle/Effects/{BurnoutGimmickConfig,RedBullGimmickConfig}.cs` (신규, `OverworkGimmickConfig` 대체·삭제)
- 주입 스왑: `Bridge/BattleBridge.cs` — `CreateGimmickConfigIfActive`(배정 타입에 맞는 config 만 주입),
  픽업 스폰 게이트(`_assignedGimmick is RedBullGimmickData`), 디버그 로그. 파괴는 두 config 모두.
- 소비 시스템 self-gate 타입: `FatigueAccrualSystem`→`BurnoutGimmickConfig`, `Pickup*/LastRunSystem`→`RedBullGimmickConfig`.
- VFX: `Data/StatusFxKind.cs`(`Burnout=3` append), `Data/Config/StatusFxRegistry.asset`(kind 3 엔트리),
  `VFX/Burnout_SKELETON.prefab` + `VFX/Materials/Burnout_{Cloud,Spark}_Mat.mat` (신규),
  `Bridge/BattleBridge.cs` `ReconcileStatusFx`(Burnout 블록).

## 구현 계약

- **배정은 상호배타**: `_assignedGimmick` 타입 분기로 config 하나만 주입. 두 룰이 한 판에 동시 활성될 일 없음
  (원래 Overwork 는 둘 다 켰음 — 이게 동작 변화점).
- **번아웃 감지 소스 = `ModifierOrigin.Stack`**: `StackModifierTickSystem` 이 임계(ApplyStat) 파생 스탯에
  `origin=Stack` 을 찍는다. 현재 Stack 파생 스탯 소스는 **Fatigue 임계뿐**(Bleed=ApplyDot). 따라서
  "origin==Stack 슬롯이 활성(remaining>0)" = 번아웃 창과 정확히 일치. 만료 시 슬롯 제거 → VFX 자동 회수.
- **reconcile 는 Empowered 와 같은 버퍼 스캔에 합류**: `_modifierSlotQuery` 1회 순회로 Empowered(Dreamcatcher net-편차)
  + Burnout(Stack 존재) 동시 판정. StatusFxSpawner 는 `(entity, kind)` 키라 한 유닛에 둘 공존 가능.
- **VFX 는 `_SKELETON`**: 먹구름=URP/Particles/Unlit **알파**(다크가 보이려면 additive 불가) 소프트 빌보드,
  번개=URP/Particles/Unlit **애디티브** 스트릭 스파크. 두 머티리얼 모두 transparent 키워드+queue 3000 필수
  (MCP float 설정만으론 opaque 로 남아 blend 무시됨 — 실측 함정).

## 완료 기준

- compile 통과 + 콘솔 에러 0. EditMode 회귀 pass.
- 배정된 기믹이 Burnout 이면 피로 누적/번아웃만, RedBull 이면 레드불/라스트런만 동작(상호배타).
- 번아웃 유닛 머리 위 먹구름(다크)+번개(블루 스파크) 표시, 15s 창 종료 시 회수.
- 레드불 스폰 주기 3초.

확인 2026-07-15 · 커밋 `<pending>` — Play: 컴파일 클린, 콘솔 0. VFX 룩 game-view 실측: 먹구름 알파(다크) + 번개
애디티브(블루) 정상 렌더(머티리얼 transparent 수정 후). ⚠ 남은 육안: 실 유닛 번아웃(50s 누적) end-to-end 트리거
+ 스케일/offset/밀도 미세 튜닝은 사용자 플레이테스트. reconcile 트리거는 검증된 Empowered 패턴 미러로 코드 검증.

## 주의점

- **먹구름 밀도/스케일은 초기값**(registry scale 0.5, offset y2, cloud rate 10·maxParticles 24). 실 유닛에서
  약하면 rate↑, 크면 scale↓. 프리팹 스테이지로 반복.
- **`Burnout_*_Mat` 는 EmpowerAura 머티리얼 텍스처 재사용**(Glow/Streak). 정식 아트는 후속.
- **번아웃 VFX 는 `season-gimmick-overwork` unit 6 의 "상태 연출 위임" 결정을 국소 번복**: 그때는 Buffed/Debuffed
  오라에 위임했으나, 번아웃은 전용 룩(먹구름+번개) 요구가 생겨 dedicated StatusFxKind 로 승격. Debuffed 오라와
  중복 가능하나 origin 소스가 달라(Stack vs 일반 디버프) 의도된 병존.
