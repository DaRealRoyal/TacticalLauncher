Write-Host "Version string (example 1.2.1): " -ForegroundColor Cyan -NoNewline
$version = Read-Host

# update assembly version
$filepath = "TacticalLauncher\Properties\AssemblyInfo.cs"
$pattern = '\[assembly: AssemblyVersion\("(.*)"\)\]'
(Get-Content $filepath) | ForEach-Object{
    if($_ -match $pattern){
        '[assembly: AssemblyVersion("{0}.0")]' -f $version
    } else {
        $_	# Output line as is
    }
} | Set-Content $filepath

# build / publish app
dotnet publish -c Release -o ".\publish"

# find Squirrel.exe path and add an alias
Set-Alias Squirrel ($env:USERPROFILE + "\.nuget\packages\clowd.squirrel\2.9.42\tools\Squirrel.exe");

New-Item -ItemType Directory -Force -Path Releases

# download currently live version
Squirrel http-down --url "https://tmr.nalsai.de/download"

# build new version and delta updates
Squirrel pack --framework net6 --packId "TacticalLauncher" --packVersion $version --packAuthors "Da Real Royal" --packDir ".\publish" --icon ".\TacticalLauncher\images\icon.ico" --splashImage "spinner.gif"
