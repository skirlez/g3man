# g3man
**G**ame**M**aker **M**od **Man**ager

<img alt="image" src="https://github.com/user-attachments/assets/b0581803-f844-4ce0-8b32-a00507c9c26a" />


A mod manager and mod patcher for GameMaker games. 

Utilizes the incredible [UndertaleModLib](https://github.com/UnderminersTeam/UndertaleModTool) and [Underanalyzer](https://github.com/UnderminersTeam/Underanalyzer) libraries.

This repository also contains gmlpweb by [@hexfae](https://github.com/hexfae),
an interactive website for creating GMLPv2 patches. You can try it out [here](https://skirlez.github.io/g3man/)!
(GMLPv2 patches will be usable in version 8, right now they're on the main branch).

## Features
- Support for all GameMaker games compiled in VM mode (theoretically)
- Its own mod format:
	- Mods can be applied simultaneously, and should keep working after most updates
	- Mod scripting: mods can run .csx scripts, like UndertaleModTool
	- A custom patch language/format called GMLP
  - Dependency checking
- A profile system to easily switch between sets of mods
- Profile save separation (opt-in)
- Xdelta mod support: g3man allows, alongside its own mod format, the loading of .xdelta mod(s)*
- General Xdelta support: g3man mods themselves can employ .xdelta files to patch other game files
- Support for both Linux and Windows
- (limited) CLI support
- Efficient pre-loading of the game's datafile

*(You can have several Xdelta patches, but every consecutive patch has to be made for the output of the prior patches)

## Coming in Version 9
- GMLPv2: a Lua-based patching solution

## Installation

### Nix/NixOS

The included flake has a default package output. Just make sure to select a version tag so you don't install
the bleeding edge release. Have fun.

### Everyone Else

Go to the [Releases](https://github.com/skirlez/g3man/releases) page, and follow the installation guide for your release.

## Mods made with g3man

- [Void Stranger Endless Void](https://github.com/skirlez/void-stranger-endless-void/): a level builder for Void Stranger

- [Nubby's Forgery](https://github.com/skirlez/nubbys-forgery) and the example mods (found in the [g3man examples monorepo](https://github.com/skirlez/g3man-examples)): an API for Nubby's Number Factory, and two mods that depends on it to add new things to the game

- [NubPak!](https://gamebanana.com/mods/618918): a huge expansion mod for Nubby's Number Factory

Have YOU made ANYTHING with g3man? I would love to add more to this list! Please open an issue regarding your creation, even if it's something really small.

## Building g3man
See this [wiki page](https://github.com/skirlez/g3man/wiki/Building-g3man-and-gmlpweb).

## TODO List

If you're thinking of contributing, here's a list of things that g3man should have.


- Support for more platforms
  
  MacOS support, for example, should be fairly trivial if anyone is interested in adding support for it. There's already an OSX constant defined in the project, I just don't have any Apple hardware.
- Support for GameMaker Studio 1.4

  I've done all testing for (relatively) recent versions of GameMaker.
  With how g3man is built, GMS1.4 support wouldn't be very hard to do.
  Who knows, maybe it even works. I actually haven't tested it.
  Support would also involve updating the build scripts to support building a GMS1.4 project.
- Translations

  Currently all messages are English and hardcoded. There's not *that* many, so this wouldn't be hard to change, I feel.
- Drag and Drop support for ZIPs

  GTK has an API for this, so I'll probably implement this soon.

- Use an actual testing library for gmlp tests

- Git diff support as a patching format

- YYC
  Data merging should still at least partially work for YYC games, for sprite replacements - but it crashes right now.

- App icon

- Code Documentation

  There's not much right now. Would be nice to have

## License
AGPLv3. Xdelta code included in `c/xdelta3` is Apache License 2.0 (`c/xdelta3/LICENSE`)


## Contributing
Please contribute
