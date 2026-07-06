# 2 · 머티리얼 임팩트 룩 — 하프톤 페이스 + 글로우 + 흰 아웃라인

## 목적

참고 이미지의 점(하프톤) 패턴 페이스 + 광채 + 흰 외곽선 임팩트 룩을 머티리얼 계층에서 구현한다. 숫자마다 오브젝트를 붙이지 않고 공용 머티리얼로 처리(모바일 안전).

## 현황 (critic 검증됨)

- 현 머티리얼 `DamageNumber Outline Mat.mat` 은 **`TextMeshPro/Mobile/Distance Field`** — `_FaceTex`·`_GlowColor`/`_GlowPower` **없음**(모바일 SDF 는 face texture·glow 미지원). `_OutlineWidth`·`_UnderlayColor` 만 있음.
- 비-모바일 **`TextMeshPro/Distance Field`** 셰이더는 `_FaceTex`·glow 노출. face texture 는 셰이더 레벨 multiply라 어떤 SDF 폰트와도 동작(`Bangers SDF` 는 실제 SDF atlas). → **셰이더 교체가 이 unit 의 명시적 선행 작업**(숨은 가정 아님).

## 의존

- **코덱스 하프톤 텍스처** (`assets-codex-request.md` 항목 1). 도착 전에는 솔리드/스톡 플레이스홀더로 배선을 먼저 완료하고, 텍스처 도착 시 `_FaceTex` 슬롯만 교체.

## 변경 대상

- **신규 머티리얼 변종** `Assets/_Project/Fonts/DamageNumber Impact Mat.mat`(비-모바일 Distance Field) — **기존 `.mat` 의 `m_Shader` 를 in-place 스왑하지 않는다**(TMP 키워드/프리셋 상태가 날아감). 새 머티리얼로 생성.
- `Assets/_Project/VFX/DamageNumber_Popup.prefab` — `m_sharedMaterial` 을 신규 변종으로 교체, `m_enableVertexGradient` 켜기(unit 1 과 정합)
- 하프톤 텍스처 임포트 세팅 (도착 시)

## 구현

- 신규 머티리얼 셰이더 = `TextMeshPro/Distance Field`.
- **`_FaceTex`** = 코덱스 하프톤 타일 텍스처. `_FaceTex_ST` 타일링으로 점 밀도 조정. face 색과 곱해져 팔레트 색이 점 패턴에 입혀진다.
- **글로우**: `_GlowColor`(에메랄드/면색 연동)·`_GlowPower`/`_GlowOuter` 로 은은한 후광(과하지 않게).
- **아웃라인**: `_OutlineColor` 검정→**흰색**, `_OutlineWidth` 유지/미세 상향. 어두운 배경 가독.
- (선택) `_UnderlayColor` 소량 드롭섀도로 배경 분리.
- **모바일 대안(폴백 A)**: 비-모바일 셰이더가 부담이면, 모바일 SDF 의 **underlay 를 offset 0 + dilate + 색상**으로 세팅해 glow 를 근사하고, 하프톤만 별도 경량 셰이더로. (glow 는 mobile underlay 로 근사 가능, face tex 만 미지원)
- **폴백 B**: face-tex+glow 만 남긴 트리밍 커스텀 모바일 셰이더 저작(README 후속 후보 승격 조건 = 아래 프로파일 게이트 실패 시).

## 완료 기준

- compile/임포트 에러 0.
- Play: 숫자 면에 하프톤 점 패턴 + 흰 외곽선 + 은은한 글로우로 배경과 분리돼 강렬하게 읽힌다(스크린샷, 참고 이미지 대조).
- **Android 실기기 프로파일 게이트(하드)**: 동시 10+ 숫자 팝업 시 프레임 급락 없음을 실기에서 측정 확인. 실패 시 폴백 A/B 로 이관(이 게이트를 통과해야 unit 2 완료 인정).
- 플레이스홀더 배선 완료 → 코덱스 텍스처 슬롯 교체가 `.mat` 한 곳 수정으로 끝난다.

---

- **검증 2026-07-07**: 신규 `DamageNumber Impact Mat`(비-모바일 `TextMeshPro/Distance Field`, 기존 outline mat 복제 — **in-place 스왑 아님**). `_FaceTex`=medium 하프톤@1.0 · 흰 아웃라인 0.28 · 강한 warm 가짜 글로우(power1/outer1). 프리팹 `DamageNumber_Popup` TMP+MeshRenderer 배선 + vertex gradient on. 오프스크린 렌더로 **인게임 크기 가독성 확인**(청록 247·오렌지 5599 에서 하프톤·아웃라인·글로우 모두 읽힘, 팔레트 충돌 없음).
- **글로우 방식 결정(2026-07-07)**: 씬에 post-processing Volume 0·배틀 카메라 post-FX OFF 확인 → 현재는 TMP SDF **가짜 글로우 밴드**. 사용자 선택 = **가짜 글로우 유지(모바일 안전)**. 진짜 emissive(URP Bloom)는 전역 렌더 변경이라 후속 스펙 보류.
- **Android 실기 프로파일 게이트: 미완** — 비-모바일 셰이더 실기 프레임 확인은 사용자 실기 테스트 대기. 문제 시 폴백 A/B.
