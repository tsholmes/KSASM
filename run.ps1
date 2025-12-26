# Push-Location KSASM
# try {
#   dotnet run --termfont
# } finally {
#   Pop-Location
# }

dotnet build

Push-Location "C:\Program Files\StarMap"
try {
  .\StarMap.exe
} finally {
  Pop-Location
}
