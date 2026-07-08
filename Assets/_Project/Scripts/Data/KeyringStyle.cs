using UnityEngine;

namespace Wassup.Data
{
    // keyring-unify 1 — 키링 스타일(스프라이트/머티리얼) 단일 소스. 인게임(월드)·아웃게임(UGUI) 공용.
    // 2단 폴백: settings 의 style == null → 전체 절차적, style 내 개별 슬롯 null → 해당 요소만 폴백.
    // 팔레트는 머티리얼 소유 — 팔레트 변경 = UI/월드 머티리얼 2곳 (UGUI 는 MaterialPropertyBlock
    // 미지원이라 SO 팔레트 런타임 주입은 clone 수명 관리 비용이 더 크다).
    [CreateAssetMenu(menuName = "Wassup/Keyring Style", fileName = "KeyringStyle")]
    public class KeyringStyle : ScriptableObject
    {
        [Header("공용 스프라이트")]
        [Tooltip("고리 스프라이트. 비우면 절차적 폴백(아웃게임 annulus / 인게임 원 루프).")]
        public Sprite ringSprite;
        [Tooltip("줄 스프라이트 — UGUI 전용(세로 스트레치 Image). 월드 줄 텍스처는 worldCordMaterial 이 직접 보유(비대칭 주의).")]
        public Sprite cordSprite;

        [Header("UGUI 머티리얼 (아웃게임)")]
        [Tooltip("줄 머티리얼(샤인/홀로 UI 셰이더). 비우면 기본 UI 머티리얼.")]
        public Material uiCordMaterial;
        [Tooltip("고리 머티리얼(발광 UI 셰이더). 비우면 기본 UI 머티리얼.")]
        public Material uiRingMaterial;

        [Header("월드 머티리얼 (인게임) — unit 2 에서 채움")]
        [Tooltip("줄 머티리얼(월드 홀로 셰이더, LineRenderer). 비우면 절차적 단색 줄.")]
        public Material worldCordMaterial;
        [Tooltip("고리 머티리얼(월드 홀로 셰이더, SpriteRenderer). 비우면 절차적 원 루프 고리.")]
        public Material worldRingMaterial;
    }
}
