# Hướng dẫn FontMod (bản Kingmaker + tiếng Việt)

Repo build: `f:\FontMod-build`
Mod đã cài: `f:\Games\PathfinderKingmaker\Mods\FontMod\`

FontMod **dựng atlas SDF từ TTF lúc game chạy** bằng FreeType native (`TMPro_Plugin.dll`).
Charset đã mở rộng phủ tiếng Việt (U+1EA0–U+1EF9 + dấu kết hợp).

Font được liệt kê từ **HỢP của 2 nguồn**: `Mods\FontMod\Fonts\*.ttf` và `Mods\FontMod\AtlasCache\*.atlas`:
- **Có TTF** → render thành 1 TMP_FontAsset lúc khởi động (rồi cache lại).
- **Chỉ có atlas, không TTF** → nạp thẳng atlas (atlas-only, xem mục F) — dùng để ship font
  vướng bản quyền mà không kèm file `.ttf`.

---

## A. Build lại DLL (khi sửa source: cỡ chữ, charset, atlas…)

> Codebase nay **chỉ còn 1 project Kingmaker** (`FontMod\FontMod-KM.csproj`). Các nhánh
> Wrath/Rogue Trader (`#if !KM`, `#if RT`, 2 csproj kia) đã gỡ bỏ.

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

> Cách khác: `& "f:\FontMod-build\build_release.ps1"` (`dotnet build -c Release`). Build **không
> còn tự copy vào thư mục game** (đã gỡ `<Copy>` trong csproj) → luôn deploy thủ công như bước 3.

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

Game Kingmaker dùng 2 nhóm font chính, hiện ĐÃ map sẵn trong `fontMappings.json`:
- `NexusSerif-Regular SDF` — font thân bài (Latin/Cyrillic) → map sang **NotoSerif_SemiCondensed-Light** (slot `default`).
- `Saber_Dist32` và `ANTQUAI SDF` — font tiêu đề/trang trí → map sang **SaberRegular-VN** (qua `$ref`).

> Lưu ý: `Name` trong `fontMappings.json` = **tên file** (bỏ đuôi) của TTF *hoặc* atlas. Đổi font
> body chỉ cần thay file trong `Fonts\` rồi sửa `Name` slot `default` cho khớp.

Để thêm/đổi font thứ 2 cho riêng tiêu đề:
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

> Chữ ký gắn `size+mtime` của TTF. Vì vậy khi **chỉ có atlas mà không có TTF**, không thể tính
> chữ ký để so → mod **bỏ qua kiểm tra chữ ký**, tin atlas và nạp thẳng (xem mục F).

---

## E. Pre-build cache bằng FontPrebuild.exe (không cần mở game)

Tool: `f:\FontMod-build\tool\` — build: `& "f:\FontMod-build\tool\build_tool.ps1"`
→ ra `tool\bin\FontPrebuild.exe` (kèm `TMPro_Plugin.dll`). Nó gọi thẳng FreeType render atlas,
ghi `.atlas` đúng chữ ký + cập nhật `fontBuild.json`. Cần x64; không cần Unity.

```powershell
# build từng font với chế độ riêng
& "f:\FontMod-build\tool\bin\FontPrebuild.exe" --font "NotoSerif_SemiCondensed-Light" --style Bold   --size-reduction 1.0
& "f:\FontMod-build\tool\bin\FontPrebuild.exe" --font "SaberRegular-VN"               --style Normal --size-reduction 1.0

