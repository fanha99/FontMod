using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using FontMod.Shared;

// ============================================================================
// FontPrebuild.exe  -  Pre-build atlas cache cho FontMod (Kingmaker), KHONG can mo game.
//
//   FontPrebuild --font "NotoSerifSC-VF" --style Bold   --size-reduction 1.05
//   FontPrebuild --font "UTM Dai Co Viet" --style Normal --size-reduction 1.0
//   FontPrebuild --all                 (build lai moi font theo fontBuild.json hien co)
//
// No goi thang TMPro_Plugin.dll (FreeType) render atlas, ghi <mod>\AtlasCache\<font>.atlas
// dung HET dinh dang + chu ky ma mod doc (FontBuildShared), va cap nhat fontBuild.json
// cho khop. Lan sau vao game: nap thang tu cache, khong build lai.
//
// LUU Y: tool phai chay x64 va co TMPro_Plugin.dll + Newtonsoft.Json.dll nam canh exe.
// ============================================================================

namespace FontPrebuildTool
{
    // ---- Khop CHINH XAC voi TMPro_FontPlugin.cs ----
    public enum FaceStyles { Normal, Bold, Italic, Bold_Italic, Outline, Bold_Sim }
    public enum RenderModes { HintedSmooth = 0, Smooth = 1, RasterHinted = 2, Raster = 3, DistanceField16 = 6, DistanceField32 = 7 }

    [StructLayout(LayoutKind.Sequential)]
    public struct FT_GlyphInfo
    {
        public int id;
        public float x, y, width, height, xOffset, yOffset, xAdvance;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FT_FaceInfo
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string name;
        public int pointSize;
        public int padding;
        public float lineHeight;
        public float baseline;
        public float ascender;
        public float descender;
        public float centerLine;
        public float underline;
        public float underlineThickness;
        public int characterCount;
        public int atlasWidth;
        public int atlasHeight;
    }

    static class Native
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetDllDirectory(string path);

