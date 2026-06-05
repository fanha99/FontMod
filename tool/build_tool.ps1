$ErrorActionPreference = "Stop"
$csc = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
$M   = "F:\Games\PathfinderKingmaker\Kingmaker_Data\Managed"
$root = "F:\FontMod-build"
$bin = "$root\tool\bin"

New-Item -ItemType Directory -Force $bin | Out-Null

$plugin = "$root\FontMod\TMP_Utils\TMPro_Plugin.dll"

$src = @(
    "$root\tool\FontPrebuild.cs",
    "$root\FontMod\Shared\FontBuildShared.cs"   # NGUON SU THAT dung chung voi mod
)

$args = @(
    "/target:exe", "/platform:x64", "/langversion:latest",
    "/out:$bin\FontPrebuild.exe",
    "/r:System.Web.Extensions.dll"              # JavaScriptSerializer (doc JSON, khong can Unity)
)
$args += $src

& $csc @args
Write-Host "EXITCODE: $LASTEXITCODE"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Native plugin nam canh exe de DllImport tim thay
Copy-Item $plugin "$bin\TMPro_Plugin.dll" -Force
Write-Host "OK -> $bin\FontPrebuild.exe"
