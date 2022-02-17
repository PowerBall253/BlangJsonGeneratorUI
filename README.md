# BlangJsonGeneratorUI
GUI utility for creating string mods for DOOM Eternal.

![Screenshot](screenshot.png)

## Usage
First, make sure you have the [.NET 6 Runtime](https://dotnet.microsoft.com/en-us/download) installed.

Afterwards, just double click on the executable on Windows and macOS, or run it from the terminal on Linux.

## Compiling
First, make sure you have the [.NET 6 SDK](https://dotnet.microsoft.com/en-us/download) installed.

Then, from a terminal, run:

```
git clone https://github.com/PowerBall253/BlangJsonGeneratorUI.git
cd BlangJsonGeneratorUI
dotnet publish -p:PublishProfile=FolderProfile -r <rid>
```

(Replace `<rid>` with `win-x64` for Windows, `linux-x64` for Linux and `osx-x64` for macOS.)

The compiled binaries will be found in `BlangJsonGeneratorUI/BlangJsonGenerator/bin/Release/net6.0/publish/`.

## Special thanks to
* proteh: For the BlangParser library, which is used in this project.
