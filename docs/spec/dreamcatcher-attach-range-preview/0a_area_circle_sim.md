# 0a — 스킬 광역 사각 → 원 (sim · 유일한 판정 변경)

> 결정 Q2/Q2-b/Q8/D2/D3. 반경 `N + 0.5`, 몸 걸침. `Chebyshev` 는 기능 보존·컴파일 차단. 범위 = `RangeMetric` 소비처만.

## 목적

`distance-based-range` 결정 4 가 남긴 「사거리는 원, 스킬 광역은 사각」 공존을 닫는다. 그 spec 계약 7 이
처방한 「arm 은 rename」을 이행하는 단위다 — 단 사각 술어 본체는 남긴다(사용자 결정).

## 변경 대상

- `Scripts/Skills/ISkillContext.cs:35` — `enum RangeMetric : byte { AreaCircle = 0, Euclidean = 1,
  [Obsolete("사각 광역 은퇴(attach-range-preview 0a) — 되살릴 땐 이 속성부터", true)] Chebyshev = 2 }`.
  **0 = 원**이라 `default(RangeMetric)`·인자 누락이 조용히 사각으로 가지 않는다.
- `Scripts/Battle/Skills/EcsSkillContext.cs:450~468` — `AreaCircle` 분기 =
  `SkillMath.InBodyReach(d/t, tileRange, CellHalfWidthTiles, targetR)`. 사각 분기는 제거하고 `default:` 는
  loud 실패(`Debug.LogError` + 후보 0). 사각 술어 본체는 `SkillMath.BodyOverlapsSquare` 에 그대로 남는다.
- concrete 10곳 → `RangeMetric.AreaCircle`(컴파일러가 전부 찍어 준다): `AreaSleepSkill:46` · `AreaCcSkill:34`
  · `AreaDotSkill:32` · `AreaStackSkill:33` · `AreaTauntSkill:42` · `StatAuraSkill:60,65` · `GrantShieldSkill:74`
  · `TileStatBurstSkill:26`(조준 셀 중심 원 — D6) · `ConeBreathSkill:52`(사전필터, 콘 판정 무변).
  ⚠ `EmitPatternSkill:91` 은 `Euclidean` **유지**(탄 비행 거리).
- `Tests/EditMode/TestSkillContext.cs:167~176` — **페이크를 몸 인식으로 승격**: `TestUnit.BodyRadius` 추가,
  두 arm 이 `SkillMath.InBodyReach` 를 **직호출**(`AreaCircle` = `(…, tileRange, 0.5f, targetR)` ·
  `Euclidean` = `(…, tileRange, 0f, targetR)`, 입력은 `/ TileSize` 로 타일 단위). 페이크가 판정 본체를
  공유하면 어댑터와 갈릴 자리가 없다. 사각 페이크 분기는 삭제.
- stale 주석·문서 정합(같은 커밋): `TileAoe.cs:40~48`(「정사각형이 남은 곳 … 스킬 arm」) ·
  `docs/spec/distance-based-range/README.md` 계약 7 rev 포인터 · 결정 4 잔여 문구.
- `Tests/Golden/*.trace.txt` 8건 + `docs/spec/battle-sim-extraction/golden-corpus.md` — **격리 커밋**.

## 구현

- **식 하나.** 광역 원 = 투사체 TileAoe 착탄(`ProjectileHitSystem:747`)과 같은 `InBodyReach(d, N, 0.5, targetR)`.
  `SelfAreaBlastSkill` 은 이미 이 식이라 무변. 중심은 `ctx.Position(caster)` 그대로(양자화 없음 — 2×2 의
  기하 중심이 셀 경계 위에 온다). ⚠ 기존 `Euclidean` arm(`InBodyReach(d, N, **0**, targetR)`)을 그대로 쓰면
  안 된다 — 칸 반폭이 빠져 반경 2 의 정대각이 빠진다. 그래서 arm 이 하나 더 필요하다(`AreaCircle`).
- **액티브 6종 중 판정이 바뀌는 것은 `slow_field`(TileStatBurst) 하나다.** 버프장 2종·회오리·운석은 이미 이 식,
  포탈은 반경 0.5 점 판정 무변(README 전수 표).
