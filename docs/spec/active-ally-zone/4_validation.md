# 4 — 검증

## 목적

"액티브는 지정한 칸에 영역을 만들고 그 안의 대상이 영향받는다" 가 예외 없이 성립하는지,
선택 흐름이 왕복 없이 이어지는지 확인한다.

## PlayMode 테스트 (`ActiveAllyZoneTest`, 신규)

`ActiveTileCastTest` 패턴 재사용(Battle 씬 로드 → `SetDefenderPool` → `BeginPlacement` →
코스트 충전 → 배치 → `StartBattle`).

1. **장판 안 강화**: 아군 2기를 인접 배치 → 그 중심에 공격폭증 → 두 기 `attackSpeedMul`/
   `damageMul` 상승. 반경 밖 1기 불변.
2. **빈 칸 허용**: 아군 없는 칸에 캐스트 → **성공**(구 0기 거절 폐기 확인).
3. **사후 진입**: 장판을 깔고 그 안에 유닛을 **새로 배치** → 강화된다.
4. **이탈 소멸**: 장판 밖으로 재배치 → `applySec` 경과 후 원복. 재배치는 뷰 코루틴을 거치지 않고
   `TryBeginDefenderRelocation(from, to, out entity, out _)` → `FinishDefenderRelocation(to, entity)`
   두 호출로 구동한다(`BattleBridge.Relocation.cs`).
5. **만료 소멸**: 지속시간 경과 → 원복. 엔티티도 정리(잔존 0).
6. **오라 합산 유지**: 선행 spec 의 `AllyBuff_StacksOnTopOfPlacementAura_NotReplacing` 를 장판
   기준으로 갱신해 그린.
7. **멱등**: 매 프레임 갱신이 돌아도 값이 누적되지 않는다.
8. **지연 0**(계약 3-2): 캐스트 직후 프레임 안에 버프가 걸린다.
9. **겹침 비누적**(M6): 같은 스킬 장판 2장을 겹쳐도 merge 키가 같아 한 슬롯으로 접힌다 —
   의도된 동작이므로 테스트로 고정한다(미래 독자가 "버그" 로 고치지 않게).
10. **정지 무회귀**(C1): 장판이 사는 동안 일시정지 → 재개 시 버프가 한꺼번에 몰려 터지지 않는다.
11. **적 장판 0기 성공 유지**: 선행 spec 의 `AllyBuff_NoAllyInRange_RejectsWithoutSpend_ButEnemyFieldStillCasts`
    는 전제가 반대로 뒤집혔으므로 **갱신이 아니라 재작성** — 적 장판 절반만 남긴다.

> 시간 경과 검증은 프레임 펌핑이 필요하다 — PlayMode 만 가능(EditMode 로 내리지 말 것).
> **대기 시간 계산**: 테스트가 만드는 `SkillData` 의 `durationSec` 를 짧게(≈0.4초) 잡고,
> `EffectSpawner.AllyBuffApplySec`(public const)를 **읽어서** 대기시간을 만든다. 상수를 테스트에
> 복제하면 knob 을 조정하는 순간 조용히 어긋난다.
> **재배치 부작용 고정**(M5): `TryBeginDefenderRelocation` 이 `PendingDeployment` 를 붙이므로
> **장판 안에서 안으로** 옮기는 동안에도 비행 시간만큼 버프가 끊긴다. 의도로 받아들이고 단정한다.

## Play e2e (에디터, 사용자 육안)

1. 공격폭증/속사를 **빈 칸에** 놓아 본다 → 장판이 보이고 각성치가 차감된다.
2. 장판 위 아군이 강화 중으로 보인다. 다음 카드를 조준해도 장판 점등이 유지된다.
3. 장판을 깔고 그 위에 유닛을 새로 배치 → 강화된다. 밖으로 재배치 → 풀린다.
4. 조준 문법이 6종 동일하다. "범위에 아군이 없습니다" 가 더 이상 없다.
5. **유닛 선택 중 액티브 카드 드래그** → 선택·패널·줌이 풀리고 손패는 남고 조준이 이어진다.
   커밋/취소 후 평시 상태. 선택 중 부착 카드 **탭 즉발**은 여전히 동작.
6. 회귀: 부착 락온 · 적 표식 · 포탈 2단계 · 드래그 중 손패 하강 · 맵 밖 릴리즈 취소 ·
   손패 유지/자동 닫힘.

## 완료 기준

- [ ] PlayMode 신규 스위트 전 케이스 그린 + 기존 EditMode/PlayMode 회귀 없음.
- [ ] Play e2e 1~6 사용자 확인.
- [ ] 콘솔 에러/워닝 0.
- [ ] 투트랙 리뷰(code-reviewer + ecs-reviewer) 지적 반영.
- [ ] `docs/reference/object-pipeline-map.md` 에 아군 버프 장판 아키타입 반영(README 파이프라인
      커버리지 표 기준).

> 확인 2026-07-30 — 커밋 `2b8b3efd` · 사용자 Play 육안 확인 완료.
