using UnityEngine;

namespace Somnia.Battle.BattleCoreUnity.View
{
    // battle-core-rebuild unit 5b — 보드 오버레이가 쓰는 머티리얼 한 자리.
    //
    // ⚠ **`AddComponent<LineRenderer>()` 는 머티리얼을 안 준다.** 5b 의 첫 Play 에서 격자와
    // 예고선이 전부 마젠타였고 원인이 그것이었다(`sharedMaterial == null`). 스프라이트 렌더러는
    // 기본을 받지만, 둘이 서로 다른 셰이더를 쓰면 같은 바닥 대역에서 색·블렌딩이 갈린다.
    //
    // battle-content-finish unit 4 — 원본은 `RuntimeMaterialSet.boardOverlay`(SO 참조)이고 복제는 `RuntimeMaterialFactory` 가 한다.
    // `Shader.Find` · `Resources` 는 없다. 색은 렌더러가 정한다 — 그래서 렌더러마다 머티리얼을 복제하지 않는다.
    // 호출자가 자기 인스턴스 하나를 들고 파괴 때 지운다. 다만 **미는 통로가 둘로 갈린다**(바로 아래 참조).
    internal static class CoreOverlayMaterial
    {
        // ⚠ **`SpriteRenderer.color` 로는 색이 안 간다.** 스프라이트 렌더러의 틴트는 정점색이
        // 아니라 per-renderer 데이터(`_RendererColor`)로 흐르고, 그건 내장 스프라이트 셰이더만
        // 읽는다 — 우리 셰이더에 붙이면 **전부 흰색**으로 그려진다(실측: 배치 가이드가 보드를
        // 통째로 하얗게 덮었다). 그래서 스프라이트의 색은 `MaterialPropertyBlock` 으로 민다.
        // `LineRenderer` 는 다르다 — start/end 색을 **정점색에 굽기** 때문에 그대로 통한다.

        /// <summary>셰이더의 틴트 프로퍼티. 스프라이트의 색은 **이것으로** 민다(위 참조).</summary>
        public static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>새 인스턴스 하나. 못 만들면 null 이고 팩토리가 한 번 경고한다(조용히 마젠타가 되지 않게).</summary>
        public static Material Create() => Somnia.Battle.Rendering.RuntimeMaterialFactory.CreateBoardOverlay();
    }
}
