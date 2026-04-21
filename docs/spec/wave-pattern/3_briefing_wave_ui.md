# Briefing Wave UI

**작업 구분**: Phase 3

## 목적

맨 처음 공격 패턴 확인 UI 를 기존 timeline/lane marker 중심에서 생성된 wave 구조에 맞는 스크롤형 summary 로 바꾼다.

## 새 UI 핵심

```text
1 Wave - Basic Unit 5, Swift Unit 10
2 Wave - Tanker Unit 4, Basic Unit 8
```

## 레이아웃

- title: `ATTACK WAVES`
- 좌상단: `MAP SETTINGS` toggle 유지
- 중앙: wave summary scroll
- 하단: `DRAFT START`

wave summary:

- ScrollRect 사용
- 각 wave 는 큰 row/card 형태
- 한 화면에 4~6개 wave 가 읽히는 크기
- row height 권장: 96~120px

카드 예시:

```text
[WAVE 01]  Basic x5    Swift x10
[WAVE 02]  Tanker x4   Basic x8
```

## 표시 정보

- wave number
- trigger time
- unit A displayName + count
- unit B displayName + count
- total count

## 데이터 주입

`TimelineBriefingView.Show()` 시점:

```text
Read map settings
Resolve wave seed
Generate preview WavePlan
Render wave rows
```

주의:

- briefing 에 보이는 wave plan 과 실제 battle 에서 쓰는 wave plan 이 동일해야 한다.
- preview 에서 매번 random seed 를 새로 뽑으면 안 된다.

## 완료 기준

- briefing UI 에 개별 spawn marker 대신 wave rows 가 표시된다.
- 각 wave row 는 `Wave N - Unit A count, Unit B count` 를 한눈에 보여준다.
- 스크롤로 10~15개 wave 전체를 볼 수 있다.
- `MAP SETTINGS` toggle 은 기존처럼 동작한다.
- `DRAFT START` 후 실제 battle wave plan 이 briefing 에 표시된 내용과 동일하다.
