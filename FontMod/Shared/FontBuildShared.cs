using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace FontMod.Shared
{
    // ============================================================================
    // NGUON SU THAT DUNG CHUNG cho ca MOD (in-game) lan TOOL (FontPrebuild.exe).
    // Moi thong so quyet dinh byte cua atlas + chu ky cache deu o day, de hai ben
    // KHONG bao gio lech nhau. Doi gi o day -> build lai ca DLL lan tool.
    // ============================================================================
    public static class FontBuildShared
    {
        // 'VNF1' - magic dau file cache .atlas
        public const int CacheMagic = 0x564E4631;

        // Charset: ASCII + tieng Viet (Latin-1, Latin Ext-A/B, dau ket hop, U+1EA0-U+1EF9)
        public const string CharacterSequence =
            "32 - 126, 160, 192 - 263, 272 - 273, 296 - 297, 360 - 361, 416 - 417, 431 - 432, 768 - 772, 777, 803, 7840 - 7929, 8203, 8211 - 8212, 8216 - 8217, 8220 - 8221, 8230, 9633";

        public const int AtlasWidth = 1024;
        public const int AtlasHeight = 1024;
        public const int Padding = 5;
        public const int RenderModeInt = 6;   // RenderModes.DistanceField16
        public const int FontSize = 72;       // auto sizing -> 72pt
        public const int PackingMethod = 0;   // FontPackingModes.Fast

        // Mac dinh khi font khong co entry trong fontBuild.json (giu hanh vi cu).
        public const string DefaultStyle = "Bold";
        public const float DefaultStyleMod = 2f;
        public const float DefaultSizeReduction = 1.05f;
        public const float DefaultBoldWeight = 0.75f;   // trong so faux-bold cua TMP (0 = tat gia-bold)
        public const float DefaultNormalWeight = 0f;     // trong so net thuong (_WeightNormal). AM = lam MANH chu

        // Chu ky cache: doi bat ky thanh phan nao -> cache cu vo hieu, build lai.
        // LUU Y: KHONG co sizeReduction o day (no chi la he so scale luc NAP, khong
        // doi byte atlas) -> doi co chu la tuc thi, khoi render lai.
        // CO style + styleMod (chung lam doi net chu -> doi byte atlas).
        public static string BuildSignature(string ttfPath, int styleInt, float styleMod)
        {
            long len = 0, ticks = 0;
            try { var fi = new FileInfo(ttfPath); len = fi.Length; ticks = fi.LastWriteTimeUtc.Ticks; } catch { }
            return "ttf:" + len + ":" + ticks
                 + "|seq:" + CharacterSequence
                 + "|atlas:" + AtlasWidth + "x" + AtlasHeight
                 + "|pad:" + Padding
                 + "|rm:" + RenderModeInt
                 + "|style:" + styleInt
                 + "|smod:" + styleMod.ToString("R", CultureInfo.InvariantCulture);
        }

        // FaceStyles: Normal=0, Bold=1, Italic=2, Bold_Italic=3, Outline=4, Bold_Sim=5
        public static int ParseStyleToInt(string s)
        {
            if (string.IsNullOrEmpty(s)) return 1;
            switch (s.Trim().ToLowerInvariant())
            {
                case "normal": return 0;
                case "bold": return 1;
                case "italic": return 2;
                case "bold_italic":
                case "bolditalic": return 3;
                case "outline": return 4;
                case "bold_sim":
                case "boldsim": return 5;
                default: return 1;
            }
        }

        // Dung CHINH XAC logic ParseNumberSequence cua TMPro_FontAssetCreatorWindow.
        public static int[] ParseCharacterSet()
        {
            var list = new List<int>();
            foreach (var seq in CharacterSequence.Split(','))
            {
                var s1 = seq.Split('-');
                if (s1.Length == 1)
                {
                    if (int.TryParse(s1[0], out var v)) list.Add(v);
                }
                else
                {
                    for (int j = int.Parse(s1[0]); j < int.Parse(s1[1]) + 1; j++)
                        list.Add(j);
                }
            }
            return list.ToArray();
        }
    }

    // POCO dung chung cho fontBuild.json (Newtonsoft doc/ghi binh thuong, khong can attribute).
    // Key cua dictionary = ten file TTF (bo duoi), vd "NotoSerifSC-VF", "UTM Dai Co Viet".
    public class FontBuildEntry
    {
        public string Style { get; set; } = FontBuildShared.DefaultStyle;
        public float SizeReduction { get; set; } = FontBuildShared.DefaultSizeReduction;
        public float StyleMod { get; set; } = FontBuildShared.DefaultStyleMod;
        // Trong so faux-bold luc CHAY (khong nam trong atlas/chu ky -> doi la tuc thi).
        // Dat 0 cho font o slot tieu de (Saber) neu khong muon chu bi bold.
        public float BoldWeight { get; set; } = FontBuildShared.DefaultBoldWeight;
        // Trong so net THUONG (_WeightNormal). 0 = giu nguyen net font; AM (vd -0.5) = lam MANH
        // chu xuong (dung khi font tu than da nang nhu SaberRegular-VN). Runtime, khong render lai.
        public float NormalWeight { get; set; } = FontBuildShared.DefaultNormalWeight;

        public static FontBuildEntry Default() => new FontBuildEntry();
    }
}
