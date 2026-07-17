# 7. 실증 유닛 — 머신건 + e2e 검증

## 목적

feature 전체를 관통하는 실증 유닛 1종을 만든다: 방향 지정 배치 → 레인 게이트 → 0.1초 간격 10연발 버스트 → 경로 히트.

## 변경 대상

- `Assets/_Project/GameData/` 머신건 `DefenderUnitData` 에셋 + 전용 `ProjectileData` 에셋 (신규 — 기존 유닛 에셋 폴더 컨벤션 따름)
- `DefenderCatalog` 에셋 units 배열 등록
- (선택) 스탯 시트 반영은 이번 스코프 밖 — SO 직접 저작(artillery-defender 선례)

## 구현

**ProjectileData**: flightMode `Directional`, pierceCount 1(첫 피격 소멸), 속도/스케일은 기존 탄 에셋 참고. 비주얼 프리팹은 기존 벤더 투사체 재사용(vendor-projectile-vfx 통합 규칙 준수).

**DefenderUnitData**: `directionalAttack true` · `shotCount 10` · `shotIntervalSec 0.1` · `spreadAngleDeg 0` · attackCooldown 은 버스트 완주 포함 체감으로 튜닝(계약 8 — CooldownAfterVolley 기산 감안). outputs 에 발당 Damage 1건(총 DPS = 발당 × 10 / 사이클 — 밸런스는 SO 수치로). id 는 카탈로그/저장 호환 불변 키로 신규 발급.

**아트**: 기존 Spine 스켈레톤 + partSkins/slotColors 재조합으로 임시 외형(placeholder — guid 유지 교체 전제). 정식 아트는 스코프 밖.

**에셋 생성**: UnityMCP execute_code 로 SO 생성·필드 기입·카탈로그 등록(artillery-defender 선례). 시트 importer 는 건드리지 않는다.

**e2e**: scripted battle 패턴(TestModeContext.Set + StartBattle + update 콜백 모니터)으로 — 배치 → 방향 확정(리플렉션으로 aim 확정 강제 가능) → 버스트 발사 → 레인 내 적 피해를 조건 기반 스크린샷/로그로 확인.

**주의**: PlayMode 스모크 4건(DreamcatcherDeckCarryIn·SquadCarryIn·DreamstoneCarryIn·MovementIntegrity)은 main 에서 이미 실패 상태로 문서화됨(`docs/spec/README.md` "PlayMode 스모크 위생" 2026-07-16) — e2e 추가 시 신규 회귀와 혼동하지 말 것.

## 완료 기준

- [x] 스쿼드 빌더/손패에 머신건 노출(카탈로그 등록만으로 — 별도 카드 에셋 없음 확인)
- [x] 레인 밖 적만 있을 땐 발사하지 않음(탄 낭비 게이트 동작)
- [ ] Play e2e: 드래그→드롭→방향 지정→활성화→레인에 적 진입 시 0.1s 간격 10연발→피격 대상 데미지 넘버·사망 처리까지 전체 플로우 1회 통과 (**사용자 확인 대기** — unit 6 배선 커밋 후)
- [ ] 실기기(Android) 스모크 1회 — 스와이프 2페이즈 조작감 확인 (사용자)

확인 2026-07-17 — 에셋 생성·카탈로그 등록(17종) · 축 매핑 실측(Directional→DirectionalLinear/PathHit, 기존 2모드 무변화) · **통합 테스트 `DirectionalVolleyIntegrationTests` 7건**으로 레인 게이트/버스트 완주/2.5s 사이클/부채꼴 3발/기존 유닛 무회귀 검증. EditMode 905 green.

**Play e2e 대체 근거**: 시뮬 계약은 실제 시스템 world 통합 테스트가 덮는다. 남은 것은 Mono 배선(드래그→조준→확정)과 시각 — 이건 사용자 Play 로만 판정 가능(오프스크린은 실제보다 관대).
