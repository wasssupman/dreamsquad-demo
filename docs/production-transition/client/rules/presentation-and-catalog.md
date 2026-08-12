# Client Rules — Presentation and Catalog

## `PT-CLI-010` — Presentation은 비권위

- **책임 owner:** Client presentation.
- **Invariant:** UI, tween, coroutine, animation, VFX, SFX, camera, haptics와 pool callback은 gameplay truth를 바꾸지 않는다.
- **허용:** Authoritative fact 하나를 여러 감각 cue와 frame-rate-independent 연출로 표현한다.
- **금지:** 연출 완료를 attack/damage/death/despawn/score/terminal 진행 조건으로 사용한다.
- **Semantic input/outcome:** Projection state + cue policy → local presentation only.
- **Production 제약:** PrimeTween 등 presentation dependency는 Client dependency policy를 따른다.
- **미결 decision:** 없음.
- **Demo source pointer:** Demo의 sim→Bridge→Presentation 관찰 결과를 freeze 때 reconcile.

## `PT-CLI-011` — Stable content ID와 catalog

- **책임 owner:** Client presentation catalog; gameplay identity는 Server/common owner.
- **Invariant:** Stable gameplay/content ID를 Addressables-compatible local asset/catalog entry에 매핑한다.
- **허용:** Asset 누락·version mismatch를 진단 가능한 fallback으로 표현한다.
- **금지:** Prefab GUID, resource path, Unity instance 또는 display string을 gameplay identity로 사용한다.
- **Semantic input/outcome:** Stable content ID + catalog version → view/prefab/animation/VFX/SFX/localization.
- **Production 제약:** Addressables와 asset provenance는 Somnia Client 정본을 따른다.
- **미결 decision:** `PT-DEC-COMMON-001`.
- **Demo source pointer:** Demo ScriptableObject/Prefab은 모습 참고이지 production catalog 정본이 아니다.

## `PT-CLI-012` — Cue policy

- **책임 owner:** Client presentation.
- **Invariant:** Cue exactly-once 여부, replayability와 correction 처리 정책을 cue kind별로 명시한다.
- **허용:** State-derived loop, event-derived one-shot과 cosmetic-only randomness를 분리한다.
- **금지:** Cosmetic RNG가 target, damage, event count, score 또는 command eligibility에 영향.
- **Semantic input/outcome:** Event ID/kind, state transition과 playback context → cue decision.
- **Production 제약:** Mobile performance와 accessibility acceptance를 함께 적용한다.
- **미결 decision:** `PT-DEC-CLIENT-001`.
- **Demo source pointer:** Demo의 VFX/SFX/UI/camera/haptics surface는 experience map에서 범위화.
