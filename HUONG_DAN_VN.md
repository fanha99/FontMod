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

## B. Đổi CỠ CHỮ (câu hỏi #3) — giờ THEO TỪNG FONT, KHÔNG build lại DLL

Cỡ chữ (`SizeReduction`) + kiểu (`Style`) + độ đậm SDF (`StyleMod`) nay là **thiết lập riêng
từng font** trong `Mods\FontMod\fontBuild.json`. **PHẢI ở dạng mảng cặp `Key`/`Value`** (giống
`fontMappings.json`) vì Newtonsoft bản Unity của game serialize Dictionary kiểu này — viết dạng
object `{ }` sẽ lỗi đọc và làm font KHÔNG swap:
```json
[
  { "Key": "NotoSerifSC-VF",  "Value": { "Style": "Bold",   "SizeReduction": 1.05, "StyleMod": 2.0 } },
  { "Key": "UTM Dai Co Viet", "Value": { "Style": "Normal", "SizeReduction": 1.0,  "StyleMod": 2.0 } }
]
```
> Mẹo: đừng sửa tay dễ sai dạng — chạy `FontPrebuild` (mục E) để nó tự ghi đúng định dạng.

**`BoldWeight`** (mặc định 0.75): trọng số *faux-bold* lúc chạy. Chỗ tiêu đề (slot `Saber_`) bị
game ép `FontStyles.Bold` → TMP tự làm dày chữ dù atlas là Normal. Đặt `BoldWeight = 0` cho font
tiêu đề (vd UTM Dai Co Viet) để **hết bị bold**. Giá trị này KHÔNG nằm trong atlas/chữ ký →
đổi là áp ngay, không render lại. Tham số tool: `--bold-weight 0`.
Cơ chế `SizeReduction`: báo PointSize LỚN hơn thật → TMP scale chữ render NHỎ lại đồng đều.

| Giá trị | Kết quả |
|---------|---------|
| `1.0`   | gốc (to nhất) |
| `1.05`  | nhỏ ~5% |
| `1.1`   | nhỏ ~9% |
| `1.2`   | nhỏ ~17% |

> **`SizeReduction` KHÔNG nằm trong chữ ký cache** → sửa số này rồi vào game là áp dụng ngay,
> **không cần render lại**. Còn đổi `Style` (Bold↔Normal) thì atlas khác byte → cần build lại
> cache font đó (xem mục E).

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

## D. Cache atlas (câu hỏi #1) — ĐÃ CÓ

`FontDataModel.CreateFontAsset` build SDF lần đầu rồi ghi `AtlasCache\<font>.atlas`
(atlas thô + FaceInfo + GlyphInfo). Lần sau `LoadAtlasCache()` nạp thẳng, **bỏ qua FreeType**
→ khởi động nhanh, **không render lại** — miễn là *chữ ký* khớp.

Chữ ký (`FontBuildShared.BuildSignature`, dùng chung mod + tool) theo **từng font**:
`TTF(size+mtime) + charset + atlas + padding + renderMode + style + styleMod`.
→ Sửa thiết lập 1 font **không** làm hết hạn cache font khác (hết cảnh "đổi thằng này build lại
thằng kia"). Đổi file TTF, charset, hoặc `Style`/`StyleMod` → tự build lại đúng font đó.

---

