param(
    [string]$UnityExe='C:\Program Files\Unity\Editor\Unity.exe',
    [string]$GameDir='R:\BigCityLegacy\prod_game',
    [string]$BclDll=''
)
$ErrorActionPreference='Stop'
$project=Join-Path $PSScriptRoot 'UnityProject'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\TextureMap.cs') -Destination "$project\Assets\Editor\TextureMap.cs"
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\TreeTextureSwapper.cs') -Destination "$project\Assets\Editor\TreeTextureSwapper.cs"
function Invoke-UnityMethod([string]$method) {
    $log=Join-Path $project ($method+'.log')
    $process=Start-Process -FilePath $UnityExe -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-projectPath',('"'+$project+'"'),'-executeMethod',$method,'-winterPackManagedDir',('"'+(Join-Path $GameDir 'game_Data\Managed')+'"'),'-logFile',('"'+$log+'"'))
    $process.WaitForExit()
    if($process.ExitCode -ne 0) { throw "Unity failed ($method): $log" }
}
foreach($method in @('BuildWinterPack.Build','ValidateWinterPack.Run')) { Invoke-UnityMethod $method }
$buildArgs=@('build',(Join-Path $PSScriptRoot 'WinterPack.csproj'),'-c','Release',"-p:GameDir=$GameDir")
if($BclDll) { $buildArgs+="-p:BclDll=$BclDll" }
& dotnet @buildArgs
if($LASTEXITCODE -ne 0) { throw 'DLL build failed' }
Invoke-UnityMethod 'ValidateWinterPack.RunEmbedded'
