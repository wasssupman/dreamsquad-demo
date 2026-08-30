using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Wassup.Bridge;

namespace Wassup.Presentation
{
    // 그림자 하이브리드 실험대 (CharacterTest). 자식 SpriteRenderer 들이
    //   (1) 맵 바닥에 진짜 실루엣 그림자를 드리우고,
    //   (2) 게임이 시작되면 발밑 블롭 그림자까지 함께 갖는다.
    // 게임 유닛 경로(SpineUnitView/QuadUnitView)는 BattleBridge.UseRealShadows 로 둘 중
    // 하나만 쓴다 — 여기는 둘을 겹쳤을 때의 룩을 보려는 테스트라 일부러 상호배타를 깬다.
    //
    // 두 토글은 **Play 중 인스펙터에서 실시간으로** 켜고 끌 수 있다. 진짜/블롭/둘 다/둘 다 없음을
    // 판을 다시 돌리지 않고 갈아 끼우며 비교하는 게 이 컴포넌트의 목적이다.
    //
    // 진짜 그림자가 나오려면 렌더러 쪽 조건이 둘 다 필요하다:
    //   - 셰이더에 ShadowCaster 패스 (URP Sprite-Unlit-Default 에는 없다 → shadowCasterMaterial)
    //   - 바닥이 그림자를 받는 셰이더 (씬의 Ground Tilemap = Wassup/Tile_ShadowReceive)
    [DisallowMultipleComponent]
    public class IngameCharacterTest : MonoBehaviour
    {
        [Header("진짜 그림자 (실시간 토글)")]
        [Tooltip("Play 중에도 즉시 반영된다. 끄면 원래 머티리얼로 되돌아간다.")]
        [SerializeField] private bool castRealShadow = true;

        [Tooltip("ShadowCaster 패스를 가진 스프라이트 머티리얼 (Wassup/Sprite_ShadowCaster). " +
                 "URP 기본 Sprite-Unlit-Default 에는 패스가 없어 그림자가 나오지 않는다. " +
                 "비우면 머티리얼은 그대로 두고 캐스팅 모드만 켠다.")]
        [SerializeField] private Material shadowCasterMaterial;

        [Header("블롭 그림자 (실시간 토글)")]
        [Tooltip("Play 중에도 즉시 반영된다. 크기/색/바닥 높이는 BattleBridge 전역값.")]
        [SerializeField] private bool attachBlobShadow = true;

        [Tooltip("BattleBridge 가 블롭 전역값을 게시(맵 빌드)할 때까지 기다리는 최대 시간(초).")]
        [SerializeField] private float blobPublishTimeout = 30f;

        private SpriteRenderer[] _sprites;
        private Material[] _originalMaterials;
        private readonly List<BlobShadow> _blobs = new List<BlobShadow>();

        // 지금 씬에 실제로 적용돼 있는 상태. 인스펙터 값과 어긋나면 Update 가 따라잡는다.
        private bool _appliedCast;
        private bool _blobsAttached;
        private bool _blobRequested;
        private float _blobWaitStart;
        private bool _blobWarned;

