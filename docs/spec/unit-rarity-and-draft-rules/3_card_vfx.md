# 3 — Card VFX: DraftCardVfxDriver

## 목적

카드 등급별 VFX를 담당하는 `DraftCardVfxDriver` MonoBehaviour를 추가한다.  
Common/Rare = PrimeTween 테두리 pulse, Epic = pulse + 파티클, Ego = pulse + 파티클 + 배너 shimmer.

## 변경 대상

- 신규: `Assets/_Project/Scripts/UI/Draft/DraftCardVfxDriver.cs`
- 수정: `Assets/_Project/Scripts/UI/Draft/DraftCardFanView.cs` (prefab 참조 + AddComponent)

## 구현

### DraftCardVfxDriver.cs

```csharp
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;

namespace Wassup.UI.Draft
{
    // Attached to draft card GO by DraftCardFanView.CreateCard().
    // Reads DefenderRarity and drives:
    //   Common/Rare — PrimeTween border color pulse
    //   Epic        — pulse + particle child
    //   Ego         — pulse + particle + banner shimmer
    public class DraftCardVfxDriver : MonoBehaviour
    {
        // borderTween / bannerTween stored so OnDestroy can stop them individually.
        private Tween _borderTween;
        private Tween _bannerTween;

        public void Configure(
            DefenderRarity rarity,
            Image borderImage,
            Image bannerImage,
            ParticleSystem epicParticlePrefab,
            ParticleSystem egoParticlePrefab)
        {
            Color borderBase   = borderImage.color;
            Color borderBright = Color.Lerp(borderBase, Color.white, 0.35f);

            float halfCycle = rarity switch
            {
                DefenderRarity.Common => 1.5f,
                DefenderRarity.Rare   => 1.0f,
                DefenderRarity.Epic   => 0.6f,
                DefenderRarity.Ego    => 0.45f,
                _                    => 1.5f,
            };

            // Border pulse: Yoyo loop, base ↔ bright
            _borderTween = Tween.Color(borderImage, borderBright, halfCycle,
                Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo);

            // Epic: ember particle
            if (rarity == DefenderRarity.Epic && epicParticlePrefab != null)
                SpawnParticle(epicParticlePrefab);

            // Ego: ember particle + banner shimmer
            if (rarity == DefenderRarity.Ego)
            {
                if (egoParticlePrefab != null) SpawnParticle(egoParticlePrefab);
                if (bannerImage != null)
                {
                    Color bannerBase   = bannerImage.color;
                    Color bannerBright = Color.Lerp(bannerBase, Color.white, 0.4f);
                    _bannerTween = Tween.Color(bannerImage, bannerBright, halfCycle,
                        Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo);
                }
            }
        }

        private void SpawnParticle(ParticleSystem prefab)
        {
            var ps = Instantiate(prefab, transform);
            ps.transform.localPosition = Vector3.zero;
            ps.Play();
        }

        private void OnDisable() { _borderTween.Stop(); _bannerTween.Stop(); }
        private void OnDestroy() { _borderTween.Stop(); _bannerTween.Stop(); }
    }
}
```

### DraftCardFanView.cs 추가 사항

SerializeField 추가:

```csharp
[SerializeField] private ParticleSystem epicCardParticlePrefab;
[SerializeField] private ParticleSystem egoCardParticlePrefab;
```

`CreateCard()` 마지막에 driver 부착:

```csharp
// borderImage = go.GetComponent<Image>()
// bannerImage = swatch (슬롯 배너 Image)
var driver = go.AddComponent<DraftCardVfxDriver>();
driver.Configure(unit.rarity, go.GetComponent<Image>(), swatch,
                 epicCardParticlePrefab, egoCardParticlePrefab);
```

### 파티클 프리팹 사양 (VFX authoring 가이드라인)

| 속성 | Epic | Ego |
|---|---|---|
| 파티클 수/초 | 8 | 15 |
| 수명 | 0.8s | 1.0s |
| 시작 속도 | 30–60 | 40–80 |
| 렌더 모드 | Billboard | Billboard |
| 색상 | `#FF8C42` fade out | `#CC44FF` → `#FFFFFF` fade |
| Sorting Layer | UI (또는 World Space canvas) | 동일 |

파티클 프리팹은 `Assets/_Project/VFX/Prefabs/` 에 `DraftCard_Epic.prefab`, `DraftCard_Ego.prefab` 으로 저장. `DraftCardFanView` Inspector에 배선.

Epic/Ego 프리팹이 null이면 VFX 없이 pulse만 동작한다 (graceful fallback).

## 완료 기준

- [ ] 컴파일 오류 없음
- [ ] PlayMode: Common/Rare 카드 테두리가 주기적으로 pulse 동작
- [ ] PlayMode: Epic 카드에서 파티클 파편이 방출됨 (프리팹 배선 후)
- [ ] PlayMode: Ego 카드에서 파티클 + 배너 shimmer 동작
- [ ] 카드 버리기(discard) 후 driver OnDestroy 시 Tween 정리됨 (콘솔 오류 없음)
