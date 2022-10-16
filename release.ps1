# TODO: automate updating release info (Assembly version, File version, copyright date, pack version)

# build / publish app
dotnet publish -c Release -o ".\publish"

# find Squirrel.exe path and add an alias
Set-Alias Squirrel ($env:USERPROFILE + "\.nuget\packages\clowd.squirrel\2.9.42\tools\Squirrel.exe");

New-Item -ItemType Directory -Force -Path  Releases

# download currently live version
Squirrel http-down --url "https://tacticalmath.games/download"

# build new version and delta updates
Squirrel pack --framework net6 --packId "TacticalLauncher" --packVersion "1.2.0" --packAuthors "Da Real Royal" --packDir ".\publish" --icon ".\TacticalLauncher\images\icon.ico"
# --splashImage "install.gif"
