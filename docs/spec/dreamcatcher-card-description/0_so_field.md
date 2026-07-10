# 0 — SO `description` 필드 추가

## 목적

`DreamcatcherCard` 에 authored 설명 텍스트 필드를 추가한다. 효과/메커니즘/플레이버를
한 문자열로 담아 뷰가 렌더한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs`

## 구현

`DreamcatcherCard` 클래스의 **가장 마지막 필드**(`skill` 뒤)에 append:

```csharp
// dreamcatcher-card-description Unit 0 — authored 효과/메커니즘 설명.
// 덱빌더 상세 팝업에서 자동 수치라인 아래에 렌더된다(빈 값이면 블록 생략).
// effects[] 자동생성이 못 덮는 Unit(mechanics/attackMods)·Active(skill) 카드의
// 유일한 읽을 수 있는 설명 소스. 순수 데이터(문자열) — SO 계층은 ECS-free 유지.
// 끝에 추가 → 기존 22개 카드 에셋은 빈 문자열로 역직렬화(inert).
[TextArea] public string description;
```

- `[TextArea]` 는 `SkillData.description` 과 동일한 인스펙터 관례.
- 위치는 반드시 `skill` **뒤**. 중간 삽입은 기존 에셋의 직렬화 순서를 깨지 않지만
  (필드는 이름 기반 매핑) 계약 1(append-only)의 일관성을 위해 끝에 둔다.

## 완료 기준

- [ ] 컴파일 성공 (`read_console` 클린).
- [ ] 인스펙터에서 아무 카드 에셋이나 열면 하단에 여러 줄 `Description` 박스가 보인다.
- [ ] 기존 카드 에셋의 다른 필드(effects/mechanics/skill 등)가 그대로 유지된다.
