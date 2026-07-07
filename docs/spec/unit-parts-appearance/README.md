# Unit Parts Appearance Spec (파츠 조합 외형 시스템)

**작성일**: 2026-07-07
**상태**: unit 0~4 완료 + unit 6(Enemy 임시 외형) 완료 (2026-07-07, 배치 검증 기준) — 에디터/실기기 시각 확인 잔여, 상세는 `5_handoff_summary.md`
**상위 맥락**: `docs/spec/spine-runtime-4-2-upgrade/` 후속. Layer Lab "2D Art Maker — AMCasual Character"(Spine 4.2.43, 파츠 스킨 479개/16 카테고리 + `full_skins` 1종) 채택 완료, 전 Defender 가 `full_skins` 단일 스킨으로 렌더 중.
**critic 리뷰**: 2026-07-07 2-lane 리뷰(파이프라인 융화 / 조립 편의성) 반영 완료 — 각 unit 문서의 rev 주석 참조. 판정: 구조 블로커 없음, unit 1(프리뷰 경로·캐시 키·eye 틴트)·unit 3(helmet/hair 배타·색 확장·프리팹 1차 입력) 계약 보강으로 해소.
**목표**: 유닛 외형을 "파츠 스킨 조합 + 슬롯 색상" 데이터로 정의하고 런타임에 combined skin 으로 합성한다. 기획/아트가 원하는 모양을 직접 조립하는 에디터 워크플로우를 제공한다. (외형 정의 방식 = 데이터 에셋 필드, 2026-07-07 사용자 확정)

## 구현 문서 목록

| 작업 구분 | 문서 | 목적 |
|---|---|---|
| Unit 0 | `0_data_contract.md` | ISpineUnitVisualData 에 파츠/색상 계약 추가 + 구현체 필드 |
| Unit 1 | `1_combined_skin_runtime.md` | SpineUnitView combined skin 합성 + 캐시 + 슬롯 틴트 |
| Unit 2 | `2_authoring_inspector.md` | 인스펙터 [SpineSkin] 드롭다운 편집 + 유효성 검증 |
| Unit 3 | `3_preset_import_tool.md` | Layer Lab 데모 프리셋 → 유닛 데이터 복사 에디터 유틸 |
| Unit 4 | `4_defender_looks.md` | Defender 16종 1차 외형 조합 적용 |
| Unit 5 | `5_handoff_summary.md` | 종료 인계 (구현 종료 시 작성) |
| Unit 6 | `6_enemy_provisional_looks.md` | Enemy 7종 임시 휴먼 외형 (기어 0 + 원색 틴트, 몬스터 리소스 전 stopgap) |

## 공통 원칙

- **외형은 데이터가 소유한다.** 파츠 스킨 경로 목록 + 슬롯 색상은 유닛 ScriptableObject 필드. 코드/씬에 조합 하드코딩 금지.
- **하위 호환**: 파츠 목록이 비어 있으면 기존 `spineSkinName` 단일 스킨 경로 그대로 동작한다. 현행 `full_skins` 상태가 기본값으로 유지된다.
- **런타임은 Layer Lab 코드 무의존.** Layer Lab 스크립트는 asmdef 없는 Assembly-CSharp 소속이라 `Wassup.Runtime`(asmdef)에서 구조적으로 참조 불가 — 이 경계를 유지한다. Layer Lab 타입 접근은 에디터 유틸(Assembly-CSharp-Editor)만 허용.
- **combined skin 은 유닛 데이터 단위로 캐시**한다 (같은 조합 = `Skin` 객체 공유, 상한 = 유닛 데이터 종수라 정리 불필요). 런타임 아틀라스 리팩(Layer Lab `IsOptimizeSkin` 류)은 사용하지 않는다 — 단일 아틀라스 페이지라 이득이 없다.
- **파츠 스킨 경로 규약**: `{category}/{category}_c_{n}` (Casual Character 스킨 네이밍, 예: `helmet/helmet_c_12`). **예외: 본체는 `skin/skin_1`** (`_c_` 인픽스 없음, unit 1 스모크에서 실측) — 이름 패턴에 의존하지 말고 스킨 목록 StartsWith 스캔으로 다룰 것.
- **색상은 슬롯 단위 틴트**로, 사망 페이드(`Skeleton.A`)와 곱연산으로 독립 동작해야 한다.
- Enemy 는 원래 몬스터형 스켈레톤 수급 전까지 범위 밖(Defender 우선, 2026-07-07)이었으나, **2026-07-07 사용자 지시로 임시 개방** — 7종을 휴먼 스켈레톤에 물림(unit 6, 기어 0 + 원색 틴트, provisional). 몬스터 리소스 도입 시 교체. 데이터 계약(unit 0)은 공용 인터페이스라 Enemy 도 자동으로 갖춘다.

## 파이프라인 커버리지

플레이 오브젝트 신설 없음. 생성→렌더 경로(스폰 → `SpineUnitPool` → `SpineUnitView.Spawn` → SkeletonAnimation)는 변경하지 않고, `Spawn` 내부의 "스킨 적용" 단계만 단일 스킨 → 조합 합성으로 확장한다. `object-pipeline-map.md` 의 정거장 추가/제거 없음 — **N/A (기존 defender/enemy 아키타입의 정거장 내부 확장)**.

## 후속 후보

- **시드 랜덤 외형 생성기** — 카테고리별 후보 풀 + 시드로 웨이브 잡몹/로스터 무한 다양화 (본 spec 의 조합 데이터 구조를 입력으로 재사용)
- Enemy 몬스터형 리소스 수급 시 적용 확장
- 슬롯 색상 팔레트 프리셋 (팀 컬러/레어도 변주)
- Layer Lab 추가 캐릭터 팩 도입 시 멀티 스켈레톤 지원
- 클래스별 공격 애니 배리에이션 (Attack1/2/3 구분 매핑)
