# 1. DcMechanics 판별자 필드 조건부 blank export

## 목적

`ccKind`/`stackKind`/`buffStat` 는 특정 payloadKind 만 소비하는 선택자인데, exporter 가 전 행에 무조건 emit 해서 소비 안 하는 행에도 enum 0번값(Stun/Fire/AttackDamage)이 찍힌다 → 기획자가 "이 카드가 CC/스택/버프를 쓴다"고 오인. 실제 소비하는 payload 행에만 값을 쓰고 나머지는 blank(빈 셀)로 export 한다.

## 소비 규칙 (DcPayloadSpec 계약)

| 필드 | 값을 쓰는 payloadKind | 현재 실사용 카드 |
|---|---|---|
| `ccKind` | `ApplyCcToTarget` | frost_arrow |
| `stackKind` | `ApplyStackToTarget` | ember_bite |
| `buffStat` | `SelfStatBuff` | devouring_craving, last_stand |

그 외 payload 행에서는 이 필드가 무의미 → export 시 null(=`NullValueHandling.Ignore` 로 JSON 키 생략 → 시트 빈 셀).

## 변경 대상

- `Assets/_Project/Editor/UnitStatImport/DcSheetExporter.cs` (`MechanicRow` 생성, `:61-70`) — 세 필드를 payloadKind 조건부로 대입:
  - `ccKind = m.payload.kind == DcPayloadKind.ApplyCcToTarget ? m.payload.ccKind : (DcCcKind?)null`
  - `stackKind = m.payload.kind == DcPayloadKind.ApplyStackToTarget ? m.payload.stackKind : (DcStackKind?)null`
  - `buffStat = m.payload.kind == DcPayloadKind.SelfStatBuff ? m.payload.buffStat : (CardBuffKind?)null`
- `docs/spec/dreamcatcher-sheet-sync/0_json_schema_contract.md` — DcMechanics payloadKind→사용컬럼 매트릭스에 "판별자는 해당 payload 행에만 값, 나머지 blank" 명시.
- `docs/spec/dreamcatcher-sheet-sync/7_full_dreamcatcher_export.json` — 재생성 스냅샷(비-소비 행에서 판별자 키 사라짐). 참조 아티팩트라 export 재시드 때 갱신.

검증은 합성 단위테스트 대신 **실제 export 산출물 기능 검증**으로 한다: 판별자 조건은 display-formatting(sim-critical 아님)이고, `ExportToFolder` 는 디스크/에셋 스캔 통합 경로라 순수 단위테스트에 부적합하며, 이를 위해 row 빌더를 추출하는 것은 과잉 추상화(제약 8)다. 실제 SO→payload 산출물에서 판별자 분포를 확인하는 것이 더 강한 회귀 체크다.

## 스코프 밖

- 트리거 스칼라(`triggerPeriod`/`triggerPeriodSeconds`/`triggerFraction`) 및 `magnitude`/`tileRange`/`duration` 의 조건부 blank 는 이번 제외 — magnitude 등은 다수 payload 가 공유해 blank 규칙이 복잡. 판별자 3종만.

## 구현 노트

- import 는 partial-update(blank=기존값 유지)라 비운 행을 re-import 해도 SO 기본값 유지 → 라운드트립 안정(export blank → 시트 blank → import no-op → export blank, IDENTICAL).
- 조건 대입은 순수 값 분기 — exporter(Editor asmdef) 내부라 아키텍처 종속 없음.

## 완료 기준

- [x] compile 0 error.
- [x] 통합 export 산출 JSON 에서 ccKind 는 frost_arrow, stackKind 는 ember_bite, buffStat 는 devouring/last_stand 행에만 존재(나머지 8행 키 부재). 시트 반영 후 fetch 재확인 동일.
- [x] 라운드트립: 재-import 후 SO mechanics 값 변화 0 (git diff 값 드리프트 0).

확인 2026-07-13 — 시트 DcMechanics 판별자 분포: ccKind→frost_arrow · stackKind→ember_bite · buffStat→devouring_craving,last_stand. 왕복 IDENTICAL.
