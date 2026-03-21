using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Shared.Ui
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public sealed class RoundedRectGraphic : MonoBehaviour, IMaterialModifier
    {
        private const string DefaultMaterialResource = "RoundedRectMaterial";

        private static readonly int WidthHeightRadiusId = Shader.PropertyToID("_WidthHeightRadius");
        private static readonly int OuterUvId = Shader.PropertyToID("_OuterUV");
        private static readonly int BorderThicknessId = Shader.PropertyToID("_BorderThickness");

        [SerializeField]
        private Material _templateMaterial;

        [SerializeField]
        [Min(0f)]
        private float _cornerRadius = 24f;

        [SerializeField]
        [Min(0f)]
        private float _borderThickness;

        private Graphic _graphic;
        private Material _runtimeMaterial;
        private Material _lastBaseMaterial;
        private bool _hasLoggedMissingMaterial;

        private void Awake()
        {
            _graphic = GetComponent<Graphic>();
            LoadDefaultMaterialIfNeeded();
        }

        private void OnEnable()
        {
            RefreshMaterial();
        }

        private void OnDisable()
        {
            ReleaseRuntimeMaterial();
        }

        private void OnRectTransformDimensionsChange()
        {
            RefreshMaterial();
        }

        private void OnDidApplyAnimationProperties()
        {
            RefreshMaterial();
        }

        private void OnValidate()
        {
            if (_graphic == null)
                _graphic = GetComponent<Graphic>();
            LoadDefaultMaterialIfNeeded();
            RefreshMaterial();
        }

        private void LoadDefaultMaterialIfNeeded()
        {
            if (_templateMaterial != null) return;
            _templateMaterial = Resources.Load<Material>(DefaultMaterialResource);
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (_graphic == null)
                return baseMaterial;

            if (!HasRoundedRectProperties(baseMaterial))
            {
                if (!_hasLoggedMissingMaterial)
                {
                    Debug.LogError(
                        "[RoundedRectGraphic] Assign a material that uses the RoundedCorners shader.",
                        this
                    );
                    _hasLoggedMissingMaterial = true;
                }

                ReleaseRuntimeMaterial();
                return baseMaterial;
            }

            _hasLoggedMissingMaterial = false;
            EnsureRuntimeMaterial(baseMaterial);
            ApplyRoundedRectProperties(_runtimeMaterial);
            return _runtimeMaterial;
        }

        private void RefreshMaterial()
        {
            if (_graphic == null)
                return;

            if (_templateMaterial != null && _graphic.material != _templateMaterial)
                _graphic.material = _templateMaterial;

            _graphic.SetMaterialDirty();
        }

        private void EnsureRuntimeMaterial(Material sourceMaterial)
        {
            if (_runtimeMaterial != null && _lastBaseMaterial == sourceMaterial)
            {
                return;
            }

            ReleaseRuntimeMaterial();

            // Keep a per-graphic instance so size/radius updates do not leak into other UI elements.
            _runtimeMaterial = new Material(sourceMaterial)
            {
                name = sourceMaterial.name + " (RoundedRect)",
                hideFlags = HideFlags.HideAndDontSave,
            };

            _lastBaseMaterial = sourceMaterial;
        }

        private void ApplyRoundedRectProperties(Material material)
        {
            if (material == null || _graphic == null)
            {
                return;
            }

            var rect = _graphic.rectTransform.rect;
            var width = Mathf.Max(0f, rect.width);
            var height = Mathf.Max(0f, rect.height);
            var minDimension = Mathf.Min(width, height);

            var radius = Mathf.Min(_cornerRadius, minDimension);
            var maxBorderThickness = Mathf.Max(0f, minDimension * 0.5f - 0.001f);
            var borderThickness = Mathf.Min(_borderThickness, maxBorderThickness);

            material.SetVector(WidthHeightRadiusId, new Vector4(width, height, radius, 0f));
            material.SetVector(OuterUvId, ResolveOuterUv());
            material.SetFloat(BorderThicknessId, borderThickness);
        }

        private Vector4 ResolveOuterUv()
        {
            if (_graphic is Image image && image.sprite != null)
            {
                return DataUtility.GetOuterUV(image.sprite);
            }

            return new Vector4(0f, 0f, 1f, 1f);
        }

        private static bool HasRoundedRectProperties(Material material)
        {
            return material != null
                && material.HasProperty(WidthHeightRadiusId)
                && material.HasProperty(OuterUvId)
                && material.HasProperty(BorderThicknessId);
        }

        private void ReleaseRuntimeMaterial()
        {
            if (_runtimeMaterial == null)
            {
                _lastBaseMaterial = null;
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_runtimeMaterial);
            }
            else
            {
                DestroyImmediate(_runtimeMaterial);
            }

            _runtimeMaterial = null;
            _lastBaseMaterial = null;
        }
    }
}
