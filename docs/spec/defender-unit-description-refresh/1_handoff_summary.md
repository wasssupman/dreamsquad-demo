# 1 — Handoff Summary

## Commit

- `aa7985ae` feat(defenders): refresh unit descriptions

## Implemented

- `DefenderCatalog` 등록 유닛 24종의 기존 `desc` 값을 실제 능력 기준으로 갱신했다.
- 모든 설명을 `기본 기능 / 배치 스킬 / 특수 효과` 순서의 정확히 3줄로 통일했다.
- 능력이 없는 항목도 생략하지 않고 `없음`으로 표시했다.
- 공격 출력, 투사체, 배치 효과, 능력 SO를 대조해 이름 기반 추측을 제거했다.
- 가디언의 실제로 없는 해저드 설명을 제거했다.
- 피어서의 현재 기본 투사체에 없는 관통 설명을 넣지 않았다.
- `defenders_desc.json`을 24종 최신 문구 스냅샷으로 갱신했다.
- 카탈로그 전체 형식과 한 줄 길이를 검사하는 EditMode 테스트를 추가했다.
- 기존 `DefenderUnitData.desc`만 사용했으며 신규 데이터 필드는 추가하지 않았다.

## Key Files

- `Assets/_Project/Data/Defenders/*.asset`
- `Assets/_Project/Tests/EditMode/UnitKitSummaryTests.cs`
- `docs/spec/squad-character-page/defenders_desc.json`
- `docs/spec/defender-unit-description-refresh/README.md`
- `docs/spec/defender-unit-description-refresh/0_roster_description_refresh.md`

## Verified

- Unity 컴파일 오류 0.
- EditMode 1,538건 중 1,535건 통과.
- 신규 `CatalogDescriptions_UseThreeFixedSections` 테스트 통과.
- 실패 3건은 캐스터 attackRange 계약, 모바일 회전 설정, Zig 맵 복도 병합으로 본 작업과 무관하다.
- 사용자 Play 확인 완료 2026-07-29.
- Defenders 시트 24행과 로컬 설명 스냅샷 24/24 일치 확인.

## Notes

- 개발/QA Play 진입 시 `LoginAutoImport`가 Defenders 시트를 메모리 SO에 자동 반영한다.
- 시트가 옛 문구이면 로컬 에셋이 올바르더라도 Play 화면은 옛 문구로 덮인다.
- 이번 완료 시점에는 시트까지 24/24 새 문구가 반영되어 있다.
- 같은 Defender 에셋에 있던 기존 밸런스 변경은 구현 커밋에서 제외했다.
- 구현 커밋은 설명 필드 hunk만 부분 스테이징했다.
- 원격 Git push는 실행하지 않았다.

## Follow-up

- 신규 유닛 추가 시 동일한 3줄 설명을 작성해야 하며 형식 위반은 EditMode 테스트가 검출한다.
- 배치 트레이나 인게임 선택 화면에 같은 설명을 노출하려면 별도 spec으로 진행한다.
