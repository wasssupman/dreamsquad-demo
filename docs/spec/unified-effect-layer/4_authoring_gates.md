# 4 — 저작 개방: 거절 기준 교체 (Unity + 코어 한 곳)

## 목적
카드·유닛 능력·악몽 저작으로 **AA 와 타격 운석을 굽힌다.** 빌더의 출처별 블랙리스트를 「원점을 못 뽑는 조합만 거절」(계약 7)로 바꾸고, 「남의 배치」 트리거를 저작할 수 있게 한다. 같이, 착탄 비산의 인라인 자를 정본 진입점으로 바꾼다(계약 6).

## 변경 대상
- `Scripts/Data/Dreamcatcher/DcMechanic.cs` — 페이로드에 **착탄 예고 체크**(`telegraph`, 기본 꺼짐 · U1). 트리거에 **주체 축**(`Self` 기본 · `NewlyPlacedAlly` — 호스트가 살아 있는 동안 새로 배치되는 아군). 필드 append · 기본값 = 오늘 동작.
- `Scripts/BattleCoreUnity/CardDefinitionBuilder.cs` — 거절 3곳 재작성: 카드 `OnPlace` 는 주체가 `Self` 일 때만 거절(붙는 순간 이미 지난 사건) · `ProjectileToTarget` 의 칸 결합 탄 허용(원점 = 맞은 적 좌표, `tileRange` = 착탄 반경, `duration` = 낙하 시간, `telegraph` = 예고 원) · `EmitProjectilePattern` 은 원점을 내는 모든 트리거 허용.
- `Scripts/BattleCoreUnity/BindingDefinitionBuilder.cs` — 주체 축 → `BindingSubject.Any` + `SubjectFilter.PlacedDefender` + `Lifetime.Owner`(탐침 케이스 1 이 손으로 만든 줄 그대로).
- `Scripts/BattleCoreUnity/CombatDefinitionBuilder.cs` `Translate` — `SkyFallOnTarget` 을 (`SkyFallOnEntity`, `SingleSplash`) 에서 칸 결합(대상 좌표 낙하 × `TileAoe`)으로 옮길지: **라이브 사용처 확인 후** 0 이면 옮기고, 있으면 값 무변 번역을 유지하고 이 문서에 사유.
- `Scripts/BattleCore/Phases/TickProjectilePhase.cs` 착탄 비산 — 인라인 `SplashRadius + HitRadius` → `SkillMath.ReachFromImpact`(자리형 — 투사체 착탄). 라이브 저작 중 비산을 켠 탄(`Projectile_CannonBall`)은 참조 0 이라 규칙 영향 없음 — 커밋 전에 참조 0 을 다시 확인.
- `Scripts/Data/Dreamcatcher/` 카드 에디터 표기(인스펙터) — 새 주체 축 드롭다운.

## 구현
- 거절 판정은 한 함수: (트리거, 주체, 페이로드, 탄 결합) → 원점을 낼 수 있나. 못 내면 Warn + 건너뜀(오늘 형식).
- 시연용 카드 2장은 **테스트 픽스처로만**(에셋 추가 금지 — 콘텐츠 추가는 사용자 몫). 빌더 테스트가 SO 를 메모리에서 만든다.

## 완료 기준
- 빌더 테스트: AA 카드 · 타격 운석 카드가 경고 0 으로 굽히고, 굽힌 줄이 탐침의 손조립 줄과 필드 동치.
- 오늘의 카드 60 · 능력 26 · 악몽 24 를 굽힌 `BindingDef` 줄 전체가 unit 3 끝과 필드 동치(빌더 스냅샷 테스트 — 정의표 해시 `ConfigHash` 는 웨이브·덱에만 반응해 증거가 못 된다).
- `EffectWitness` 가 두 카드를 관측(Assets lane) · 헤드리스 · 골든 11 · EditMode 3 어셈블리(선행 2 외 0) · PlayMode Core.
- `core-reviewer` APPROVE(unit 1~4 묶음).
- **체크포인트**: 사용자 플레이 확인 — 테스트 픽스처 카드를 판에 넣는 개발용 경로로 AA · 타격 운석을 눈으로.
