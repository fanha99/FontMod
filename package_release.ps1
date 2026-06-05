<#
  package_release.ps1 - Gom cac file CAN THIET de DUNG mod vao 1 file zip release (chuan UnityModManager).

  Layout zip (UMM doc Info.json trong thu muc con cung ten Id):
    FontMod/
      Info.json
      FontMod.dll
      TMPro_Plugin.dll        (native FreeType plugin)
      fontBuild.json          (dinh nghia build per-font -> atlas dung lai khop ban da build)
      fontMappings.json       (map font game -> font thay)
      Fonts/*.ttf
	  AtlasCache/*.atlas

  Dung local:   .\package_release.ps1                       (lay DLL tu .\out\FontMod.dll)
  Dung CI:      .\package_release.ps1 -Dll .\release-assets\FontMod.dll
#>
[CmdletBinding()]
param(
    [string]$Dll    = "$PSScriptRoot\out\FontMod.dll",
    [string]$OutDir = "$PSScriptRoot\dist"
)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# --- Doc version tu Info.json (nguon su that) ---
$infoPath = Join-Path $root "FontMod\Info.json"
$info = Get-Content $infoPath -Raw | ConvertFrom-Json
$ver  = $info.Version
$id   = $info.Id
Write-Host "Packaging $id v$ver"

# --- Tap hop nguon: cap [duong dan nguon, duong dan trong zip] ---
$plugin = Join-Path $root "FontMod\TMP_Utils\TMPro_Plugin.dll"
$items = @(
    @{ Src = $infoPath;                                  Dst = "Info.json" }
    @{ Src = $Dll;                                       Dst = "FontMod.dll" }
    @{ Src = $plugin;                                    Dst = "TMPro_Plugin.dll" }
    @{ Src = Join-Path $root "release-assets\fontBuild.json";    Dst = "fontBuild.json" }
    @{ Src = Join-Path $root "release-assets\fontMappings.json"; Dst = "fontMappings.json" }
)

# --- Kiem tra ton tai truoc khi dong goi ---
foreach ($it in $items) {
    if (-not (Test-Path $it.Src)) { throw "THIEU file can thiet: $($it.Src)" }
}
$fontsDir = Join-Path $root "FontMod\Fonts"
$fonts = @(Get-ChildItem $fontsDir -Filter *.ttf -ErrorAction SilentlyContinue)
if ($fonts.Count -eq 0) { throw "Khong tim thay font .ttf nao trong FontMod\Fonts" }
# Kem ca file giay phep (OFL...) di cung font
$fontExtras = @(Get-ChildItem $fontsDir -File | Where-Object { $_.Extension -ne ".ttf" })

$atlasDir = Join-Path $root "FontMod\AtlasCache"
$atlas = @(Get-ChildItem $atlasDir -Filter *.atlas -ErrorAction SilentlyContinue)
if ($atlas.Count -eq 0) { throw "Khong tim thay file .atlas nao trong FontMod\AtlasCache" }
# Kem ca file ghi chu Atlas
$atlasExtras = @(Get-ChildItem $atlasDir -File | Where-Object { $_.Extension -ne ".ttf" })

# --- Dung staging sach: <OutDir>\stage\FontMod\... ---
$stageRoot = Join-Path $OutDir "stage"
$modDir    = Join-Path $stageRoot $id
if (Test-Path $stageRoot) { Remove-Item $stageRoot -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $modDir "Fonts") | Out-Null
New-Item -ItemType Directory -Force (Join-Path $modDir "AtlasCache") | Out-Null

foreach ($it in $items) {
    Copy-Item $it.Src (Join-Path $modDir $it.Dst) -Force
}
foreach ($f in $fonts) {
    Copy-Item $f.FullName (Join-Path $modDir "Fonts\$($f.Name)") -Force
    Write-Host "  + Fonts\$($f.Name)  ($([math]::Round($f.Length/1KB)) KB)"
}
foreach ($x in $fontExtras) {
    Copy-Item $x.FullName (Join-Path $modDir "Fonts\$($x.Name)") -Force
    Write-Host "  + Fonts\$($x.Name)  (license/extra)"
}
foreach ($a in $atlas) {
    Copy-Item $a.FullName (Join-Path $modDir "AtlasCache\$($a.Name)") -Force
    Write-Host "  + AtlasCache\$($a.Name)  ($([math]::Round($a.Length/1KB)) KB)"
}
foreach ($t in $atlasExtras) {
    Copy-Item $t.FullName (Join-Path $modDir "AtlasCache\$($t.Name)") -Force
    Write-Host "  + AtlasCache\$($t.Name)  ($([math]::Round($t.Length/1KB)) KB)"
}

# --- Nen zip ---
$zip = Join-Path $OutDir "$id-$ver.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stageRoot "$id") -DestinationPath $zip -CompressionLevel Optimal
Remove-Item $stageRoot -Recurse -Force

$size = [math]::Round((Get-Item $zip).Length / 1MB, 2)
Write-Host ""
Write-Host "OK -> $zip  ($size MB)" -ForegroundColor Green
