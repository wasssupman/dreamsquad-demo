# 6 — Play e2e 검증 + 무회귀 + 문서 갱신

## 목적

feature 검증 질문 4개에 증거로 답하고, 파이프라인 맵과 CLAUDE.md 를 갱신한다.

## 변경 대상

- `docs/reference/object-pipeline-map.md` — §투사체 스폰 진입점 3곳 → **4곳**
- `docs/spec/projectile-emission-pattern/README.md` — 상태 라인 → 완료
- 신규 `docs/spec/projectile-emission-pattern/7_handoff_summary.md`
- `docs/spec/README.md` — Follow-up Backlog 에 이관 항목 등록

## 구현 (검증 절차)

### 1. 검증 질문별 증거

| 질문 | 증거 |
|---|---|
| 새 사격 스킬이 코드 0줄로 만들어지는가 | unit 5 의 커밋 diff 가 asset·SO 만 |
| 융단폭격이 전용 arm 없이 값 보존되어 도는가 | unit 4 이관 전/후 Play 대조(주기·데미지·반경·텔레그래프·진앙 순회) + `grep AreaBarrage` 라이브 arm 0 |
| 곡선 호밍이 arm 하나로 붙는가 | unit 1 diff = 순수함수 1파일 + Move arm + view Y arm + enum append. 신규 시스템·드레인·태그 0 |
| 발사 결정이 `Entities` 무참조인가 | unit 0 신규 6파일에 `using Unity.Entities` 0건 |

### 2. Play e2e (배틀 직행 하네스 — `TestModeContext.Set` + `StartBattle`)

- 보스 웨이브(5웨이브) 진입 → 3슬롯 동시 동작: 융단폭격 10초 · 미사일 0.5초 · 채찍질 0.5초
- 미사일 타겟 분포: 20발 이상 관측해 후보 전체가 최소 1회 이상 선택되는지(맵 양 끝 배치로 사거리 무제한 확인)
- 곡선 육안: 발사→명중 구간 스크린샷 3장(초·중·종) — XZ 곡선 + Y 아치가 모두 보이는지. 정지 스크린샷으로 애니메이션을 판정할 수 없으므로 시각 검증은 **연속 3프레임 이상**으로 본다
- 텔레포트 미발생: 보스 HP 를 70%/40%/10% 아래로 관통시켜도 제자리
- 방어유닛 전멸 상태에서 발사 소모(경고/에러 없이 조용히)

### 3. 무회귀

- EditMode 전량 그린(unit 0·1 신규분 포함, `BarrageEpicenterTests` 삭제 반영)
- PlayMode: 기존 사전 실패(`CardBuffs` 가디언 `dmgTaken` ×1.25)는 main HEAD 부터라 이번 변경의 회귀가 아니다 — clean HEAD 재현으로 구분한다
- 수동: defender 기본 홈잉 공격 · 머신건 10연발(`VolleyFireState` 무접촉) · 폭탄(`GrenadeToCell`) · 곡사포(`BallisticArcToPoint`) · 플레이어 Meteor(`SkyFall`) 각 1회
- `EmitterInstance` 가 붙은 보스 사망 시 teardown 경고 0

### 4. 리뷰

- ECS 변경이 있으므로 **투트랙 리뷰**(`code-reviewer` + `ecs-reviewer`). 대상 = unit 2·3 (emitter 시스템 + bake/arm). unit 0·1 은 순수 로직이라 일반 리뷰로 충분하다.

## 완료 기준

- 위 4개 검증 질문 전부 증거 확보
- 파이프라인 맵 §투사체 갱신(스폰 진입점 4곳, `Emission/` 컴포넌트, view Y arm)
- `7_handoff_summary.md` 작성 — 30~80줄, "구현됨 / 핵심 파일 / 검증 / 주의점(되돌리면 안 되는 것) / 다음 후보"
- README 후속 후보 중 이관 대상(`PayloadKind` 해결시점/효과 분리 · fan/ring · selection rule 확장 · defender volley 수렴 · 레지스트리 싱글턴)을 `docs/spec/README.md` Follow-up Backlog 에 등록
- 사용자 체감 확인 요청: 미사일 데미지 40 · 0.5초 주기 · 곡선 폭(`bezierLateral` 1.2)
