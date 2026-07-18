# 0 · Focus Config SO

## 목적

전 피드백 요소(dim / base-ring / 리티클 / 콜아웃 / 확정비트)와 화살표 시인성의 **모든 시각·타이밍 수치를 SO로 외부화**한다(하드코딩 0, 제약 #6). 이 유닛은 데이터 토대만 — 소비는 후속 유닛. 컴파일·인스펙터 노출까지.

## 변경 대상

- **신규** `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherFocusConfig.cs` (`ScriptableObject`, `[CreateAssetMenu]`)
- **신규 에셋** `Assets/_Project/.../DreamcatcherFocusConfig.asset` (기본값 채운 1개)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — `[SerializeField] DreamcatcherFocusConfig focusConfig;` 추가 (아직 미소비, 참조만)

## 구현

필드 그룹(값은 튜닝 기본, 전부 SO 노브):

- **dim(A)**: `dimColor`(어두운 남색/검정), `dimAlpha`(≈0.35), `dimFadeInSec`(≈0.12), `dimFadeOutSec`(≈0.10)
- **arrow**: `arrowOutlineColor`, `arrowOutlineWidth`, `arrowGlowColor`, `arrowMinAlpha`(tail 최소 알파 상향), `arrowLockBlend`(≈0.7 — 끝점 유닛 당김 계수, 계약 #5)
- **reticle(C)**: `reticleColor`, `reticleInvalidColor`, `reticleCornerLen`, `reticleThickness`, `reticlePadding`, `reticleMinScreenSize`(코너가 **손끝 반경 초과** 보장, 계약 #7), `reticleSnapSpring`·`reticleSnapDamp`(위치 이징), `reticlePopScale`, `lockSwitchHysteresisPx`(정체 전환 마진, 계약 #4)
- **callout(D)**: `calloutScreenOffset`(락온 렉트 상단 + 손끝 반경 초과 오프셋), `calloutIconSize`, `calloutBgColor`, `calloutValidTextColor`·`calloutFullTextColor`(X/3=full 강조), `calloutEdgeClampPad`(화면 밖 clamp 여백), `calloutFadeSec`
- **baseRing(B)**: `baseRingColor`, `baseRingRadius`, `baseRingThickness`, `baseRingPulseSec`, `baseRingLockedFade`(락온 유닛 링 감광/숨김), `baseRingRevealRadius`·`baseRingDistanceFade`(근접 reveal, 계약 #11)
- **confirm(E)**: `confirmConvergeSec`, `confirmPulseColor`, `confirmPulseSec`, `confirmPulseMinRadius`(**손끝 반경 초과** 하한, 계약 #7), `confirmFlashSec`, `enableHaptic`(bool)

authoring 은 Unity 인스펙터 or `manage_scriptable_object` MCP. 색 톤은 기존 `DreamcatcherHandView.UnitHoverTint`(붉은 hover) 참고. **`unitPopBrighten` 은 두지 않는다** — Spine 곱셈 틴트로 밝힘 불가(계약 #6).

## 완료 기준

- 컴파일 통과, 콘솔 클린.
- `DreamcatcherFocusConfig.asset` 생성·기본값 채움, `DreamcatcherHandView` 인스펙터에 `focusConfig` 슬롯 노출·에셋 배선.
- 시각 변화 없음(소비는 유닛 1~6). 회귀 없음.
