using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace GabrielBertasso.Visual.MeshSection
{
    // Injects a render pass after opaques that draws one cube per active
    // MeshSectionArea using the MeshSectionCap shader. Stencil test on the
    // cap shader gates the fill to pixels previously marked by MeshSectionLit's
    // StencilMark pass.
    //
    // Setup: add this feature to your URP Renderer asset (Renderer Features list
    // at the bottom). Only needs to be configured once per project.
    public sealed class MeshSectionCapFeature : ScriptableRendererFeature
    {
        [Tooltip("Material using the MeshSectionCap shader. The feature draws one box per active area with this material, pulling per-area values from the area's CapProfile via MaterialPropertyBlock.")]
        [SerializeField] private Material _capMaterial;

        [Tooltip("Fallback shader name used to auto-locate a material if none is assigned. Leave default unless you renamed the shader.")]
        [SerializeField] private string _capShaderName = "GabrielBertasso/MeshSection/MeshSectionCap";

        private MeshSectionCapPass _pass;
        private Mesh _unitCubeMesh;

        public override void Create()
        {
            _pass = new MeshSectionCapPass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques,
            };

            _unitCubeMesh = BuildUnitCube();
        }

        protected override void Dispose(bool disposing)
        {
            if (_unitCubeMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_unitCubeMesh);
                }
                else
                {
                    DestroyImmediate(_unitCubeMesh);
                }

                _unitCubeMesh = null;
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_capMaterial == null)
            {
                return;
            }

            if (_unitCubeMesh == null)
            {
                _unitCubeMesh = BuildUnitCube();
            }

            _pass.Setup(_capMaterial, _unitCubeMesh);
            renderer.EnqueuePass(_pass);
        }

        // 1x1x1 cube centered on the origin. The feature scales / positions it
        // per-draw via the MeshSectionArea's matrix.
        private static Mesh BuildUnitCube()
        {
            var mesh = new Mesh { name = "MeshSection_UnitCube", hideFlags = HideFlags.HideAndDontSave };

            Vector3[] vertices =
            {
                new(-0.5f, -0.5f, -0.5f), new( 0.5f, -0.5f, -0.5f),
                new( 0.5f,  0.5f, -0.5f), new(-0.5f,  0.5f, -0.5f),
                new(-0.5f, -0.5f,  0.5f), new( 0.5f, -0.5f,  0.5f),
                new( 0.5f,  0.5f,  0.5f), new(-0.5f,  0.5f,  0.5f),
            };

            int[] triangles =
            {
                0, 2, 1, 0, 3, 2, // -Z
                4, 5, 6, 4, 6, 7, // +Z
                0, 4, 7, 0, 7, 3, // -X
                1, 2, 6, 1, 6, 5, // +X
                3, 7, 6, 3, 6, 2, // +Y
                0, 1, 5, 0, 5, 4, // -Y
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.UploadMeshData(markNoLongerReadable: true);
            return mesh;
        }

        // -----------------------------------------------------------------
        private sealed class MeshSectionCapPass : ScriptableRenderPass
        {
            private static readonly int AreaIndexId = Shader.PropertyToID("_MeshSectionAreaIndex");
            private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
            private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
            private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
            private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
            private static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
            private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
            private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
            private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
            private static readonly int TextureScaleId = Shader.PropertyToID("_TextureScale");

            private Material _material;
            private Mesh _cubeMesh;
            private int _capShaderPassIndex = -1;
            private readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();
            private readonly List<CapDrawCall> _drawCalls = new List<CapDrawCall>(MeshSectionAreaManager.MaxAreas);

            public void Setup(Material material, Mesh cubeMesh)
            {
                _material = material;
                _cubeMesh = cubeMesh;

                if (_material != null && _capShaderPassIndex < 0)
                {
                    _capShaderPassIndex = _material.FindPass("Cap");
                }
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameContext)
            {
                if (_material == null || _cubeMesh == null)
                {
                    return;
                }

                if (_capShaderPassIndex < 0)
                {
                    _capShaderPassIndex = _material.FindPass("Cap");
                    if (_capShaderPassIndex < 0)
                    {
                        return;
                    }
                }

                var manager = MeshSectionAreaManager.Instance;
                if (manager == null || manager.ActiveAreaCount == 0)
                {
                    return;
                }

                CollectDrawCalls(manager);
                if (_drawCalls.Count == 0)
                {
                    return;
                }

                var resourceData = frameContext.Get<UniversalResourceData>();

                using (var builder = renderGraph.AddUnsafePass<PassData>("MeshSectionCap", out var passData))
                {
                    passData.Material = _material;
                    passData.CubeMesh = _cubeMesh;
                    passData.ShaderPassIndex = _capShaderPassIndex;
                    passData.PropertyBlock = _propertyBlock;
                    passData.DrawCalls = _drawCalls;

                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((PassData data, UnsafeGraphContext ctx) =>
                    {
                        var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);

                        for (int i = 0; i < data.DrawCalls.Count; i++)
                        {
                            var call = data.DrawCalls[i];
                            data.PropertyBlock.Clear();
                            ApplyProfileToBlock(data.PropertyBlock, call.Profile, call.AreaIndex);
                            cmd.DrawMesh(
                                data.CubeMesh,
                                call.Matrix,
                                data.Material,
                                submeshIndex: 0,
                                shaderPass: data.ShaderPassIndex,
                                properties: data.PropertyBlock);
                        }
                    });
                }
            }

            private void CollectDrawCalls(MeshSectionAreaManager manager)
            {
                _drawCalls.Clear();

                var areas = manager.Areas;
                int areaIndex = 0;

                for (int i = 0; i < areas.Count && areaIndex < MeshSectionAreaManager.MaxAreas; i++)
                {
                    var area = areas[i];
                    if (area == null || !area.isActiveAndEnabled)
                    {
                        continue;
                    }

                    // Only enqueue areas that have a profile. Without a profile,
                    // no cap is drawn for that area — the cut simply leaves a hole.
                    var profile = area.CapProfile;
                    if (profile == null)
                    {
                        areaIndex++;
                        continue;
                    }

                    _drawCalls.Add(new CapDrawCall
                    {
                        Matrix = area.GetBoxMatrix(),
                        Profile = profile,
                        AreaIndex = areaIndex,
                    });

                    areaIndex++;
                }
            }

            private static void ApplyProfileToBlock(
                MaterialPropertyBlock block,
                MeshSectionCapProfile profile,
                int areaIndex)
            {
                block.SetFloat(AreaIndexId, areaIndex);

                Color albedo = profile.AlbedoColor;
                block.SetColor(BaseColorId, albedo);
                block.SetVector(BaseMapStId, new Vector4(1f, 1f, 0f, 0f));

                if (profile.AlbedoMap != null)
                {
                    block.SetTexture(BaseMapId, profile.AlbedoMap);
                }

                if (profile.NormalMap != null)
                {
                    block.SetTexture(BumpMapId, profile.NormalMap);
                }

                block.SetFloat(BumpScaleId, profile.NormalScale);
                block.SetFloat(MetallicId, profile.Metallic);
                block.SetFloat(SmoothnessId, profile.Smoothness);
                block.SetColor(EmissionColorId, profile.EmissionColor);
                block.SetFloat(TextureScaleId, profile.TextureScale);
            }

            private struct CapDrawCall
            {
                public Matrix4x4 Matrix;
                public MeshSectionCapProfile Profile;
                public int AreaIndex;
            }

            private class PassData
            {
                public Material Material;
                public Mesh CubeMesh;
                public int ShaderPassIndex;
                public MaterialPropertyBlock PropertyBlock;
                public List<CapDrawCall> DrawCalls;
            }
        }
    }
}
