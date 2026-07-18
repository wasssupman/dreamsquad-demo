# 2 — 스폰 시 워닝 훅 (BattleBridge)

## 목적

보스가 스폰되는 순간 `BossWarningView.Show()`를 호출한다. 웨이브/생성기와 무관하게 **보스 스폰 이벤트**가
워닝의 유일한 트리거(seed·authored 어느 경로든 자동).

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 구현

- serialized 참조 추가: `[SerializeField] private Wassup.UI.BossWarningView _bossWarning;`
  (기존 `ScoreHudView` HUD 참조와 동일한 배선 패턴.)
- **워닝 호출은 단일 지점으로 확정**: `BakeNightmareMechanics` 가 보스 확정(= `mechanics.Length == 0` early-return
  을 통과, `BossTag` 부착 `BattleBridge.cs:4756-4758`)하는 **그 지점에서** `_bossWarning?.Show();` 를 호출한다.
  `SpawnUnit` 에서 `nightmareMechanics` 를 **재판정하지 않는다** — BossTag 판정과 로직이 이중화되어 드리프트/이중
  발화하는 것을 원천 차단. 보스 판별의 단일 진실 = `BakeNightmareMechanics`.
  - `_bossWarning?.Show();` (null-safe — 미배선 시 무동작).
- 잡몹(빈 `nightmareMechanics`)은 early-return 되어 트리거되지 않음. `SpawnUnit`은 배틀 중에만 도므로 페이즈 가드 불필요.
- (참고: bake 메서드가 Mono 프레젠테이션 부수효과(`Show`)를 갖는 냄새는 감수 — 보스 판별이 여기 한 곳뿐이라
  단일화 이득이 더 크다. `bool IsBoss(AttackUnitData)` 헬퍼 추출로 BossTag/워닝이 공유하는 대안도 허용.)

## 완료 기준

- 컴파일 통과(단 `BossWarningView` 타입 선재 필요 — unit 1 완료 후).
- 보스 스폰 시 정확히 1회 `Show()` 호출, 잡몹 스폰 시 미호출(unit 3 Play 로 실측).
- 재진입 코얼레스는 뷰(`_showing` 가드)가 담당 — 브리지는 매 보스 스폰마다 호출만.
