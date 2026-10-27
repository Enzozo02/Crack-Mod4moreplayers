// CRACK - MorePlayers
// Raises the 4-player cap (default 8). Rooms become double rooms:
// players 5-8 get a bunk bed above the bed of players 1-4 and share
// their room, badge door and wardrobe (player 5 -> room 1, etc.).
//
// Every player in the lobby must install this mod: CRACK flags BepInEx
// lobbies as "modded" and refuses to mix modded and vanilla players.
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Mirror;

namespace CrackMorePlayers
{
    [BepInPlugin(Guid, "MorePlayers", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "nezorspy.crack.moreplayers";
        public const string Version = "1.0.0";

        // The game ships with 4 player quarters (rooms, beds, wardrobes, doors).
        public const int VanillaSlots = 4;

        internal static ConfigEntry<int> MaxPlayers;
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            MaxPlayers = Config.Bind("General", "MaxPlayers", 8,
                new ConfigDescription(
                    "Maximum players in a lobby (host's value is the one that counts).",
                    new AcceptableValueRange<int>(1, 8)));

            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo($"MorePlayers {Version} loaded, MaxPlayers = {MaxPlayers.Value}");
        }

        internal static void ApplyMax(NetworkManager nm)
        {
            if (nm == null) return;
            if (nm.maxConnections != MaxPlayers.Value)
            {
                Log.LogInfo($"maxConnections {nm.maxConnections} -> {MaxPlayers.Value}");
                nm.maxConnections = MaxPlayers.Value;
            }
        }

        // Maps any player index onto one of the 4 vanilla quarters.
        public static int Home(int index)
        {
            return index < 0 ? index : index % VanillaSlots;
        }
    }

    // ---- 1. Network / Steam lobby size -------------------------------------

    [HarmonyPatch(typeof(MyNetworkManager), "Awake")]
    static class MyNetworkManager_Awake
    {
        static void Postfix(MyNetworkManager __instance) => Plugin.ApplyMax(__instance);
    }

    [HarmonyPatch(typeof(MyNetworkManager), "Start")]
    static class MyNetworkManager_Start
    {
        static void Postfix(MyNetworkManager __instance) => Plugin.ApplyMax(__instance);
    }

    [HarmonyPatch(typeof(MyNetworkManager), "Host")]
    static class MyNetworkManager_Host
    {
        static void Prefix(MyNetworkManager __instance) => Plugin.ApplyMax(__instance);
    }

    [HarmonyPatch(typeof(SteamLobby), "HostLobby")]
    static class SteamLobby_HostLobby
    {
        static void Prefix() => Plugin.ApplyMax(NetworkManager.singleton);
    }

    // ---- 2. Player slot allocation (hard-coded "i < 4" in the game) --------

    [HarmonyPatch(typeof(GameManager), "GetAvailablePlayerIndex")]
    static class GameManager_GetAvailablePlayerIndex
    {
        static bool Prefix(GameManager __instance, ref int __result)
        {
            var players = __instance.players;
            int max = Plugin.MaxPlayers.Value;
            for (int i = 0; i < max; i++)
            {
                bool taken = false;
                if (players != null)
                {
                    foreach (var p in players)
                    {
                        if (p != null && p.index == i) { taken = true; break; }
                    }
                }
                if (!taken) { __result = i; return false; }
            }
            __result = -1;
            return false;
        }
    }

    // ---- 3. Quarters lookups: player 5+ reuse rooms/beds 1-4 ---------------

    [HarmonyPatch(typeof(QuartersRoom), "Get")]
    static class QuartersRoom_Get
    {
        static void Prefix(ref int playerIndex) => playerIndex = Plugin.Home(playerIndex);
    }

    [HarmonyPatch(typeof(Bed), "GetForPlayer")]
    static class Bed_GetForPlayer
    {
        // Use the player's own bunk if it exists, otherwise fall back to the room's bed.
        static void Prefix(ref int playerIndex)
        {
            if (playerIndex >= Plugin.VanillaSlots && !Bunks.Beds().ContainsKey(playerIndex))
                playerIndex = Plugin.Home(playerIndex);
        }
    }

    // ---- 3b. Double rooms: a bunk bed above each vanilla bed ---------------

    static class Bunks
    {
        public const float Height = 1.55f;   // bunk mattress above the lower bed
        static readonly FieldInfo PlayerIndexF = AccessTools.Field(typeof(Bed), "_playerIndex");
        static int PlayerIndex(Bed b) => (int)PlayerIndexF.GetValue(b);
        static readonly FieldInfo ByIndex = AccessTools.Field(typeof(Bed), "_byPlayerIndex");

