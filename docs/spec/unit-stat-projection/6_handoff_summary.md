# 6. Handoff Summary

## Commit

- `8a9da47` spec 신설 (unit 0 계약 포함)
- `fe9f653` unit 1 — AttackOutputStats helper
- `9ccd2f6` unit 2·3 — 카드 DMG outputs 파생 + 임포터 atk/heal 투영
- `eb0d2b8` unit 4 — SO attackDamage 필드 삭제
- `6762d84` unit 5 — AttackState.damage dead 필드 삭제
- 선행 hotfix: `f404bfe` (importer 견고성, `unit-stat-spreadsheet-schema/2_...`)

## Implemented

- 기획 시트 `atk`/`heal` → outputs의 유일 Damage/Heal 항목 magnitude로 투영 (exactly-1, 0·2+ skip+사유 로그)
- `AttackOutputStats` static helper (TryGet/TrySetUniqueMagnitude) — UI·임포터 공유 SoT
- 드래프트 카드 DMG를 outputs 파생으로 전환 (Archer 죽은 표기 25→실 15, 직접 데미지 없는 유닛 "-")
- 임포터 DTO `atk`/`heal` + skip-list 상수화 + `attackDamage` deprecation shim 경고 + projected/skipped 카운트
- `UnitRosterInvariantTests` — 전 asset Damage≤1·Heal≤1·타입별 id 유일 고정
- 레거시 `attackDamage`(SO) + `AttackState.damage`(ECS) 완전 제거 — 실데미지 SoT는 outputs 단일화

## Key Files

- `Assets/_Project/Scripts/Data/AttackOutputStats.cs` — 투영 불변식 단일 구현
- `Assets/_Project/Editor/UnitStatImport/` — DTO/매퍼/윈도우(투영·shim)
- `Assets/_Project/Scripts/UI/Draft/DraftCardFanView.cs:261` — 카드 DMG 파생
- `Assets/_Project/Tests/EditMode/{AttackOutputStatsTests, UnitStatImport/*}` — 커버리지

## Verified

- compile 클린, EditMode 444개 회귀 없음 (기지 실패 `ObstaclePlacerTests.Place_PreservesWalkAndMinimumPlaceRatio` 1건은 무관·선재)
- end-to-end 왕복(execute_code): projected/skip+사유/shim 경고 실 asset 경로 확인
- 데이터 검증: Archer outputs=15 등 카드 표기값 확정
- grep: SO attackDamage·AttackState.damage 참조 0건 (DTO shim·aggroAttackDamage 제외)

## Notes

- **aggroAttackDamage는 삭제 금지** — AggroAttackProfile→TauntAttackGrantSystem 소비 live 스칼라. 리플렉션 매핑 유지.
- **투영 exactly-1 규칙**은 미래 "Damage 2회 분할" 유닛에서 게이트가 됨. `UnitRosterInvariantTests` 실패 메시지가 재협상 프롬프트 역할.
- unit 4 후 asset의 stale `attackDamage:` YAML 라인은 무해(Unity 무시). 재직렬화 시 자연 제거 — 별도 스크럽 안 함.
- `90d88a9`(다른 세션)이 죽은 attackDamage를 25로 "밸런스"했으나 실데미지(15) 불변이었음. 필드 삭제로 그 값 소멸, 게임플레이 영향 0. **다른 세션에 "attackDamage는 죽은 필드였다" 공유 필요.**

## Follow-up

- 실 Swagger 엔드포인트 왕복 검증 (URL 확보 시)
- unit 2·4·5 Play smoke 시각 확인 (동시 세션 일단락 후)
- DTO `attackDamage` shim 최종 제거 (시트 v2 전환 확인 후) — README 후속 후보 등재됨
- hazard/스택 틱뎀 시트 노출 (동형 패턴 별도 spec)
