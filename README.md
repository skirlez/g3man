# g3man
**G**ame**M**aker **M**od **Man**ager

<img alt="image" src="https://github.com/user-attachments/assets/b01774bc-eb69-4ec3-9336-2c6579cda8b2" />

A mod manager and mod patcher for GameMaker games. 

Depends on [UndertaleModLib](https://github.com/UnderminersTeam/UndertaleModTool) and [Underanalyzer](https://github.com/UnderminersTeam/Underanalyzer). These two projects are incredible, and I would not have been able to make this without them.

This repository also contains gmlpweb by [@hexfae](https://github.com/hexfae), an interactive website for creating gmlp patches used by g3man. You can try it out [here](https://skirlez.github.io/g3man/)!

## Features

- Support for all GameMaker games compiled with VM mode (theoretically)
- A profile system to easily switch between sets of mods
- Mod scripting: mods can run .csx scripts, like UndertaleModTool
- Support for both Linux and Windows

## Mods made with g3man

[Void Stranger Endless Void](https://github.com/skirlez/void-stranger-endless-void/): a level builder for Void Stranger

[Nubby's Forgery](https://github.com/skirlez/nubbys-forgery) and the [example mod](https://github.com/Skirlez/nubbys-forgery-example-mod): an API for Nubby's Number Factory, and a mod that depends on it to add new things to the game

Have YOU made ANYTHING with g3man? I would love to add more to this list! Please open an issue regarding your creation, even if it's something really small.

## Building g3man

#### Nix/NixOS
```bash
nix develop
dotnet build g3man
```

### Linux (Other)

1. Install GTK4 and libadwaita
2. Download .NET 8 SDK
3. Clone the repository
4. Run `dotnet build g3man` inside the repository folder

### Windows

1. Download and install [msys2](https://www.msys2.org/). 
At the moment the .csproj is hardcoded to use `C:/msys64`, so make sure you don't change the install location.
2. Open the MSYS2 MINGW64 shell
3. Run `pacman -Syu && pacman -S mingw-w64-x86_64-libadwaita` to install libadwaita (and GTK4)
4. Add `C:/msys64/mingw64/bin` to PATH (or wherever you placed msys64 if you changed it)
5. Download .NET 8 SDK
6. Clone the repository
7. Run `dotnet build g3man` inside the repository folder


## Building gmlpweb

### Nix/NixOS
```bash
nix develop
dotnet build gmlpweb
```
### Anything Else
1. Download .NET 8 SDK
2. Clone the repository
3. `dotnet build gmlpweb`


## TODO List

- Script Security

  Mods can run just any C# code they want, which is not ideal for security.
  Scripts are turned off by default for this reason, but there should likely be better measures in place.
- Parallelize the patching process
  
  Currently the patching process is mostly bottlenecked by compilation. It is done in sequence because
  Underanalyzer's compile function has side effects on the data.win. Theoretically this should be possible, and would
speed up patching immensely.

- Support for more platforms
  
  MacOS support, for example, should be fairly trivial if anyone is interested in adding support for it. There's already an OSX constant defined in the project, I just don't have any Apple hardware.
- Support for GameMaker Studio 1.4

  I've done all testing for (relatively) recent versions of GameMaker.
  With how g3man is built, GMS1.4 support wouldn't be very hard to do.
  Who knows, maybe it even works. I actually haven't tested it.
  Support would also involve updating the build scripts to support building a GMS1.4 project.
- Translations

  Currently all messages are English and hardcoded. There's not *that* many, so this wouldn't be hard to change, I feel.
- Code Documentation

  There's not much right now. Would be nice to have

## Contributing
Please contribute
