# 3 — 검증 (PlayMode 테스트 + Play e2e + 회귀)

## 목적

"Active 6종이 하나의 문법으로 쓰이고, 아군 버프가 반경 내 전부에 걸리며, 무차감 취소가
모든 경로에서 유지되는가" 에 답한다.

## 변경 대상

- `Assets/_Project/Tests/PlayMode/ActiveTileCastTest.cs` (신규)
- 필요 시 `Assets/_Project/Tests/EditMode/` 기대값 갱신

## 구현 — PlayMode 테스트 (`PlacementAuraTest` 패턴)

1. `SceneNames.Battle` 로드 → `BattleBridge` 획득 → `SetDefenderPool` + `BeginPlacement` +
   코스트 충전 → 유닛 3기 배치(두 기는 인접, 한 기는 반경 밖).
2. **아군 광역**: 인접 2기의 중심 타일에 `CastSkillAtTile(PowerSurge)` → 두 기 `damageMul`
   상승, 반경 밖 1기 불변. `CountDefendersInRange` 가 실제 적용 수와 일치.
3. **아군 0기 거절**: 아무도 없는 타일에 캐스트 → `false` + affected 0
   (호출자 계약: 무차감).
4. **적 장판 무회귀**: 빈 타일에 `CastSkillAtTile(SlowField)` → `true`(0기여도 성공, 계약 5).
5. **퇴화 포탈 거절**(rev, 리뷰 H2): `CastPortal(skill, tile, tile)` → `false`, 서로 다른 두
   타일은 `true`.
5-1. **오라 위에 합산**(rev, 계약 5-1): 가디언 배치(오라) → 인접 레인저 배치 → 두 기를 덮는
   타일에 공격폭증 → 두 기 모두 `damageMul` **증분 +1.0**(오라 기여를 덮지 않음).
6. **EditMode**: `GridMathTests` — `WorldToCellUnclamped` 가 보드 밖 셀을 접지 않고,
   `WorldToCell` 이 그것의 clamp 판임을 고정(계약 9 의 엄격 판정 토대).

> 주: 아군 광역 검증은 **속사(AttackSpeedMul)** 로 잰다. 공격폭증의 `DamageMul` 은 인접 시너지가
> 같은 stat 을 쓰는 채널이라(stackId 만 다름) 절대값이 배치 인접성에 따라 흔들린다.

## Play e2e (에디터, 사용자 육안)

1. 손패에서 Active 카드 press → 카드가 **손패에 남고** 화살표가 뻗는다(부착 카드와 동일).
2. 운석/감속장/회오리: 타일 조준 시 범위 프리뷰가 셀 단위로 따라오고, 릴리즈 = 시전 +
   각성치 20 차감 + 카드가 큐 맨 뒤로.
3. 공격폭증/속사: 아군 없는 곳 조준 = 붉은 화살표 + `범위에 아군이 없습니다`, 릴리즈해도
   **무차감**. 아군 2기 포함 조준 = `놓으면 아군 2기에 시전`, 릴리즈 후 두 기 모두 강화.
4. 포탈: 릴리즈로 입구 지정 → 화살표 기점이 입구로 이동 + 입구/출구 동시 점등 → 두 번째
   탭에 포탈 생성.
5. **회귀**: 부착 카드(Unit/Squad) 락온·적 표식·드래그 중 손패 하강·손패 유지/자동 닫힘·
   유닛 선택 중 Active 차단·쿨다운 없는 순환 재사용.
6. 손패 열린 채(및 포탈 2단계 중) 매치 종료 → 슬로모 lease·`IsAiming`·점등 누수 없음.

## 완료 기준

- [ ] PlayMode `ActiveTileCastTest` 전 케이스 그린.
- [ ] 기존 EditMode/PlayMode 스위트 회귀 없음.
- [ ] Play e2e 1~6 사용자 확인.
- [ ] 콘솔 에러/워닝 0.
- [ ] 투트랙 리뷰(code-reviewer + ecs-reviewer) 지적 반영.
