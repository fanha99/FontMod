using FontMod.Shared;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;

namespace FontMod.FontSwap;

// Doc thiet lap build theo TUNG font tu fontBuild.json (key = ten file TTF bo duoi).
// Neu khong co file / khong co entry -> dung mac dinh (Bold / 1.05) nhu cu.
public static class FontBuildConfig
{
    public const string FileName = "fontBuild.json";
    private static Dictionary<string, FontBuildEntry> _map;

    private static void EnsureLoaded()
    {
        if (_map != null) return;

        _map = new Dictionary<string, FontBuildEntry>();

        try
        {
            var path = Path.Combine(Main.ModEntry.Path, FileName);
            if (!File.Exists(path)) return;

            // fontBuild.json o dang MANG cap [{ "Key":.., "Value":{..} }] (giong fontMappings.json).
            // KHONG deserialize thang vao Dictionary<,> (Newtonsoft can JSON object {} -> gap array se
            // nem loi -> roi ve mac dinh Bold cho moi font, bug 2026-06-04). Cung KHONG de Newtonsoft
            // tu bind kieu (ban Newtonsoft cua game co converter Dictionary/reference rieng, kho luong).
            // Parse cay JSON tho bang JArray roi tu rut tung field -> mien nhiem moi quirk.
            var arr = JArray.Parse(File.ReadAllText(path));
            foreach (var item in arr)
            {
                var key = (string)item["Key"];
                var v = item["Value"] as JObject;
                if (string.IsNullOrEmpty(key) || v == null) continue;

                var d = FontBuildEntry.Default();
                var e = new FontBuildEntry
                {
                    Style = (string)v["Style"] ?? d.Style,
                    SizeReduction = v["SizeReduction"] != null ? (float)v["SizeReduction"] : d.SizeReduction,
                    StyleMod = v["StyleMod"] != null ? (float)v["StyleMod"] : d.StyleMod,
                    BoldWeight = v["BoldWeight"] != null ? (float)v["BoldWeight"] : d.BoldWeight,
                    NormalWeight = v["NormalWeight"] != null ? (float)v["NormalWeight"] : d.NormalWeight,
                };
                _map[key] = e;
            }

            Main.Logger.Log($"fontBuild.json: nap {_map.Count} font config.");
        }
        catch (System.Exception e)
        {
            // KHONG de loi doc config lam hong viec dung font -> dung mac dinh.
            Main.Logger.Error("Doc fontBuild.json loi, dung mac dinh: " + e.Message);
            _map = new Dictionary<string, FontBuildEntry>();
        }
    }

    public static FontBuildEntry Get(string fontName)
    {
        EnsureLoaded();

        if (!string.IsNullOrEmpty(fontName) && _map.TryGetValue(fontName, out var entry) && entry != null)
            return entry;

        return FontBuildEntry.Default();
    }
}
