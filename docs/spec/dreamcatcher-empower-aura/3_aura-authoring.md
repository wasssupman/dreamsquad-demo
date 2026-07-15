# 3 — Empower 오라 저작 + 정식화

## 목적

임시 폴백 글리프를 대체할 실제 강화 오라 VFX 를 저작하고, 사용자 폴리시 승인 후 `_SKELETON` 을 정식
프리팹으로 승격한다. (unity-vfx-authoring 스킬 기반. 초사이언식 파워업 오라)

## 변경 대상

- `Assets/_Project/VFX/EmpowerAura.prefab` — 정식(구 `EmpowerAura_SKELETON.prefab` 승격)
- `Assets/_Project/VFX/Textures/EmpowerAura_{Glow,Streak}.png` — 전용 텍스처 2종(절차 저작)
- `Assets/_Project/VFX/Materials/EmpowerAura_{Glow,Streak}_Mat.mat` — 가산 머티리얼 2종
- `Assets/_Project/Data/Config/StatusFxRegistry.asset` — Empowered(kind 2) 프리팹/scale 배선

## 구현

1. **5요소 Shuriken 구성**(가산, Local space 유닛 추종):
   - AuraFlame: 몸 감싸 위로 솟는 조밀한 화염 쉘(Cone, Stretch)
   - EnergyFlares: 위로 뻗는 에너지 창끝(Cone 좁음, Stretch)
   - CoreBacklight: 파란 맥동 백라이트(Billboard)
   - GroundRing: 발밑 충격파 링(Circle 둘레 burst, radial 확산)
   - Sparks: 위·바깥 크래클 스파크(Stretch)
2. **팔레트**: 파랑/주황 이중톤(입자별 랜덤) + 화이트-핫 코어. 가산 겹침으로 승화.
3. **전용 텍스처**(Default-Particle 대체): Glow(라디얼 소프트, billboard용) / Streak(세로 화염혀, stretch용) 절차 생성.
4. **정식화**: 폴리시 OK 후 `EmpowerAura_SKELETON` → `EmpowerAura.prefab` 승격(같은 guid 유지 → registry 배선 보존),
   구 단일 mat 삭제.
5. **조정**: 화염혀 뭉툭화(lengthScale/속도↓·taper 완화), registry `scale` 1→0.7.
6. **모바일 예산**: 총 ≈90(임팩트 티어). 가산 겹침 overdraw 유의(다수 유닛 동시 강화 시 밀도 주의 — 후속 프로파일).

## 완료 기준

- [x] 프리팹/텍스처/머티리얼 저장, registry 배선, 구 _SKELETON+단일 mat 정리
- [x] 컴파일/임포트 클린, 콘솔 에러 0
- [x] Play 프리뷰: 드림캐쳐 강화 유닛에 파랑/주황 파워업 오라(사용자 OK)

사용자 확인 2026-07-15. 커밋 `10370f6e`(스켈레톤)·`b7d58720`(폴리싱)·`33d3b90f`(정식화).
