# 4 — 사운드 (SoundManager + 처치 틱)

> **상태: 완료 2026-07-07** — SoundManager 신설 + ScoreHudView 배선(연속 처치 피치 상승 heat). 클립=ElevenLabs 생성 `ScoreTick.mp3`(후보 5종 오디션 → A_arcade 채택). BattleScene 에 SoundManager GameObject 배선.

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

## 구현 메모 (실제)

- `SoundManager`(`Wassup.Core`, `Scripts/Audio/SoundManager.cs`): 씬-로컬 싱글톤(DontDestroyOnLoad 아님), `Awake` 에서 `voiceCount`(6)개 `AudioSource` 라운드로빈 풀 생성(2D, playOnAwake=false). `PlayScoreTick(pitch)` — 클립 null 이면 no-op, pitch 0.5~3 clamp.
- **피치 heat**: `ScoreHudView` 가 flush마다 `_soundHeat += killCount*soundPitchPerKill`(상한 `soundPitchMax-soundPitchBase`), `PlayScoreTick(soundPitchBase+_soundHeat)`. heat 는 `soundHeatDecay`/s 로 감쇠 → 처치 멈추면 기본 피치. Battle 리셋 시 heat=0.
- 클립: ElevenLabs `POST /v1/sound-generation`(mp3 128k/44.1k, duration 0.5 최소). 프롬프트 "crisp bright arcade score tick". 5종 생성→오디션→`ScoreTick.mp3` 채택, 나머지 폐기. 저작-시점 생성, 런타임 API 0.

✅ 2026-07-07: compile 0 err + SoundManager GameObject BattleScene 배선 + ScoreTick 클립 할당. Play 청각 검증(틱·피치 상승)은 사용자 확인.
