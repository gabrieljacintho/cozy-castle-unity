#ifndef MESH_SECTION_CORE_INCLUDED
#define MESH_SECTION_CORE_INCLUDED

// Must match MeshSectionAreaManager.MaxAreas
#define MESH_SECTION_MAX_AREAS 8

// Stencil bit reserved for "this pixel had mesh geometry that was cut away by a
// MeshSection area". The Lit shader's StencilMark pass writes this bit. The Cap
// shader reads it. 0x40 is safe in URP Forward; if another system needs the
// same bit, change both sides together.
#define MESH_SECTION_STENCIL_BIT 64

// Globally-bound arrays. Updated once per frame by MeshSectionAreaManager.
// _MeshSectionAreaCount encodes how many of the MESH_SECTION_MAX_AREAS slots are live.
//
// Layout per area (index i):
//   _MeshSectionAreaCenter[i]        : world-space box center (xyz), unused w
//   _MeshSectionAreaHalfExtents[i]   : world-space half extents (xyz), unused w
//   _MeshSectionAreaRight[i]         : box local +X axis in world space (xyz), unused w
//   _MeshSectionAreaUp[i]            : box local +Y axis in world space (xyz), unused w
//   _MeshSectionAreaForward[i]       : box local +Z axis in world space (xyz), unused w
//   _MeshSectionAreaParams[i]        : x = alpha (0 hidden .. 1 visible),
//                                      y = edge thickness (world units),
//                                      z = feather (world units, soft boundary),
//                                      w = reserved
CBUFFER_START(MeshSectionGlobals)
    float4 _MeshSectionAreaCenter[MESH_SECTION_MAX_AREAS];
    float4 _MeshSectionAreaHalfExtents[MESH_SECTION_MAX_AREAS];
    float4 _MeshSectionAreaRight[MESH_SECTION_MAX_AREAS];
    float4 _MeshSectionAreaUp[MESH_SECTION_MAX_AREAS];
    float4 _MeshSectionAreaForward[MESH_SECTION_MAX_AREAS];
    float4 _MeshSectionAreaParams[MESH_SECTION_MAX_AREAS];
    int _MeshSectionAreaCount;
CBUFFER_END

// Transform a world-space position into the box-local space of area i.
float3 MeshSection_WorldToBoxLocal(float3 worldPos, int i)
{
    float3 d = worldPos - _MeshSectionAreaCenter[i].xyz;
    float x = dot(d, _MeshSectionAreaRight[i].xyz);
    float y = dot(d, _MeshSectionAreaUp[i].xyz);
    float z = dot(d, _MeshSectionAreaForward[i].xyz);
    return float3(x, y, z);
}

// Signed distance to an axis-aligned box (in box-local space).
// Negative inside, positive outside. Classic IQ formulation.
float MeshSection_BoxSDF(float3 localPos, float3 halfExtents)
{
    float3 q = abs(localPos) - halfExtents;
    float outside = length(max(q, 0.0));
    float inside = min(max(q.x, max(q.y, q.z)), 0.0);
    return outside + inside;
}

// Result of evaluating all active cutting areas against a world-space position.
// hideAmount : 0 = fully visible, 1 = fully hidden. Drives discard / alpha.
// edgeAmount : 0 = not near any cut boundary, 1 = exactly on boundary.
//              Drives the intersection material blend.
struct MeshSectionResult
{
    float hideAmount;
    float edgeAmount;
};

MeshSectionResult MeshSection_Evaluate(float3 worldPos)
{
    MeshSectionResult r;
    r.hideAmount = 0.0;
    r.edgeAmount = 0.0;

    int count = min(_MeshSectionAreaCount, MESH_SECTION_MAX_AREAS);

    UNITY_UNROLL
    for (int i = 0; i < MESH_SECTION_MAX_AREAS; i++)
    {
        if (i >= count)
        {
            break;
        }

        float alpha = _MeshSectionAreaParams[i].x;
        float edgeThickness = max(_MeshSectionAreaParams[i].y, 1e-4);
        float feather = max(_MeshSectionAreaParams[i].z, 1e-4);

        float3 local = MeshSection_WorldToBoxLocal(worldPos, i);
        float sdf = MeshSection_BoxSDF(local, _MeshSectionAreaHalfExtents[i].xyz);

        // Inside transition: negative sdf -> 1, positive sdf -> 0, with feather.
        // insideMask == 1 means "this area wants to hide this fragment".
        float insideMask = 1.0 - saturate((sdf + feather) / (2.0 * feather));

        // (1 - alpha) is how much the area hides. alpha=1 means fully visible, so
        // the area contributes nothing. alpha=0 means fully hidden inside the box.
        float hideContribution = insideMask * (1.0 - alpha);
        r.hideAmount = max(r.hideAmount, hideContribution);

        // Edge band: fragments whose |sdf| is within edgeThickness of the boundary.
        // Only meaningful if the area is actually hiding something (alpha < 1).
        float edgeBand = 1.0 - saturate(abs(sdf) / edgeThickness);
        float edgeContribution = edgeBand * (1.0 - alpha);
        r.edgeAmount = max(r.edgeAmount, edgeContribution);
    }

    // An edge only matters where geometry is still being drawn. If hideAmount
    // already equals 1 we've discarded, so clamp edgeAmount by visible region.
    r.edgeAmount = min(r.edgeAmount, 1.0 - r.hideAmount);
    return r;
}