        private void Awake()
        {
            // 블롭은 자식으로 생기므로 여기서 한 번 잡아 캐시한다 — 이후 다시 훑으면
            // 블롭 자신의 SpriteRenderer 까지 캐스터로 만들어 버린다.
            // **블롭 계열은 명시적으로 걸러낸다.** 캐시 한 번으로는 부족했다 — 한 번 씬에 저장된
            // 블롭(Play 상태가 씬에 구워진 사고)이 있으면 다음 Awake 가 그걸 캐릭터로 오인해
            // 블롭이 그림자를 드리우고, 그 블롭에 또 블롭을 붙인다.
            var found = GetComponentsInChildren<SpriteRenderer>(true);
            var keep = new List<SpriteRenderer>(found.Length);
            for (int i = 0; i < found.Length; i++)
                if (found[i] != null && found[i].GetComponentInParent<BlobShadow>(true) == null)
                    keep.Add(found[i]);
            _sprites = keep.ToArray();
            if (_sprites.Length == 0)
            {
                Debug.LogWarning($"[IngameCharacterTest] {name} 아래에 SpriteRenderer 가 없다.", this);
                return;
            }

            _originalMaterials = new Material[_sprites.Length];
            for (int i = 0; i < _sprites.Length; i++)
                _originalMaterials[i] = _sprites[i] != null ? _sprites[i].sharedMaterial : null;

            ApplyCasters(); // 첫 Update 를 기다리지 않고 스폰 시점에 맞춘다

            _blobRequested = attachBlobShadow;
            _blobWaitStart = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            if (_sprites == null || _sprites.Length == 0) return;

            if (_appliedCast != castRealShadow) ApplyCasters();

            if (_blobRequested != attachBlobShadow)
            {
                _blobRequested = attachBlobShadow;
                _blobWaitStart = Time.realtimeSinceStartup;
                _blobWarned = false;
                if (!attachBlobShadow) DetachBlobs();
            }
            if (attachBlobShadow && !_blobsAttached) TryAttachBlobs();
        }

        // 실루엣 그림자를 드리우려면 머티리얼(ShadowCaster 패스)과 캐스팅 모드가 둘 다 필요하다.
        // 평면이라 뒤를 보이는 프레임에도 그림자가 유지되도록 TwoSided.
        private void ApplyCasters()
        {
            _appliedCast = castRealShadow;

            if (castRealShadow && shadowCasterMaterial == null)
                Debug.LogWarning("[IngameCharacterTest] shadowCasterMaterial 이 비어 있다. " +
                                 "Sprite-Unlit-Default 에는 ShadowCaster 패스가 없어 그림자가 나오지 않는다.", this);

            var mode = castRealShadow ? ShadowCastingMode.TwoSided : ShadowCastingMode.Off;
            for (int i = 0; i < _sprites.Length; i++)
            {
                var sr = _sprites[i];
                if (sr == null) continue;
                if (shadowCasterMaterial != null)
                    // 스프라이트 텍스처는 렌더러가 _MainTex 로 주입 — 캐스터 머티리얼 하나를 넷이 공유한다.
                    sr.sharedMaterial = castRealShadow ? shadowCasterMaterial : _originalMaterials[i];
                sr.shadowCastingMode = mode;
                sr.receiveShadows = false; // unlit 스프라이트 — 받아도 앞면에 반영되지 않는다
            }
        }

        // 블롭 전역값(sprite/size/color/groundY)은 BattleBridge 가 맵을 빌드할 때 게시한다.
        // 그 전에 붙이면 sprite 가 null 이라 보이지 않으므로 게시될 때까지 매 프레임 재시도한다.
        private void TryAttachBlobs()
        {
            if (BattleBridge.BlobShadowSprite == null)
            {
                if (!_blobWarned && Time.realtimeSinceStartup - _blobWaitStart > blobPublishTimeout)
                {
                    _blobWarned = true; // 경고는 한 번만 — 재시도 자체는 계속한다
                    Debug.LogWarning("[IngameCharacterTest] BattleBridge 블롭 전역값이 아직 게시되지 않았다 " +
                                     "(맵 빌드가 일어났는지 확인). 게시되면 자동으로 붙는다.", this);
                }
                return;
            }

            // 유닛과 같은 블롭 경로. live=true 로 스프라이트를 옮기면 그림자도 따라온다.
            for (int i = 0; i < _sprites.Length; i++)
            {
                var sr = _sprites[i];
                if (sr == null) continue;
                _blobs.Add(BlobShadow.Attach(sr.transform,
                    BattleBridge.BlobShadowSprite,
                    BattleBridge.BlobShadowSize,
                    BattleBridge.BlobShadowColor,
                    BattleBridge.BlobShadowLift,
                    BoardSortOrder.ShadowOrder,
                    live: true));
            }
            _blobsAttached = true;
        }

        private void DetachBlobs()
        {
            for (int i = 0; i < _blobs.Count; i++)
                if (_blobs[i] != null) Destroy(_blobs[i].gameObject);
            _blobs.Clear();
            _blobsAttached = false;
        }
    }
}
