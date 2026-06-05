using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using TMPro.EditorUtilities;

namespace FontMod.FontSwap;

public class FontCollection : ICollection<FontDataModel>
{
    private readonly HashSet<FontDataModel> _fontDataModels = [];

    public int Count => _fontDataModels.Count;

    public bool IsReadOnly => false;

    public void Add(FontDataModel item)
    {
        if (item == null)
            throw new ArgumentNullException("Cannot add null item to FontCollection");

        if (_fontDataModels.Contains(item))
            throw new ArgumentException($"FontCollection already contains {item.Name}");

        _fontDataModels.Add(item);
    }

    public void AddFromFilePath(string fontPath) => Add(FontDataModel.CreateFromPath(fontPath));

    // Liet ke font tu CA hai nguon: Fonts/*.ttf VA AtlasCache/*.atlas.
    //  - Co TTF  -> dung file TTF (build lai khi font doi).
    //  - Chi atlas (khong TTF) -> nap thang tu atlas; ship khong kem TTF de tranh ban quyen font.
    // Bo qua moi file khong phai .ttf trong Fonts (vd file license OFL.txt).
    public void AddFontsAndAtlas(string fontsFolder, string atlasFolder)
    {
        // name -> duong dan TTF (null = chi co atlas). So sanh ten khong phan biet hoa thuong.
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(fontsFolder))
            foreach (var ttf in Directory.GetFiles(fontsFolder, "*.ttf"))
                names[Path.GetFileNameWithoutExtension(ttf)] = ttf;

        if (Directory.Exists(atlasFolder))
            foreach (var atlas in Directory.GetFiles(atlasFolder, "*.atlas"))
            {
                var n = Path.GetFileNameWithoutExtension(atlas);
                if (!names.ContainsKey(n)) names[n] = null; // chi co atlas, khong co TTF
            }

        foreach (var kvp in names)
        {
            try { Add(FontDataModel.CreateFromName(kvp.Key, kvp.Value)); }
            catch (Exception e) { Main.Logger.Error($"Nap font '{kvp.Key}' loi: {e.Message}"); }
        }
    }

    public void AddFromFolderPath(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Folder path not found: {folderPath}");

        foreach (var font in Directory.GetFiles(folderPath))
            AddFromFilePath(font);
    }

    public FontDataModel GetFontByName(string name)
    {
        var model = _fontDataModels.Where(x => x.Name == name).First();

        if (model == null)
            throw new ArgumentException($"Font name {name} not found in collection");

        return model;
    }

    public void Clear() => _fontDataModels.Clear();

    public bool Contains(FontDataModel item) => _fontDataModels.Contains(item);

    public void CopyTo(FontDataModel[] array, int arrayIndex) => _fontDataModels.CopyTo(array, arrayIndex);

    public IEnumerator<FontDataModel> GetEnumerator() => _fontDataModels.GetEnumerator();

    public bool Remove(FontDataModel item) => _fontDataModels.Remove(item);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