// Convenience: call this in the fragment shader. Discards fully-hidden fragments.
// Returns the evaluation so the caller can use edgeAmount for intersection blending.
MeshSectionResult MeshSection_ApplyClip(float3 worldPos, float clipThreshold)
{
    MeshSectionResult r = MeshSection_Evaluate(worldPos);
    clip(clipThreshold - r.hideAmount);
    return r;
}

// -----------------------------------------------------------------------------
// Cap helpers. Used by the backface "cap" pass that fills in the hollow left by
// clipped-away front faces. Each fragment of a cap draw call is by definition
// inside at least one cutting area.
// -----------------------------------------------------------------------------

// Find which active area contains worldPos and has alpha < 1. Returns the area
// index, or -1 if none. When multiple areas contain the point, picks the one
// whose boundary is closest (smallest outward sdf = deepest interior).
int MeshSection_FindContainingArea(float3 worldPos, out float outAlpha)
{
    int best = -1;
    float bestDepth = 1e20;
    outAlpha = 1.0;

    int count = min(_MeshSectionAreaCount, MESH_SECTION_MAX_AREAS);

    UNITY_UNROLL
    for (int i = 0; i < MESH_SECTION_MAX_AREAS; i++)
    {
        if (i >= count)
        {
            break;
        }

        float alpha = _MeshSectionAreaParams[i].x;
        if (alpha >= 1.0)
        {
            continue;
        }

        float3 local = MeshSection_WorldToBoxLocal(worldPos, i);
        float sdf = MeshSection_BoxSDF(local, _MeshSectionAreaHalfExtents[i].xyz);

        if (sdf < 0.0 && sdf < bestDepth)
        {
            bestDepth = sdf;
            best = i;
            outAlpha = alpha;
        }
    }

    return best;
}

// Given a world position inside the box of area i, return the world-space normal
// of the closest box face. This is what a "cut surface" would actually face.
float3 MeshSection_BoxFaceNormal(float3 worldPos, int i)
{
    float3 local = MeshSection_WorldToBoxLocal(worldPos, i);
    float3 halfExtents = _MeshSectionAreaHalfExtents[i].xyz;
    float3 dist = halfExtents - abs(local);

    // Pick axis with smallest remaining distance to the face.
    float3 axisLocal;
    if (dist.x < dist.y && dist.x < dist.z)
    {
        axisLocal = float3(sign(local.x), 0, 0);
    }
    else if (dist.y < dist.z)
    {
        axisLocal = float3(0, sign(local.y), 0);
    }
    else
    {
        axisLocal = float3(0, 0, sign(local.z));
    }

    // Transform local axis back to world.
    return normalize(
        axisLocal.x * _MeshSectionAreaRight[i].xyz +
        axisLocal.y * _MeshSectionAreaUp[i].xyz +
        axisLocal.z * _MeshSectionAreaForward[i].xyz);
}

// Triplanar-style UVs for the cap: use the two axes perpendicular to the
// chosen face, in box-local space, so the texture aligns with the cut surface.
float2 MeshSection_BoxFaceUV(float3 worldPos, int i, float uvScale)
{
    float3 local = MeshSection_WorldToBoxLocal(worldPos, i);
    float3 halfExtents = _MeshSectionAreaHalfExtents[i].xyz;
    float3 dist = halfExtents - abs(local);

    if (dist.x < dist.y && dist.x < dist.z)
    {
        return local.zy * uvScale;
    }
    if (dist.y < dist.z)
    {
        return local.xz * uvScale;
    }
    return local.xy * uvScale;
}

#endif // MESH_SECTION_CORE_INCLUDED
