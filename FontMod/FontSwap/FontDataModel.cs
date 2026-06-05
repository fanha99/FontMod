using Newtonsoft.Json;
using System;
using System.IO;
using TMPro;
using TMPro.EditorUtilities;
using UnityEngine;
using static RootMotion.FinalIK.GrounderQuadruped;

namespace FontMod.FontSwap;

[Serializable]
public class FontDataModel
{
    [JsonProperty]
    public string Name { get; set; }
    [JsonProperty]
    public bool IsIgnored { get; set; }
    [JsonIgnore]
    public string FontPath { get; set; }
    [JsonIgnore]
    public TMP_FontAsset TMP_FontAsset { get; private set; }

    private FontDataModel() { }

    // fontPath co the null/khong ton tai: khi do font duoc nap THANG tu atlas (atlas-only,
    // ship khong kem TTF de tranh ban quyen font). Name luon bat buoc.
    private FontDataModel(string name, string fontPath)
    {
        try
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException("null name in creation of FontDataModel.");

            FontPath = fontPath;
            Name = name;
            TMP_FontAsset = CreateFontAsset(name, fontPath);
        }
        catch (Exception e)
        {
            Main.Logger.Error(e);
        }
    }

    public override bool Equals(object obj)
    {
        if (obj is FontDataModel other)
            return Equals(FontPath, other.FontPath) && Equals(TMP_FontAsset, other.TMP_FontAsset);

        return false;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (FontPath != null ? FontPath.GetHashCode() : 0);
            hash = hash * 31 + (TMP_FontAsset != null ? TMP_FontAsset.GetHashCode() : 0);
            return hash;
        }
    }
    public static FontDataModel CreateEmptyIgnored() => new() { IsIgnored = true };
    public static FontDataModel CreateFromPath(string fontPath) => new(Path.GetFileNameWithoutExtension(fontPath), fontPath);
    // name = ten font (= ten file bo duoi). fontPath null/khong ton tai -> nap tu atlas (atlas-only).
    public static FontDataModel CreateFromName(string name, string fontPath) => new(name, fontPath);
    public static TMP_FontAsset CreateFontAsset(string name, string fontPath)
    {
        TMP_FontAsset asset = null;

        try
        {
            bool hasTtf      = !string.IsNullOrEmpty(fontPath) && File.Exists(fontPath);
            string cachePath = Path.Combine(Main.ModEntry.Path, "AtlasCache", name + ".atlas");
            bool hasAtlas    = File.Exists(cachePath);

            if (!hasTtf && !hasAtlas)
                throw new FileNotFoundException($"Font '{name}': khong co ca TTF lan atlas.");

            var create = new TMPro_FontAssetCreatorWindow();
            // Atlas-only: dat font_TTF_path = name de ten texture/material van dung (chi dung de dat ten).
            create.font_TTF_path = fontPath ?? name;
            create.ApplyBuildSettings(FontBuildConfig.Get(name)); // style/co chu rieng tung font

            // CACHE: co TTF -> build SDF lan dau roi luu, cac lan sau nap thang (nhanh).
            bool fromCache;
            if (hasTtf)
            {
                // Cache phai khop chu ky; TTF doi -> render lai (giu vong lap chinh font khi dev).
                fromCache = create.LoadAtlasCache(cachePath, true);
                if (!fromCache)
                    create.GenerateFontAtlas();
            }
            else
            {
                // Khong TTF -> tin atlas, bo qua chu ky (khong co gi de render lai).
                fromCache = create.LoadAtlasCache(cachePath, false);
                if (!fromCache)
                    throw new InvalidDataException($"Font '{name}': atlas hong/doc loi va khong co TTF de dung lai.");
            }

            create.CreateFontTexture();

            asset = create.Save_SDF_FontAsset();

            // Chi luu cache khi vua render moi tu TTF (atlas-only thi atlas da co san).
            if (hasTtf && !fromCache && asset != null)
                create.SaveAtlasCache(cachePath);
            Main.Logger.Log(
                !hasTtf     ? ("Atlas-only (khong TTF, tranh ban quyen): " + name)
                : fromCache ? ("Atlas tu CACHE (nhanh): " + name)
                            : ("Atlas dung moi + da luu cache: " + name));

            if (asset == null)
                throw new NullReferenceException($"Creation of TMP_FontAsset failed for font {name}");
            else
                Main.Logger.Log($"Created font asset {asset.name}");

            asset.name = name;

            if (asset != null)
                MaterialReferenceManager.AddFontAsset(asset);
        }
        catch (Exception e)
        {
            Main.Logger.Error(e);
        }

        return asset;
    }
}