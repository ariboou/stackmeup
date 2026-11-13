# StackMeUp'ı başkasına gönderilebilecek tek bir exe olarak hazırlar: dagitim\StackMeUp.exe
# .NET kurulu olmayan bilgisayarda da çalışır. Sıkıştırma kapalı: dosya büyük ama bellek kullanımı düşük.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }

& $dotnet publish (Join-Path $root 'src\StackMeUp.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=false -p:DebugType=none `
    -o (Join-Path $root 'dagitim') -nologo -v q
& $dotnet build-server shutdown | Out-Null

$exe = Get-Item (Join-Path $root 'dagitim\StackMeUp.exe')
"Hazır: $($exe.FullName)  ($([math]::Round($exe.Length / 1MB, 1)) MB)"
