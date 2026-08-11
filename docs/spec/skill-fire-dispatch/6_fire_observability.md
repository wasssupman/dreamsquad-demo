# 6 — 발동 관측

## 목적

발동이 디스패치 한 지점을 지나게 된 것을 관측으로 현금화한다. 도넛 자기무효화 버그
(발동이 조우당 1회로 퇴화했는데 아무도 못 봄)의 재발 방지가 목적 — "거의 안 된다"는
boolean 단언에 안 잡힌다(boss-mamemo 교훈 2).

## 변경 대상

| 파일 | 내용 |
|---|---|
| `Battle/Combat/Skills/SkillFireLog.cs` | 신규 — 발동 카운트 집계(스킬 종류·시전자별) |
| `Tests/EditMode/SkillFireCountTests.cs` | 신규 — **결정적** 발동 횟수 단언(고정 dt bare world) |
| `Tests/PlayMode/BossLullabyLiveTest.cs` | 로그에 집계 수치 추가(단언은 boolean 유지) |
| `docs/spec/skill-fire-dispatch/README.md` | 스킬 완료 기준 템플릿에 관측 규칙 반영 |

## 구현

- **집계는 sim 안쪽에 둔다** — 채널·시스템 신설 0. 로직 파일이 UnityEngine 을 모르므로
  집계도 순수 카운터여야 한다(로그 출력은 감시/브리지 쪽에서). 매치 시작 시 리셋.
- **기존 `DcTriggerFiredEvents` 와 중복 아님**(검산 확정): 그쪽은 AttackN 계열 3지점의
  방어유닛 host 전용이다. 수렴시키지 않는다.
- **결정적 단언은 EditMode 에서 잰다** — 고정 dt bare world 에서 "12초 상당 tick 동안
  자장가 N회". `LullabyLive` 는 실프레임 델타라 수치가 재현되지 않으므로 **boolean
  골든으로만** 쓰고 계측은 로그로만 남긴다(계약 10).
- `skillName` 이 로그에 흐른다: `[Skill] 자장가 ×2 (마메모)`. 콘텐츠 이름은 저작 SO 필드
  담당이지 코드 심볼이 아니다.

## 완료 기준

- [ ] 컴파일 에러 0 · units 1~5 무회귀
- [ ] `SkillFireCountTests` 그린 — 자장가 발동 횟수 하한 단언. **도넛 규칙으로 되돌리면
      빨개지는지 1회 확인**(회귀 가드가 실제로 무는지)
- [ ] 발동 로그가 스킬명으로 찍힘 (Play 1회 육안)
- [ ] README 에 "신규 스킬 완료 기준 = 발동 관측 포함" 규칙 반영