        public static Dictionary<int, Bed> Beds() => (Dictionary<int, Bed>)ByIndex.GetValue(null);

        static bool _cloning;

        public static void TryCreate(Bed lower)
        {
            if (_cloning) return;
            int idx = PlayerIndex(lower);
            if (idx < 0 || idx >= Plugin.VanillaSlots) return;
            int bunkIdx = idx + Plugin.VanillaSlots;
            if (bunkIdx >= Plugin.MaxPlayers.Value) return;
            if (Beds().ContainsKey(bunkIdx)) return;

            Bed upper;
            _cloning = true;
            try
            {
                var go = UnityEngine.Object.Instantiate(lower.gameObject, lower.transform.parent);
                go.name = lower.gameObject.name + " (Bunk)";
                go.transform.localRotation = lower.transform.localRotation;
                go.transform.localScale = lower.transform.localScale;
                go.transform.position = lower.transform.position + UnityEngine.Vector3.up * Height;
                upper = go.GetComponent<Bed>();
            }
            finally { _cloning = false; }

            // The clone's Awake registered itself under the lower bed's index: fix the registry.
            PlayerIndexF.SetValue(upper, bunkIdx);
            var beds = Beds();
            beds[idx] = lower;
            beds[bunkIdx] = upper;

            BuildVisual(lower, upper);
            Plugin.Log.LogInfo($"Bunk bed created: room {idx + 1} -> player {bunkIdx + 1}");
        }

        // The quarters beds are static-batched: a clone of their renderer draws nothing.
        // Rebuild the bed from geometry extracted offline (BedMeshData) + 4 posts.
        static UnityEngine.Mesh _bedMesh, _postMesh;

        static T[] Arr<T>(string b64, int stride, System.Func<float[], int, T> make)
        {
            var bytes = System.Convert.FromBase64String(b64);
            var f = new float[bytes.Length / 4];
            System.Buffer.BlockCopy(bytes, 0, f, 0, bytes.Length);
            var r = new T[f.Length / stride];
            for (int i = 0; i < r.Length; i++) r[i] = make(f, i * stride);
            return r;
        }

        static UnityEngine.Mesh BedMesh()
        {
            if (_bedMesh != null) return _bedMesh;
            var m = new UnityEngine.Mesh { name = "BunkBed" };
            m.vertices = Arr(BedMeshData.Positions, 3, (f, i) => new UnityEngine.Vector3(f[i], f[i + 1], f[i + 2]));
            m.normals = Arr(BedMeshData.Normals, 3, (f, i) => new UnityEngine.Vector3(f[i], f[i + 1], f[i + 2]));
            m.tangents = Arr(BedMeshData.Tangents, 4, (f, i) => new UnityEngine.Vector4(f[i], f[i + 1], f[i + 2], f[i + 3]));
            m.uv = Arr(BedMeshData.UV, 2, (f, i) => new UnityEngine.Vector2(f[i], f[i + 1]));
            var tb = System.Convert.FromBase64String(BedMeshData.Triangles);
            var tris = new int[tb.Length / 4];
            System.Buffer.BlockCopy(tb, 0, tris, 0, tb.Length);
            m.triangles = tris;
            m.RecalculateBounds();
            return _bedMesh = m;
        }

        // Unit cube whose UVs all point at one texel of the bed frame -> same colour as the frame.
        static UnityEngine.Mesh PostMesh()
        {
            if (_postMesh != null) return _postMesh;
            var tmp = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
            var m = UnityEngine.Object.Instantiate(tmp.GetComponent<UnityEngine.MeshFilter>().sharedMesh);
            UnityEngine.Object.Destroy(tmp);
            var uv = new UnityEngine.Vector2[m.vertexCount];
            for (int i = 0; i < uv.Length; i++) uv[i] = new UnityEngine.Vector2(BedMeshData.FrameU, BedMeshData.FrameV);
            m.uv = uv;
            m.name = "BunkPost";
            return _postMesh = m;
        }

        static UnityEngine.GameObject MeshObject(string name, UnityEngine.Transform parent,
            UnityEngine.Mesh mesh, UnityEngine.Material mat)
        {
            var go = new UnityEngine.GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<UnityEngine.MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<UnityEngine.MeshRenderer>();
            r.sharedMaterial = mat;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
            return go;
        }