- **밸런스 파급 — 숨기지 않는다.** 사각 → 내접원은 연속 면적 −21.5%. 카드 4장(N=1)은 셀 기준 손실 0 이지만
  같은 concrete 를 쓰는 저작이 좁아진다:

  | 저작 | 조합 | N |
  |---|---|---|
  | `Ability_AllyDamageAura_Guardian` · `Ability_AreaShield_ShieldShuttle` · `Ability_BleedBurst_Slasher` · `Ability_OpeningBeam_Busters` · `Ability_Quake_Malphite` · `Ability_Taunt_Bastion` | OnPlace × 광역 | 2 |
  | `Ability_SlowAura_Archer` | OnPlace × OpponentStatAura | 3 |
  | `Enemy_Boss_Mamemo`(AreaSleep · GrantShield) · `Enemy_Boss_Nightmare`(AllyMoveSpeedAura) | PeriodicTimer | 4 · 3 |
  | `Enemy_Dragon` | AttackN × AreaBreath | 3 (사전필터만 — 결과 무변) |

  방향은 `TileAoe.cs` 헤더가 이미 「의도한 일관성」으로 적어 둔 쪽이다. **골든 재베이크 후 시나리오별
  킬·유출 변동을 수치로 보고하고 사용자 승인을 받은 뒤** 0b 로 간다(D2).
- **커밋 2개 — 예외를 명시한다.** ① sim 코드 + 테스트 + 주석 정합, ② 골든 8건만(부모 계약 10 「무관 dirty
  격리 · 골든만 담은 커밋」). Play 중 `Wassup/Battle/Sim Harness/Regenerate Golden Corpus` → 2회 일치 확인.
- 신규/변경 `.cs` 뒤 `refresh_unity(scope=all)` — `.meta` 미생성으로 어셈블리에서 빠지면 테스트가 stale 로 초록이다.

## 완료 기준

- [x] `RangeMetric.Chebyshev` 참조 0 — 컴파일러가 보장(grep 테스트 없음). (`9cd583b0`)
- [x] `AreaSleepSkillTests` · `StatAuraSkillTests` · `GrantShieldSkillTests` · `SkillLayerEndpointTests` 초록.
      N=1 대각 후보 포함 단언은 **유지**(`AreaCircleMembershipTests.AreaSleep_N1_KeepsDiagonalNeighbour`).
      `AreaTauntSkillTests` 의 사각 단언은 원 2건(안쪽 대각 포함 · 정대각 모서리 제외)으로 교체.
- [x] 페이크·어댑터가 같은 `SkillMath.InBodyReach` 를 호출한다. `AttackReachTests` 의 `BodyOverlapsSquare`
      단언은 그대로 초록(보존 기능).
- [x] EditMode 코어·에셋 lane 2692건 중 실패 2건 = 선행(`bomb_man`·`boomerang` 문안).
- [x] **골든 A/B(2026-09-02, 같은 세션·같은 조건)**: 0a 이전 코드와 이후 코드로 각각 8건을 구웠고 **8건 전부
      바이트 동일**(Verify 2회 일치 포함). 이 코퍼스의 시드에서는 사각 모서리 대역에 후보가 선 적이 없다 —
      **킬 경제 변동 0.** 파일: 스크래치 `golden_pre0a/` · `golden_post0a/`.
      ⚠ 리뷰(ecs H-1): 이 결과는 **N=1 만 증언**한다 — 코퍼스에서 바뀐 arm 을 타는 카드가 `shield_lull`(N=1) 하나라
      동일은 당연하다. N≥2 저작 9건(보스 마메모 수면 4 · 아처 둔화 오라 3 · 드래곤 3 · 배치 스킬 6종 2)의 축소는
      셀 기준 N=2 25→21 · N=3 49→45 · N=4 81→69 이고, 이 손실표를 `AreaCircleMembershipTests
      .SquareToCircle_LosesOnlyCorners_ByTheDocumentedCount` 가 핀한다(원 ⊂ 사각, 빠지는 칸은 모서리만). 밸런스
      수용은 D2 로 결정됐고 실측은 Play 육안(unit 4) 몫이다.
- [ ] ⚠ **골든 재베이크는 커밋하지 않았다 — 조건 드리프트.** 현 세션 베이크의 `configHash` 가 HEAD 골든
      (`e1c11669`, 36ffa86a…)과 다르다(454575ab…). 헤더 주석대로 「코드 회귀가 아니라 조건 드리프트」이며 0a 와
      무관하다(A/B 동일이 증거). 결과도 크게 다르다(basic 5→2킬 · long_boss 21→11 · `no_defense` 유출 0→3).
      유력 출처: 시트 임포트는 로비 로그인(`LoginAutoImport` → `AllRuntimeRefresher`)에서만 돌아 **BattleScene
      직접 Play 는 디스크 SO 값**을 쓴다 — 이전 베이크 환경(로그인 후 전투 진입?)과 다를 수 있다. 씬은 둘 다
      `BattleScene`(MapTest 에는 브리지가 없다) · dev 맵 오버라이드 키 없음. **어느 조건이 정본인지 사용자 결정 후**
      그 조건에서 1회 재베이크·커밋(부모 spec 계약 10). HEAD 골든은 복구해 뒀다.
