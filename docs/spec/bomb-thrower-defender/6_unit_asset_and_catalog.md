# 6 — 유닛 에셋 저작 + 카탈로그 등록 + Play 검증

## 목적

`Defender_BombMan.asset` 을 저작해 폭탄맨 능력을 실제 수치로 활성화하고, 카탈로그에 등록해
로스터에 노출한다. 이 단위가 feature 의 통합 Play 검증 지점.

## 변경 대상

- `Assets/_Project/Data/Defenders/Defender_BombMan.asset` (+.meta, 신규)
- `Assets/_Project/Data/DefenderCatalog.asset` — 등록
- `Assets/_Project/Data/Projectiles/Projectile_Bomb.asset` (unit 5 와 공유 — 여기서 배선)

## 구현

- **에셋 값**(초안, Play 튜닝 전제 — 하드코딩 아님, 전부 SO):
  - `id`: `bomb_man` · `displayName`: "폭탄맨"(대안 "폭탄 돌리기" — 핫포테이토 슬랭, 야근 테마) · `role`: 근접/원거리 배치 클래스
  - `directionalAttack`: 1 (조준 활성) · `attackCooldown`: ~3.0(투척 간격) · `attackRange`: 조준 가이드용
  - `bombLandingTiles`(N): 3 · `bombTravelSec`(n): 1.0 · `bombFuseSec`(m): 1.0 · `bombArcHeight`: 0.15(낮은 구르기)
  - `bombAoeTileRange`: 1 · `bombAoeTargetCap`(B): 3
  - `bombDamage`(C): ~60 · `bombSleepSec`: ~2.5 · `bombStunSec`: ~1.5
  - `projectile`: `Projectile_Bomb`(구르기 뷰, unit 5) · Spine/파츠 = 플레이스홀더(guid 유지 교체 전제)
- **카탈로그 등록**: `DefenderCatalog.asset` 에 추가(미등록 = 로스터 미노출 — 파이프라인 맵 규칙).
- 경로 지정 git add 시 **`.meta` 짝 필수**(GUID 재생성 방지).

## 완료 기준 (feature 통합 Play)

- [ ] compile 0 에러 · 전체 EditMode green(`BombLandingTests`/`AoeTargetCapTests` 포함).
- [ ] 로스터에 폭탄맨 노출 → 배치 시 머신거너식 방향 조준(4방향) → 확정.
- [ ] 쿨다운마다 방향×N(=3)칸으로 폭탄 **굴러감**(n=1s) → 착지 → 퓨즈(m=1s) 점멸 → 폭발.
- [ ] 폭발이 착지 셀 범위 내 **가까운 순 최대 B(=3)명** 타격. 데미지탄=피해 · 수면탄=Sleep(피해0) · 스턴탄=Stun(피해0), 3종 랜덤.
- [ ] 결정론: 같은 matchSeed → 같은 타입/착탄 시퀀스.
- [ ] 콘솔 에러 0. 기존 유닛 회귀 없음.

## 종료 처리

- README 상단 상태 → "완료 YYYY-MM-DD", `7_handoff_summary.md` 작성.
- 파이프라인 맵 구조 변경 확인: 신규 채널 0·신규 맥락 0 → 맵 갱신 불필요(신규 `MovementKind` arm 은 수치/필드 범주 — 맵 갱신 트리거 아님). 확인만.
