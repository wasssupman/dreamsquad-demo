# 3 — 레거시 hostless 영속 apply 은퇴

## 목적

드림캐쳐의 "match-permanent, never revoked" 진입점 `BattleBridge.ApplyDreamcatcherCard(handle=0)`
를 제거한다. 라이브 플로우엔 없지만(§awakening-hand §11 dormant), 코드상 남아있어 재활성 시
영속 버프가 부활하는 지뢰. 유일 소비자는 dormant `DreamcatcherController.Pick` + PlayMode 테스트 4개.

**정리 = 삭제가 아니라 이관**: 효과 적용은 동일하게 유지하되, 호출부를 revocable
`ApplyDreamcatcherCardHosted` 로 옮겨 "영구 계약" 공개 API 자체를 없앤다.

## 스코프 경계 (중요)

- `_activeDcEffects` 의 **handle=0 개념은 유지**한다 — **드림스톤**(`ApplyPendingDreamstones`)이
  매치-롱 스톤 효과에 쓰는 값이다(설계상 영구, 별 시스템). `RevokeDreamcatcherEffects` 의
  `if (handle <= 0) return;` 가드는 스톤 보호용으로 **그대로 둔다**.
- 즉 이번 정리는 **드림캐쳐 카드의 hostless 영속 apply 메서드만** 없앤다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
  - `public void ApplyDreamcatcherCard(card)` 메서드 삭제
  - `ActiveDcEffect.handle` 주석 갱신(0=드림스톤 매치-롱 / ≥1=hosted revocable)
  - `ApplyPendingDreamstones` 주변 스테일 주석 2곳(`ApplyDreamcatcherCard` → `ApplyDreamcatcherCardInternal`)
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherController.cs`
  - `Pick()` 의 `bridge.ApplyDreamcatcherCard(card)` → `bridge.ApplyDreamcatcherCardHosted(card)`(반환 무시)
- 테스트 3개 call site 이관(`bridge.ApplyDreamcatcherCard(` → `bridge.ApplyDreamcatcherCardHosted(`)
  - `Tests/PlayMode/DreamcatcherEffectTest.cs` (3곳, 스택 검증 포함)
  - `Tests/PlayMode/DreamcatcherCombatDamageTest.cs` (1곳)
  - `Tests/PlayMode/DreamstoneCarryInSmokeTest.cs` (1곳)

## 근거 (동작 불변)

- `ApplyDreamcatcherCardHosted(card)` = `ApplyDreamcatcherCardInternal(card, 새 handle≥1)` + 핸들 반환.
  effects[] 적용·스택(`_dcStackCounter++`)·warmup 은 동일. 테스트는 host 를 죽이지 않으므로
  효과가 테스트 동안 유지 → 기존 assert 그대로 통과.
- dormant 컨트롤러는 host 미추적이라 revoke 안 되지만, 브릿지에 "영구 계약" API 가 사라지는 것이
  핵심(재활성돼도 revocable 경로만 존재).

## 완료 기준

- [ ] 컴파일 클린 + `grep ApplyDreamcatcherCard\b` 잔존 참조 0(Hosted/ToUnit/Internal 제외).
- [ ] PlayMode 테스트 4종(Effect/CombatDamage/DreamstoneCarryIn/DeckCarryIn) 그린.
- [ ] 드림스톤 매치-롱 효과(handle=0) 무영향(회귀 없음).
