using FontMod.Utility;
using HarmonyLib;
using Kingmaker.UI.Common;
using Kingmaker.UI.Overtip;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
namespace FontMod.FontSwap;

[HarmonyPatch]
public static class TMPTestPach
{
    [HarmonyPatch(typeof(TextMeshProUGUI), "LoadFontAsset")]
    [HarmonyPrefix]
    static void TextPatch(TextMeshProUGUI __instance)
    {
        if (__instance.m_fontAsset != null)
            __instance.m_fontAsset = FontMapper.Instance.GetFontMapped(__instance.m_fontAsset);
    }

    [HarmonyPatch(typeof(MaterialReferenceManager), nameof(MaterialReferenceManager.TryGetFontAsset))]
    [HarmonyPostfix]
    static void TryGetFontAsset(ref TMP_FontAsset fontAsset)
    {
        if (fontAsset != null)
            fontAsset = FontMapper.Instance.GetFontMapped(fontAsset);
    }

    [HarmonyPatch(typeof(TMP_Text), "ValidateHtmlTag")]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> TagPatch(IEnumerable<CodeInstruction> instructions, ILGenerator iLGen)
    {
        var method = AccessTools.Method(typeof(MaterialReferenceManager), nameof(MaterialReferenceManager.AddFontAsset));

        var assetIntructions = new CodeInstruction[]
        {
            new(OpCodes.Call, method)
        };

        var instructionIndex = instructions.FindCodes(assetIntructions);

        if (instructionIndex >= 0)
        {
            var ldlocs = instructions.ElementAt(instructionIndex - 1);

            var patchCodes = new CodeInstruction[]
            {
                new(OpCodes.Call, AccessTools.PropertyGetter(typeof(FontMapper), nameof(FontMapper.Instance))),
                new(OpCodes.Ldloc_S, ldlocs.operand),
                new(OpCodes.Callvirt, AccessTools.Method(typeof(FontMapper), nameof(FontMapper.GetFontMapped))),
                new(OpCodes.Stloc_S, ldlocs.operand)
            };

            return instructions.InsertRange(instructionIndex, patchCodes, true);
        }
        else
        {
            Main.Logger.Error("TagPatch Transpile Failed.");
            return instructions;
        }
    }

    // Hack to fix incorrect material.  I think the bigger problem is the material tag not being handled properly.
    // Will have to prob transpile more in the TMP library.

    [HarmonyPatch(typeof(UIUtility), nameof(UIUtility.GetSaberBookFormat))]
    [HarmonyPrefix]
    static void GetSaberBookFormatPatch(string name, Color color, int size, ref Material material)
    {
        if (material == null)
            return;

        material = FontMapper.Instance.FontMappings.ContainsKey("Saber_Dist32") ?
            FontMapper.Instance.FontMappings["Saber_Dist32"].TMP_FontAsset.material :
            FontMapper.Instance.DefaultFontMapping.TMP_FontAsset.material;
    }

    // Ten noi tren dau don vi (Overtip) hien NGUYEN VAN tag
    //   <font="Saber_Dist32" material="...VN Material">Ten
    // Nguyen nhan: OvertipController.SetName goi GetSaberBookFormat de boc tag rich-text
    // (chu cai dau mau do, font Saber). FontMod (GetSaberBookFormatPatch) thay material
    // thanh material font VN - material nay KHONG dang ky trong MaterialReferenceManager
    // nen TMP CU coi tag <font=...material=...> la khong hop le -> in nguyen ca tag.
    // (Menu Quests/Equipment render OK vi tag cua chung KHONG co thuoc tinh material=.)
    // FIX rieng cho overtip: tu boc tag GIONG game (size 140%, mau m_NameSaberColor) NHUNG
    // BO thuoc tinh material -> transpiler TagPatch tu map Saber_Dist32 -> SaberRegular-VN
    // (giong het duong render cua menu Quests dang chay dung) => giu chu-cai-dau mau do
    // font Saber-VN, phan con lai font thuong. KHONG goi GetSaberBookFormat goc (no NPE
    // neu material=null, va FontMod lai thay material hong neu khong null).
    [HarmonyPatch(typeof(OvertipController), "SetName")]
    [HarmonyPrefix]
    static bool OvertipSetNamePatch(OvertipController __instance, string text)
    {
        var name = __instance.Name;
        if (name == null)
            return false;
        if (string.IsNullOrEmpty(text))
        {
            name.SetText(text);
            return false;
        }

        Color c = __instance.m_NameSaberColor;
        if (c == default(Color))
        {
            var root = Kingmaker.Blueprints.Root.UIRoot.Instance;
            if (root != null)
                c = root.PaperSaberColor;
        }
        string hex = ColorUtility.ToHtmlStringRGB(c);

        // CHU Y: KHONG dung <font="Saber_Dist32"> - doi font giua chuoi lam overtip
        // (canvas world-space) render TRONG (mat het chu). Chi trang tri = mau do + size
        // 140% cho ky tu dau, GIU font Viet hien tai -> hien duoc va co diem nhan.
        string formatted = "<color=#" + hex + "><size=120%>"
            + text[0] + "</size></color>" + text.Substring(1);
        name.SetText(formatted);
        return false;
    }
}