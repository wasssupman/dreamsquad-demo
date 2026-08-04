# 8 — 청사진 ②: 데이터 대응표 + 게이트 이식 매트릭스

## 목적

ECS 컴포넌트의 **형태가 아니라 시맨틱**을 이식하기 위한 대응표. 특히 "컴포넌트 부재 = 상태"(`WithNone`·tag)와 `RequireForUpdate` 게이트(= 시스템의 **행동**, 35개 실측)를 기계적으로 나열해, 이식 중 조용한 시맨틱 유실(예: IncomingDamage 0 이면 Regen 힐도 정지)을 차단한다(설계 정본 §3).

## 변경 대상

- 신규 `docs/spec/battle-sim-extraction/m1_blueprint_data_mapping.md`
- (선택) 추출 보조 에디터 유틸 — 수기 누락 방지용. 대응표가 산출물이고 유틸은 소모품

## 구현

- **컴포넌트 전수**: IComponentData 96 + IBufferElementData 21(2026-08-03 실측). 각 행: 이름 · 소유 맥락 · 태그/데이터 구분 · 쓰기 시스템 · 읽기 맥락 · plain struct 매핑(필드/컬렉션/소속 모듈) · 부재-상태 여부.
- **게이트 매트릭스**: `RequireForUpdate` 35 시스템 — 게이트 조건(all/any/singleton) → 신 sim 가드 조건 번역. `RequireAnyForUpdate` 비-Burst 분리 지점 포함.
- **enableable**: `ModifierStatsDirty`(유일) → 명시 dirty set 설계.
- **비보존 아티팩트 폐기 목록**: `[InternalBufferCapacity]`·ParallelWriter 방어 타이핑·죽은 ECB 등(설계 정본 §3 "비보존").
- **SimEntityId 등록부**: 매치 내 ordinal ↔ 신 sim 객체 참조의 매핑 규칙(unit 4 의 producer tick 정규화 등록부가 선례).

## 완료 기준

- 컴포넌트 96+21 전수(당시 grep 수와 대조해 빠짐 0 증명), 게이트 35 전수.
- 부재-상태 로직 목록이 별도 섹션으로 분리돼 있다(이식 시 개별 체크박스가 되도록).
- 코드 변경 0(선택 유틸 제외).
