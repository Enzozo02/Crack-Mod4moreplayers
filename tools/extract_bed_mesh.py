#!/usr/bin/env python3
"""
Regenerates src/BedMeshData.cs from your own copy of CRACK.

Why: the quarters beds are static-batched at build time, so their MeshFilter points
to "Combined Mesh (root: scene) 11" (submesh 12) whose vertices are baked in WORLD
space and which is not readable at runtime. A cloned bed therefore renders nothing.
This script pulls that submesh out of CRACK_Data/level1, converts it back into the
bed's local space, and writes it as base64 arrays the plugin rebuilds at runtime.

Usage:  pip install UnityPy numpy
        python extract_bed_mesh.py "<CRACK install dir>" ../src/BedMeshData.cs
"""
import base64, sys
import numpy as np
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

COMBINED, SUBMESH = "Combined Mesh (root: scene) 11", 12

def quat_to_matrix(q):
    x, y, z, w = q["x"], q["y"], q["z"], q["w"]
    return np.array([[1-2*(y*y+z*z), 2*(x*y-z*w),   2*(x*z+y*w)],
                     [2*(x*y+z*w),   1-2*(x*x+z*z), 2*(y*z-x*w)],
                     [2*(x*z-y*w),   2*(y*z+x*w),   1-2*(x*x+y*y)]])

def main(game_dir, out):
    env = UnityPy.load(f"{game_dir}/CRACK_Data/level1")
    names, trs, go_tr, mesh = {}, {}, {}, None
    for o in env.objects:
        if o.type.name == "GameObject":
            names[o.path_id] = o.read_typetree()["m_Name"]
        elif o.type.name == "Transform":
            t = o.read_typetree(); trs[o.path_id] = t; go_tr[t["m_GameObject"]["m_PathID"]] = o.path_id
        elif o.type.name == "Mesh" and mesh is None:
            m = o.read()
            if m.m_Name == COMBINED: mesh = m
    assert mesh is not None, "combined mesh not found (game updated?)"

    def world(t):
        d = trs[t]
        p = np.array([d["m_LocalPosition"][k] for k in "xyz"])
        M = quat_to_matrix(d["m_LocalRotation"]) @ np.diag([d["m_LocalScale"][k] for k in "xyz"])
        f = d["m_Father"]["m_PathID"]
        if not f: return M, p
        PM, PP = world(f)
        return PM @ M, PM @ p + PP

    # The bed of the first quarters room ("Quarters/PF_QuartersRoom/Bed").
    bed = next(go_tr[g] for g, n in names.items() if n == "Bed"
               and names.get(trs[trs[go_tr[g]]["m_Father"]["m_PathID"]]["m_GameObject"]["m_PathID"]) == "PF_QuartersRoom")
    M, P = world(bed)
    R = M / np.linalg.norm(M, axis=0); Rinv = np.linalg.inv(R)

    h = MeshHandler(mesh); h.process()
    V = np.array(h.m_Vertices).reshape(-1, 3); N = np.array(h.m_Normals).reshape(-1, 3)
    T = np.array(h.m_Tangents).reshape(-1, 4); UV = np.array(h.m_UV0).reshape(-1, 2)
    sm = mesh.m_SubMeshes[SUBMESH]; isz = 2 if h.m_Use16BitIndices else 4
    tri = np.array(h.m_IndexBuffer)[sm.firstByte // isz: sm.firstByte // isz + sm.indexCount]
    used = np.unique(tri); remap = {v: i for i, v in enumerate(used)}

    pos = (np.linalg.inv(M) @ (V[used] - P).T).T.astype(np.float32)
    nrm = (Rinv @ N[used].T).T.astype(np.float32)
    tan = T[used].copy(); tan[:, :3] = (Rinv @ tan[:, :3].T).T; tan = tan.astype(np.float32)
    uv = UV[used].astype(np.float32)
    tris = np.array([remap[i] for i in tri], dtype=np.int32)
    frame_uv = np.median(uv[pos[:, 0] > 1.42], axis=0)   # headboard -> frame colour texel

    b = lambda a: base64.b64encode(a.tobytes()).decode()
    with open(out, "w") as f:
        f.write(f'''// Auto-extracted from CRACK level1 "{COMBINED}", submesh {SUBMESH} (quarters bed), bed-local space.
namespace CrackMorePlayers {{
static class BedMeshData {{
    public const string Positions = "{b(pos)}";
    public const string Normals = "{b(nrm)}";
    public const string Tangents = "{b(tan)}";
    public const string UV = "{b(uv)}";
    public const string Triangles = "{b(tris)}";
    public const float FrameU = {frame_uv[0]:.6f}f, FrameV = {frame_uv[1]:.6f}f;
}}}}
''')
    print(f"{out}: {len(pos)} vertices, {len(tris)//3} triangles")

if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
