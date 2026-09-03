# 4 — Play 검증

> 검증 질문: 부착 확정 전에 카드가 어디에 작용하는지를 화면이 판정과 같은 자로, **dim 아래 · 엄지 밑에서도**
> 말하는가. 카드는 `id` 로 지목한다(`displayName` 은 사람이 바꾼다).

## 목적

unit 0a~3 을 손패 흐름에서 한 번에 확인한다. Editor Play 1회 + **실기기(Android) 1회 필수**. 스크린샷은
`Assets/Screenshots/darp_*.png`(비추적). 실기기 사진 중 1장은 **손가락이 프레임에 들어간** 것이어야 한다.

## 체크리스트

### A. 공간 카드 — 원 (0a · 1 · 2 · 3)
- [ ] `cornered_burst` 궁지폭발(r=1.5): 락온 순간 host 몸 중심에 원. 부착 후 발동 시 **가장자리에 그림자가 걸친
      적까지** 맞는다(계약 2).
- [ ] `tremor_plate` 진동갑주(HealthThreshold → `SkillIdForPayload` 폴스루 경로)도 원.
- [ ] `shield_burst` 실드폭발(가디언 host)도 원.
- [ ] `shield_lull` **실드수면**(AreaSleep, r=1.5): **0a 의 사각→원 확인** — 파열 시 잠드는 적이 원 안이고 옛 3×3
      모서리(대각 1.414 < 1.5)는 여전히 걸린다.
- [ ] `farewell` **사망폭발**(r=2.5)·`severance_meteor` **퇴직위로금**(r=1.5): 락온 시 host 중심 원(rev 2026-09-03 —
      자기 사망/퇴근은 자리가 자기 위치라 노출로 전환). `corpse_burst` 시체폭발·`ember_field` 잿불(처치 트리거)은 여전히 링 없음.

### B. 비공간 — 표시 0 (계약 3)
- [ ] `frenzy` 광란(10) · `frostbite` 동상(5) · `poke_needle` 비수(4) · `lullaby_dart` 자장가(ApplyCc 0) — 링 없음.
      폭탄맨 host 에 비수를 붙여도 없음(Q6).
- [ ] `sub_fattened_offering` 제물표식(`EnemyMark`) — 부착 경로 밖, 링 없음.
- [ ] `moth_swarm` 불나방떼 — 시트 range 0 이면 없음. ⚠ 시트가 올렸으면 반경 = N 원(칸 반폭 없음)이 떠야 한다.

### C. 락온 흐름 (3)
- [ ] 유효 → 유효: 링이 새 host 로 이동(히스테리시스 뒤라 튀지 않음) · 유효 → full 3/3: 소멸 + invalid 리티클.
- [ ] 손 떼기 3종(커밋 · 손패 안 취소 · 보드 밖 취소) 즉시 소멸 · 항아리 토글/페이즈 전환 → 잔류 0.
- [ ] 사망 연출 중인 host 락온 → 링 없음(생존 술어 = `_defenderByTile`).

### D. 판독 — dim · 엄지 · 색 (D1 · 계약 9/10) **실기기**
- [ ] dim 켜진 상태 · **엄지가 host 위**에 있는 채로 `cornered_burst` 링(채움)이 **1초 안에 읽힌다**. 사진 1장.
- [ ] 링이 배치 사거리 링(라임)과 **색상으로**, base-ring·리티클 시안과 **값·형태(채움 vs 선)로** 갈린다.
- [ ] 야외 밝기에서도 읽힌다. 부족하면 스타일 SO 튠 → 그래도 부족하면 후속 F3 로 기록(구현자가 여기서 멈추지 않는다).

### E. 액티브 조준 · 텔레그래프 (0b · D6)
- [ ] 액티브 셀 조준(파워 서지류 r=1.5 · 토네이도 r=2.5)이 **조준 셀 중심 원**으로 뜨고 채움이 펄스한다. 손가락이
      중심을 가려도 범위가 읽힌다. 발동 결과와 링 일치. 포탈(range 0)은 단일 셀 점등 그대로.
- [ ] 메테오·보스 착탄 텔레그래프가 원 링으로 뜨고 위험 구간이 읽힌다.

