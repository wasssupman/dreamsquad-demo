using UnityEngine;
using Wassup.Data;

namespace Wassup.Rendering
{
    // battle-content-finish unit 4 — 런타임 머티리얼은 **SO 참조(`RuntimeMaterialSet`)의 복제**다. 옛 `Resources.Load` 경로와
    // `Shader.Find` 폴백 사슬은 없다 — 전자는 somnia 가 `Resources` 폴더명을 거절하고, 후자는 모바일 셰이더 스트리핑에서
    // 에디터에선 멀쩡하다가 빌드에서 null 이 된다(CLAUDE.md 「Unity 함정」).
    //
    // 묶음은 `BattleDriver.Awake` 가 `BattleContent.runtimeMaterials` 로 꽂는다(드라이버 실행 순서가 앞이라 다른 컴포넌트의
    // `OnEnable` 보다 먼저다). 안 꽂혔거나 슬롯이 비면 null 을 돌려주고 **한 번 크게** 말한다 — 호출자는 null 을 「없음」으로
    // 다룬다(마젠타가 보이면 그것이 신호다).
    public static class RuntimeMaterialFactory
    {
        private static RuntimeMaterialSet _set;
        // 슬롯마다 한 번만 말한다 — 래치가 하나면 첫 결측 뒤의 다른 슬롯 결측이 영원히 조용하다(리뷰).
        private static readonly System.Collections.Generic.HashSet<string> _reported = new System.Collections.Generic.HashSet<string>();

        /// <summary>묶음을 꽂는다. null 이면 이후 생성이 전부 null + 슬롯마다 에러 1회.</summary>
        public static void Configure(RuntimeMaterialSet set)
        {
            _set = set;
            _reported.Clear();
        }

        public static bool IsConfigured => _set != null;

        public static Material CreateOpaque(Color color)
            => Tint(Clone(_set != null ? _set.solidOpaque : null, nameof(RuntimeMaterialSet.solidOpaque)), color);

        public static Material CreateTransparent(Color color)
            => Tint(Clone(_set != null ? _set.solidTransparent : null, nameof(RuntimeMaterialSet.solidTransparent)), color);

        /// <summary>텍스처 불투명(`Tile_Unlit`). 쿼드 유닛 뷰가 컷아웃 · 양면을 얹는다.</summary>
        public static Material CreateOpaqueTexture(Texture texture, Color color)
        {
            var material = Tint(Clone(_set != null ? _set.texturedOpaque : null, nameof(RuntimeMaterialSet.texturedOpaque)), color);
            if (material == null) return null;
            if (texture != null)
            {
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            }
            return material;
        }

        /// <summary>보드 오버레이 한 인스턴스(색은 렌더러 · `MaterialPropertyBlock` 이 민다).</summary>
        public static Material CreateBoardOverlay()
            => Clone(_set != null ? _set.boardOverlay : null, nameof(RuntimeMaterialSet.boardOverlay));

        /// <summary>카드면 구김의 **인스턴스**(카드마다 `_Unfold` 가 다르다). 없으면 null — 호출부는 기본 UI 머티리얼로(구김만 없다). 그래서 이 슬롯만 선택(경고).</summary>
        public static Material CreateCardCrumpleUi()
        {
            var material = Clone(_set != null ? _set.cardCrumpleUi : null, nameof(RuntimeMaterialSet.cardCrumpleUi), optional: true);
            if (material == null) return null;
            material.name = "CardCrumpleInst";
            material.hideFlags = HideFlags.HideAndDontSave;
            return material;
        }

        /// <summary>길막 플레이스홀더 파티클(URP Particles/Unlit).</summary>
        public static Material CreateHazardParticle(Color color)
            => Tint(Clone(_set != null ? _set.hazardParticle : null, nameof(RuntimeMaterialSet.hazardParticle)), color);

        public static void ApplyColor(Material material, Color color)
        {
            if (material == null) return;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            material.color = color;
        }

        private static Material Tint(Material material, Color color)
        {
            ApplyColor(material, color);
            return material;
        }

        private static Material Clone(Material source, string slot, bool optional = false)
        {
            if (source != null) return new Material(source);
            if (_reported.Add(_set == null ? "(set)" : slot))
            {
                string msg = _set == null
                    ? "[RuntimeMaterialFactory] 머티리얼 묶음이 안 꽂혔다 — BattleDriver 의 BattleContent.runtimeMaterials 를 확인하라."
                    : $"[RuntimeMaterialFactory] RuntimeMaterialSet 의 '{slot}' 슬롯이 비었다 — "
                      + (optional ? "그 연출만 빠진다." : "그 머티리얼을 쓰는 것이 마젠타로 그려진다.");
                if (optional && _set != null) Debug.LogWarning(msg); else Debug.LogError(msg);
            }
            return null;
        }
    }
}
