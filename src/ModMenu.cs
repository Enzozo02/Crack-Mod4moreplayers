// "MODS" entry in CRACK's main menu: lists every loaded BepInEx plugin with
// its image, title, version, author and description.
//
// Metadata convention (any mod can use it), looked up next to the plugin DLL:
//   <DllName>.png  + <DllName>.json        e.g. CrackMorePlayers.png / .json
//   icon.png       + manifest.json         (Thunderstore layout, mod in its own folder)
// JSON keys: "title" (or "name"), "author", "description".
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CrackMorePlayers
{
    class ModEntry
    {
        public string Title, Version, Guid, Author, Description;
        public Texture2D Icon;
    }

    [HarmonyPatch(typeof(MainMenuController), "Awake")]
    static class MainMenuController_Awake
    {
        static void Postfix(MainMenuController __instance)
        {
            try { ModMenu.Build(__instance); }
            catch (Exception e) { Plugin.Log.LogError("Mods menu: " + e); }
        }
    }

    static class ModMenu
    {
        static readonly Color CardColor = new Color(1f, 1f, 1f, 0.05f);
        static readonly Color MetaColor = new Color(1f, 1f, 1f, 0.55f);

        static T Get<T>(object o, string field) => (T)AccessTools.Field(o.GetType(), field).GetValue(o);

        public static void Build(MainMenuController mm)
        {
            var lbPanel = Get<CanvasGroup>(mm, "_leaderboardsPanel");
            var lbButton = Get<GameObject>(mm, "_leaderboardsButton");
            if (lbPanel == null || lbButton == null) { Plugin.Log.LogWarning("Mods menu: leaderboards UI not found"); return; }

            _built = false;   // the menu scene is rebuilt every time we come back to it
            var panel = BuildPanel(lbPanel);
            BuildButton(mm, lbButton, panel);

            // Register the panel so the menu hides it when another one is shown.
            var panels = new List<CanvasGroup>(Get<CanvasGroup[]>(mm, "_panels")) { panel };
            AccessTools.Field(typeof(MainMenuController), "_panels").SetValue(mm, panels.ToArray());
        }

        // ---- Button ---------------------------------------------------------

        static void BuildButton(MainMenuController mm, GameObject lbButton, CanvasGroup panel)
        {
            // Climb to the "Holder" that sits directly in the vertical layout.
            var holder = lbButton.transform;
            while (holder.parent != null && holder.parent.GetComponent<VerticalLayoutGroup>() == null)
                holder = holder.parent;
            if (holder.parent == null) holder = lbButton.transform;

            var go = UnityEngine.Object.Instantiate(holder.gameObject, holder.parent, false);
            go.name = "Holder (Mods)";
            go.transform.SetSiblingIndex(holder.GetSiblingIndex() + 1);
            go.SetActive(true);
            foreach (var lt in go.GetComponentsInChildren<LocalizedText>(true)) UnityEngine.Object.DestroyImmediate(lt);

            // One more row in the menu: nudge the column up so it stays on screen.
            if (holder.parent is RectTransform col) col.anchoredPosition += new Vector2(0f, 50f);

            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
                if (t.gameObject.activeSelf) { t.text = "MODS"; break; }

            var icon = FindChild(go.transform, "Icon");
            var img = icon != null ? icon.GetComponent<Image>() : null;
            if (img != null)
            {
                img.sprite = ToSprite(LoadPng(Convert.FromBase64String(ModsIconData.Png)));
                img.preserveAspect = true;
            }

            var shatter = go.GetComponentInChildren<UIShatter>(true);
            if (shatter == null) { Plugin.Log.LogWarning("Mods menu: button has no UIShatter"); return; }
            // The clone still carries the Leaderboards persistent call: replace the event.
            AccessTools.Field(typeof(UIShatter), "_onShatter").SetValue(shatter, new UnityEvent());
            var show = AccessTools.Method(typeof(PanelMenu), "Show");
            shatter.AddShatterListener(() =>
            {
                Refresh(panel);
                show.Invoke(mm, new object[] { panel });
            });
        }

        // ---- Panel ----------------------------------------------------------

        static Transform _content;
        static TMP_Text _titleText, _bodyTemplate;

        static CanvasGroup BuildPanel(CanvasGroup lbPanel)
        {
            var go = UnityEngine.Object.Instantiate(lbPanel.gameObject, lbPanel.transform.parent, false);
            go.name = "Mods";
            go.transform.SetSiblingIndex(lbPanel.transform.GetSiblingIndex() + 1);

            // Strip leaderboard behaviour; keep frame, title, scroll view, back button.
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var n = mb.GetType().Name;
                if (n == "LeaderboardPanel" || n == "LeaderboardRow" || n == "LeaderboardTabButton")
                    UnityEngine.Object.DestroyImmediate(mb);
            }

            var list = go.transform.Find("List");
            var title = list.Find("Title");
            foreach (var lt in title.GetComponents<LocalizedText>()) UnityEngine.Object.DestroyImmediate(lt);
            _titleText = title.GetComponent<TMP_Text>();

            // Body text style comes from the leaderboard "Name" column.
            var nameCol = FindChild(list, "Name");
            if (nameCol != null)
            {
                var tmpl = UnityEngine.Object.Instantiate(nameCol.gameObject, go.transform, false);
                tmpl.name = "BodyTemplate";
                foreach (var le in tmpl.GetComponents<LayoutElement>()) UnityEngine.Object.DestroyImmediate(le);
                tmpl.SetActive(false);
                _bodyTemplate = tmpl.GetComponent<TMP_Text>();
            }

            foreach (var n in new[] { "Tabs", "Options", "YourRow" })
            {
                var c = list.Find(n);
                if (c != null) UnityEngine.Object.DestroyImmediate(c.gameObject);
            }

            // Scroll view takes the space freed by tabs/options.
            var scroll = (RectTransform)list.Find("Scroll View");
            scroll.offsetMin = new Vector2(30f, 30f);
            scroll.offsetMax = new Vector2(-30f, -100f);

            var viewport = scroll.Find("Viewport");
            var status = viewport.Find("Status");
            if (status != null) UnityEngine.Object.DestroyImmediate(status.gameObject);
            _content = viewport.Find("Content");
            for (int i = _content.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(_content.GetChild(i).gameObject);

            var vlg = _content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 16;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childControlHeight = true; vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;

            return go.GetComponent<CanvasGroup>();
        }

        static bool _built;

        static void Refresh(CanvasGroup panel)
        {
            var mods = Collect();
            if (_titleText != null) _titleText.text = "MODS (" + mods.Count + ")";
            if (_built || _content == null) return;
            foreach (var m in mods) Card(m);
            _built = true;
        }

        static void Card(ModEntry m)
        {
            var card = new GameObject("Mod: " + m.Guid, typeof(RectTransform));
            card.transform.SetParent(_content, false);
            card.AddComponent<Image>().color = CardColor;
            var h = card.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(20, 20, 20, 20);
            h.spacing = 24;
            h.childAlignment = TextAnchor.UpperLeft;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(card.transform, false);
            var img = iconGo.AddComponent<Image>();
            img.preserveAspect = true;
            if (m.Icon != null) img.sprite = ToSprite(m.Icon);
            else img.color = new Color(1f, 1f, 1f, 0.12f);
            var ile = iconGo.AddComponent<LayoutElement>();
            ile.preferredWidth = ile.minWidth = 160;
            ile.preferredHeight = ile.minHeight = 160;

            var info = new GameObject("Info", typeof(RectTransform));
            info.transform.SetParent(card.transform, false);
            var v = info.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            info.AddComponent<LayoutElement>().flexibleWidth = 1;

            Text(info.transform, m.Title, 40, FontStyles.Bold, Color.white);
            var meta = "v" + m.Version + (string.IsNullOrEmpty(m.Author) ? "" : "  •  " + m.Author) + "  •  " + m.Guid;
            Text(info.transform, meta, 22, FontStyles.Normal, MetaColor);
            Text(info.transform, string.IsNullOrEmpty(m.Description) ? "No description." : m.Description,
                 26, FontStyles.Normal, new Color(1f, 1f, 1f, 0.9f));
        }

        static void Text(Transform parent, string s, float size, FontStyles style, Color color)
        {
            GameObject go;
            if (_bodyTemplate != null)
            {
                go = UnityEngine.Object.Instantiate(_bodyTemplate.gameObject, parent, false);
                go.SetActive(true);
            }
            else
            {
                go = new GameObject("Text", typeof(RectTransform));
                go.transform.SetParent(parent, false);
                go.AddComponent<TextMeshProUGUI>();
            }
            var t = go.GetComponent<TMP_Text>();
            t.text = s;
            t.enableAutoSizing = false;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
        }

        // ---- Mod discovery --------------------------------------------------

        static List<ModEntry> Collect()
        {
            var res = new List<ModEntry>();
            foreach (var kv in Chainloader.PluginInfos)
            {
                var pi = kv.Value;
                var e = new ModEntry
                {
                    Guid = pi.Metadata.GUID,
                    Title = pi.Metadata.Name,
                    Version = pi.Metadata.Version.ToString(),
                };
                try { ReadSidecars(pi.Location, e); }
                catch (Exception ex) { Plugin.Log.LogWarning($"Mods menu: metadata of {e.Guid}: {ex.Message}"); }
                res.Add(e);
            }
            res.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));
            return res;
        }

        static void ReadSidecars(string dll, ModEntry e)
        {
            if (string.IsNullOrEmpty(dll)) return;
            var dir = Path.GetDirectoryName(dll);
            var stem = Path.Combine(dir, Path.GetFileNameWithoutExtension(dll));
            bool ownFolder = !string.Equals(Path.GetFileName(dir), "plugins", StringComparison.OrdinalIgnoreCase);

            string json = First(stem + ".json", ownFolder ? Path.Combine(dir, "manifest.json") : null);
            string png = First(stem + ".png", ownFolder ? Path.Combine(dir, "icon.png") : null);

            if (json != null)
            {
                var o = JObject.Parse(File.ReadAllText(json));
                var title = (string)(o["title"] ?? o["name"]);
                if (!string.IsNullOrEmpty(title)) e.Title = title.Replace('_', ' ');
                e.Author = (string)(o["author"] ?? o["author_name"]);
                e.Description = (string)o["description"];
            }
            if (png != null) e.Icon = LoadPng(File.ReadAllBytes(png));
        }

        static string First(params string[] paths)
        {
            foreach (var p in paths) if (p != null && File.Exists(p)) return p;
            return null;
        }

        // ---- Helpers --------------------------------------------------------

        static Texture2D LoadPng(byte[] bytes)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            ImageConversion.LoadImage(tex, bytes);
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        static Sprite ToSprite(Texture2D t) =>
            Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);

        static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
