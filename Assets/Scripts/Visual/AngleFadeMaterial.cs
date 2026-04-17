using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GabrielBertasso.Visual
{
    [RequireComponent(typeof(Collider))]
    public class AngleFadeMaterial : MonoBehaviour
    {
        [Header("Angle Settings")]
        [SerializeField, Range(1f, 180f), Tooltip("Horizontal half-angle in degrees. The camera must be within this angle on the horizontal plane relative to the object's forward to trigger the fade-in.")]
        private float _halfAngle = 45f;

        [Header("Fade Settings")]
        [SerializeField, Range(0.01f, 50f), Tooltip("Speed at which the alpha increases when the camera is inside the angle.")]
        private float _fadeInSpeed = 5f;

        [SerializeField, Range(0.01f, 50f), Tooltip("Speed at which the alpha decreases when the camera is outside the angle.")]
        private float _fadeOutSpeed = 5f;

        [SerializeField, Range(0f, 1f), Tooltip("Minimum alpha value when fully faded out.")]
        private float _minAlpha = 0f;

        [Header("References")]
        [SerializeField, Tooltip("The camera transform used for angle checking. If null, Camera.main will be used.")]
        private Transform _cameraTransform;

        [Header("Trigger Settings")]
        [SerializeField, Tooltip("Layer mask to filter which objects in the trigger should be affected.")]
        private LayerMask _layerMask = -1;

        [Header("Material Settings")]
        [SerializeField, Tooltip("The name of the base color property in the shader. Default is '_BaseColor' for URP shaders.")]
        private string _baseColorPropertyName = "_BaseColor";

        private static readonly Dictionary<Renderer, AngleFadeMaterial> s_rendererOwnership = new Dictionary<Renderer, AngleFadeMaterial>();
        private static readonly Dictionary<ParticleSystem, AngleFadeMaterial> s_particleOwnership = new Dictionary<ParticleSystem, AngleFadeMaterial>();
        private static readonly Dictionary<Renderer, Material[]> s_originalMaterials = new Dictionary<Renderer, Material[]>();

        private readonly List<Renderer> _trackedRenderers = new List<Renderer>();
        private readonly List<ParticleSystem> _trackedParticleSystems = new List<ParticleSystem>();
        private readonly Dictionary<Renderer, Material[]> _instanceMaterials = new Dictionary<Renderer, Material[]>();

        [ShowInInspector, ReadOnly] private float _currentAlpha = 1f;
        private int _baseColorId;
        private Collider _triggerCollider;
        
        private Collider[] _cachedCollidersInTrigger;
        private bool _cacheValid = false;
        
        private readonly List<ParticleSystem> _tempParticleSystems = new List<ParticleSystem>();
        private readonly List<Renderer> _tempRenderers = new List<Renderer>();
        private readonly List<Renderer> _tempParticleRenderers = new List<Renderer>();
        private readonly List<Material> _tempMaterials = new List<Material>();

        private static readonly int SurfaceTypeId = Shader.PropertyToID("_Surface");
        private static readonly int BlendModeId = Shader.PropertyToID("_Blend");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");

        private void Awake()
        {
            _triggerCollider = GetComponent<Collider>();

            if (_triggerCollider == null)
            {
                Debug.LogError($"[{nameof(AngleFadeMaterial)}] No Collider component found. This component requires a Collider set as trigger.", this);
                enabled = false;
                return;
            }

            if (!_triggerCollider.isTrigger)
            {
                Debug.LogWarning($"[{nameof(AngleFadeMaterial)}] Collider is not set as trigger. Setting isTrigger to true.", this);
                _triggerCollider.isTrigger = true;
            }

            if (string.IsNullOrEmpty(_baseColorPropertyName))
            {
                Debug.LogWarning($"[{nameof(AngleFadeMaterial)}] Base color property name is empty. Using default '_BaseColor'.", this);
                _baseColorPropertyName = "_BaseColor";
            }

            _baseColorId = Shader.PropertyToID(_baseColorPropertyName);
        }

        private void OnEnable()
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            CleanupAllTrackedObjects();
        }

        private void Start()
        {
            if (_cameraTransform == null)
            {
                UnityEngine.Camera mainCamera = UnityEngine.Camera.main;

                if (mainCamera != null)
                {
                    _cameraTransform = mainCamera.transform;
                }
                else
                {
                    Debug.LogWarning($"[{nameof(AngleFadeMaterial)}] No camera assigned and Camera.main is null.", this);
                    enabled = false;
                    return;
                }
            }

            ProcessGameObject(gameObject);
            DetectObjectsInTrigger();

            bool isInsideAngle = IsCameraInsideAngle();
            _currentAlpha = isInsideAngle ? _minAlpha : 1f;
        }

        private void Update()
        {
            if (_cameraTransform == null)
            {
                return;
            }

            bool isInsideAngle = IsCameraInsideAngle();

            if (isInsideAngle)
            {
                ReclaimOwnership();
            }
            else
            {
                ValidateOwnership();
            }

            if (_trackedRenderers.Count == 0 && _trackedParticleSystems.Count == 0)
            {
                return;
            }

            float targetAlpha = isInsideAngle ? _minAlpha : 1f;
            float speed = isInsideAngle ? _fadeOutSpeed : _fadeInSpeed;

            _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, speed * Time.deltaTime);
            
            ApplyAlphaToRenderers(_currentAlpha);
            ApplyStateToParticles(_currentAlpha >= 1f);
        }

        private void OnDestroy()
        {
            CleanupAllTrackedObjects();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            CleanupStaticDictionaries();
        }

        private static void CleanupStaticDictionaries()
        {
            var deadRenderers = new List<Renderer>();
            foreach (var kvp in s_rendererOwnership)
            {
                if (kvp.Key == null || kvp.Value == null)
                {
                    deadRenderers.Add(kvp.Key);
                }
            }
            for (int i = 0; i < deadRenderers.Count; i++)
            {
                var renderer = deadRenderers[i];
                s_rendererOwnership.Remove(renderer);
                s_originalMaterials.Remove(renderer);
            }

            var deadParticles = new List<ParticleSystem>();
            foreach (var kvp in s_particleOwnership)
            {
                if (kvp.Key == null || kvp.Value == null)
                {
                    deadParticles.Add(kvp.Key);
                }
            }
            for (int i = 0; i < deadParticles.Count; i++)
            {
                var ps = deadParticles[i];
                s_particleOwnership.Remove(ps);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            _cacheValid = false;
            
            if (!IsInLayerMask(other.gameObject.layer))
            {
                return;
            }

            ProcessGameObject(other.gameObject);
        }

        private void OnTriggerExit(Collider other)
        {
            _cacheValid = false;
            
            if (!IsInLayerMask(other.gameObject.layer))
            {
                return;
            }

            RemoveGameObject(other.gameObject);
        }

        private bool IsCameraInsideAngle()
        {
            Vector3 directionToCamera = _cameraTransform.position - transform.position;
            Vector3 projectedDirection = Vector3.ProjectOnPlane(directionToCamera, transform.up);

            if (projectedDirection.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            projectedDirection.Normalize();
            float angle = Vector3.Angle(transform.forward, projectedDirection);

            return angle <= _halfAngle;
        }

        private void ApplyAlphaToRenderers(float alpha)
        {
            bool shouldBeOpaque = alpha >= 1f;

            foreach (var kvp in _instanceMaterials)
            {
                Renderer renderer = kvp.Key;
                Material[] materials = kvp.Value;

                if (renderer == null || materials == null)
                {
                    continue;
                }

                if (shouldBeOpaque)
                {
                    if (s_originalMaterials.TryGetValue(renderer, out Material[] originals))
                    {
                        renderer.materials = originals;
                    }
                }
                else
                {
                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material mat = materials[i];
                        if (mat != null)
                        {
                            SetupTransparentMaterial(mat);
                            
                            if (mat.HasProperty(_baseColorId))
                            {
                                Color color = mat.GetColor(_baseColorId);
                                color.a = alpha;
                                mat.SetColor(_baseColorId, color);
                            }
                        }
                    }
                    
                    renderer.materials = materials;
                }
            }
        }

        private void ApplyStateToParticles(bool shouldPlay)
        {
            foreach (ParticleSystem ps in _trackedParticleSystems)
            {
                if (ps != null)
                {
                    if (shouldPlay)
                    {
                        if (!ps.isPlaying)
                        {
                            ps.Play();
                        }
                    }
                    else
                    {
                        if (ps.isPlaying)
                        {
                            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        }
                    }
                }
            }
        }

        private void ProcessGameObject(GameObject go)
        {
            _tempParticleSystems.Clear();
            _tempParticleRenderers.Clear();
            _tempRenderers.Clear();
            
            go.GetComponentsInChildren(_tempParticleSystems);
            go.GetComponentsInChildren(_tempRenderers);

            for (int i = 0; i < _tempParticleSystems.Count; i++)
            {
                var ps = _tempParticleSystems[i];
                if (_trackedParticleSystems.Contains(ps))
                {
                    continue;
                }

                if (TryClaimParticleSystem(ps))
                {
                    _trackedParticleSystems.Add(ps);

                    Renderer psRenderer = ps.GetComponent<Renderer>();
                    if (psRenderer != null)
                    {
                        _tempParticleRenderers.Add(psRenderer);
                    }
                }
            }

            for (int i = 0; i < _tempRenderers.Count; i++)
            {
                var renderer = _tempRenderers[i];
                if (_tempParticleRenderers.Contains(renderer) || _trackedRenderers.Contains(renderer))
                {
                    continue;
                }

                if (TryClaimRenderer(renderer))
                {
                    _trackedRenderers.Add(renderer);
                    SetupRendererMaterials(renderer);
                }
            }
        }

        private void RemoveGameObject(GameObject go)
        {
            _tempRenderers.Clear();
            _tempParticleSystems.Clear();
            
            go.GetComponentsInChildren(_tempRenderers);
            go.GetComponentsInChildren(_tempParticleSystems);

            for (int i = 0; i < _tempRenderers.Count; i++)
            {
                var renderer = _tempRenderers[i];
                if (_trackedRenderers.Contains(renderer))
                {
                    ReleaseRenderer(renderer);
                }
            }

            for (int i = 0; i < _tempParticleSystems.Count; i++)
            {
                var ps = _tempParticleSystems[i];
                if (_trackedParticleSystems.Contains(ps))
                {
                    ReleaseParticleSystem(ps);
                }
            }
        }

        private void SetupRendererMaterials(Renderer renderer)
        {
            if (renderer == null || _instanceMaterials.ContainsKey(renderer))
            {
                return;
            }

            if (!s_originalMaterials.TryGetValue(renderer, out Material[] originalMaterials))
            {
                _tempMaterials.Clear();
                renderer.GetMaterials(_tempMaterials);
                originalMaterials = new Material[_tempMaterials.Count];
                for (int i = 0; i < _tempMaterials.Count; i++)
                {
                    originalMaterials[i] = _tempMaterials[i];
                }
                s_originalMaterials[renderer] = originalMaterials;
            }

            Material[] instanceMaterials = new Material[originalMaterials.Length];
            for (int i = 0; i < originalMaterials.Length; i++)
            {
                instanceMaterials[i] = new Material(originalMaterials[i]);
            }

            _instanceMaterials[renderer] = instanceMaterials;
        }

        private void CleanupAllTrackedObjects()
        {
            for (int i = _trackedRenderers.Count - 1; i >= 0; i--)
            {
                ReleaseRenderer(_trackedRenderers[i]);
            }
            _trackedRenderers.Clear();

            for (int i = _trackedParticleSystems.Count - 1; i >= 0; i--)
            {
                ReleaseParticleSystem(_trackedParticleSystems[i]);
            }
            _trackedParticleSystems.Clear();
        }

        private void ReleaseRenderer(Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            if (s_rendererOwnership.TryGetValue(renderer, out AngleFadeMaterial owner) && owner == this)
            {
                s_rendererOwnership.Remove(renderer);
            }

            if (_instanceMaterials.TryGetValue(renderer, out Material[] materials))
            {
                if (s_originalMaterials.TryGetValue(renderer, out Material[] originals))
                {
                    renderer.materials = originals;
                }

                foreach (Material mat in materials)
                {
                    if (mat != null)
                    {
                        Destroy(mat);
                    }
                }

                _instanceMaterials.Remove(renderer);
            }

            _trackedRenderers.Remove(renderer);
        }

        private void ForceReleaseRenderer(Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            if (_instanceMaterials.TryGetValue(renderer, out Material[] materials))
            {
                if (s_originalMaterials.TryGetValue(renderer, out Material[] originals))
                {
                    renderer.materials = originals;
                }

                foreach (Material mat in materials)
                {
                    if (mat != null)
                    {
                        Destroy(mat);
                    }
                }

                _instanceMaterials.Remove(renderer);
            }

            _trackedRenderers.Remove(renderer);
            s_rendererOwnership.Remove(renderer);
        }

        private void ReleaseParticleSystem(ParticleSystem ps)
        {
            if (ps == null)
            {
                return;
            }

            if (s_particleOwnership.TryGetValue(ps, out AngleFadeMaterial owner) && owner == this)
            {
                if (ps.gameObject != null && !ps.isPlaying)
                {
                    ps.Play();
                }
                s_particleOwnership.Remove(ps);
            }

            _trackedParticleSystems.Remove(ps);
        }

        private void ForceReleaseParticleSystem(ParticleSystem ps)
        {
            if (ps == null)
            {
                return;
            }

            if (ps.gameObject != null && !ps.isPlaying)
            {
                ps.Play();
            }

            _trackedParticleSystems.Remove(ps);
            s_particleOwnership.Remove(ps);
        }

        private bool TryClaimRenderer(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            if (s_rendererOwnership.TryGetValue(renderer, out AngleFadeMaterial currentOwner))
            {
                if (currentOwner == null || currentOwner.gameObject == null)
                {
                    s_rendererOwnership.Remove(renderer);
                }
                else if (currentOwner == this)
                {
                    return true;
                }
                else
                {
                    bool currentOwnerInside = currentOwner.IsCameraInsideAngle();
                    bool thisInside = IsCameraInsideAngle();

                    if (thisInside && !currentOwnerInside)
                    {
                        currentOwner.ForceReleaseRenderer(renderer);
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            s_rendererOwnership[renderer] = this;
            return true;
        }

        private bool TryClaimParticleSystem(ParticleSystem ps)
        {
            if (ps == null)
            {
                return false;
            }

            if (s_particleOwnership.TryGetValue(ps, out AngleFadeMaterial currentOwner))
            {
                if (currentOwner == null || currentOwner.gameObject == null)
                {
                    s_particleOwnership.Remove(ps);
                }
                else if (currentOwner == this)
                {
                    return true;
                }
                else
                {
                    bool currentOwnerInside = currentOwner.IsCameraInsideAngle();
                    bool thisInside = IsCameraInsideAngle();

                    if (thisInside && !currentOwnerInside)
                    {
                        currentOwner.ForceReleaseParticleSystem(ps);
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            s_particleOwnership[ps] = this;
            return true;
        }

        private void ReclaimOwnership()
        {
            Collider[] collidersInTrigger = GetCollidersInTrigger();
            if (collidersInTrigger == null)
            {
                return;
            }

            for (int i = 0; i < collidersInTrigger.Length; i++)
            {
                var col = collidersInTrigger[i];
                if (col == null || col.gameObject == gameObject || col == _triggerCollider)
                {
                    continue;
                }

                if (!IsInLayerMask(col.gameObject.layer))
                {
                    continue;
                }

                _tempParticleSystems.Clear();
                _tempParticleRenderers.Clear();
                _tempRenderers.Clear();
                
                col.gameObject.GetComponentsInChildren(_tempParticleSystems);
                col.gameObject.GetComponentsInChildren(_tempRenderers);

                for (int j = 0; j < _tempParticleSystems.Count; j++)
                {
                    var ps = _tempParticleSystems[j];
                    if (!_trackedParticleSystems.Contains(ps))
                    {
                        if (TryClaimParticleSystem(ps))
                        {
                            _trackedParticleSystems.Add(ps);
                        }
                    }

                    Renderer psRenderer = ps.GetComponent<Renderer>();
                    if (psRenderer != null)
                    {
                        _tempParticleRenderers.Add(psRenderer);
                    }
                }

                for (int j = 0; j < _tempRenderers.Count; j++)
                {
                    var renderer = _tempRenderers[j];
                    if (_tempParticleRenderers.Contains(renderer) || _trackedRenderers.Contains(renderer))
                    {
                        continue;
                    }

                    if (TryClaimRenderer(renderer))
                    {
                        _trackedRenderers.Add(renderer);
                        SetupRendererMaterials(renderer);
                    }
                }
            }
        }

        private void ValidateOwnership()
        {
            for (int i = _trackedRenderers.Count - 1; i >= 0; i--)
            {
                var renderer = _trackedRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (s_rendererOwnership.TryGetValue(renderer, out AngleFadeMaterial owner))
                {
                    if (owner != this)
                    {
                        if (_instanceMaterials.TryGetValue(renderer, out Material[] materials))
                        {
                            if (s_originalMaterials.TryGetValue(renderer, out Material[] originals))
                            {
                                renderer.materials = originals;
                            }

                            for (int j = 0; j < materials.Length; j++)
                            {
                                var mat = materials[j];
                                if (mat != null)
                                {
                                    Destroy(mat);
                                }
                            }

                            _instanceMaterials.Remove(renderer);
                        }
                        _trackedRenderers.RemoveAt(i);
                    }
                }
            }

            for (int i = _trackedParticleSystems.Count - 1; i >= 0; i--)
            {
                var ps = _trackedParticleSystems[i];
                if (ps == null)
                {
                    continue;
                }

                if (s_particleOwnership.TryGetValue(ps, out AngleFadeMaterial owner))
                {
                    if (owner != this)
                    {
                        _trackedParticleSystems.RemoveAt(i);
                    }
                }
            }
        }

        private Collider[] GetCollidersInTrigger()
        {
            if (_cacheValid && _cachedCollidersInTrigger != null)
            {
                return _cachedCollidersInTrigger;
            }

            if (_triggerCollider == null)
            {
                _cachedCollidersInTrigger = null;
                _cacheValid = true;
                return null;
            }

            Collider[] result = null;

            if (_triggerCollider is BoxCollider boxCollider)
            {
                Vector3 center = transform.TransformPoint(boxCollider.center);
                Vector3 halfExtents = Vector3.Scale(boxCollider.size * 0.5f, transform.lossyScale);
                result = Physics.OverlapBox(center, halfExtents, transform.rotation, _layerMask);
            }
            else if (_triggerCollider is SphereCollider sphereCollider)
            {
                Vector3 center = transform.TransformPoint(sphereCollider.center);
                float radius = sphereCollider.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
                result = Physics.OverlapSphere(center, radius, _layerMask);
            }
            else if (_triggerCollider is CapsuleCollider capsuleCollider)
            {
                Vector3 center = transform.TransformPoint(capsuleCollider.center);
                float radius = capsuleCollider.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
                float height = capsuleCollider.height * transform.lossyScale.y;

                Vector3 point1 = center + transform.up * (height * 0.5f - radius);
                Vector3 point2 = center - transform.up * (height * 0.5f - radius);
                result = Physics.OverlapCapsule(point1, point2, radius, _layerMask);
            }
            else
            {
                Bounds bounds = _triggerCollider.bounds;
                result = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity, _layerMask);
            }

            _cachedCollidersInTrigger = result;
            _cacheValid = true;
            return result;
        }

        private void DetectObjectsInTrigger()
        {
            Collider[] collidersInTrigger = GetCollidersInTrigger();

            if (collidersInTrigger != null)
            {
                foreach (Collider col in collidersInTrigger)
                {
                    if (col != null && col.gameObject != gameObject && col != _triggerCollider)
                    {
                        ProcessGameObject(col.gameObject);
                    }
                }
            }
        }

        private bool IsInLayerMask(int layer)
        {
            return (_layerMask.value & (1 << layer)) != 0;
        }

        private void SetupTransparentMaterial(Material material)
        {
            material.SetFloat(SurfaceTypeId, 1f);
            material.SetFloat(BlendModeId, 0f);
            material.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            material.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat(ZWriteId, 0f);
            material.SetFloat(AlphaClipId, 0f);

            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 position = transform.position;
            Vector3 forward = transform.forward;
            float gizmoLength = 2f;

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(position, forward * gizmoLength);

            Quaternion leftRotation = Quaternion.AngleAxis(-_halfAngle, transform.up);
            Quaternion rightRotation = Quaternion.AngleAxis(_halfAngle, transform.up);

            Vector3 leftDirection = leftRotation * forward;
            Vector3 rightDirection = rightRotation * forward;

            Gizmos.color = new Color(0f, 1f, 0.5f, 0.6f);
            Gizmos.DrawRay(position, leftDirection * gizmoLength);
            Gizmos.DrawRay(position, rightDirection * gizmoLength);

            DrawWireArc(position, transform.up, leftDirection, _halfAngle * 2f, gizmoLength, 20);

            if (_cameraTransform != null)
            {
                bool isInside = IsCameraInsideAngle();
                Gizmos.color = isInside ? Color.green : Color.red;
                Gizmos.DrawLine(position, _cameraTransform.position);
            }
        }

        private static void DrawWireArc(Vector3 center, Vector3 axis, Vector3 from, float totalAngle, float radius, int segments)
        {
            float stepAngle = totalAngle / segments;
            Vector3 previousPoint = center + from.normalized * radius;

            for (int i = 1; i <= segments; i++)
            {
                Quaternion rotation = Quaternion.AngleAxis(stepAngle * i, axis);
                Vector3 nextPoint = center + rotation * from.normalized * radius;
                Gizmos.DrawLine(previousPoint, nextPoint);
                previousPoint = nextPoint;
            }
        }
    }
}
