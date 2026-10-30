param([Parameter(Mandatory=$true)][string]$OutputDirectory,[string]$PackageCache='')
$ErrorActionPreference='Stop'; $root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')); $out=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $out)-and @(Get-ChildItem -LiteralPath $out -Force).Count -gt 0){throw 'Çıktı klasörü boş olmalı; mevcut paketin üzerine yazılmaz.'}
$oldCache=$env:NUGET_PACKAGES; $oldAppData=$env:APPDATA
try{
 $env:NUGET_PACKAGES=if($PackageCache){[IO.Path]::GetFullPath($PackageCache)}else{Join-Path $root '.packages'}; $env:APPDATA=Join-Path $root '.appdata'; New-Item -ItemType Directory -Force -Path $env:NUGET_PACKAGES,$env:APPDATA|Out-Null
 $project=Join-Path $root 'src/Halberion.Nexus.App/Halberion.Nexus.App.csproj'; $props=@('-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true','-p:IncludeAllContentForSelfExtract=true','-p:EnableCompressionInSingleFile=true','-p:PublishTrimmed=false','-p:DebugType=None','-p:DebugSymbols=false')
 & dotnet publish $project -c Release -r win-x64 --self-contained true --configfile (Join-Path $root 'NuGet.Config') -o $out @props; if($LASTEXITCODE -ne 0){throw "Paketleme başarısız: $LASTEXITCODE"}
 Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $out 'LICENSE')
 Copy-Item -LiteralPath (Join-Path $root 'docs/THIRD-PARTY-NOTICES.md') -Destination (Join-Path $out 'THIRD-PARTY-NOTICES.md')
 Get-FileHash -LiteralPath (Join-Path $out 'HalberionNexus.exe') -Algorithm SHA256
}finally{$env:NUGET_PACKAGES=$oldCache;$env:APPDATA=$oldAppData}