        static void BuildVisual(Bed lower, Bed upper)
        {
            var lowerRenderer = lower.GetComponent<UnityEngine.MeshRenderer>();
            var mat = lowerRenderer != null ? lowerRenderer.sharedMaterial : null;

            // Drop the dead static-batched renderer on the clone, draw our own mesh instead.
            var deadR = upper.GetComponent<UnityEngine.MeshRenderer>();
            var deadF = upper.GetComponent<UnityEngine.MeshFilter>();
            if (deadR != null) UnityEngine.Object.DestroyImmediate(deadR);
            if (deadF != null) UnityEngine.Object.DestroyImmediate(deadF);
            MeshObject("BunkBedMesh", upper.transform, BedMesh(), mat);

            // Posts on the lower bed's legs, up to the top of the upper headboard.
            const float legX = 1.53f, legZ = 0.81f, bottom = -0.29f, topOfHead = 0.80f, w = 0.11f;
            float top = topOfHead + Height;
            var root = new UnityEngine.GameObject("BunkPosts");
            root.transform.SetParent(lower.transform, false);
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                {
                    var p = MeshObject("Post", root.transform, PostMesh(), mat);
                    p.transform.localPosition = new UnityEngine.Vector3(sx * legX, (bottom + top) / 2f, sz * legZ);
                    p.transform.localScale = new UnityEngine.Vector3(w, top - bottom, w);
                }
        }
    }

    [HarmonyPatch(typeof(Bed), "Awake")]
    static class Bed_Awake
    {
        static void Postfix(Bed __instance) => Bunks.TryCreate(__instance);
    }

    // Players 5-8 spawn on the upper bunk instead of on top of their roommate.
    [HarmonyPatch(typeof(GameManager), "GetSpawnPoint")]
    static class GameManager_GetSpawnPoint
    {
        static void Postfix(int playerIndex,
            ref System.ValueTuple<UnityEngine.Vector3, UnityEngine.Quaternion> __result)
        {
            if (playerIndex >= Plugin.VanillaSlots && Bunks.Beds().ContainsKey(playerIndex))
                __result.Item1 += UnityEngine.Vector3.up * Bunks.Height;
        }
    }

    // Door name plate shows both roommates.
    [HarmonyPatch(typeof(QuartersRoom), "Update")]
    static class QuartersRoom_Update
    {
        static readonly FieldInfo IdxF = AccessTools.Field(typeof(QuartersRoom), "_playerIndex");
        static readonly FieldInfo LabelF = AccessTools.Field(typeof(QuartersRoom), "_nameLabel");
        static readonly FieldInfo LastF = AccessTools.Field(typeof(QuartersRoom), "_lastDisplayedName");

        static void Postfix(QuartersRoom __instance)
        {
            var label = (TMPro.TMP_Text)LabelF.GetValue(__instance);
            int idx = (int)IdxF.GetValue(__instance);
            var gm = GameManager.instance;
            if (label == null || gm == null) return;
            var mate = gm.GetPlayerControllerFromIndex(idx + Plugin.VanillaSlots);
            if (mate == null || string.IsNullOrEmpty(mate.steamName)) return;
            var owner = gm.GetPlayerControllerFromIndex(idx);
            string first = owner != null && !string.IsNullOrEmpty(owner.steamName) ? owner.steamName.ToUpper() : "-";
            string text = first + " & " + mate.steamName.ToUpper();
            if (text != (string)LastF.GetValue(__instance))
            {
                label.text = text;
                LastF.SetValue(__instance, text);
            }
        }
    }

    // ---- 4. Ownership checks comparing PlayerController.index directly -----
    // Inserts Plugin.Home(...) after every read of PlayerController.index.

    [HarmonyPatch]
    static class OwnershipChecks
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            string[][] targets =
            {
                new[] { "QuartersDoor", "ReaderHoldsAssignedPlayer" },
                new[] { "QuartersDoor", "HasUnauthorizedBadge" },
                new[] { "QuartersDoor", "ShowWaitForCrewTip" },
                new[] { "Wardrobe", "IsAssignedPlayer" },
                new[] { "Wardrobe", "ServerCycle" },
                new[] { "Wardrobe", "UserCode_CmdEquip__NetworkConnectionToClient" },
            };
            foreach (var t in targets)
            {
                var m = AccessTools.Method(AccessTools.TypeByName(t[0]), t[1]);
                if (m != null) yield return m;
                else Plugin.Log.LogWarning($"Method {t[0]}.{t[1]} not found (game updated?)");
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code, MethodBase original)
        {
            var indexField = AccessTools.Field(typeof(PlayerController), "index");
            var home = AccessTools.Method(typeof(Plugin), nameof(Plugin.Home));
            int n = 0;
            foreach (var ins in code)
            {
                yield return ins;
                if (ins.LoadsField(indexField))
                {
                    yield return new CodeInstruction(OpCodes.Call, home);
                    n++;
                }
            }
            if (n == 0) Plugin.Log.LogWarning($"No index read patched in {original.DeclaringType.Name}.{original.Name}");
        }
    }
}
