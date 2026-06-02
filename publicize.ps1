$ErrorActionPreference="Stop"
Add-Type -Path "F:\Games\PathfinderKingmaker\Kingmaker_Data\Managed\UnityModManager\dnlib.dll"
function Publicize($inp,$out){
  $mod=[dnlib.DotNet.ModuleDefMD]::Load($inp)
  $TV=[dnlib.DotNet.TypeAttributes]; $FV=[dnlib.DotNet.FieldAttributes]; $MV=[dnlib.DotNet.MethodAttributes]
  foreach($t in $mod.GetTypes()){
    $vis = if($t.IsNested){2}else{1}    # NestedPublic / Public
    $t.Attributes = [dnlib.DotNet.TypeAttributes](([int]$t.Attributes -band (-bnot 7)) -bor $vis)
    foreach($f in $t.Fields){ $f.Attributes = [dnlib.DotNet.FieldAttributes](([int]$f.Attributes -band (-bnot 7)) -bor 6) }
    foreach($m in $t.Methods){ $m.Attributes = [dnlib.DotNet.MethodAttributes](([int]$m.Attributes -band (-bnot 7)) -bor 6) }
  }
  $mod.Write($out); $mod.Dispose()
  Write-Host "WROTE $out"
}
$M="F:\Games\PathfinderKingmaker\Kingmaker_Data\Managed"
Publicize "$M\Assembly-CSharp-firstpass.dll" "F:\FontMod-build\pub\Assembly-CSharp-firstpass.dll"
Publicize "$M\Assembly-CSharp.dll" "F:\FontMod-build\pub\Assembly-CSharp.dll"
Publicize "$M\UnityModManager\UnityModManager.dll" "F:\FontMod-build\pub\UnityModManager.dll"
