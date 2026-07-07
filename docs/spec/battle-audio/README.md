# battle-audio

> 상태: **구현 완료 2026-07-08** — 투사체 발사 SFX + 전투 BGM. `SoundManager`(score-hud-impact-upgrade unit 4 도입) 인프라 확장. Play 청각 검증은 사용자. **BGM 은 현재 SFX-폴백 루프**(ElevenLabs Music API 는 유료 플랜 필요 → 무료 티어 402) — 유료 전환/본인 트랙 시 스왑.

## 상위 목표

전투 청각 피드백 확장: ① **방어유닛 투사체 발사 SFX**, ② **전투 배경음(BGM)**. score-hud 의 `SoundManager` 를 재사용/확장한다(새 매니저 신설 없음).

## 작업 단위

| # | 작업 | 변경 대상 |
|---|---|---|
| 0 | **SoundManager 확장** — `PlayProjectileFire()`(스로틀) + BGM 소스(루프)·`PlayBgm/StopBgm`·`GameManager.PhaseChanged` 구독(Battle 에만) | `Scripts/Audio/SoundManager.cs` |
| 1 | **투사체 발사 SFX** — 생성 + `BattleBridge` 투사체 스폰 드레인 훅(스폰 성공 시 `PlayProjectileFire`) | `BattleBridge.cs`, `Audio/ProjectileFire.mp3` |
| 2 | **BGM** — 루프 트랙 + Battle 페이즈 자동 재생/정지, import loadType=Streaming | `Audio/BattleBgm.mp3`, SoundManager |

## Feature-wide 계약

- **SoundManager 재사용**: 새 매니저 없음. score-hud 의 `Wassup.Core.SoundManager`(BattleScene GameObject)에 필드/메서드 추가.
- **투사체 훅 위치**: `BattleBridge` 의 `ProjectileSpawnRequest` 드레인 루프(방어유닛 원거리 공격). 스킬/메테오(`ApplyMeteor` → `SpawnProjectile` 직접)는 이 경로 밖이라 미포함. `BattleBridge` 는 MonoBehaviour 이므로 `SoundManager` 호출은 ECS 경계 위반 아님.
- **스로틀**: 다발 발사 과중첩 방지 — `PlayProjectileFire` 는 `projectileFireMinInterval`(0.045s) 내 재호출 skip.
- **BGM 페이즈**: `SoundManager` 가 `GameManager.PhaseChanged` 지연 구독 → `Battle` 진입 재생, 이탈 정지(`bgmOnlyInBattle`). BGM import=Streaming(메모리 절약).
- **저작-시점 클립**: ElevenLabs 로 생성한 로컬 에셋 재생. 런타임 API 호출 0. 튜닝값(볼륨·스로틀·`bgmOnlyInBattle`) 전부 `[SerializeField]`.
- **BGM 소스 제약(현재)**: ElevenLabs **Music API 는 유료 플랜 전용**(무료=402). 현재 BGM 은 Sound Effects 엔드포인트 22초 루프(음악 전용 아님, 이음새 seamless 미보장). 유료 전환 또는 사용자 트랙 확보 시 `BattleBgm.mp3` 스왑(같은 경로/GUID 유지 → 배선 불변).

## 후속 후보

- **유닛/속성별 발사 SFX** — 현재 단일 발사음. 유닛 타입·투사체 속성(fire/ice/arrow)별 클립. [M]
- **진짜 음악 BGM** — ElevenLabs Music API(유료) 또는 외부 트랙. seamless 루프 포인트. [S]
- **적 처치/피격·스킬 캐스트·UI SFX** — 청각 팔레트 확장. [M]
- **오디오 믹싱/덕킹** — 마일스톤·스킬 순간 BGM 덕킹, AudioMixer 그룹/볼륨 설정. [M]
- **음소거/볼륨 설정 UI** — 옵션 메뉴. [S]
