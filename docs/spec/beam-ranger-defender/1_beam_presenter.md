# 1 — BeamPresenter: FireBeam 통합 + 공격 이벤트 코얼레스

## 목적

고속 틱 공격 이벤트를 **지속 빔**으로 번역하는 프레젠터를 만든다. 이 spec 의 신규 메커니즘
절반(레이저)이 이 unit 에 있다 — 전부 Presentation 계층, 심 무변경.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/BeamPresenter.cs` (신규 MonoBehaviour)
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `beamVfxPrefab` 필드 (참조 유무 = 빔 유닛 판별자)
- `Assets/_Project/Prefabs/`(또는 VFX 폴더 관례 위치) — FireBeam 게임용 사본 프리팹
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — attack visual drain 에서 BeamPresenter 구동 (기존 drain 확장)
- `Assets/_Project/Scenes/BattleScene.unity` — 배선 (`unity-feature-wiring` 스킬)

## 구현

1. **소스 프리팹**: `Assets/PixPlays/ElementalBeams/FireBeam/Version_URP/FireBeam.prefab` 을
   `_Project` 쪽 사본으로 복사 후 벤더 통합 규칙 적용(무버/RB/Collider 스트립, PS scalingMode
   Hierarchy 확인, 파티클 emitterVelocityMode). 원본 벤더 폴더는 불변.
2. **코얼레스 규칙**: `UnitAttackVisualEvents` drain 에서 (attacker → target) 빔 세션을 유지.
   - 이벤트 수신 시: 해당 attacker 의 빔이 없으면 생성, 있으면 TTL 갱신(≈ 0.35s = 틱 간격 여유).
   - TTL 만료·attacker/target 사망 시 빔 종료(풀 반납).
   - 타겟 변경 시 빔 끝점을 새 타겟으로 스냅(MVP — 스윕 보간은 후속).
   - **빔 유닛 판별은 데이터**: `DefenderUnitData` 의 beam 여부 필드(예: `beamVfxPrefab` 참조가
     곧 판별자 — kind 분기 하드코딩 금지, 메커닉 연출은 메커닉이 소유).
3. **빔 지오메트리**: muzzle(=castAnchorBone 또는 유닛 view 상단) → 타겟 view 중심.
   길이 스트레치는 FireBeam 프리팹 구조(start/beam/end 파츠) 확인 후 결정 — 스케일 방식이면
   비균등 스케일에 따른 파티클 왜곡 확인. **view 공간에서 처리(sim-Y 금지 — BoardSpace 교훈).**
4. **애님/SFX 코얼레스**: 빔 유지 중 공격 애님은 캐스트 루프 1회(재트리거 억제), 발사 SFX 는
   빔 시작 시 1회 + 루프. unit 0 관측 결과를 반영.
5. 풀링 필수(TrailRenderer 류 autodestruct 함정 — 벤더 lessons).

## 완료 기준

- [ ] 에디터 Play: 버스터즈가 공격하는 동안 빔이 유닛→타겟에 지속 표시, 타겟 죽으면 다음 타겟으로 이동, 공격 중지 시 소멸
- [ ] 오프스크린/Play 스크린샷으로 빔 시각 확인 (배경/프랍 스크린샷 검증 관례)
- [ ] 콘솔 클린 (풀 leak·머티리얼 인스턴스 누수 없음)
