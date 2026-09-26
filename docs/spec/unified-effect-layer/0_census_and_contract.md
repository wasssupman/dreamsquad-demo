# 0 — 전수 표와 계약 싣기 (문서)

## 목적
구멍 H1~H6 의 범위를 코드·에셋에서 센다. 하드 케이스는 표본이라, 같은 구멍이 다른 조합에 있는지와 **무엇이 라이브인지**를 표로 확정하고 unit 1~5 의 변경 대상·무변 단언을 그 표에서 뽑는다. 코드 변경 0.

## 변경 대상
- 이 폴더 `census.md` — 표 3개:
  1. **라이브 조합**: `Assets/_Project/Data/**` 의 (출처 · 에셋 · 트리거 · 효과 · 탄 궤적 결합 · 발사 명세 전원 손잡이) — 실측 42쌍(초안 41 · `census.md` 로 정정). 각 행 = unit 1~5 의 **무변 단언 행**. 캐논 폭격(`Ability_SkyStrike_Cannon` → `Pattern_Cannon_Strike` 전원 손잡이 → `Projectile_CannonStrike` `SkyFallOnTarget`) 처럼 궤적 결합에 기대는 분기를 표시.
  2. **concrete × 원점 읽기**: `Scripts/Skills/Concrete/` 29개 × (발사 자리를 어디서 · 효과 좌표를 어디서 · 원점 항 · `Source`) — 후보 거리 계산(수면·실드·브레스·발사 명세 후보)은 「원점 읽기 아님」으로 구분.
  3. **출처 × 거절 규칙**: `CardDefinitionBuilder` · `BindingDefinitionBuilder`(유닛 능력 · 악몽) · 기믹 · 액티브의 거절 분기 × (사유가 「원점 불가」 / 「수명상 영영 안 터짐」 / 「출처 관례」).
- `docs/reference/battle-core-architecture.md` — 트리거/효과 절에 계약 1~5 를 **예정** 표기로(확정은 unit 6).
- 탐침 두 파일 머리 주석에 「`unified-effect-layer` 완료 기준」 · `[Ignore]` 사유 끝에 해제 unit(반경·예고 → 1 · AA 두 건 → 3 · 낙하 그림 → 4). U3 로 이름·단언이 바뀌는 둘(`…귀속은_H` · `…귀속만_다르고…`)은 unit 3 에서 고친다고 적는다.
- `docs/spec/README.md` 목록 행 · Follow-up Backlog 「통합 효과 층」을 이 spec 으로 연결 · H4+시트 후속 등재.

## 완료 기준
- 표 3개 빈칸 없음(모르는 칸은 `?` + 사유) · 리드가 표본 5칸을 코드와 직접 대조.
- unit 1~5 「변경 대상」이 표와 어긋나면 이 커밋에서 같이 고친다.
- 테스트 파일은 주석만(헤드리스 918 · Ignore 5 동일).
