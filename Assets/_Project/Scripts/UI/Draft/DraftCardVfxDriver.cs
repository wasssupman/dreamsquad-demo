using PrimeTween;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;

namespace Wassup.UI.Draft
{
    public class DraftCardVfxDriver : MonoBehaviour
    {
        private Tween _borderTween;
        private Tween _bannerTween;
        private readonly List<UiEmber> _uiEmbers = new();
        private RectTransform _uiEmberRoot;
        private Material _foilMaterial;
        private Image _foilImage;
        private float _foilBaseIntensity;

        public void Configure(
            DefenderRarity rarity,
            Image borderImage,
            Image bannerImage,
            ParticleSystem epicParticlePrefab,
            ParticleSystem egoParticlePrefab)
        {
            if (borderImage == null) return;

            Color borderBase = borderImage.color;
            Color borderBright = Color.Lerp(borderBase, Color.white, 0.35f);
            float halfCycle = rarity switch
            {
                DefenderRarity.Common => 1.5f,
                DefenderRarity.Rare => 1.0f,
                DefenderRarity.Epic => 0.6f,
                DefenderRarity.Ego => 0.45f,
                _ => 1.5f,
            };

            _borderTween = Tween.Color(
                borderImage,
                borderBright,
                halfCycle,
                Ease.InOutSine,
                cycles: -1,
                cycleMode: CycleMode.Yoyo);

            SpawnFoilOverlay(rarity);

            if (rarity == DefenderRarity.Epic)
            {
                SpawnVfx(epicParticlePrefab, new Color(1f, 0.55f, 0.26f, 1f), 8);
            }
            else if (rarity == DefenderRarity.Ego)
            {
                SpawnVfx(egoParticlePrefab, new Color(0.8f, 0.27f, 1f, 1f), 15);
                if (bannerImage != null)
                {
                    Color bannerBase = bannerImage.color;
                    Color bannerBright = Color.Lerp(bannerBase, Color.white, 0.4f);
                    _bannerTween = Tween.Color(
                        bannerImage,
                        bannerBright,
                        halfCycle,
                        Ease.InOutSine,
                        cycles: -1,
                        cycleMode: CycleMode.Yoyo);
                }
            }
        }

        private void SpawnFoilOverlay(DefenderRarity rarity)
        {
            float intensity = rarity switch
            {
                DefenderRarity.Common => 0.08f,
                DefenderRarity.Rare => 0.22f,
                DefenderRarity.Epic => 0.48f,
                DefenderRarity.Ego => 0.72f,
                _ => 0.12f,
            };

            if (intensity <= 0f) return;

            var shader = Shader.Find("Wassup/UI/DraftCardFoil");
            if (shader == null) return;

            var overlayGo = new GameObject("FoilOverlay", typeof(RectTransform), typeof(Image));
            overlayGo.transform.SetParent(transform, false);
            var rt = (RectTransform)overlayGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6f, 6f);
            rt.offsetMax = new Vector2(-6f, -6f);
            overlayGo.transform.SetAsLastSibling();

            _foilMaterial = new Material(shader)
            {
                name = $"DraftCardFoil_{rarity}",
                hideFlags = HideFlags.HideAndDontSave,
            };
            _foilBaseIntensity = intensity;
            _foilMaterial.SetFloat("_Intensity", intensity);
            _foilMaterial.SetFloat("_Speed", rarity == DefenderRarity.Ego ? 1.35f : 0.9f);
            _foilMaterial.SetFloat("_HueShift", rarity switch
            {
                DefenderRarity.Rare => 0.58f,
                DefenderRarity.Epic => 0.08f,
                DefenderRarity.Ego => 0.78f,
                _ => 0.0f,
            });

            _foilImage = overlayGo.GetComponent<Image>();
            _foilImage.color = Color.white;
            _foilImage.material = _foilMaterial;
            _foilImage.raycastTarget = false;
        }

