# 8 — 철거와 개방

## 목적

**이전이 끝났으므로 legacy 를 죽이고 문을 연다.** 이 unit 이 끝나면 어휘가 하나다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/DcTrigger.cs` — 화이트리스트 2술어
- `Assets/_Project/Tests/EditMode/DcTriggerTests.cs` · `DcTriggerArmedTests.cs` — 핀 갱신
- 잔여 legacy arm · 죽은 enum · flat 필드
- `CLAUDE.md` — 채널 목록 · ECS 맥락 서술
- `docs/spec/README.md` — Follow-up Backlog

## 구현

1. **화이트리스트를 마지막에 철거한다.** `EnemyTriggerArmed`(`PeriodicTimer|HealthThreshold|
   AttackN`)와 `DefenderTriggerArmed`(`OnPlace`)는 **진영 하드코딩의 안전핀**이었다.
   토대 unit 2b 가 리터럴 56곳을 풀었고 가족 이전이 전부 끝났으므로 이제 열 수 있다.
   ⚠ 먼저 열면 legacy enum 경로인 payload 와 개방된 조합이 공존하는 창이 생긴다(계약 6).
2. ⚠ **선행 조건 — `ShieldBreakEvent` 에 진영 스냅샷을 실어야 한다** (투트랙 리뷰 2026-08-25,
   양측 확인). 오늘 파열 대상 풀은 드레인 시점에 host 진영을 **파생**한다. 그런데 실드 파열은
   **정의상 host 를 죽인 그 히트에서 발동하는 경우가 흔하다**(오버킬 관통) — 그때 host 가
   이미 파괴돼 진영이 미상이 되고, 폴백이 적 풀이라 **보스가 자기 진영을 때린다.**
   지금은 화이트리스트가 막아 무해하지만 이 unit 이 그 문을 여는 순간 재유입된다.
   → producer(`DamageApplicationSystem`, host 진영을 아직 아는 자리)가 emit 시점에 진영을
   싣게 고친 뒤에 화이트리스트를 연다. **아래 3번 단언은 host 생존 케이스만 잡는다 —
   오버킬 파열 케이스를 별도 단언으로 추가할 것.**

3. **`DcTrigger.cs:100~106` 이 경고한 위험이 해소됐는지 확인한다** — 보스가
   `OnShieldBreak` 를 쓸 때 자기 진영을 때리지 않아야 한다. 이것이 화이트리스트를 열어도
   되는지의 **실증**이다. PlayMode 단언으로 고정한다.
3. **핀 테스트를 갱신한다.** 지금 EditMode 가 현행 술어를 고정하고 있어서 철거하면 빨개진다.
   테스트를 지우는 게 아니라 **새 불변식**(트리거 × 주체 조합이 열려 있다)으로 바꾼다.
5. **`skillId == 0` 경로가 남아 있는지 확인한다.** 남아 있으면 이전이 안 끝난 것이다.
   라우팅 축 자체를 은퇴시킬지, 미등록 검출용으로 남길지 판정한다.
6. **문서 갱신** — `CLAUDE.md` 채널 목록, `docs/spec/README.md` Follow-up Backlog 의
   skill-fire-dispatch 그룹을 이 두 spec 링크로 대체.
7. **`skillId == 0` 라우팅 축의 거취를 판정한다** — 이전이 끝났으니 은퇴시킬지
   미등록 검출용으로 남길지.

## 완료 기준

- [ ] 화이트리스트 2술어가 삭제되고 트리거 × 주체 조합이 열렸다
- [ ] 보스 `OnShieldBreak` 가 자기 진영을 때리지 않는다 (PlayMode 단언)
- [ ] ⚠ **오버킬 파열**(host 가 발동 프레임에 사망)에서도 자기 진영을 안 때린다 — 진영 스냅샷 선행
- [ ] `skillId == 0` legacy 경로가 남아 있지 않다 (또는 남기는 근거가 기재됐다)
- [ ] `DcPayloadKind` arm · `OnPlaceEffectType` · `SkillEffectType` 실행 코드가 전부 삭제됐다
- [ ] **검증 질문 참**: 새 스킬 하나 = concrete 1 + 저작 SO 1 (switch 4곳 → 1곳)
- [ ] **끝점 참**: 보스 · 배치 · 특수 스킬이 한 `ISkill` 레지스트리에 있다
- [ ] EditMode 전 lane + PlayMode 초록, Play 육안 종합
