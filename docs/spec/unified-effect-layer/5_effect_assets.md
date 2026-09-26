# 5 — 효과 자산화 (저작 2단계)

## 목적
효과를 **한 번 정의하고** 출처가 참조한다. 오늘 `DcMechanic` 은 트리거와 페이로드를 값 struct 로 한 덩어리에 들고 있어, 같은 효과를 두 출처가 쓰면 값이 **복사**된다(탐침 케이스 1: A·AA 두 줄). 계약 1 의 「효과는 출처를 모른다」를 저작에서도 참이게 한다.

## 변경 대상
- 신설 SO `Scripts/Data/Effects/EffectData.cs`(가칭) — 오늘 `DcPayloadSpec` 의 필드(페이로드 종류 · 크기 · 탄 · 반경 · 시간 · 발사 명세 …).
- `DcMechanic` — `payload`(값) 옆에 `effect`(SO 참조) + 필드별 오버라이드 없음(첫 판은 참조 아니면 값, 둘 중 하나). 참조가 있으면 참조가 이긴다.
- `BindingDefinitionBuilder` · `CardDefinitionBuilder` — 참조를 풀어 오늘과 같은 `BindingDef` 를 만든다(코어 무변).
- 마이그레이션 에디터 스크립트: 카드 60 · `UnitSkillAbility` 26 · 악몽 24 의 값 → 중복 제거한 `EffectData` 에셋 + 참조. **dry-run 표 먼저**(같은 효과로 묶이는 것 목록)를 사용자에게 보인 뒤 실행.

## 구현
- 값 필드는 마이그레이션 끝나도 한동안 남긴다(시트 임포터가 unit 6 전까지 값 필드에 쓴다). 제거는 unit 6.
- 에셋 위치 `Assets/_Project/Data/Effects/`. 이름 = 효과 뜻(`Effect_Meteor_Radius2` 식이 아니라 사람이 부르는 이름 — dry-run 표에서 사용자 확인).

## 완료 기준
- 굽힌 `BindingDef` 줄 전체가 마이그레이션 전과 필드 동치(unit 4 의 빌더 스냅샷 테스트 재사용)(코어 입장에서 무변) · 골든 11 · EditMode Assets lane.
- 시트 임포트를 한 번 돌려도 참조가 안 깨진다(임포터가 값 필드만 쓰므로) — curl 읽기 전용 대조로 확인.
