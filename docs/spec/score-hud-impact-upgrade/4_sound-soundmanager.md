# 4 — 사운드 (SoundManager + 처치 틱) · [게이트]

> **상태: 게이트** — ElevenLabs 클립 확보 후 진행. API 사용은 일단 미룸(사용자 결정 2026-07-07). 0~3 시각 목표 먼저.

## 목적

처치마다 **점수 틱 SFX**(빠른 연속 시 피치 상승)로 청각 타격감을 더한다. 프로젝트 최초 오디오 도입 — 경량 `SoundManager` 신설(싱글톤 제약 해제 반영).

## 변경 대상

- 신규 `Assets/_Project/Scripts/Audio/SoundManager.cs`
- `Assets/_Project/Scripts/UI/ScoreHudView.cs` (호출부)
- 오디오 에셋 `Assets/_Project/Audio/*.wav` (ElevenLabs 저작-시점 생성)
- 씬 배선: SoundManager GameObject (`unity-feature-wiring` 스킬)

## 구현

- **정책 전제**: SoundManager 싱글톤은 CLAUDE.md §5 · TRD §5.2 에 의도된 예외로 등재됨(2026-07-07). 신설 가능.
- **SoundManager**: 경량 싱글톤 MonoBehaviour. 풀드 `AudioSource` N개(라운드로빈, 동시 틱 겹침 대응) + 직렬화 클립 참조(score tick 등). `PlayScoreTick(float pitch = 1f)` 공개 메서드. 클립 미할당 시 no-op(무음 안전).
- **호출**: 프레임당 flush 1회로 `SoundManager.Instance?.PlayScoreTick(pitch)`(README 병합 계약 — AoE 다처치가 같은 프레임 틱 N발로 클리핑되지 않게). 피치는 짧은 연속 처치(프레임 간) 시 상승(직렬화 상한/감쇠) — 순수 청각 연출, 점수값 불변.
- **클립 생성 (저작-시점)**: ElevenLabs Text-to-Sound-Effects (`POST /v1/sound-generation`, 모델 `eleven_text_to_sound_v2`, `duration_seconds` 0.5~, `prompt_influence`)로 후보 생성 → 사용자 오디션 → 채택본 `Assets/_Project/Audio/` 커밋. **런타임 API 호출 금지**(오프라인/모바일/키유출 회피).

## 계약/주의

- 클립은 로컬 에셋으로 재생. 런타임 네트워크 의존 0.
- 튜닝값(볼륨·피치 상한/감쇠·풀 크기) 전부 `[SerializeField]`.
- 표시/점수 로직 불변 — 사운드는 순수 피드백.

## 완료 기준

- SoundManager compile + 씬 배선 + `ScoreHudView` 호출 연결.
- 클립 할당 후 Play: 처치 시 틱, 빠른 연속 처치 시 피치 상승 육안(청각) 확인.
- 클립 미할당 상태에서도 에러/예외 0(no-op).
