# 6 — 인계 요약

## Commit
브랜치 `unified-effect-layer`(워크트리 `wassup-core`) · **미푸시** · 981498696(U12~U16) 이후 커밋 60여 개. 경계: 1a `26829ee61` `34edea449` · 1b `f8df37837` `4e225742b` `64539a1e7` · 2 `d631cb359`~`92b177185` + U15 `c2e4da732` `3a7230311` `46a5cc628` `0a70da811` · 3 `9c3d0a4e5` `74af83ff2` + U17 `7e18ec9be` · 4 `4dd668aea`~`fc8ac220e` · 이전 `2933243c2` `eacbae0ce` · 4-정리 `f64e18aa3` `f95c44499` `a897eadb8` `6db290c20` `665de2b5d` · 5 `658988c93` `7dc32158f` `980367c1d` `ba48b6742` `e8c735538` `9e05af586` `23628191f`.

## Implemented
- 코어 효과 표(`EffectDef` · 안정 Id) — 규칙 줄은 효과를 가리킨다 · 해시는 해석값만(id·순서 밖).
- 피해는 효과 줄 `damage` 하나(U10 — 패턴·장판·길막·슬램).
- 소유자 쪽 결손: 적이 든 자원 효과 무효(U4) · 판 시전 진영 = 규칙 인스턴스 · 검증 `Any` 오판 수정 · 표식 판정 = 효과.
- 연출은 효과 기준(U15) · 카드 발동 연출은 카드 줄만(U16).
- 비율형 수치(U7 · U11 · U17): 시전 순간 최종 스탯 1회 · 공격력 = 한 발 × 공격자 배율 · 남의 사건 = 숙주 스탯.
- 저작 한 형식: 효과 SO 69 + 소유자 77 의 `bindings` · 거울 enum 은퇴(코어 enum 직접) · 옛 칸·옛 능력 타입·능력 에셋 18·죽은 사본(B21) 삭제.
- 새 시트 I/O: `Skills`(효과) · `SkillOwners`(소유 줄) · 쓰기 전 diff · 옛 `DcMechanics` 탭 은퇴.
- 액티브 카드 비용 문안 = 실제 비용 20(U18) · 별똥 타격 문안 = 「맞은 적 자리에 운석 낙하」.

## Key Files
`Scripts/BattleCore/Trigger/{BindingDef,EffectMagnitude,EffectComboRule,TriggerDispatcher,IntentApplier}.cs` · `Scripts/Data/Effects/{EffectData,EffectValues,BindingSpec,EffectSlots}.cs` · `Scripts/BattleCoreUnity/BindingSpecBuilder.cs` · `Scripts/Data/StatImport/{SkillSheet,SkillSheetDto,DcSheetTabs}.cs` · 굽기 스냅샷 `Tests/EditModeAssets/Fixtures/*_bake_snapshot.txt` · 이전 계획 기록 `dry-run/dry_run_table.md`.

## Verified (2026-09-29)
헤드리스 1035/4 · Unity EditMode 3 어셈블리 2612 — 실패 3 = 선행(카드 아트 · bomb_man · 시트 설명 드리프트 7장) · 굽기 스냅샷: 이전 커밋의 4줄(깃발 목록)만 변경 · 시트 왕복 = 스냅샷 동치 · PlayMode Core 97/97 · 콘솔 0 · core-reviewer APPROVE ×2.

## Notes
- 헤드리스 Retire.Check 는 전 소스를 한 덩어리로 컴파일해 **asmdef 경계를 못 잡는다**(에디터 asmdef 참조 누락을 Unity 가 잡았다 `033afe747`). 아웃게임 EditMode 테스트도 헤드리스가 실행하지 않는다.
- 열린 씬 YAML 을 밖에서 고치면 Unity 가 Reload 창으로 멈춘다 — 에이전트가 씬 YAML 을 만지면 사용자에게 알릴 것.
- 검증 스크립트는 필요한 경로만 export 하고 스스로 지운다(`00bd826cf` — 이전엔 실행마다 ~0.9GB 누적).
- 시트 push 는 업서트라 고아 행을 안 지운다(`5_sheet_io.md`).

## Follow-up
- **사용자**: 실제 시트에 `Skills` · `SkillOwners` 탭 생성(헤더 = `5_sheet_io.md`) → 초기 데이터는 **에디터 export** 로 · 서버 GET 두 탭 · push 업서트 키(`effect_id` / `owner_kind+owner_id+slot`) · 액티브 6장 설명 「비용 20」 · star_strike 설명 · 시트 설명 드리프트 7장 · bomb_man · `kind_ko` 한국어 39개 검토 · 개사기·별똥 타격 카드 아트.
- 도형 탭(탄·패턴·장판) — 옛 피해 칸 SO 정리 뒤.
- 액티브 스킬 드림캐쳐 분리 spec(U18) — `SkillData` 흡수 · DcSkills 이중 원천.
- 방어유닛 이동 시 칸 점유(하드 케이스 3 — 시기상조).
- 코어 전수 적용 가능성 행렬(옛 B21 행렬 대체) · 반각 0° `SectorGate` 경계.
- 머지·푸시는 사용자 승인 후.
