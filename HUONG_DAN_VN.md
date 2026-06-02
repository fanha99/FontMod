# Hướng dẫn FontMod (bản Kingmaker + tiếng Việt)

Repo build: `f:\FontMod-build`
Mod đã cài: `f:\Games\PathfinderKingmaker\Mods\FontMod\`

FontMod **dựng atlas SDF từ TTF lúc game chạy** bằng FreeType native (`TMPro_Plugin.dll`).
Mỗi font trong thư mục `Mods\FontMod\Fonts\` được render thành 1 TMP_FontAsset trong RAM mỗi
lần khởi động. Charset đã mở rộng phủ tiếng Việt (U+1EA0–U+1EF9 + dấu kết hợp).

---

## A. Build lại DLL (khi sửa source: cỡ chữ, charset, atlas…)

1. Sửa source trong `f:\FontMod-build\FontMod\...`
2. Mở **PowerShell**, chạy:
   ```powershell
   & "f:\FontMod-build\build.ps1"
   ```
   Thành công khi in `EXITCODE: 0` → ra `f:\FontMod-build\out\FontMod.dll`.
3. Deploy: copy đè vào mod rồi **xoá cache**:
   ```powershell
   Copy-Item "f:\FontMod-build\out\FontMod.dll" "f:\Games\PathfinderKingmaker\Mods\FontMod\FontMod.dll" -Force
   Remove-Item "f:\Games\PathfinderKingmaker\Mods\FontMod\*.cache" -Force -ErrorAction SilentlyContinue
   ```
4. Chạy game, bật FontMod trong UMM (Ctrl+F10).

> `publicize.ps1` CHỈ chạy lại khi **game update** (đổi `Assembly-CSharp*.dll` /
> `UnityModManager.dll`). Nó sinh bản "publicized" trong `f:\FontMod-build\pub\` để compile.
> Build thường ngày KHÔNG cần chạy lại.

---

## B. Đổi CỠ CHỮ (câu hỏi #3)

File: `FontMod\TMP_Utils\TMPro_FontAssetCreatorWindow.cs`, dòng:
```csharp
public static float SizeReductionFactor = 1.1f;
```
Cơ chế: báo PointSize LỚN hơn thật → TMP scale chữ render NHỎ lại đồng đều (cả chữ lẫn giãn dòng).

| Giá trị | Kết quả |
|---------|---------|
| `1.0f`  | gốc (to nhất) |
| `1.05f` | nhỏ ~5% |
| `1.1f`  | nhỏ ~9% (hiện tại) |
| `1.2f`  | nhỏ ~17% |

Sửa số → **Build lại (mục A)**. Không cần đụng gì khác.

---

## C. Dùng 2 FONT (câu hỏi #2) — KHÔNG cần build lại DLL

Game Kingmaker dùng 2 font chính:
- `NexusSerif-Regular SDF` — font thân bài (Latin/Cyrillic) → đang map sang Noto Serif SC.
- `Saber_Dist32` — font tiêu đề/trang trí → đang `IsIgnored=true` (giữ nguyên font gốc).

Để render font thứ 2 cho riêng tiêu đề:
1. Bỏ 1 file TTF thứ 2 (PHẢI phủ glyph tiếng Việt) vào `Mods\FontMod\Fonts\`,
   ví dụ `MyTitleFont.ttf`.
2. Sửa `Mods\FontMod\fontMappings.json`, đổi entry `Saber_Dist32` từ ignored thành map:
   ```json
   {
     "Key": "Saber_Dist32",
     "Value": { "$id": "3", "Name": "MyTitleFont", "IsIgnored": false }
   }
   ```
   (`Name` = tên file TTF bỏ đuôi.)
3. Chạy game. FontMod sẽ render CẢ 2 TTF lúc load (lâu hơn ~gấp đôi) và thay đúng từng font.

> Mỗi TTF thêm vào `Fonts\` = thêm 1 lần dựng atlas lúc khởi động. Muốn nhanh thì chỉ để
> đúng các TTF đang dùng; TTF dự phòng để ở `Fonts_unused\`.
> Tên font game lạ gặp về sau xem trong `encounteredFonts.json` (FontMod tự ghi).

---

## D. Vì sao mỗi lần chạy game lại render? (câu hỏi #1)

Hiện tại: KHÔNG có cache. `FontDataModel.CreateFontAsset` gọi `GenerateFontAtlas()` (FreeType
render) → `Save_SDF_FontAsset()` tạo TMP_FontAsset **chỉ trong RAM**, không ghi đĩa. Tắt game
là mất → lần sau dựng lại.

Có thể thêm cache: serialize `m_texture_buffer` (atlas thô) + FaceInfo + GlyphInfo + Kerning ra
file; lần sau nếu cache khớp (hash TTF + charset + size) thì nạp thẳng, bỏ qua FreeType. Cần sửa
code FontMod (xem chi tiết khi triển khai). Với ~286 glyph/1 font thì render khá nhanh nên cache
chỉ đáng làm nếu thêm nhiều font hoặc charset lớn.
