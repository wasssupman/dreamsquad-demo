# 5 — 효과에 정체를 준다 (저작 · H4)

## 목적
효과를 **한 번 정의하고 참조**한다(계약 5). 오늘 `DcMechanic` 은 트리거와 효과 값을 한 struct 에 들고 있어, 같은 효과를 두 출처가 쓰면 **값이 복사**된다 — 한쪽만 고쳐지는 드리프트의 자리다.

## 변경 대상
- 신설 SO `Scripts/Data/Effects/EffectData.cs`(가칭) — 오늘 `DcPayloadSpec` 의 필드.
- `DcMechanic` — `payload`(값) 옆에 `effect`(참조). 참조가 있으면 참조가 이긴다. 필드별 오버라이드는 두지 않는다(필요가 보이면 후속).
- `BindingDefinitionBuilder` · `CardDefinitionBuilder` — 참조를 풀어 오늘과 같은 `BindingDef`(코어 무변).
- 마이그레이션 에디터 스크립트 — 카드 60 · `UnitSkillAbility` 26 · 악몽 24 의 값 → 중복 제거 `EffectData` + 참조. **dry-run 표(묶이는 효과 목록 · 이름 후보) 먼저 사용자 확인** 후 실행.

## 구현
- 값 필드는 시트 임포터가 쓰므로 남긴다(시트 탭 통일은 후속 후보). 임포터가 값을 바꾸면 참조 쪽이 안 따라가는 문제는 이 unit 에서 **빌더 경고**로 드러낸다(참조와 값이 둘 다 있고 다르면 Warn).
- 에셋 위치 `Assets/_Project/Data/Effects/`.

## 완료 기준
- 빌더 스냅샷(unit 4) 필드 동치 · 골든 11 · Assets lane(선행 2 외 0).
- 로그인 임포트 한 번 뒤에도 스냅샷 동치 또는 경고로 드러남(시트 확인은 curl 읽기 전용).
