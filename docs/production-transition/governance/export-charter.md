# Production Transition Export Charter

> **DORMANT · OWNER-GATED · NOT AN EXPORT INSTRUCTION.** Project owner의 명시적 transition 활성화 전에는 실행·검증·후속 작업으로 사용하지 않는다.

> 상태: **dormant preparation artifact — owner activation 전 미활성 · export-safe reference candidate**

이 문서는 미래 freeze snapshot의 `references/governance/transition-charter.md` 위치로
byte-copy할 수 있는 축약 계약이다. 현재 파일 자체는 official freeze, export 또는 production
승인이 아니다. Demo 준비 정본은 `docs/production-transition/README.md`이며, official
publication 때 이 문서의 review/hash도 같은 manifest에 고정한다.

## Delivery model

- Official consumer package는 `shared`, `client`, `game-server` 세 개다.
- `references`는 manifest·governance·evidence를 전달하는 closure partition이지 네 번째
  consumer package가 아니다.
- Client는 root manifest, `shared`, `client`, 적용 가능한 `references`를 받는다.
- Game Server는 같은 root manifest, byte-identical `shared`, `game-server`, 적용 가능한
  `references`를 받는다.
- 공통 의미의 snapshot 경로는 `shared/README.md`다.
- Manifest 형식의 snapshot 경로는 `references/governance/manifest.schema.json`이다.

## One-time transition

Official publication 한 번이 하나의 freeze ID, source commit과 byte set을 고정한다. Client와
Game Server import는 같은 coordinated event다. 중단된 copy는 같은 ID와 같은 bytes만
재개할 수 있다. 새 freeze, 부분 update와 두 번째 Demo import는 허용하지 않는다. Publication
이후 오류는 production errata, ADR 또는 일반 change control로 처리한다.

## Strict inclusion

포함 record는 모두 `complete + current + reviewed + ready`이고, exact area/revision/source
review와 `decided` gameplay blocker를 가져야 한다. Dependency와 local-link closure, target
containment, file SHA-256, Shared file list/bytes와 두 destination을 publication 전에 검증한다.
모든 selected source artifact는 manifest의 source commit에 tracked blob으로 존재하고 package에
사용한 bytes와 byte-identical해야 한다. Demo 전환 source subtree의 text checkout은 LF로
고정해 Windows `core.autocrlf` 설정도 이 비교나 package hash를 바꾸지 못하게 한다.
Root manifest의 file entry는 stable record ID를 포함하고, canonical
`governance_attestation`은 record gate metadata, exact review tuple과 관련 decision을 frozen
bytes로 보존한다. Source/watch provenance와 implementation wave/stage도 함께 보존하므로
Production consumer는 live Demo registry 없이 승인과 후속 실행 위치를 다시 감사할 수 있다.

## Destinations

```text
somnia-client/docs/migration-input/dreamsquad-demo/<freeze-id>/
somnia-game-server/docs/migration-input/dreamsquad-demo/<freeze-id>/
```

각 receipt는 같은 freeze ID, root manifest hash, assigned partition hashes와 Shared hash를
확인한다. 이 foundation은 wire DTO, 인증, transport 또는 production protocol을 승인하지
않으며 해당 선택은 production ADR gate에 남긴다.
