# 1 — 카드 데이터 전환과 검증

## 목적

기존 authored 설명이 구조화 효과와 어긋나도 UI가 오래된 수치를 다시 표시하지 않도록
구조화 데이터 중심으로 전환한다. 구조화되지 않은 미래 카드만 fallback을 사용할 수 있다.

## 변경 대상

- `Assets/_Project/Data/Dreamcatcher/Card_*.asset`
- `Assets/_Project/Data/Dreamcatcher/Active_*.asset`
- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs`
- `Assets/_Project/Tests/EditMode/DreamcatcherCardTextTests.cs`

## 데이터 계약

- Squad 효과 요약의 고정 트리거 라벨은 `항상`을 사용한다. 실제 호스트 생명주기와
  효과 회수는 기존 런타임 부착 경로가 담당하며 카드 SO에 별도 텍스트 필드를 추가하지 않는다.
- 현재 Dreamcatcher 데이터 폴더의 구조화 카드 37장은 `effects`, `mechanics`,
  `attackMods`, `skill` 중 하나 이상으로 본문을 생성해야 한다.
- 현재 37장 카드의 `description`은 구조화 formatter가 생성한 트리거→효과 본문을 평문으로 mirror한다.
  SO에서 직접 확인하거나 시트로 내보낼 때도 같은 요약을 볼 수 있어야 한다.
  실제 런타임 동작과 수치는 계속 구조화 데이터/런타임 구현을 source of truth로 삼는다.

## 테스트 계약

1. Squad 대상·용어·양수/음수 퍼센트를 검증한다.
2. 공격/피격/사망/킬/실드 파괴/HP 임계치 트리거를 검증한다.
3. 투사체 튕김, CC(넉백 지속시간 포함), 스택, 수면, 강공, 오라, 표식, 자폭 payload의
   수치와 조건을 검증한다.
4. Active 스킬의 배율·범위·경고·끌어당김 속도·지속시간·비용·재사용시간을 검증한다.
5. 소수점 후행 0 제거와 구조화 요약의 `description` mirror를 검증한다.
6. AssetDatabase로 현재 37장에 구조화 summary와 동일한 `description` mirror가 있고,
   출력 본문에 summary가 중복되지 않는지 검증한다.

## 완료 기준

- [x] 현재 카드 37장 구조화 summary 검증 통과.
- [x] Unity EditMode 전체 테스트 통과(기존 Ignore 제외).
- [x] 덱빌더, 덱 페이지, 유닛 인스펙트, 손패 툴팁이 같은 포맷터를 호출한다.
- [x] 수치 변경 후 설명 문자열을 수동 수정하지 않아도 출력이 갱신된다.
- [x] 기존 `dreamcatcher-card-description`/`dreamcatcher-hand-drag-tooltip` 문서에 본
      spec이 새 formatter 계약을 대체한다는 포인터가 남아 있다.