        [DllImport("TMPro_Plugin")] public static extern int Initialize_FontEngine();
        [DllImport("TMPro_Plugin")] public static extern int Destroy_FontEngine();
        [DllImport("TMPro_Plugin")] public static extern int Load_TrueType_Font(string fontPath);
        [DllImport("TMPro_Plugin")] public static extern int FT_Size_Font(int fontSize);
        [DllImport("TMPro_Plugin")]
        public static extern int Render_Characters(
            byte[] buffer, int buffer_width, int buffer_height, int character_padding,
            int[] asc_set, int char_count, FaceStyles style, float style_mod, bool autoSize,
            RenderModes renderMode, int method, ref FT_FaceInfo fontData,
            // [In, Out]: native dien mang glyph TAI CHO. Duoi .NET Framework (khac Mono
            // cua game) mang KHONG tu copy nguoc -> thieu attribute nay thi glyph ve toan 0
            // -> cache hong (mat toa do glyph). Bat buoc phai co.
            [In, Out] FT_GlyphInfo[] Output);
    }

    class Program
    {
        const string DefaultModDir = @"F:\Games\PathfinderKingmaker\Mods\FontMod";

        static int Main(string[] args)
        {
            try
            {
                // Cho native loader tim TMPro_Plugin.dll canh exe.
                Native.SetDllDirectory(AppDomain.CurrentDomain.BaseDirectory);

                var a = ParseArgs(args);
                string modDir = a.TryGetValue("mod-dir", out var md) ? md : DefaultModDir;
                string fontsDir = Path.Combine(modDir, "Fonts");
                string cacheDir = Path.Combine(modDir, "AtlasCache");
                string buildJson = Path.Combine(modDir, "fontBuild.json");

                if (!Directory.Exists(modDir))
                {
                    Console.Error.WriteLine("Khong thay mod-dir: " + modDir);
                    return 2;
                }

                var config = LoadConfig(buildJson);

                List<string> targets;
                if (a.ContainsKey("all"))
                {
                    targets = new List<string>();
                    foreach (var ttf in Directory.GetFiles(fontsDir, "*.ttf"))
                        targets.Add(Path.GetFileNameWithoutExtension(ttf));
                    if (targets.Count == 0) { Console.Error.WriteLine("Fonts/ rong."); return 2; }
                }
                else
                {
                    if (!a.TryGetValue("font", out var fontArg) || string.IsNullOrEmpty(fontArg))
                    {
                        PrintUsage();
                        return 1;
                    }
                    string name = Path.GetFileNameWithoutExtension(fontArg);
                    targets = new List<string> { name };

                    // CLI ghi de config cho font nay (co default neu thieu).
                    var e = config.TryGetValue(name, out var cur) && cur != null ? cur : new FontBuildEntry();
                    if (a.TryGetValue("style", out var st)) e.Style = st;
                    if (a.TryGetValue("size-reduction", out var sr)) e.SizeReduction = float.Parse(sr, System.Globalization.CultureInfo.InvariantCulture);
                    if (a.TryGetValue("style-mod", out var sm)) e.StyleMod = float.Parse(sm, System.Globalization.CultureInfo.InvariantCulture);
                    if (a.TryGetValue("bold-weight", out var bw)) e.BoldWeight = float.Parse(bw, System.Globalization.CultureInfo.InvariantCulture);
                    if (a.TryGetValue("normal-weight", out var nw)) e.NormalWeight = float.Parse(nw, System.Globalization.CultureInfo.InvariantCulture);
                    config[name] = e;
                }

                Directory.CreateDirectory(cacheDir);

                if (Initialize_OK() != 0)
                {
                    Console.Error.WriteLine("Khong khoi tao duoc FreeType (TMPro_Plugin.dll). " +
                                            "Dam bao TMPro_Plugin.dll x64 nam canh FontPrebuild.exe.");
                    return 3;
                }

                int built = 0;
                foreach (var name in targets)
                {
                    string ttf = Path.Combine(fontsDir, name + ".ttf");
                    if (!File.Exists(ttf))
                    {
                        Console.Error.WriteLine("Bo qua (khong thay TTF): " + ttf);
                        continue;
                    }

                    var cfg = config.TryGetValue(name, out var c) && c != null ? c : new FontBuildEntry();
                    string outPath = Path.Combine(cacheDir, name + ".atlas");

                    Console.WriteLine($"Build: {name}  [Style={cfg.Style}, SizeReduction={cfg.SizeReduction}, StyleMod={cfg.StyleMod}, BoldWeight={cfg.BoldWeight}, NormalWeight={cfg.NormalWeight}]");
                    BuildOne(ttf, cfg, outPath);
                    config[name] = cfg; // dam bao co entry
                    built++;
                    Console.WriteLine("  -> " + outPath);
                }

                Native.Destroy_FontEngine();

                SaveConfig(buildJson, config);
                Console.WriteLine($"Xong. Da build {built} font. Da cap nhat {Path.GetFileName(buildJson)}.");
                return built > 0 ? 0 : 4;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("LOI: " + ex);
                return 10;
            }
        }

        static int Initialize_OK()
        {
            int err = Native.Initialize_FontEngine();
            if (err == 0xF0) err = 0; // da init roi
            return err;
        }

        static void BuildOne(string ttfPath, FontBuildEntry cfg, string outPath)
        {
            int err = Native.Load_TrueType_Font(ttfPath);
            if (err == 0xF1) err = 0;
            if (err != 0) throw new Exception("Load_TrueType_Font loi " + err + " (" + ttfPath + ")");

            err = Native.FT_Size_Font(FontBuildShared.FontSize);
            if (err != 0) throw new Exception("FT_Size_Font loi " + err);

            int[] charSet = FontBuildShared.ParseCharacterSet();
            int count = charSet.Length;

            var buffer = new byte[FontBuildShared.AtlasWidth * FontBuildShared.AtlasHeight];
            var face = new FT_FaceInfo();
            var glyphs = new FT_GlyphInfo[count];

            int styleInt = FontBuildShared.ParseStyleToInt(cfg.Style);
            var style = (FaceStyles)styleInt;

            // DistanceField16: stroke = styleMod * 16 (giong GenerateFontAtlas)
            float strokeSize = cfg.StyleMod * 16f;

            err = Native.Render_Characters(
                buffer, FontBuildShared.AtlasWidth, FontBuildShared.AtlasHeight, FontBuildShared.Padding,
                charSet, count, style, strokeSize, /*autoSize*/ true,
                RenderModes.DistanceField16, FontBuildShared.PackingMethod, ref face, glyphs);
            if (err != 0) throw new Exception("Render_Characters loi " + err);

            // Chu ky PHAI khop FontBuildShared.BuildSignature ben mod (style + styleMod, KHONG co sizeReduction).
            string signature = FontBuildShared.BuildSignature(ttfPath, styleInt, cfg.StyleMod);

            WriteAtlas(outPath, signature, face, glyphs, buffer);
        }

        // Ghi dung HET dinh dang SaveAtlasCache cua mod.
        static void WriteAtlas(string path, string signature, FT_FaceInfo f, FT_GlyphInfo[] glyphs, byte[] buffer)
        {
            using (var bw = new BinaryWriter(File.Create(path)))
            {
                bw.Write(FontBuildShared.CacheMagic);
                bw.Write(signature);
                bw.Write(FontBuildShared.AtlasWidth);
                bw.Write(FontBuildShared.AtlasHeight);

                bw.Write(f.name ?? "");
                bw.Write(f.pointSize);
                bw.Write(f.padding);
                bw.Write(f.lineHeight);
                bw.Write(f.baseline);
                bw.Write(f.ascender);
                bw.Write(f.descender);
                bw.Write(f.centerLine);
                bw.Write(f.underline);
                bw.Write(f.underlineThickness);
                bw.Write(f.characterCount);
                bw.Write(f.atlasWidth);
                bw.Write(f.atlasHeight);

                bw.Write(glyphs.Length);
                for (int i = 0; i < glyphs.Length; i++)
                {
                    var g = glyphs[i];
                    bw.Write(g.id);
                    bw.Write(g.x); bw.Write(g.y);
                    bw.Write(g.width); bw.Write(g.height);
                    bw.Write(g.xOffset); bw.Write(g.yOffset); bw.Write(g.xAdvance);
                }

                bw.Write(buffer.Length);
                bw.Write(buffer);
            }
        }

        // QUAN TRONG: Newtonsoft (ban Unity) cua game serialize Dictionary thanh MANG cap
        // [{ "Key":.., "Value":.. }] (giong fontMappings.json), KHONG phai object {}.
        // Mod doc bang JSON.LoadJSONFromFile<Dictionary<..>> nen file PHAI o dang mang nay.
        static Dictionary<string, FontBuildEntry> LoadConfig(string path)
        {
            var result = new Dictionary<string, FontBuildEntry>();
            if (!File.Exists(path)) return result;

            // Doc tolerant: khong dung Newtonsoft cua game (no can UnityEngine).
            List<Dictionary<string, object>> list;
            try
            {
                list = new JavaScriptSerializer().Deserialize<List<Dictionary<string, object>>>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                // File hong (vd sua tay thieu dau phay) -> KHONG crash, bo qua va se ghi lai sach.
                Console.Error.WriteLine("CANH BAO: fontBuild.json hong, bo qua noi dung cu (" + e.Message + ")");
                return result;
            }
            if (list == null) return result;

            foreach (var item in list)
            {
                if (item == null || !item.TryGetValue("Key", out var k) || k == null) continue;
                var e = new FontBuildEntry();
                if (item.TryGetValue("Value", out var vObj) && vObj is Dictionary<string, object> v)
                {
                    if (v.TryGetValue("Style", out var st) && st != null) e.Style = st.ToString();
                    if (v.TryGetValue("SizeReduction", out var sr) && sr != null) e.SizeReduction = Convert.ToSingle(sr, System.Globalization.CultureInfo.InvariantCulture);
                    if (v.TryGetValue("StyleMod", out var sm) && sm != null) e.StyleMod = Convert.ToSingle(sm, System.Globalization.CultureInfo.InvariantCulture);
                    if (v.TryGetValue("BoldWeight", out var bw) && bw != null) e.BoldWeight = Convert.ToSingle(bw, System.Globalization.CultureInfo.InvariantCulture);
                    if (v.TryGetValue("NormalWeight", out var nw) && nw != null) e.NormalWeight = Convert.ToSingle(nw, System.Globalization.CultureInfo.InvariantCulture);
                }
                result[k.ToString()] = e;
            }
            return result;
        }

        static void SaveConfig(string path, Dictionary<string, FontBuildEntry> config)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("[\n");
            int i = 0;
            foreach (var kvp in config)
            {
                var e = kvp.Value ?? new FontBuildEntry();
                sb.Append("  { \"Key\": ").Append(JsonStr(kvp.Key))
                  .Append(", \"Value\": { ")
                  .Append("\"Style\": ").Append(JsonStr(e.Style)).Append(", ")
                  .Append("\"SizeReduction\": ").Append(e.SizeReduction.ToString("0.0###", inv)).Append(", ")
                  .Append("\"StyleMod\": ").Append(e.StyleMod.ToString("0.0###", inv)).Append(", ")
                  .Append("\"BoldWeight\": ").Append(e.BoldWeight.ToString("0.0###", inv)).Append(", ")
                  .Append("\"NormalWeight\": ").Append(e.NormalWeight.ToString("0.0###", inv)).Append(" } }");
                sb.Append(++i < config.Count ? ",\n" : "\n");
            }
            sb.Append("]\n");
            File.WriteAllText(path, sb.ToString());
        }

        static string JsonStr(string s)
        {
            s = s ?? "";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        static Dictionary<string, string> ParseArgs(string[] args)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--")) continue;
                string key = args[i].Substring(2);
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    d[key] = args[++i];
                else
                    d[key] = "true"; // co dang flag (vd --all)
            }
            return d;
        }

        static void PrintUsage()
        {
            Console.WriteLine(@"FontPrebuild - pre-build atlas cache cho FontMod (Kingmaker)

  FontPrebuild --font ""NotoSerifSC-VF"" --style Bold   --size-reduction 1.05
  FontPrebuild --font ""UTM Dai Co Viet"" --style Normal --size-reduction 1.0
  FontPrebuild --all

Tham so:
  --font <ten|duong-dan>   Ten file TTF (bo duoi) trong Fonts/ hoac duong dan TTF.
  --style <Normal|Bold|..> Mac dinh Bold.
  --size-reduction <float> Mac dinh 1.05 (cang lon chu cang nho). KHONG can render lai.
  --style-mod <float>      Do dam net SDF, mac dinh 2.
  --bold-weight <float>    Trong so faux-bold luc chay, mac dinh 0.75. Dat 0 de KHONG bi bold
                           o cho game ep Bold (vd font tieu de Saber). Khong can render lai.
  --normal-weight <float>  Trong so net thuong, mac dinh 0. AM (vd -0.4) = lam MANH chu xuong
                           khi font tu than da nang (vd SaberRegular-VN). Khong can render lai.
  --all                    Build moi TTF trong Fonts/ theo fontBuild.json hien co.
  --mod-dir <path>         Mac dinh: " + DefaultModDir);
        }
    }
}