### F. 채널 경합 · 정렬 · 위치
- [ ] 카드 드래그 → 배치 드래그: 배치 링 정상 소유. **역순(배치 드래그 진행 중 카드 드래그 락온)**: 배치 링이
      사라지지 않는다(양보).
- [ ] 링·채움은 유닛 **아래**(정렬 −8, 2026-08-31 결정) — 끊김은 채움이 흡수.
- [ ] **2×2 host** 에서 링 중심 = 셀 경계 교점. Subway 맵에서 확인(StreetDay 만으로 통과 판정 X).
- [x] ~~링 중심이 캐릭터와 어긋남~~ — **버그 아님, A 수용(사용자 결정 2026-09-03)**: 링 = 판정 중심(footprint
      기하 중심), 캐릭터 뷰 = 발밑(하단 행 중앙)이라 세로로 `(H−1)/2` 타일 갈린다(2×2 반 타일 · 2×3 한 타일).
      링을 발밑으로 옮기면 표기가 판정에 거짓말을 한다 — 옮기지 말 것. 판정 중심 자체를 발밑으로 내리는 안(C)은
      sim 변경 + 골든 재베이크가 딸린 별도 spec 감.

## 자동 검증 기록 (2026-09-02 · 원격 세션 — 손가락·실기기 항목은 미소화)

- **EditMode** 코어+에셋 2692건 — 실패 2건 = 선행(`boomerang`·`bomb_man` 문안). 신규 24건(0a 3 · 1 21) 초록.
- **PlayMode `AttachRangePreviewTest` 2건 통과**(unit 2 채널): host 몸 중심(2×2 → 셀 경계 교점)에 `_Range` = 카탈로그
  반경 그대로 · `_HalfExtent` 0 · Clear 로 소멸 · 비공간 spec 은 채널 무접촉 · Placement 소유 중 양보 · 비소유 Clear 무해.
- **PlayMode 전체 219건**: 실패 25건+(목록 cap). 격리 A/B(같은 그룹을 0a 이전 코드와 HEAD 로 각각 실행)로 귀속:
  - **0a 귀속 2건** — `OnPlaceBindNearbyTest` 2건. 픽스처가 「반경 안」 칸을 체비셰프로 골라 2×2 호스트의 (−2,−2)
    모서리(중심거리 3.54)를 뽑았고, 원 3.5(더미 몸 0) 밖이라 감속이 안 걸렸다. **의도된 변화** → 픽스처를 호스트
    기하 중심 유클리드(안 ≤ 2 · 밖 ≥ 4.5)로 교체.
  - **선행 15건**(0a 이전 코드에서도 동일 실패, 이 spec 무관): `AbilityAreaShieldTest` · `AbilityBombManBarrelTest` ·
    `ActiveAllyZoneTest` · `BossThresholdSelfAoeTest` · `DefenderRetireTest.Retire_WithOnRetireCard…` ·
    `DreamcatcherKillThresholdTest` 2 · `OnPlaceBoostNearbyTest` · `OnPlaceDotNearbyTest` · `OnPlaceMeleeBurstTest` 2 ·
    `OnPlaceSkyStrikeTest` 2 · `OnPlaceTauntNearbyTest` · `PatrolDefenderPlayTest`. 증상은 배치 실패·초과 피해(44·80,
    기본 공격이 더미에 닿음)·프로필 스톤 ×1.012 — 부모 spec 의 2×2 몸 반경(도달 +0.75) 이후 PlayMode 전체가 돌지
    않은 것으로 보인다. `docs/spec/README.md` 「PlayMode 사전 실패」 절 갱신은 별도 docs 작업으로.
  - 그 외 기존 목록의 환경 실패(`AuthE2ETest`·PrimeTween OnComplete·`DragCancelZoneTest`/`DragPlacementReachTest`
    리플렉션 인자 수 — `ResolveFocusAndTarget` 시그니처 변경, 이 spec 무관).

## 완료 기준

- [ ] A~F 전부 체크 · 콘솔 에러 0 · 스크린샷 3장(실기기 손가락 포함 1 · 원 카드 락온 1 · 액티브 조준 원 1).
- [ ] EditMode 코어 + 에셋 lane 초록(선행 2건 제외) · 골든 바이트 무변(0a 재베이크 기준).
- [ ] 사용자 Play 확인 → README 상태 「완료」 + `5_handoff_summary.md`.
