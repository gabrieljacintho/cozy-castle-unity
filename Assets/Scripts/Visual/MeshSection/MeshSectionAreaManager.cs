using System.Collections.Generic;
using UnityEngine;

namespace GabrielBertasso.Visual.MeshSection
{
    [DefaultExecutionOrder(-100)]
    public sealed class MeshSectionAreaManager : MonoBehaviour
    {
        public const int MaxAreas = 8;

        private static readonly int AreaCountId = Shader.PropertyToID("_MeshSectionAreaCount");
        private static readonly int AreaCenterId = Shader.PropertyToID("_MeshSectionAreaCenter");
        private static readonly int AreaHalfExtentsId = Shader.PropertyToID("_MeshSectionAreaHalfExtents");
        private static readonly int AreaRightId = Shader.PropertyToID("_MeshSectionAreaRight");
        private static readonly int AreaUpId = Shader.PropertyToID("_MeshSectionAreaUp");
        private static readonly int AreaForwardId = Shader.PropertyToID("_MeshSectionAreaForward");
        private static readonly int AreaParamsId = Shader.PropertyToID("_MeshSectionAreaParams");

        private static MeshSectionAreaManager s_instance;

        private readonly List<MeshSectionArea> _areas = new List<MeshSectionArea>(MaxAreas);
        private readonly Vector4[] _centerBuffer = new Vector4[MaxAreas];
        private readonly Vector4[] _halfExtentsBuffer = new Vector4[MaxAreas];
        private readonly Vector4[] _rightBuffer = new Vector4[MaxAreas];
        private readonly Vector4[] _upBuffer = new Vector4[MaxAreas];
        private readonly Vector4[] _forwardBuffer = new Vector4[MaxAreas];
        private readonly Vector4[] _paramsBuffer = new Vector4[MaxAreas];

        private bool _isDirty = true;

        public static MeshSectionAreaManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    EnsureInstance();
                }

                return s_instance;
            }
        }

        public int ActiveAreaCount => _areas.Count;
        public IReadOnlyList<MeshSectionArea> Areas => _areas;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(this);
                return;
            }

            s_instance = this;
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                ClearShaderGlobals();
                s_instance = null;
            }
        }

        private void LateUpdate()
        {
            if (RequiresUpload())
            {
                UploadToShader();
            }
        }

        public static void Register(MeshSectionArea area)
        {
            if (area == null)
            {
                return;
            }

            EnsureInstance();
            var instance = s_instance;

            if (instance._areas.Contains(area))
            {
                return;
            }

            if (instance._areas.Count >= MaxAreas)
            {
                Debug.LogWarning(
                    $"[MeshSection] Cannot register area '{area.name}': max {MaxAreas} areas reached.",
                    area);
                return;
            }

            instance._areas.Add(area);
            instance._isDirty = true;
        }

        public static void Unregister(MeshSectionArea area)
        {
            if (area == null || s_instance == null)
            {
                return;
            }

            if (s_instance._areas.Remove(area))
            {
                s_instance._isDirty = true;
            }
        }

        public static void MarkDirty()
        {
            if (s_instance != null)
            {
                s_instance._isDirty = true;
            }
        }

        private static void EnsureInstance()
        {
            if (s_instance != null)
            {
                return;
            }

            var existing = FindFirstObjectByType<MeshSectionAreaManager>();

            if (existing != null)
            {
                s_instance = existing;
                return;
            }

            var holder = new GameObject("[MeshSectionAreaManager]");
            holder.hideFlags = HideFlags.HideAndDontSave;
            s_instance = holder.AddComponent<MeshSectionAreaManager>();
        }

        private bool RequiresUpload()
        {
            if (_isDirty)
            {
                return true;
            }

            for (int i = 0; i < _areas.Count; i++)
            {
                if (_areas[i] != null && _areas[i].HasChanged())
                {
                    return true;
                }
            }

            return false;
        }

        private void UploadToShader()
        {
            int writeIndex = 0;

            for (int i = 0; i < _areas.Count && writeIndex < MaxAreas; i++)
            {
                var area = _areas[i];

                if (area == null || !area.isActiveAndEnabled)
                {
                    continue;
                }

                area.FillShaderData(
                    out Vector3 center,
                    out Vector3 halfExtents,
                    out Vector3 right,
                    out Vector3 up,
                    out Vector3 forward,
                    out float alpha,
                    out float edgeThickness,
                    out float feather);

                _centerBuffer[writeIndex] = new Vector4(center.x, center.y, center.z, 0f);
                _halfExtentsBuffer[writeIndex] = new Vector4(halfExtents.x, halfExtents.y, halfExtents.z, 0f);
                _rightBuffer[writeIndex] = new Vector4(right.x, right.y, right.z, 0f);
                _upBuffer[writeIndex] = new Vector4(up.x, up.y, up.z, 0f);
                _forwardBuffer[writeIndex] = new Vector4(forward.x, forward.y, forward.z, 0f);
                _paramsBuffer[writeIndex] = new Vector4(alpha, edgeThickness, feather, 0f);

                area.ClearChangedFlag();
                writeIndex++;
            }

            for (int i = writeIndex; i < MaxAreas; i++)
            {
                _centerBuffer[i] = Vector4.zero;
                _halfExtentsBuffer[i] = Vector4.zero;
                _rightBuffer[i] = Vector4.zero;
                _upBuffer[i] = Vector4.zero;
                _forwardBuffer[i] = Vector4.zero;
                _paramsBuffer[i] = new Vector4(1f, 0.01f, 0.001f, 0f);
            }

            Shader.SetGlobalInt(AreaCountId, writeIndex);
            Shader.SetGlobalVectorArray(AreaCenterId, _centerBuffer);
            Shader.SetGlobalVectorArray(AreaHalfExtentsId, _halfExtentsBuffer);
            Shader.SetGlobalVectorArray(AreaRightId, _rightBuffer);
            Shader.SetGlobalVectorArray(AreaUpId, _upBuffer);
            Shader.SetGlobalVectorArray(AreaForwardId, _forwardBuffer);
            Shader.SetGlobalVectorArray(AreaParamsId, _paramsBuffer);

            _isDirty = false;
        }

        private void ClearShaderGlobals()
        {
            Shader.SetGlobalInt(AreaCountId, 0);

            for (int i = 0; i < MaxAreas; i++)
            {
                _centerBuffer[i] = Vector4.zero;
                _halfExtentsBuffer[i] = Vector4.zero;
                _rightBuffer[i] = Vector4.zero;
                _upBuffer[i] = Vector4.zero;
                _forwardBuffer[i] = Vector4.zero;
                _paramsBuffer[i] = new Vector4(1f, 0.01f, 0.001f, 0f);
            }

            Shader.SetGlobalVectorArray(AreaCenterId, _centerBuffer);
            Shader.SetGlobalVectorArray(AreaHalfExtentsId, _halfExtentsBuffer);
            Shader.SetGlobalVectorArray(AreaRightId, _rightBuffer);
            Shader.SetGlobalVectorArray(AreaUpId, _upBuffer);
            Shader.SetGlobalVectorArray(AreaForwardId, _forwardBuffer);
            Shader.SetGlobalVectorArray(AreaParamsId, _paramsBuffer);
        }
    }
}
