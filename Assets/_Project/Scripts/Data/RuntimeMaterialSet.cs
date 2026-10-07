using UnityEngine;

namespace Wassup.Data
{
    // battle-content-finish unit 4 — **런타임이 복제해 쓰는 머티리얼의 원본 묶음.** 옛 `Assets/Resources/RuntimeMaterials/*`
    // (경로 로드)와 `Shader.Find` 폴백 사슬의 자리다. 에셋 참조라 빌드에 반드시 들어가고(스트리핑 없음), 어느 슬롯이
    // 비면 `RuntimeMaterialFactory` 가 **한 번 크게** 말한다 — 조용한 폴백은 없다.
    //
    // 슬롯은 셰이더 하나당 하나다. 색은 소비자가 `RuntimeMaterialFactory.ApplyColor` 로 인스턴스에 민다.
    [CreateAssetMenu(menuName = "Wassup/Runtime Material Set", fileName = "RuntimeMaterialSet")]
    public sealed class RuntimeMaterialSet : ScriptableObject
    {
        [Tooltip("단색 불투명(`Wassup/Solid_Unlit`). 픽업 · 사직서 · 퇴근 비행 종이.")]
        public Material solidOpaque;
        [Tooltip("단색 반투명(`Wassup/Solid_Transparent`). 배치 링 · 표식 링 · 코드 폴백.")]
        public Material solidTransparent;
        [Tooltip("텍스처 불투명(`Wassup/Tile_Unlit`). 쿼드 유닛 뷰(알파 컷아웃 · 양면).")]
        public Material texturedOpaque;
        [Tooltip("보드 오버레이(`Wassup/BoardOverlay_Unlit`). 격자 · 사거리 링 · 예고선 · 배치 가이드.")]
        public Material boardOverlay;
        [Tooltip("손패 카드면 구김(`Wassup/UI/CardCrumple`). 카드마다 인스턴스.")]
        public Material cardCrumpleUi;
        [Tooltip("길막 플레이스홀더 파티클(URP Particles/Unlit).")]
        public Material hazardParticle;
    }
}