        private void SpawnVfx(ParticleSystem prefab, Color color, int emberCount)
        {
            if (prefab != null)
            {
                var particles = Instantiate(prefab, transform);
                particles.transform.localPosition = Vector3.zero;
                particles.Play();
            }

            SpawnUiEmbers(color, emberCount);
        }

        private void SpawnUiEmbers(Color color, int emberCount)
        {
            if (_uiEmberRoot == null)
            {
                var rootGo = new GameObject("UiEmberParticles", typeof(RectTransform));
                rootGo.transform.SetParent(transform, false);
                _uiEmberRoot = (RectTransform)rootGo.transform;
                _uiEmberRoot.anchorMin = Vector2.zero;
                _uiEmberRoot.anchorMax = Vector2.one;
                _uiEmberRoot.offsetMin = Vector2.zero;
                _uiEmberRoot.offsetMax = Vector2.zero;
                _uiEmberRoot.SetAsLastSibling();
            }

            var rect = (RectTransform)transform;
            float width = Mathf.Max(rect.rect.width, 240f);
            float height = Mathf.Max(rect.rect.height, 340f);
            for (int i = 0; i < emberCount; i++)
            {
                var emberGo = new GameObject($"Ember_{i:00}", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                emberGo.transform.SetParent(_uiEmberRoot, false);
                var emberRt = (RectTransform)emberGo.transform;
                emberRt.anchorMin = new Vector2(0.5f, 0f);
                emberRt.anchorMax = new Vector2(0.5f, 0f);
                emberRt.pivot = new Vector2(0.5f, 0.5f);

                float size = Random.Range(5f, 10f);
                emberRt.sizeDelta = new Vector2(size, size);

                var image = emberGo.GetComponent<Image>();
                image.color = color;
                image.raycastTarget = false;

                var group = emberGo.GetComponent<CanvasGroup>();
                group.alpha = 0f;

                var start = new Vector2(Random.Range(-width * 0.38f, width * 0.38f), Random.Range(16f, 56f));
                var end = start + new Vector2(Random.Range(-30f, 30f), Random.Range(height * 0.35f, height * 0.65f));
                _uiEmbers.Add(new UiEmber
                {
                    rect = emberRt,
                    group = group,
                    start = start,
                    end = end,
                    phase = Random.value,
                    lifetime = Random.Range(1.1f, 1.8f),
                    scale = Random.Range(0.75f, 1.35f),
                });
            }
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            UpdateFoil(now);

            for (int i = 0; i < _uiEmbers.Count; i++)
            {
                var ember = _uiEmbers[i];
                if (ember.rect == null || ember.group == null) continue;

                float t = Mathf.Repeat(now / ember.lifetime + ember.phase, 1f);
                ember.rect.anchoredPosition = Vector2.LerpUnclamped(ember.start, ember.end, t);
                float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.18f, t));
                float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1f, t));
                ember.group.alpha = 0.85f * fadeIn * fadeOut;
                float scale = ember.scale * Mathf.Lerp(0.8f, 0.35f, t);
                ember.rect.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void UpdateFoil(float now)
        {
            if (_foilMaterial == null || _foilImage == null) return;

            float z = transform.localEulerAngles.z;
            if (z > 180f) z -= 360f;
            float tilt = Mathf.Clamp(z / 28f, -1f, 1f);
            float shimmer = 0.82f + 0.18f * Mathf.Sin(now * 2.1f + transform.GetSiblingIndex() * 0.73f);

            _foilMaterial.SetFloat("_Tilt", tilt);
            _foilMaterial.SetFloat("_Intensity", _foilBaseIntensity * shimmer);
        }

        private void OnDisable()
        {
            _borderTween.Stop();
            _bannerTween.Stop();
        }

        private void OnDestroy()
        {
            _borderTween.Stop();
            _bannerTween.Stop();
            if (_foilMaterial != null) Destroy(_foilMaterial);
        }

        private struct UiEmber
        {
            public RectTransform rect;
            public CanvasGroup group;
            public Vector2 start;
            public Vector2 end;
            public float phase;
            public float lifetime;
            public float scale;
        }
    }
}
