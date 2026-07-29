# 4 — handoff summary

## Commit

- `e109271a` docs(enemy-fire-stack-shooter): 레인저 저격 화염 축적 원거리 적 spec
- `58f7b25c` fix(projectile): 투사체가 부여한 스택이 누적되지 않던 것 (unit 0)
- `698b38e0` feat(enemy): 화염 스택 임계 규칙 신설 + 씬 배선 (unit 1)
- `8799bf8d` feat(projectile): 파이어볼 투사체 에셋 (unit 2)
- `74148cb7` feat(enemy): 킨들러 — 레인저 저격 화염 축적 원거리 적 (unit 3)
- `0fa44ab8` fix(projectile): 파이어볼 크기 실측 보정 (unit 2 rev1)
- `c0577064` feat(enemy): 킨들러 외형 — 얼굴을 가려 실루엣으로 구분 (unit 3 rev1)
- `40aef22d` balance(enemy): 킨들러 화상을 1초마다 4데미지 5초간으로 (unit 1 rev1)

## Implemented

- **투사체 스택 귀속 결함 수정** — `ProjectileHitSystem` 의 `ApplyStack` 이 `source` 로
  투사체 엔티티를 실어 발사마다 새 슬롯을 만들었다. 사수(`ProjectileState.owner`)로 교체해
  근접 경로와 같은 규약이 됐다. 이게 없으면 이 feature 전체가 성립하지 않는다.
- **`StackModifier_Fire` 신설** — 프로젝트 최초의 Fire 스택 임계 규칙.
  `atStack 5 · Consume · ApplyDot` · 틱당 4 / 1.0s / 지속 4.85s = **5틱 · 1회분 20**
  (2026-07-30 사용자 지정 "1초마다 4데미지 5초간" 반영).
- **파이어볼 투사체** — PixPlays 부품 프리팹 3종 복제(스트립 불요) + `ProjectileData`.
  크기는 오프스크린 실측으로 확정: `visualScale 1.4`(월드 0.49 ≈ 반 타일) ·
  `hitVfxScale 0.55`(1.45). 초안 0.35 는 타일의 12% 라 점으로 보였다.
- **킨들러** — Shooter · 사거리 4 · 쿨다운 1.2 · HP 45 · `targetClassMask = Ranger 단독` ·
  `FocusUntilDead` · `Halt` · `minWaveNumber 2`. `EnemyCatalog` + 라이브 맵 덱 6종 등록.
- **신규 테스트 2** — `ProjectileApplyStackAccumulatesTest`(귀속 회귀) ·
  `KindlerFireStackE2ETest`(스폰→조준→히트→누적→임계→도트 전 사슬).

## Key Files

- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` — `ApplyStack` 귀속
- `Assets/_Project/Data/Dreamcatcher/StackModifier_Fire.asset` — 임계 규칙
- `Assets/_Project/Data/Enemies/Enemy_Kindler.asset` — 유닛 스탯·outputs·클래스 마스크
- `Assets/_Project/Data/Projectiles/Projectile_Enemy_Fireball.asset` + `VFX/Projectiles/PixPlays/`
- `Assets/_Project/Tests/PlayMode/{ProjectileApplyStackAccumulatesTest,KindlerFireStackE2ETest}.cs`
- `Assets/_Project/Scenes/BattleScene.unity` — `stackModifierAuthoring` 4번째 칸

## Verified

- **EditMode 1584 중 1582 pass / 0 fail** (skip 2 = 기존 Ignored).
- **PlayMode 신규 회귀 0** — 베이스라인 `3e7440af` 69/57/12 → 변경 후 71/59/12,
  **실패 집합이 동일**. 신규 테스트 2건은 둘 다 통과.
- **mutation 실측** — `source = entity` 로 되돌리면 14발이 14슬롯(`Expected 1 / But was 14`).
  결함 지문과 테스트 검출력을 동시에 증명했다.
- **e2e 실측** — 사거리 안의 가디언이 화염 스택을 한 번도 못 받는다(클래스 필터 직접 증거),
  아처는 단일 슬롯에 누적 → `(Stack, Fire)` 도트 발화.
- 전부 **testrig 배치 실행**. 에디터가 Play Mode 라 MCP 경로가 막혀 있었다.
- **rev 후 재검증(`40aef22d`)** — 화상 수치 변경·외형 변경 후 신규 테스트 2건 재실행 **둘 다 Passed**.

## Notes (되돌리지 말 것)

- **`ApplyStack` 의 `source` 는 사수다.** 투사체 엔티티로 되돌리면 누적이 즉시 죽는다.
  가드 = `ProjectileApplyStackAccumulatesTest` 의 "슬롯 1개" 단언.
- **`ApplyStat` 은 일부러 안 고쳤다.** 같은 결함이지만 `Enemy_Debuffer` 의 `DamageMul ×0.6` 이
  곱누적 → 상시 ×0.6 으로 바뀌어 라이브 밸런스가 움직인다. `modifier-stacking-policy` 의
  클램프 `[0.2, 5]` 가 현재 병리를 경계하고 있다. 사유는 코드 주석 + README 후속 후보.
- **`duration 4.85` 를 `5.0` 으로 바꾸지 말 것.** `tickInterval` 배수에 걸리면 마지막 틱과
  만료가 같은 프레임에서 경합한다. 실측: `5.0` 은 60·144fps 5틱 / 30·50·72·23.7fps **6틱**
  (20 vs 24, 20% 편차). `4.85` 는 전 구간 5틱 고정.
- **`targetMode` 를 `Nearest` 로 바꾸지 말 것.** 걸어가며 최근접이 바뀌면 어느 레인저도
  5스택에 도달하지 못한다 — 누적형 적의 전제 조건이다.
- **`maxStack`/`perAppDuration` 은 유닛 SO outputs 와 `StackModifierSO` 양쪽에 있다.**
  한쪽만 바꾸면 조용히 어긋난다(가드 = EditMode 이중 권위 단언은 testrig 임시본이라
  커밋되지 않았다 — 다음에 손댈 때 주의).
- **투사체 outputs 는 `SingleSplash` 페이로드에서만 처리된다.** 파이어볼을 방향탄/광역으로
  바꾸면 스택이 조용히 멎는다.
- **테스트에서 적 SO 를 `EnemyCatalog` 로 찾지 말 것.** BattleScene 에 로드되지 않는다
  (OutgameScene 전용). `MapDocumentPool`→덱→`attackUnitPool` 경로로 올라온 것을 쓴다.
- **투사체 PlayMode 테스트는 `bridge.StartBattle()` 필수.** 스폰 드레인이
  `if (!_running) return;` 뒤에 있다.

## Follow-up

- **사용자 Play 확인 (미완)** — 아래는 프레젠테이션이라 배치로 판정할 수 없다:
  ① 파이어볼이 진행 방향으로 서는지 · 트레일이 남는지 · 보드에 눕거나 묻히지 않는지
  ② **히트 VFX 가 어두운 연기 위주라 밝은 보드에서 검은 얼룩처럼 보이지 않는지**
  (오프스크린은 어두운 배경이라 이 판정에 관대하다) ③ `StatusFxKind.Fire` 오라 점등/소등
  ④ 데미지 숫자가 **1초 간격 정수 "4" × 5회** ⑤ 펄스 리듬(4.85s 화상 → 1.15s 공백 → 재발화)
  ⑥ 어그로 예외(가디언이 때리면 조준이 넘어감) · 레인저 부재 시 통과.
- ✅ **씬 리로드 완료 (2026-07-30)** — `stackModifierAuthoring` 4칸 확인
  (`StackModifier_Fire(Fire, max5, thr1)` 포함), 씬 dirty 아님.
- **밸런스 미검증** — 수치는 전부 placeholder. 화상 1회분 20 은 아처 HP 293 의 약 7%.
  킨들러 1기 총 ≈7.5 DPS(직격 4.2 + 화상 평균 3.3) → 아처 1기 처치에 약 39초.
- 나머지는 README 후속 후보 참조(`ApplyStat` 귀속 · 전투 스택 오버헤드 아이콘 ·
  화상 히트 VFX · 다중 킨들러 화상 합산 · 전용 아트).
