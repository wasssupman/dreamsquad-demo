# Projectile Visual Upgrade — Design

**작성일**: 2026-04-28
**Spec 폴더**: `docs/spec/projectile-visual-upgrade/`

## 목표

ECS RenderMesh 기반의 단순 mesh+material 투사체를 prefab 기반 시각으로 교체하고, 데이터-드리븐 + per-shot 랜덤 두 층의 배리에이션 인프라를 도입한다.

## 핵심 결정

- ProjectileData 가 `projectilePrefab`/`hitPrefab` 을 직접 보유 (source of truth).
- ECS 시뮬레이션은 변경 없음. Combat→Presentation 임팩트 채널 (`ProjectileHitEventsSingleton`) 신설.
- Presentation 계층에 `ProjectileViewPool` (MonoBehaviour) 도입; 적군 view 풀과 같은 패턴.
- 배리에이션은 (1) data 결정적 (tint/scale/spin/facing/textureVariants) + (2) per-shot 랜덤 (jitter), 시뮬레이션 결정성에는 영향 없음.
- 텍스처 변종은 에디터 타임 베이크 자산. 런타임 절차 생성 금지.

## 스코프

이번 spec 은 **Projectile prefab + Hit prefab 만**. Cast(머즐 플래시), Waterball 매핑, 디펜더별 무기 anchor 추출은 후속 후보로 분리.

상세는 spec 폴더 README + 0~8 작업 단위 참조.
