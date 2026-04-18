# Common Skill VFX Reference

상태: 사용자 승인 대기 중. 아래 카탈로그 엔트리는 모두 draft 이며, 사용자 승인 없이 카탈로그에 항목 추가 금지.

## Whirlwind
- **Visual elements**: dust spiral, pale wind ring, upward swirl
- **Typical palette**: pale cyan, white, gray-brown
- **Timing**: 1.2s burst into 3.0s sustained spin, then 0.4s fade
- **Particle types**: sprite, trail
- **Reference games**: Diablo 4, Hades, LoL 분위기 참고
- **Project tone**: donut shape + velocity over lifetime 기반의 경량 Shuriken 회오리
- **Suggested MaxParticles**: 50
- **sound_cue_hint**: airy whoosh with light grit

## Fireball
- **Visual elements**: hot core, ember trail, small smoke tail
- **Typical palette**: orange, yellow-white, dark ember red
- **Timing**: 0.15s launch flare, travel sustain, 0.25s impact pop
- **Particle types**: sprite, trail
- **Reference games**: Diablo 4, PoE 2, LoL 분위기 참고
- **Project tone**: 발사체 trail 최소화, impact 는 짧은 burst 로 분리
- **Suggested MaxParticles**: 60
- **sound_cue_hint**: short ignition snap

## Meteor
- **Visual elements**: warning ring, descending ember streak, ground burst
- **Typical palette**: orange, ash brown, red
- **Timing**: 0.8s warning, 0.1s hit flash, 0.9s debris fade
- **Particle types**: sprite, trail
- **Reference games**: Diablo 4, Slay the Spire, LoL 분위기 참고
- **Project tone**: 경고링은 직접 호출, 폭발은 ECS 시점 동기화 burst 로 분리
- **Suggested MaxParticles**: 100
- **sound_cue_hint**: warning hum into heavy impact

## Portal
- **Visual elements**: circular rim, inner swirl, sparse sparks
- **Typical palette**: violet-blue, cyan, white
- **Timing**: 0.3s open, 2.0s stable loop, 0.25s close
- **Particle types**: sprite, trail
- **Reference games**: Diablo 4, PoE 2, Hades 분위기 참고
- **Project tone**: 두 겹 이상 겹치지 않는 얇은 링과 느린 회전 입자로 구성
- **Suggested MaxParticles**: 80
- **sound_cue_hint**: arcane hum

## Shield Aura
- **Visual elements**: soft ring, orbit motes, thin pulse
- **Typical palette**: cyan, mint, white
- **Timing**: 0.2s appear, 2.5s loop pulse, 0.3s fade
- **Particle types**: sprite
- **Reference games**: LoL, Hades, Diablo 4 분위기 참고
- **Project tone**: 과한 반투명 볼륨 대신 저밀도 orbit motes 로 표현
- **Suggested MaxParticles**: 50

## Poison Drip
- **Visual elements**: toxic droplets, faint splash, hanging mist
- **Typical palette**: sickly green, dark olive, black-green
- **Timing**: 0.4s drip spawn, 0.2s splash, 1.0s residue fade
- **Particle types**: sprite
- **Reference games**: PoE 2, Diablo 4, Hades 분위기 참고
- **Project tone**: 중력 기반 droplet 몇 개와 지면 근처 mist 최소치만 사용
- **Suggested MaxParticles**: 40
- **sound_cue_hint**: wet acidic tick

## Lightning Bolt
- **Visual elements**: sharp beam, branch spark, brief hit flash
- **Typical palette**: white, electric blue, pale violet
- **Timing**: 0.05s strike, 0.1s branch flicker, 0.15s afterglow
- **Particle types**: trail, sprite
- **Reference games**: LoL, Diablo 4, PoE 2 분위기 참고
- **Project tone**: 라인은 짧게, branch 는 sprite burst 로만 처리
- **Suggested MaxParticles**: 45
- **sound_cue_hint**: dry electric crack

## Heal Glow
- **Visual elements**: soft pulse, upward motes, center shimmer
- **Typical palette**: warm gold, soft green, white
- **Timing**: 0.2s bloom, 1.2s sustain, 0.4s fade
- **Particle types**: sprite
- **Reference games**: Hades, LoL, Slay the Spire 분위기 참고
- **Project tone**: emissive color 감은 material 파라미터 위주, 입자는 적게
- **Suggested MaxParticles**: 45
- **sound_cue_hint**: gentle rising chime

## Ice Shard
- **Visual elements**: shard burst, cold mist, tiny sparkle
- **Typical palette**: icy blue, white, pale teal
- **Timing**: 0.08s crack, 0.25s shard burst, 0.6s cold mist fade
- **Particle types**: sprite, mesh
- **Reference games**: Diablo 4, LoL, Hades 분위기 참고
- **Project tone**: mesh shard 는 최소 수량만, 주 표현은 sprite burst 로 처리
- **Suggested MaxParticles**: 70
- **sound_cue_hint**: brittle ice snap

## Teleport Portal
- **Visual elements**: collapse ring, vertical streaks, exit spark bloom
- **Typical palette**: magenta, blue-white, black
- **Timing**: 0.2s open flash, 0.6s transit shimmer, 0.2s exit pop
- **Particle types**: sprite, trail
- **Reference games**: PoE 2, Diablo 4, LoL 분위기 참고
- **Project tone**: 입구/출구를 동일 skeleton 으로 두고 색상만 분기 가능하게 설계
- **Suggested MaxParticles**: 80
- **sound_cue_hint**: compressed warp pop
