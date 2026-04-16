# MeshSection Shaders

Shader-based cutting system for URP. Hides portions of meshes inside world-space
box volumes and renders a PBR cross-section material where the cut intersects
the surface.

## Files

- `MeshSectionCore.hlsl` — shared include. Contains the shader global buffer
  layout, the box SDF, and the `MeshSection_Evaluate` / `MeshSection_ApplyClip`
  entry points.
- `MeshSectionLit.shader` — URP Lit with cutting support. Forward, ShadowCaster,
  DepthOnly, DepthNormals, and Meta passes all run the same clip.

## Shader globals

All managed by `MeshSectionAreaManager`. Do not set these from your own code.

| Name | Type | Notes |
| --- | --- | --- |
| `_MeshSectionAreaCount` | `int` | Number of live areas. |
| `_MeshSectionAreaCenter[8]` | `float4` | World-space box center. |
| `_MeshSectionAreaHalfExtents[8]` | `float4` | World-space half extents. |
| `_MeshSectionAreaRight/Up/Forward[8]` | `float4` | Box-local axes in world space (supports rotation). |
| `_MeshSectionAreaParams[8]` | `float4` | x=alpha, y=edge thickness, z=feather. |

## Integrating into a custom shader

Include `MeshSectionCore.hlsl`, then in the fragment shader:

```hlsl
MeshSectionResult section = MeshSection_ApplyClip(input.positionWS, 0.999);
// section.edgeAmount in [0..1] for blending your intersection material.
```

Every pass that writes depth or shadows must run the same clip, otherwise the
cut will leak into shadows or SSAO.

## Limits

`MESH_SECTION_MAX_AREAS` defaults to 8. Increase both the `#define` here and
`MeshSectionAreaManager.MaxAreas` together if you need more — shader globals
and the C# side must agree.