# hoặc build lại MỌI font trong Fonts\ theo fontBuild.json hiện có
& "f:\FontMod-build\tool\bin\FontPrebuild.exe" --all
```

Tham số: `--font <tên|đường-dẫn>`, `--style Normal|Bold|...`, `--size-reduction <float>`,
`--style-mod <float>` (mặc định 2), `--all`, `--mod-dir <path>` (mặc định mod đang cài).

Quy trình thường ngày (đổi font/chế độ): chạy `FontPrebuild` cho font cần đổi → vào game.
Cache cố định, lần sau khởi động nạp thẳng. **Không phải build lại DLL** trừ khi sửa code C#
(charset, atlas, logic… → mục A, và nhớ chạy lại `build_tool.ps1` để tool khớp chữ ký mới).

> Nếu cache lệch chữ ký (vd sửa `Style` trong JSON mà quên chạy tool), game vẫn tự render lại
> font đó trong lúc load (chậm 1 lần) rồi ghi đè cache — không hỏng, chỉ chậm lần đó.

---

## F. Atlas-only — dùng atlas không cần file TTF (tránh bản quyền)

Atlas (`.atlas`) là **ảnh SDF đã render**, không phải font software → ship atlas an toàn bản quyền
hơn ship file `.ttf` với các font không được phép phát hành lại (vd `UTM Dai Co Viet`,
`SaberRegular-VN`). Font OFL (như `NotoSerif_SemiCondensed-Light`) thì ship `.ttf` bình thường.

**Cách làm cho 1 font atlas-only:**
1. Dựng atlas từ TTF như bình thường (mục E): `FontPrebuild --font "SaberRegular-VN" ...`
   → ra `AtlasCache\SaberRegular-VN.atlas`.
2. Khi đóng gói: bỏ `SaberRegular-VN.atlas` vào `AtlasCache\`, **KHÔNG** bỏ `SaberRegular-VN.ttf`
   vào `Fonts\`.
3. `fontMappings.json` để `Name` = `SaberRegular-VN` (= tên atlas bỏ đuôi) như cũ.

Lúc chạy, mod thấy font có atlas mà không TTF → nạp thẳng atlas, **bỏ qua kiểm tra chữ ký**
(log: `Atlas-only (khong TTF, tranh ban quyen): <font>`).

**Giới hạn:** `Style`/`StyleMod` đã "nướng" cứng trong atlas → muốn đổi phải có TTF render lại.
Còn `SizeReduction`, `BoldWeight`, `NormalWeight` **vẫn chỉnh được** lúc nạp (không nằm trong atlas).

> Code liên quan: `FontCollection.AddFontsAndAtlas` (liệt kê Fonts/ ∪ AtlasCache/),
> `FontDataModel.CreateFontAsset` (rẽ nhánh có/không TTF), `LoadAtlasCache(path, verifySignature)`.
> Đổi các chỗ này thì build lại DLL (mục A).

---

## G. Đóng gói & phát hành lên GitHub

Phiên bản ghi ở **2 chỗ phải khớp**: `FontMod\Info.json` và `Repository.json`.

**Đóng gói zip (chuẩn UMM):**
```powershell
& "f:\FontMod-build\package_release.ps1"          # ra dist\FontMod-<ver>.zip
```
Zip có thư mục con `FontMod\` gồm: `Info.json, FontMod.dll, TMPro_Plugin.dll, fontBuild.json,
fontMappings.json, Fonts\*.ttf, AtlasCache\*.atlas` (+ file license đi kèm font).
→ Font OFL ship qua `Fonts\`; font vướng bản quyền ship qua `AtlasCache\` (atlas-only, mục F).

**Phát hành qua GitHub Action (chỉ đóng gói, không compile vì DLL game có bản quyền):**
1. Build DLL ở máy (mục A) rồi copy sang chỗ CI lấy + commit:
   ```powershell
   Copy-Item "f:\FontMod-build\out\FontMod.dll" "f:\FontMod-build\release-assets\FontMod.dll" -Force
   ```
2. Bump version ở `Info.json` + `Repository.json`, commit.
3. Đẩy tag để kích hoạt release:
   ```powershell
   git tag v1.1.3 ; git push fork v1.1.3
   ```
   → `.github\workflows\release.yml` gom zip + tạo Release (kèm bản tên ổn định `FontMod.zip`
   cho UMM auto-update qua `DownloadUrl` trong `Repository.json`).

Tổng quan công khai cho người chơi: xem [`README.md`](README.md).
