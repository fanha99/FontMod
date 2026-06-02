$ErrorActionPreference = "Stop"
$csc = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
$M   = "F:\Games\PathfinderKingmaker\Kingmaker_Data\Managed"
$UMM = "$M\UnityModManager"
$refs = @()
$refs += "$M\mscorlib.dll","$M\System.dll","$M\System.Core.dll"
$refs += (Get-ChildItem "$M\UnityEngine*.dll" | ForEach-Object FullName)
$refs += "F:\FontMod-build\pub\Assembly-CSharp.dll","F:\FontMod-build\pub\Assembly-CSharp-firstpass.dll","$M\Newtonsoft.Json.dll","$M\UniRx.dll"
$refs += "F:\FontMod-build\pub\UnityModManager.dll","$UMM\0Harmony.dll"
$src = (Get-ChildItem -Recurse "F:\FontMod-build\FontMod" -Filter *.cs | ForEach-Object FullName)
$args = @("/nostdlib+","/noconfig","/target:library","/platform:x64","/unsafe+","/langversion:latest","/define:KM","/out:F:\FontMod-build\out\FontMod.dll")
$args += ($refs | ForEach-Object { "/r:$_" })
$args += $src
Write-Host "REF COUNT: $($refs.Count)  SRC COUNT: $($src.Count)"
& $csc @args
Write-Host "EXITCODE: $LASTEXITCODE"
