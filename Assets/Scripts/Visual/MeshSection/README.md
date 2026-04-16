# MeshSection — Shader-Based Mesh Cutting

Shader-driven cut-away system for Unity 6 URP. Define box volumes in the
scene; meshes using the `MeshSectionLit` shader get clipped inside them, and
a companion RendererFeature draws the box of each area with a PBR fill
material, producing a solid "cross-section" look.

## How it works

Three pieces cooperate via the stencil buffer:

1. **`MeshSectionLit.shader`** — attached to cuttable meshes. Has two passes:
   - `StencilMark` (runs via `SRPDefaultUnlit`): for every fragment that
     would be clipped by a cutting area, writes bit `0x40` to the stencil
     buffer. No color, no depth write.
   - `ForwardLit`: the normal forward pass, which `discard`s fragments
     inside cutting areas.
2. **`MeshSectionArea` component** — defines a cutting box in the scene and
   exposes a `CapProfile` field (intersection material).
3. **`MeshSectionCapFeature` (RendererFeature)** — after all opaques have
   rendered, draws one unit cube per active area, scaled/rotated to the
   area's transform, with:
   - `Cull Front` so you see the box's interior back faces (which coincide
     with the cut plane on the mesh).
   - `Stencil Comp Equal Ref 64 ReadMask 64` — only paints where a mesh
     was clipped.
   - The area's `CapProfile` drives the PBR inputs (albedo, normal,
     metallic, smoothness, emission).

The result: where a mesh is cut, the stencil bit is set, the cap cube's back
face is inside the cut volume, and the cap pixel shader paints using the
area's profile. Where a mesh is not cut, no stencil is set, and the cap cube
fails the stencil test → no fill.

## Setup (one-time)

### 1. Enable the RendererFeature

- Open your URP Renderer asset (the one referenced in your URP Asset).
- Scroll to **Renderer Features** at the bottom.
- Click **Add Renderer Feature** → **Mesh Section Cap Feature**.
- Assign a material using the `MeshSectionCap` shader to the feature's
  `Cap Material` field. (Any material works — its values are overridden
  per-area at draw time via MaterialPropertyBlock.)

### 2. Create cap profiles

- Right-click in the Project window → **Create → GabrielBertasso →
  MeshSection → Cap Profile**.
- Configure albedo, normal, metallic, smoothness, etc.
- Create one profile per "type of cut" you need (concrete, soil, brick…).

### 3. Prepare a cuttable mesh

- Create a material using the `MeshSectionLit` shader and assign it to
  your Mesh Renderer.
- Optionally add the `MeshSectionRenderer` component for inspector
  validation.

### 4. Create cutting areas

- Empty GameObject → add `MeshSectionArea` (or right-click in Hierarchy
  → `GabrielBertasso/MeshSection/Create Area`).
- Drag a `CapProfile` into the area's `Cap Profile` field.
- Position / rotate / scale the area's box.
- `Alpha = 0` hides meshes inside fully. `Alpha = 1` disables the area.

## Design summary

- **Cap material lives on the area.** Every area carries one profile. Up
  to 8 areas active per frame → up to 8 visually distinct caps
  simultaneously. Switch profiles or areas at runtime to show different
  cap types per cut context.
- **Stencil bit `0x40`** flags "this pixel was cut". Shared between
  `MeshSectionLit`'s StencilMark pass and `MeshSectionCap`'s stencil
  test. If another system needs the same bit, change
  `MESH_SECTION_STENCIL_BIT` in `MeshSectionCore.hlsl` plus the
  `Ref/ReadMask/WriteMask` fields in both shaders.
- **No stencil ID allocation.** The cap cube reads `_MeshSectionAreaIndex`
  (set per-draw by the feature) to know which area's globals to sample.
- **Cap cube draws via `AddUnsafePass` + `DrawMesh`** after opaques.
  Render Graph native, no compatibility mode.

## File layout

```
Art/Shaders/MeshSection/
    MeshSectionCore.hlsl       # SDF + box helpers, shared include
    MeshSectionLit.shader      # URP Lit with clip + stencil mark
    MeshSectionCap.shader      # Runs on the cap cube, stencil-gated
    README.md

Scripts/Visual/MeshSection/
    MeshSectionAreaManager.cs  # Uploads area globals each frame
    MeshSectionArea.cs         # Cutting box + CapProfile reference
    MeshSectionRenderer.cs     # Inspector validation for cuttable meshes
    MeshSectionCapProfile.cs   # ScriptableObject of PBR cap inputs
    MeshSectionCapFeature.cs   # RendererFeature that draws cap cubes
    GabrielBertasso.Visual.MeshSection.asmdef
    Editor/
        MeshSectionAreaEditor.cs
        MeshSectionRendererEditor.cs
        GabrielBertasso.Visual.MeshSection.Editor.asmdef
```

## Alpha semantics

| Alpha | Effect |
| --- | --- |
| `0.0` | Mesh fully hidden inside the area. Cap fully visible. |
| `0.5` | Both dimmed (partial transparency). |
| `1.0` | Area has no effect. |

The cap brightness multiplies by `(1 - alpha)`.

## Known caveats

- **SRP Batcher.** The `StencilMark` pass uses `SRPDefaultUnlit`, which
  breaks SRP Batcher for `MeshSectionLit`. GPU instancing still works.
- **Stencil buffer requirement.** URP's Forward Renderer preserves stencil
  by default. If you use a custom RendererFeature that clears it between
  opaques and this feature, the cap will fail. Verify in Frame Debugger.
- **Max 8 cutting areas simultaneously.** Extend `MESH_SECTION_MAX_AREAS`
  in `MeshSectionCore.hlsl` + `MeshSectionAreaManager.MaxAreas` together
  if you need more.
- **Box areas only.** Spheres / capsules would be another SDF branch in
  `MeshSection_Evaluate`.
- **Skewed parent transforms** give slightly off boxes since axes come
  from `transform.right/up/forward`, not a full matrix decomposition.
- **Cap texture tiles in world space** via `Texture Scale` on the profile,
  not the mesh's UVs. This is intentional: the cap is a synthetic surface
  on the cut plane, not the mesh's skin.
- **Multi-submesh meshes.** If a cuttable mesh has multiple submeshes
  (e.g. different materials on different parts), attach `MeshSectionLit`
  materials to each relevant slot. The cap is a separate feature and
  doesn't consume material slots on the mesh — only one `MeshSectionLit`
  material per slot is needed.
