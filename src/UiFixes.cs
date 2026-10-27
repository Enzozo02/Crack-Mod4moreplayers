// UI fixes for crews larger than 4:
//  * unique colours for every player (shift summary cards, graph lines, egg blips)
//  * Shift Complete "Team Performance" + dream lifetime board: auto-scrolling lists
//  * "Employee Productivity Ranking" monitor: auto-paging (1-4, 5-8, ...)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrackMorePlayers
{
    // ---- Colours -------------------------------------------------------------

    static class Palette
    {
        // Picked to stay distinct from the game's 4 crew colours (red, blue, green, orange)
        // and from each other. Used in order for players 5, 6, 7, 8 (the rest are spares).
        static readonly Color[] Candidates =
        {
            Hex(0xA855F7), // 5 purple
            Hex(0xFACC15), // 6 yellow
            Hex(0xF472B6), // 7 pink
            Hex(0x2DD4BF), // 8 teal
            Hex(0xA3E635), // lime
            Hex(0xF5F5F4), // white
            Hex(0x8B5CF6), // violet
            Hex(0x22D3EE), // cyan
            Hex(0xB45309), // brown
            Hex(0x94A3B8), // slate
            Hex(0xE11D48), // crimson
            Hex(0x1D4ED8), // deep blue
        };

        static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

        static float Dist(Color a, Color b)
        {
            float r = a.r - b.r, g = a.g - b.g, bl = a.b - b.b;
            return Mathf.Sqrt(r * r + g * g + bl * bl);
        }

        public static Color[] Extend(Color[] src, int need)
        {
            if (src == null || src.Length == 0 || src.Length >= need) return src;
            var list = new List<Color>(src);
            foreach (var minDist in new[] { 0.35f, 0.2f, 0f })
                foreach (var c in Candidates)
                {
                    if (list.Count >= need) return list.ToArray();
                    bool far = true;
                    foreach (var e in list) if (Dist(e, c) <= minDist) { far = false; break; }
                    if (far && !list.Contains(c)) list.Add(new Color(c.r, c.g, c.b, src[0].a));
                }
            for (int i = 0; list.Count < need; i++)   // safety net: golden-ratio hues
                list.Add(Color.HSVToRGB((i * 0.618034f) % 1f, 0.65f, 0.95f));
            return list.ToArray();
        }

        public static int Needed(int atLeast) => Math.Max(atLeast, Plugin.HardMax);
    }

    [HarmonyPatch(typeof(ShiftCompleteUI), "BuildRows")]
    static class ShiftCompleteUI_BuildRows
    {
        static readonly FieldInfo Colors = AccessTools.Field(typeof(ShiftCompleteUI), "_playerColors");
        static readonly FieldInfo Container = AccessTools.Field(typeof(ShiftCompleteUI), "_rowContainer");

        // Cards and graph lines use _playerColors[i % Length]: give it one colour per player.
        static void Prefix(ShiftCompleteUI __instance) =>
            Colors.SetValue(__instance, Palette.Extend((Color[])Colors.GetValue(__instance), Palette.Needed(0)));

        static void Postfix(ShiftCompleteUI __instance) =>
            AutoScroll.Attach((RectTransform)Container.GetValue(__instance));
    }

    [HarmonyPatch(typeof(PlayerBlips), "ColorForIndex")]
    static class PlayerBlips_ColorForIndex
    {
        static readonly FieldInfo Colors = AccessTools.Field(typeof(PlayerBlips), "playerColors");
        static void Prefix(PlayerBlips __instance, int i)
        {
            var c = (Color[])Colors.GetValue(__instance);
            if (c != null && c.Length < Palette.Needed(i + 1))
                Colors.SetValue(__instance, Palette.Extend(c, Palette.Needed(i + 1)));
        }
    }

    [HarmonyPatch(typeof(DreamUI), "BuildLifetimeBoard")]
    static class DreamUI_BuildLifetimeBoard
    {
        static readonly FieldInfo Content = AccessTools.Field(typeof(DreamUI), "_lifetimeContent");
        static void Postfix(DreamUI __instance) => AutoScroll.Attach((RectTransform)Content.GetValue(__instance));
    }

    // ---- Auto-scrolling list ------------------------------------------------
    // Clips a VerticalLayoutGroup to its own rect and slides its content through
    // padding.top: follows new rows while they pop in, then ping-pongs top <-> bottom.

    class AutoScroll : MonoBehaviour
    {
        const float FollowSpeed = 260f, CruiseSpeed = 70f, Hold = 2.5f, FollowWindow = 1.2f;

        VerticalLayoutGroup _vlg;
        RectTransform _rt;
        int _basePad, _lastCount;
        float _offset, _lastGrowth, _holdUntil;
        bool _goingDown = true, _aligned;

        public static void Attach(RectTransform rt)
        {
            if (rt == null || rt.GetComponent<VerticalLayoutGroup>() == null) return;
            if (rt.GetComponent<AutoScroll>() == null) rt.gameObject.AddComponent<AutoScroll>();
        }

        void Awake()
        {
            _rt = (RectTransform)transform;
            _vlg = GetComponent<VerticalLayoutGroup>();
            _basePad = _vlg.padding.top;
            var mask = GetComponent<RectMask2D>() ?? gameObject.AddComponent<RectMask2D>();
            mask.padding = new Vector4(0f, 0f, 0f, Mathf.Max(0, _basePad - 6)); // keep rows under the header
        }

        void OnDisable()
        {
            _offset = 0f; _lastCount = 0;
            if (_vlg != null) { _vlg.padding.top = _basePad; LayoutRebuilder.MarkLayoutForRebuild(_rt); }
        }

        int ActiveRows()
        {
            int n = 0;
            for (int i = 0; i < transform.childCount; i++) if (transform.GetChild(i).gameObject.activeSelf) n++;
            return n;
        }

        void LateUpdate()
        {
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            // Height the rows need, independent of our current scroll offset.
            float content = _vlg.preferredHeight - _vlg.padding.top + _basePad;
            float overflow = Mathf.Max(0f, content - _rt.rect.height);

            if (overflow > 1f && !_aligned)
            {
                // Lists anchored to the bottom/middle would grow upwards: pin them to the top.
                var a = _vlg.childAlignment;
                if (a == TextAnchor.LowerLeft || a == TextAnchor.MiddleLeft) _vlg.childAlignment = TextAnchor.UpperLeft;
                else if (a == TextAnchor.LowerCenter || a == TextAnchor.MiddleCenter) _vlg.childAlignment = TextAnchor.UpperCenter;
                else if (a == TextAnchor.LowerRight || a == TextAnchor.MiddleRight) _vlg.childAlignment = TextAnchor.UpperRight;
                _aligned = true;
            }

            int count = ActiveRows();
            if (count > _lastCount) { _lastGrowth = now; _goingDown = true; _holdUntil = now + FollowWindow + Hold; }
            _lastCount = count;

            float target, speed;
            if (overflow <= 1f) { target = 0f; speed = FollowSpeed; }
            else if (now - _lastGrowth < FollowWindow) { target = overflow; speed = FollowSpeed; }  // follow the newest row
            else
            {
                target = _goingDown ? overflow : 0f; speed = CruiseSpeed;
                if (now < _holdUntil) target = _offset;                                        // pause at an end
                else if (Mathf.Abs(_offset - target) < 0.5f) { _goingDown = !_goingDown; _holdUntil = now + Hold; }
            }

            float next = Mathf.MoveTowards(_offset, Mathf.Min(target, overflow), speed * dt);
            if (Mathf.Abs(next - _offset) > 0.01f || _vlg.padding.top != _basePad - Mathf.RoundToInt(next))
            {
                _offset = next;
                _vlg.padding.top = _basePad - Mathf.RoundToInt(_offset);
                LayoutRebuilder.MarkLayoutForRebuild(_rt);
            }
        }
    }

    // ---- Ranking monitors: pages of 4 ---------------------------------------
    // Both lobby monitors (Productivity = EfficiencyDisplayApp, Balance = BalanceDisplayApp)
    // share the same layout: rowContainer, _rows, a SyncList "standings" and maxRows.

    [HarmonyPatch]
    static class RankingApp_Awake
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(EfficiencyDisplayApp), "Awake");
            yield return AccessTools.Method(typeof(BalanceDisplayApp), "Awake");
        }
        static void Postfix(Component __instance)
        {
            var maxRows = AccessTools.Field(__instance.GetType(), "maxRows");
            if (maxRows != null && (int)maxRows.GetValue(__instance) < Plugin.HardMax)
                maxRows.SetValue(__instance, Plugin.HardMax);
            if (__instance.GetComponent<RankingPager>() == null)
                __instance.gameObject.AddComponent<RankingPager>().App = __instance;
        }
    }

    [HarmonyPatch]
    static class RankingApp_Rebuild
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(EfficiencyDisplayApp), "Rebuild");
            yield return AccessTools.Method(typeof(BalanceDisplayApp), "Rebuild");
        }
        static void Postfix(Component __instance) => __instance.GetComponent<RankingPager>()?.Apply();
    }

    class RankingPager : MonoBehaviour
    {
        const int PageSize = 4;
        const float PageSeconds = 6f;

        Component _app;
        FieldInfo RowsF, StandingsF, ContainerF;
        public Component App
        {
            get => _app;
            set
            {
                _app = value;
                var t = value.GetType();
                RowsF = AccessTools.Field(t, "_rows");
                StandingsF = AccessTools.Field(t, "standings");
                ContainerF = AccessTools.Field(t, "rowContainer");
            }
        }
        int _page;
        float _timer;
        TMP_Text _indicator;

        int Count()
        {
            var s = StandingsF.GetValue(App);
            return s == null ? 0 : (int)AccessTools.Property(s.GetType(), "Count").GetValue(s);
        }

        void Update()
        {
            if (App == null) return;
            int pages = Mathf.Max(1, Mathf.CeilToInt(Count() / (float)PageSize));
            if (pages <= 1) { if (_page != 0) { _page = 0; Apply(); } return; }
            _timer += Time.unscaledDeltaTime;
            if (_timer >= PageSeconds) { _timer = 0f; _page = (_page + 1) % pages; Apply(); }
        }

        public void Apply()
        {
            if (App == null) return;
            int count = Count();
            int pages = Mathf.Max(1, Mathf.CeilToInt(count / (float)PageSize));
            if (_page >= pages) _page = 0;
            if (RowsF.GetValue(App) is IList rows)
                for (int i = 0; i < rows.Count; i++)
                    if (rows[i] is Component row && row != null)
                        row.gameObject.SetActive(i < count && i / PageSize == _page);

            var ind = Indicator();
            if (ind != null)
            {
                ind.gameObject.SetActive(pages > 1);
                ind.text = (_page + 1) + "/" + pages;
            }
        }

        // Small "1/2" label in the bottom-right corner, styled like the footer text.
        TMP_Text Indicator()
        {
            if (_indicator != null) return _indicator;
            var container = ContainerF.GetValue(App) as Transform;
            TMP_Text footer = null;
            foreach (var t in App.GetComponentsInChildren<TMP_Text>(true))
            {
                if (container != null && t.transform.IsChildOf(container)) continue;
                if (footer == null || t.rectTransform.anchoredPosition.y < footer.rectTransform.anchoredPosition.y) footer = t;
            }
            if (footer == null) return null;
            var go = Instantiate(footer.gameObject, footer.transform.parent, false);
            go.name = "PageIndicator";
            foreach (var lt in go.GetComponents<LocalizedText>()) DestroyImmediate(lt);
            _indicator = go.GetComponent<TMP_Text>();
            _indicator.enableAutoSizing = false;
            _indicator.fontSize = Mathf.Max(9f, footer.fontSize * 0.7f);
            _indicator.alignment = TextAlignmentOptions.Right;
            var rt = _indicator.rectTransform;
            rt.anchorMin = rt.anchorMax = footer.rectTransform.anchorMin;
            rt.sizeDelta = new Vector2(50f, 14f);
            // In the free strip between the last row and the bottom dashed line, right-aligned.
            float x = footer.rectTransform.anchoredPosition.x + footer.rectTransform.rect.width / 2f - 27f;
            float y = footer.rectTransform.anchoredPosition.y + footer.rectTransform.rect.height / 2f + 25f;
            if (container is RectTransform c && c.parent == rt.parent)
                y = c.anchoredPosition.y - c.rect.height * c.pivot.y - 8f;
            rt.anchoredPosition = new Vector2(x, y);
            return _indicator;
        }
    }
}
