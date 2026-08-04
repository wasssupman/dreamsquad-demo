# 9 — 청사진 ③: 틱 파이프라인 순서도

## 목적

신 sim 의 `Sim.Tick` 페이즈 명세. **정본은 unit 0 의 order-capture(44개 유효 총순서)다** — 스케치·기억이 아니라 캡처를 phase 로 접는다(feature-wide 계약 6). 여기서 확정한 순서가 M1 이식의 뼈대이고, A/B parity 가 이 문서 기준으로 판정된다.

## 변경 대상

- 신규 `docs/spec/battle-sim-extraction/m1_blueprint_tick_pipeline.md`

## 구현

- **phase 군집화**: order-capture 44개를 스케치(커맨드 반입 → CC/필드 적용 → 이동 → 타겟팅·공격 → 투사체 → 피해·사망 정산 → CC 감쇠(이동 후) → 스폰 → 점수 → 이벤트 플러시)에 대응시키되, 캡처와 스케치가 어긋나면 **캡처가 이긴다**. 하네스 스텝 순서(unit 2: 입력 → SkillRuntime → Bridge 프레임 → 심그룹 → 도약 드레인)와의 합성도 명시.
- **사망 4단계 2-phase delete** 보존: DamageApplication(DeadTag) → HealthDeath → PatrolLifecycle/ResignationDrop → UnitLifecycle. 즉시 삭제 금지(§3 형태 보존).
- **내부 phase queue 9채널**의 같은-틱 소비 지점(생산 phase → 소비 phase)을 화살표로 명시 — 틱 끝 플러시로 미루면 공격·텔레포트가 1틱 늦는다.
- **ECB 로컬-즉시** → "루프 중 기록, 루프 후 적용" 형태 유지 지점 목록(ModifierApplySystem 선례).
- **동률 예외 5지점 + 병합 duration 정책**(값 축 LWW · 지속 축 max · tickTimer carry-over · ApplyStack `remaining` 만 덮어쓰기)을 이식 계약으로 승격 — unit 4 가 로그로만 다루던 것을 신 sim 의 명시 규칙으로.
- **RNG 지도**: `MatchSeed.Derive*` 서브스트림 + `PatternShotRandomizer` seed(`SimEntityId` 축) — xorshift 상수 계승(System.Random 치환 금지).
- 재배치 결정이 필요하면(예: 미선언이던 클러스터의 정리) **명시적 행동 변경 항목**으로 분리 표기 — 침묵 변경 금지.

## 완료 기준

- order-capture 의 44개 시스템이 phase 도표에 **전부** 배치(빠짐 0, 대조표 포함).
- 9채널 생산→소비 화살표 전수.
- parity 관점에서 "이 문서와 다르게 구현하면 골든이 깨진다"가 성립하는 수준의 구체성.
- 코드 변경 0.

> 진행 기록 2026-08-04: 완료 — `m1_blueprint_tick_pipeline.md`. 44/44 를 P1~P12 로 군집화(캡처
> 정본 — 스케치와 달리 투사체가 공격 앞, DotApply 가 이동 앞, CC 감쇠는 사망 창 뒤). 내부 9채널
> 26쌍 전수(같은 틱 12 · 1틱 지연 14 — 지연은 박제된 계약), 사망 4단계 릴레이("죽었지만 아직
> 있는" 1틱 창), ECB 44 전수 로컬-즉시 확인("루프 중 기록, 루프 후 적용" 번역 규칙), RNG 지도
> (sim 보유 2 + 무상태 파생 1 — PatternShot 은 MatchSeed 계보가 아님), 동률 5지점·병합 정책
> 비대칭의 계약 승격. 재배치 결정 0.
