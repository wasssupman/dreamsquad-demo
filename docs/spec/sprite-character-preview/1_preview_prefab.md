# 1 · 인스펙터 상태 버튼 + 템플릿 프리팹

## 목적

사용자가 **맵에 올려놓고 상태를 바꿔가며 눈으로 확인**할 수 있게 만든다.
unit 0 의 컴포넌트만으로는 상태를 전환할 방법이 없어서 사실상 idle 확인용에 그친다.

## 변경 대상

- `Assets/_Project/Editor/FlipbookCharacterViewEditor.cs` (신규)
- `Assets/_Project/Prefabs/Characters/SpriteCharacter.prefab` (신규)

## 구현

### 인스펙터 상태 버튼

기본 인스펙터 아래에 상태 5개 버튼. **Play 중에만 활성**한다 —
에디트 모드에서 누르면 `SpriteFlipbookPlayer.Play(data)` 가 직렬화 필드
`flipbook` 에 써서 컴포넌트를 dirty 하게 만든다(재생기의 기존 동작).
확인용 도구가 씬/프리팹을 조용히 오염시켜선 안 된다.

에디트 모드에서는 버튼 대신 "Play 중에만 전환 가능" 안내를 띄운다.

현재 상태와 `IsPlaying` 도 같이 표시한다 — 원샷이 갇혔는지(README 함정)를
버튼을 눌러보는 것만으로 바로 알 수 있어야 한다.

### 템플릿 프리팹

```
SpriteCharacter
├─ SpriteRenderer          정렬은 여기서 authored (뷰는 안 건드린다)
├─ SpriteFlipbookPlayer    playOnEnable = false
│                          disableRendererWhenFinished = false  ← Death 마지막 프레임 유지
├─ FlipbookCharacterView   5슬롯 비어 있음 (사용자가 채운다)
└─ Billboard               Tilted / 45°
```

**`Billboard` 는 이미 프리팹 오소링을 지원한다** (`mode`/`tiltAngle` 이 `SerializeField`).
새 코드 없이 컴포넌트만 얹으면 된다. 없으면 스프라이트가 월드 XY 평면에 눕는다 —
맵이 틸트된 보드라 그대로는 쓸 수 없다.

`BlobShadow` 는 넣지 않는다. `authoredInPrefab` 로 프리팹 오소링을 지원하지만,
접지 그림자는 캐릭터 실루엣이 정해진 뒤에 맞추는 게 순서다. 필요하면 사용자가 얹는다.

사용자는 이 프리팹의 **배리언트**를 캐릭터마다 만들어 슬롯을 채운다.

`transform.localScale` 은 1 로 둔다. Spine 유닛은 `BattleBridge.CharacterVisualScale`(0.42) 을
곱해 쓰지만, 여기는 시트의 PPU 와 캐릭터 크기가 정해지기 전이라 미리 맞출 근거가 없다.
시트를 넣고 눈으로 보면서 잡는다.

## 완료 기준

- compile clean.
- 프리팹이 씬에 배치되고, Play 중 인스펙터 버튼으로 5개 상태가 전환된다.
- 에디트 모드에서 버튼이 비활성이고 안내 문구가 보인다.
- **맵 위에서 캐릭터가 눕지 않고 서 있다** (`Billboard` 틸트 확인).
- `Attack` 을 누르면 재생 후 자동으로 `Idle` 로 돌아온다.
- `Death` 를 누르면 마지막 프레임에서 멈추고 GameObject 가 살아 있다.

**시각 검증은 사용자가 시트를 넣고 신호할 때 수행한다** (README 계약).
그전까지는 슬롯이 빈 상태로 compile/배치/버튼 동작까지만 확인한다.
