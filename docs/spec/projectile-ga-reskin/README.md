# Projectile GA Reskin Spec

**작성일**: 2026-07-03
**상태**: 완료 2026-07-03 (유닛 0~5 구현·커밋, 실게임 Play 검증 PASS). 최종 변종/스케일 선택은 사용자 취향 대기 → `6_handoff_summary.md` 참조.
**파이프라인 토대**: `docs/spec/projectile-visual-upgrade/` (완료 2026-04-28). ViewPool·ProjectileData 스키마·hit event 채널을 그대로 재사용한다. 이 spec 은 벤더 자산만 교체하는 리스킨이다.
**목표**: 투사체 시각을 Gabriel Aguiar *Unique Projectiles Vol 4* 프리팹 외형으로 업그레이드한다. 파일럿 = Arrow. 코드 파이프라인 무변경(단, streak 가드 1건 추가), side-by-side 신규 에셋.

## 검증 질문

> GA 투사체 VFX 를, ECS 가 transform 을 구동하는 우리 파이프라인에서 **데모와 같은 외형(as-is)** 으로 재생할 수 있는가?

파일럿 Arrow 가 BattleScene Play 대 GA 데모 씬 A/B 스크린샷으로 "같은 외형" 이면 YES.

## 사전 조사 결론 (근거)

- GA Arrow01 의 모든 시각 요소는 **transform 위치**에만 의존한다 — world-space 흩뿌림 궤적, local-space 몸체 오라, TrailRenderer×3, Light. 우리 `SyncTransforms` 가 매 프레임 transform 을 world 공간에서 구동하므로 데모의 Rigidbody 무버와 시각적으로 동일한 입력이다.
- `emitterVelocityMode: Rigidbody`(4중 3) 는 무해 — 그 속도를 쓰는 두 기능(InheritVelocity, RateOverDistance)이 **전부 꺼짐**. RB 제거가 외형에 영향 0. (일반화 안전장치로 스트립 시 전 PS 를 `Transform` 모드로 강제.)
- 프리팹의 2번째 "MonoBehaviour" 는 URP `UniversalAdditionalLightData` = Light 부속(=시각). **제거 금지**. 스트립은 타입 지정 제거만 한다.

## 런타임 실측 (2026-07-03, Play 직접 검증)

정적 분석이 놓친 것을 런타임 테스트가 잡음:

- **`autodestruct=True` (트레일 3개)** — GA 데모용 자가정리 속성인데 우리 풀링과 충돌해 재사용 시 트레일이 사라짐. **최우선 필수 수정**, 스트립에 `autodestruct=false` 추가로 해결. (스크린샷: 비행 시 트레일 유지 확인.)
- **색 씻김(preserveVfxColors)** — 예측보다 **미묘**. native 가 약간 더 선명한 HDR 광채, MPB-흰색은 약간 옅음. 선택적 화질 향상(사용자 유지 결정).
- **streak** — 순간이동 시 world-space 파티클發 modest 잔상. autodestruct=false 로 트레일이 살아남으면 재사용 streak 이 생기므로 `ResetVfx` Clear 가 짝으로 필요.

## 작업 단위 목록

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| 0 | `0_strip_tool.md` | GA 프리팹 → view-only 파생 프리팹 자동 생성 에디터 툴 + 속도 의존성 감사 |
| 1 | `1_viewpool_spawn_reset.md` | ViewPool 스폰 즉시 위치 세팅 + Trail/Particle 리셋 (풀 재사용 streak 제거) |
| 2 | `2_pilot_arrow_wiring.md` | 파일럿 `Projectile_Arrow_GA` 신규 에셋 + Archer 와이어링 + A/B 시각 검증 |
| 3 | `3_variant_pack.md` | GA 변종 4종(ExplosiveBullet/Shard/Shuriken/Rock) + Archer 순환 스왑 메뉴 |
| 4 | `4_full_library.md` | GA 투사체 50종 전체 라이브러리(스트립본 + SO) |
| 5 | `5_render_height_and_sorting.md` | visualHeightOffset(타일 위 부양) + ProjectileOffset(유닛 위 sorting) + hit=muzzle/scale |
| 6 | `6_handoff_summary.md` | 인계 요약 — 커밋/구현/검증/주의점/후속 |

## 공통 원칙 (계약)

- ECS 시뮬(`ProjectileMoveSystem`/`ProjectileHitSystem` 데미지 로직) 무변경. 임팩트는 `ProjectileHitEventsSingleton` 채널만 통한다.
- GA 원본 벤더 프리팹을 ProjectileData 에 **직접 참조 금지**(무버/RB/Collider 포함 → SyncTransforms 와 충돌). 반드시 유닛 0 툴로 만든 view-only 파생 프리팹을 참조한다.
- 스트립 규칙: `ProjectileMoveScript` + `Rigidbody` + 모든 `Collider` **제거**. ParticleSystem/TrailRenderer/Light(+URP LightData)/렌더러 **유지**. 모든 `ParticleSystem.main.emitterVelocityMode = Transform` 강제.
- side-by-side: 신규 `Projectile_Arrow_GA.asset`. 기존 `Projectile_Arrow` 및 현재 배송 룩은 불변. Archer 스왑은 파일럿 검증 배선일 뿐, 영구 승격은 검증 후 사용자 결정.
- 모바일 최적화 보류(full-fidelity first). 라이트/트레일 감축은 후속.
- 시각 검증은 스크린샷 A/B 육안(프로젝트 표준). Play 측정은 에디터 포커스 필요.

## 후속 후보 (이 spec 밖)

- 나머지 계열 GA 매핑: `Projectile_Bolt` / `Projectile_CannonBall` / `Projectile_Enemy_*` / `Projectile_Sniper_Crimson`.
- 계열별 속도 의존성 감사(InheritVelocity/rateOverDistance/Stretched-Billboard 인 프리팹은 스트립 시 Transform 모드 강제로 이미 커버되나, 룩 확인 필요).
- 모바일 최적화 pass: 포인트라이트 제거·트레일 감축·soft particle 토글, 실기기 프로파일.
- tint/hueJitter ShaderGraph 플러밍: MPB 프로퍼티(`_BaseColor`/`_EmissionColor` 등) 매핑 확인 — 현재 데이터 배리에이션이 GA 머티리얼에 닿는지 미검증.
- 파일럿 승격: 검증 후 `Projectile_Arrow` in-place 교체 여부 결정.
