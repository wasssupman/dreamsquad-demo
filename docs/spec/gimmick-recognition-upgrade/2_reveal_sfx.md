# 2 — 리빌 등장 효과음

## 목적

리빌의 인지력을 **UI 영역을 전혀 먹지 않는 채널**로 보강한다. 소리는 화면을 차지하지 않으면서 "이번 판은 다르다"를 즉시 알린다. 보이스는 배제한다 — 경험상 어색하다는 사용자 판정(2026-07-31).

## 변경 대상

- `Assets/_Project/Scripts/Audio/SoundManager.cs` — 재생 진입점 1개 + 볼륨 필드
- `Assets/_Project/Scripts/UI/GimmickPhaseView.cs` — 비트 ①에서 재생 호출
- `Assets/_Project/Audio/GimmickReveal.mp3` — 공용 등장음 (**미착지**, 아래 참조)
- `Assets/_Project/Data/Config/GimmickRevealConfig.asset` — `defaultSfxClip` 배선 (클립 도착 후)

씬 변경 없음 — 볼륨은 `SoundManager` 코드 기본값(0.8)으로 충분하고, 클립은 SO 가 소유한다.

## 구현

**재생 진입점** — `SoundManager` 에 `PlayGimmickReveal(AudioClip clip)` 추가. `PlayAttack(AudioClip)` / `PlayDeployPlace(AudioClip)` 가 이미 clip 인자를 받는 전례이므로 같은 모양을 따른다. `clip == null` 이면 조용히 no-op — 클립이 없어도 리빌은 성립한다.

**클립 해석 순서** (`GimmickPhaseView`):
```
엔트리의 sfxClip → 없으면 config 의 공용 defaultSfxClip → 없으면 무음
```
기믹별 세분화는 후속이다. 지금은 **공용 1개**만 만들고, 엔트리의 `sfxClip` 은 슬롯만 열어둔다(계약 5의 nullable 패턴).

**소리 성격** — 짧은 등장 스팅(0.5~1.0s). `Audio/BossWarning.mp3` 가 같은 계열(경고 스팅)이니 볼륨·길이 감각의 기준으로 삼되, 경고가 아니라 **선언**이라 톤은 다르다. ElevenLabs SFX 생성은 최소 0.5s 제약이 있다.

**재생 시점** — 비트 ①(도장)의 시작. 아이콘이 찍히는 순간과 소리가 맞아야 한다. `useUnscaledTime` 시퀀스와 같은 프레임에서 호출한다.

**스킵 시** — 탭 스킵으로 리빌을 건너뛰면 소리는 이미 재생 중이다. 끊지 않는다(원샷이라 곧 끝나고, 중간에 끊으면 더 어색하다).

## 완료 기준

- 컴파일 에러 0.
- Play: 리빌 비트 ① 시작과 함께 등장음이 들리고, 아이콘 등장 타이밍과 어긋나지 않는다.
- 클립 슬롯을 비운 상태에서 리빌이 무음으로 정상 재생된다(크래시·경고 없음).
- 탭 스킵 시 소리가 부자연스럽게 끊기지 않는다.
- 기믹 비활성 매치에서 소리가 나지 않는다.
- **이 커밋 단독 revert 시** 리빌이 무음으로 정상 동작한다 — 소리가 어색하면 이 커밋만 되돌린다(계약 10).

## 2026-08-01 착지 상태

**배선 완료 · 클립 미착지.**

- `SoundManager.PlayGimmickReveal(AudioClip)` + `gimmickRevealVolume`(0.8) 추가. 클립은 호출측이 넘긴다 — `PlayAttack`/`PlayDeployVoice` 와 같은 "볼륨만 여기, 클립은 호출측" 형태.
- `GimmickPhaseView.PlayRevealSfx` 가 비트 ① 시작과 같은 프레임에 호출. 해석 순서 = 기믹 전용 `sfxClip` → 공용 `defaultSfxClip` → 무음.
- **발화 검증 통과**: `defaultSfxClip` 에 임시 클립(`BossWarning.mp3`)을 물리고 Play 에서 `BeginReveal` → 재생 중 AudioSource 0 → 1. 전용 클립이 없는 기믹(ClockOut)으로 확인해 **공용 폴백 경로**까지 탔다. 검증 후 임시 클립은 제거(에셋 diff 0 확인).

**막힌 것**: 등장음 에셋 자체. MCP `generate_audio` 의 fal provider 가 `configured: false` 이고, ElevenLabs 경로는 키가 스크래치패드 전용이라 세션 간 넘어오지 않는다. 클립이 생기면 `defaultSfxClip` 슬롯에 꽂기만 하면 되고 **코드 변경은 없다**.

**클립 만들 때**: 짧은 등장 스팅(0.5~1.0s, ElevenLabs SFX 최소 길이 0.5s). `Audio/BossWarning.mp3` 가 같은 계열(스팅어)이라 길이·볼륨 감각의 기준이 되지만, 경고가 아니라 **선언**이라 톤은 달라야 한다.
