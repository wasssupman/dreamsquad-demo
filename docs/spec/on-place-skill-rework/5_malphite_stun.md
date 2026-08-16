# 5 — 말파이트: 2타일 · 3초 정지

## 목적

말파이트의 배치 스킬을 반경 2타일 · **3초** 스턴으로 올린다. 지금은 반경 1 · 0.8초인데,
**자기 평타의 넉업(`knockupOnHitSec` 0.8)과 길이가 똑같다** — 배치 스킬이 평소 공격과 구분되지
않는 가장 노골적인 사례다.

## 스코프 결정 — 레거시 경로에 남긴다

말파이트는 units 0·4 가 만든 규칙 경로로 **이관하지 않는다**(사용자 결정 2026-08-16 — 이관 범위).
이관하려면 범위 CC 페이로드(`AreaCc{DcCcKind}`)가 필요한데, 캐논이 fan-out 때문에 이미 emitter
코드 변경을 요구하므로 스코프를 더 늘리는 쪽이 위험하다. README 계약 2 는 "enum 을 **늘리지**
않는다"이지 "기존 값을 못 쓴다"가 아니다.

⚠ **대가를 알고 남긴다**: `MeleeBurst`(4)는 **Bruiser** 도 쓰고 `StunNearby`(9)는 말파이트가
쓰므로, 캐논·배스티온이 빠져도 `ApplyOnPlaceEffect` 의 arm 은 **하나도 죽지 않는다.** 즉 이번
spec 에서 「통일」은 **새 것이 규칙으로 가는 방향**으로만 증명되고 레거시 코드는 그대로다.
그래서 README 계약 2 에 **만료 조건**을 박았다 — 다음 on-place 작업이 레거시 전량을 이관한다.
그 이관 때 말파이트는 `AreaCc` 1개, Bruiser 는 기존 `SelfTileAoe`(2) 재사용이라 **신규 kind 1개**로
둘 다 옮겨지고 그때 enum 값 2개와 arm 2개가 실제로 죽는다.

## 변경 대상

- `Assets/_Project/Data/Defenders/Defender_Malphite.asset` — 값 2개
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `StunNearby` 분기의 **띄움 길이** 한 줄
- 신규 `Assets/_Project/Tests/PlayMode/OnPlaceStunNearbyTest.cs`

⚠ **기존 테스트에 케이스를 얹는 게 아니다.** `KnockupOnHitTest` 는 **평타** 넉업이고,
`StunNearby`·`MeleeBurst`·on-place push 는 **PlayMode 커버리지가 아예 없다.** units 2·4·5 가
정확히 그 셋을 바꾸므로 이 unit 의 테스트는 신규 작성이다.

## 구현

### 에셋 값

| 필드 | 현재 | 변경 |
|---|---|---|
| `onPlaceRange` | 1 | **2** |
| `onPlaceDuration` | 0.8 | **3** |

`onPlaceEffect` 는 `StunNearby`(9) 그대로 — 새 효과 타입이 필요 없다.

### 띄움 길이와 스턴 길이를 가른다

현재 `StunNearby` 분기는 뷰에 `PlayKnockupHop(onPlaceDuration, knockupVisualHeight)` 를 넘긴다.
`onPlaceDuration` 을 3 으로 올리면 **적이 3초 동안 공중에 떠 있는다** — 지진 충격으로 튀어오른
그림이 아니라 무중력이다.

심의 사실은 처음부터 「스턴」이고 「공중」은 뷰의 해석이다
(`knockup-fighter-defender` unit 3). 그러니 뷰에 넘기는 길이를 스턴 길이에서 떼어낸다:

```
float hopSec = unitData.knockupOnHitSec > 0f
    ? math.min(unitData.knockupOnHitSec, unitData.onPlaceDuration)
    : unitData.onPlaceDuration;
hopView.PlayKnockupHop(hopSec, unitData.knockupVisualHeight);
```

`knockupOnHitSec`(말파이트 0.8)은 **이 유닛이 적을 띄우는 길이**라는 하나의 성질이다 —
평타든 배치든 같은 높이·같은 체공이 자연스럽다. 필드를 새로 만들지 않는 이유이며(제약 8),
`min` 을 쓰는 이유는 스턴보다 오래 떠 있어 **땅에 닿기 전에 적이 다시 움직이는** 역전을
막기 위함이다.

결과 그림: 0.8초 튀어올랐다 착지 → 남은 2.2초는 땅에서 굳어 있음.

## 완료 기준

- [x] compile 0 error
- [x] PlayMode
  - 반경 2 안 적 전원 `CcEffect{kind=Stun}` 3초 · 반경 밖 적 무영향
  - 3초 동안 적이 **이동하지 않는다**(전후 위치 비교). 지속만 늘리고 정지가 안 되면 의미 없음
  - 3초 뒤 다시 이동
- [x] 기존 `KnockupOnHitTest` 무회귀 — 평타 넉업(0.8)은 그대로
- [ ] Play 육안: 적 무리 위에 말파이트 배치 → **튀어올랐다 떨어진 뒤 한동안 굳어 있다.**
      떠 있는 채로 3초를 버티지 않는다
