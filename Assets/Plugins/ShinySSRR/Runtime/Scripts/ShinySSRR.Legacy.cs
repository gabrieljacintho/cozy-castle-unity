using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if !UNITY_6000_4_OR_NEWER
namespace ShinySSRR {

    public partial class ShinySSRR {

        public partial class SmoothnessMetallicPass {

            public override void Configure (CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) {

                RenderTextureDescriptor desc = cameraTextureDescriptor;
                desc.colorFormat = RenderTextureFormat.RGHalf; // r = smoothness, g = metallic
                desc.depthBufferBits = 24;
                desc.msaaSamples = 1;

                cmd.GetTemporaryRT(ShaderParams.SmoothnessMetallicRT, desc, FilterMode.Point);
                cmd.SetGlobalTexture(ShaderParams.SmoothnessMetallicRT, smootnessMetallicRT);
                ConfigureTarget(smootnessMetallicRT);
                ConfigureClear(ClearFlag.All, Color.black);
            }

            public override void Execute (ScriptableRenderContext context, ref RenderingData renderingData) {

                CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);

                SortingCriteria sortingCriteria = SortingCriteria.CommonOpaque;
                var drawSettings = CreateDrawingSettings(shaderTagIdList, ref renderingData, sortingCriteria);
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings);

                context.ExecuteCommandBuffer(cmd);

                CommandBufferPool.Release(cmd);
            }
        }

        partial class SSRPass {

            public override void Configure (CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) {
                ConfigureInput(GetRequiredInputs());
            }

            public override void Execute (ScriptableRenderContext context, ref RenderingData renderingData) {

                Camera cam = renderingData.cameraData.camera;

                // execute reflections passes
                CommandBuffer cmd = CommandBufferPool.Get(SHINY_CBUFNAME);
                passData.cmd = cmd;
                passData.cam = renderingData.cameraData.camera;
                passData.sourceDesc = renderingData.cameraData.cameraTargetDescriptor;
#if UNITY_2022_2_OR_NEWER
                passData.source = renderer.cameraColorTargetHandle;
#else
                passData.source = renderer.cameraColorTarget;
#endif
                ExecuteInternal(passData);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        partial class SSRBackfacesPass {

            public override void Configure (CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) {
                RenderTextureDescriptor depthDesc = cameraTextureDescriptor;
                int downsampling = settings.downsampling.value;
                depthDesc.width = Mathf.CeilToInt(depthDesc.width / downsampling);
                depthDesc.height = Mathf.CeilToInt(depthDesc.height / downsampling);
                depthDesc.colorFormat = RenderTextureFormat.Depth;
                depthDesc.depthBufferBits = 24;
                depthDesc.msaaSamples = 1;

                cmd.GetTemporaryRT(ShaderParams.DownscaledBackDepthRT, depthDesc, FilterMode.Point);
                cmd.SetGlobalTexture(ShaderParams.DownscaledBackDepthRT, m_Depth);
                ConfigureTarget(m_Depth);
                ConfigureClear(ClearFlag.All, Color.black);
            }

            public override void Execute (ScriptableRenderContext context, ref RenderingData renderingData) {
                CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                SortingCriteria sortingCriteria = SortingCriteria.CommonOpaque;
                var drawSettings = CreateDrawingSettings(shaderTagIdList, ref renderingData, sortingCriteria);
                drawSettings.perObjectData = PerObjectData.None;
                if (depthOnlyMaterial == null) {
                    Shader depthOnly = Shader.Find(m_DepthOnlyShader);
                    depthOnlyMaterial = new Material(depthOnly);
                    depthOnlyMaterial.SetInt(_Cull, (int)CullMode.Front);
                }
                drawSettings.overrideMaterial = depthOnlyMaterial;
                drawSettings.overrideMaterialPassIndex = 0;
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings);

                context.ExecuteCommandBuffer(cmd);

                CommandBufferPool.Release(cmd);
            }
        }

        public partial class DepthRenderPass {

            public override void Configure (CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) {
                if (transparentLayerMask != filterSettings.layerMask) {
                    filterSettings = new FilteringSettings(RenderQueueRange.transparent, transparentLayerMask);
                }
                RenderTextureDescriptor depthDesc = cameraTextureDescriptor;
                depthDesc.colorFormat = RenderTextureFormat.Depth;
                depthDesc.depthBufferBits = 24;
                depthDesc.msaaSamples = 1;

                cmd.GetTemporaryRT(ShaderParams.CustomDepthTexture, depthDesc, FilterMode.Point);
                cmd.SetGlobalTexture(ShaderParams.CustomDepthTexture, m_Depth);
                ConfigureTarget(m_Depth);
                ConfigureClear(ClearFlag.All, Color.black);
            }

            public override void Execute (ScriptableRenderContext context, ref RenderingData renderingData) {
                if (transparentLayerMask == 0) return;
                CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();


                SortingCriteria sortingCriteria = SortingCriteria.CommonTransparent;
                var drawSettings = CreateDrawingSettings(shaderTagIdList, ref renderingData, sortingCriteria);
                drawSettings.perObjectData = PerObjectData.None;

                if (useOptimizedDepthOnlyShader) {
                    if (depthOnlyMaterial == null) {
                        Shader depthOnly = Shader.Find(m_DepthOnlyShader);
                        depthOnlyMaterial = new Material(depthOnly);
                    }
                    drawSettings.overrideMaterial = depthOnlyMaterial;
                }
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings);

                int transparentSupportCount = transparentSupport.Count; 
                for (int i = 0; i < transparentSupportCount; i++) {
                    Renderer renderer = transparentSupport[i].theRenderer;
                    if (renderer != null) {
                        cmd.DrawRenderer(renderer, depthOnlyMaterial);
                    }
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
#endif
