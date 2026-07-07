# battle-audio

> 상태: **구현 완료 2026-07-08** — 투사체 발사 SFX(디펜더 전용) + **BGM 1분 루프**(ElevenLabs Music API, 유료) + **캐릭터별 배치 추임새 음성**(16 디펜더 각 다른 애니 라이브러리 보이스·남녀 8:8·클래스 어울리는 라인). `SoundManager`(score-hud-impact-upgrade unit 4 도입) 인프라 확장. Play 청각 검증은 사용자.

## 상위 목표

전투 청각 피드백 확장: ① **방어유닛 투사체 발사 SFX**, ② **전투 배경음(BGM)**. score-hud 의 `SoundManager` 를 재사용/확장한다(새 매니저 신설 없음).

## 작업 단위

| # | 작업 | 변경 대상 |
|---|---|---|
| 0 | **SoundManager 확장** — `PlayProjectileFire()`(스로틀) + BGM 소스(루프)·`PlayBgm/StopBgm`·`GameManager.PhaseChanged` 구독(Battle 에만) | `Scripts/Audio/SoundManager.cs` |
| 1 | **투사체 발사 SFX** — 생성 + `BattleBridge` 투사체 스폰 드레인 훅(스폰 성공 시 `PlayProjectileFire`) | `BattleBridge.cs`, `Audio/ProjectileFire.mp3` |
| 2 | **BGM** — 1분 루프 트랙(Music API) + Battle 페이즈 자동 재생/정지, import loadType=Streaming | `Audio/BattleBgm.mp3`, SoundManager |
| 3 | **캐릭터별 배치 추임새(TTS)** — 디펜더 배치 성공 시 그 캐릭터 보이스로 클래스 어울리는 라인 재생 | `DefenderUnitData.deployVoiceClip`, `BattleBridge`, `Audio/DeployVoice/Deploy_{name}.mp3`, SoundManager |

## Feature-wide 계약

- **SoundManager 재사용**: 새 매니저 없음. score-hud 의 `Wassup.Core.SoundManager`(BattleScene GameObject)에 필드/메서드 추가.
- **투사체 훅 위치 + 디펜더 필터**: `BattleBridge` 의 `ProjectileSpawnRequest` 드레인 루프. 이 드레인은 **디펜더·적 원거리 공격 공용**이므로, 슈터가 `DefenderUnitTag` 보유일 때만 `PlayProjectileFire`(적 투사체는 무음, 사용자 요청). 스킬/메테오(`ApplyMeteor` → `SpawnProjectile` 직접)는 이 경로 밖이라 미포함. `BattleBridge` 는 MonoBehaviour 이므로 `SoundManager` 호출은 ECS 경계 위반 아님.
- **스로틀**: 다발 발사 과중첩 방지 — `PlayProjectileFire` 는 `projectileFireMinInterval`(0.045s) 내 재호출 skip.
- **BGM 페이즈**: `SoundManager` 가 `GameManager.PhaseChanged` 지연 구독 → `Battle` 진입 재생, 이탈 정지(`bgmOnlyInBattle`). BGM import=Streaming(메모리 절약).
- **저작-시점 클립**: ElevenLabs 로 생성한 로컬 에셋 재생. 런타임 API 호출 0. 튜닝값(볼륨·스로틀·`bgmOnlyInBattle`) 전부 `[SerializeField]`.
- **BGM**: ElevenLabs **Music API**(`music_v2`, 유료 플랜) 로 생성한 **60초 tense-battle 루프**. import loadType=Streaming.
- **캐릭터별 배치 추임새(unit 3)**: 클립은 **`DefenderUnitData.deployVoiceClip`**(캐릭터 데이터) — 16 디펜더 각각 다른 애니 라이브러리 보이스(남녀 8:8, 무작위 배정) + `role`(클래스) 어울리는 짧은 라인. 훅 = `BattleBridge.TryBeginDefenderDeployment` 성공 직전 → `SoundManager.PlayDeployVoice(unitData.deployVoiceClip)`. 저작: ElevenLabs 공유 라이브러리 보이스(add→TTS→remove 슬롯 재사용) `eleven_multilingual_v2`. 클립은 `Audio/DeployVoice/Deploy_{디펜더명}.mp3

## 후속 후보

- **유닛/속성별 발사 SFX** — 현재 단일 발사음. 유닛 타입·투사체 속성(fire/ice/arrow)별 클립. [M]
- **BGM seamless 루프 다듬기** — 60초 트랙 이음새/페이드 튜닝, 스테이지별 BGM 변주. [S]
- **배치 추임새 라인/보이스 튜닝** — 캐릭터별 라인/보이스 취향 조정, 여러 라인 순환(현재 캐릭터당 1라인). [S]
- **적 처치/피격·스킬 캐스트·UI SFX** — 청각 팔레트 확장. [M]
- **오디오 믹싱/덕킹** — 마일스톤·스킬 순간 BGM 덕킹, AudioMixer 그룹/볼륨 설정. [M]
- **음소거/볼륨 설정 UI** — 옵션 메뉴. [S]
