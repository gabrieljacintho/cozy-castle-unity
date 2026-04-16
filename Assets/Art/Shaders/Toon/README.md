# URP Forward+ Toon Shader

A production-ready cel/toon shader for Unity's Universal Render Pipeline with full Forward+ support.

## Requirements

- Unity 2022.2 or newer (Forward+ shipped in URP 14 / Unity 2022.2)
- Universal Render Pipeline package installed and active
- Forward+ is *optional* — the shader also works under classic Forward and Deferred

## Installation

1. Drop the whole `ToonShader` folder anywhere under your project's `Assets/` (e.g. `Assets/Shaders/ToonShader`).
2. Create a material, set its shader to **Custom / URP / Toon**.
3. Assign to a MeshRenderer / SkinnedMeshRenderer.

The five files must stay in the same folder — the `.shader` includes the `.hlsl` files by relative path.

## Files

| File | Purpose |
|---|---|
| `Toon.shader` | Entry point — declares properties and passes |
| `ToonInput.hlsl` | `UnityPerMaterial` CBUFFER and texture samplers |
| `ToonLighting.hlsl` | Core cel-shading math + Forward+ light loop |
| `ToonForwardPass.hlsl` | Vertex/fragment for the lit pass |
| `ToonOutlinePass.hlsl` | Inverted-hull outline pass |

## Features

- **Stepped half-lambert** diffuse with adjustable step count and edge smoothness
- **Optional ramp texture** — toggle `_USE_RAMP` and assign a 1D ramp to override the procedural bands
- **Toon specular** — hard-edge Blinn-Phong lobe
- **Rim light** — optionally biased toward the main light
- **Shadow tinting** — multiply shadowed areas by a colored tint
- **Main-light realtime shadows** with smoothstep-quantized shadow edge
- **Additional lights** — each one runs through the full toon pipeline (or falls back to Lambert if `_ADDITIONAL_LIGHTS_TOON` is disabled for perf)
- **Forward+ aware** — uses `LIGHT_LOOP_BEGIN / LIGHT_LOOP_END` under `USE_FORWARD_PLUS`, so hundreds of lights per frame work correctly
- **Inverted-hull outline** with view-space normal expansion (constant screen-space width)
- **Full URP pass set** — ShadowCaster, DepthOnly, DepthNormals (so SSAO, screen-space shadows, and decals all work)
- **Lightmaps, SH probes, reflection probe blending, shadowmask** supported
- **GPU instancing** and **SRP Batcher** compatible (single `UnityPerMaterial` CBUFFER)

## Forward+ notes

Under Forward+, URP clusters lights in screen-space tiles and the shader queries them via `GetAdditionalLightsCount()` + `LIGHT_LOOP_BEGIN`. The macro expands differently depending on whether clustered lighting is active. This shader handles both branches, so the same material works identically on Forward and Forward+.

**Keyword compatibility:** URP renamed the Forward+ keyword in URP 17 / Unity 6:
- Old (URP 14–16, Unity 2022.3 / 2023): `_FORWARD_PLUS` + `USE_FORWARD_PLUS`
- New (URP 17+, Unity 6): `_CLUSTER_LIGHT_LOOP` + `USE_CLUSTER_LIGHT_LOOP`

The shader declares `_CLUSTER_LIGHT_LOOP` (which URP's deprecation shim maps back to the old name on older versions if needed) and aliases both internal defines, so it compiles clean on either version — no deprecation warnings.

**Directional additional lights under Forward+:** In clustered lighting, non-main directional lights are not counted by `GetAdditionalLightsCount()` and must be iterated separately via `URP_FP_DIRECTIONAL_LIGHTS_COUNT`. The shader handles this explicitly, matching URP Lit.shader behaviour.

To enable Forward+ in your project:
`URP Asset → Rendering → Rendering Path → Forward+`

## Tuning tips

- **Steps**: 2–3 gives a classic anime look; 4–5 is more painterly; 1 gives pure binary shading.
- **Step Smoothness**: keep very low (0.01–0.05) for hard cel edges. Raise for a softer hand-drawn feel.
- **Shadow Strength**: 1.0 applies full shadow tint; 0 disables tinting entirely.
- **Outline Width**: units are roughly pixels at 1080p. Thicker lines read better at distance.
- **Rim Light Align**: 0 = omnidirectional rim (Fresnel), 1 = rim only where main light hits. 0.2–0.4 usually looks best.
- For characters, set **Cull** to `Back` (default). For foliage/clothes, consider `Off` and enable alpha clip.

## Keywords exposed

| Keyword | Purpose |
|---|---|
| `_ALPHATEST_ON` | Alpha cutout |
| `_USE_RAMP` | Use ramp texture instead of procedural bands |
| `_SPECULAR_ON` | Enable specular highlights |
| `_RIM_ON` | Enable rim light |
| `_ADDITIONAL_LIGHTS_TOON` | Per-light toon treatment for extra lights (off = cheaper Lambert) |
| `_OUTLINE_ON` | Enable outline pass |

## Limitations

- No normal map sampling in this version — add `TEXTURE2D(_BumpMap)` and compute the TBN matrix in `ToonForwardPass.hlsl` if you need it (tangentWS is already passed through).
- No transparency blending — shader is opaque/cutout only. Adding a transparent variant requires a second SubShader with `Blend SrcAlpha OneMinusSrcAlpha` and `ZWrite Off`.
- The outline pass uses an inverted hull, which produces artifacts on hard-edged geometry (cubes, low-poly meshes). Smooth normals or a custom "outline normal" vertex channel give better results.
