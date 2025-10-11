# g3man
**G**ame**M**aker **M**od **Man**ager

A mod manager and mod patcher for GameMaker games. 

Depends on [UndertaleModLib](https://github.com/UnderminersTeam/UndertaleModTool) and [Underanalyzer](https://github.com/UnderminersTeam/Underanalyzer).

## Background
(as of 7/10/2025)

The GameMaker modding ecosystem is strange.

Most mods use UndertaleModTool to modify the game.
UndertaleModTool has existed for a long time, and it is a great, incredibly powerful tool for modding GameMaker games. There's really nothing it can't do.

Yet, basic things you'd expect with mods for other games are usually not possible for your average GameMaker game (unless it has official modding support):
- Loading several mods at once
- Mods not existing in a format which makes them *impossible* to update game versions without reconstructing them from scratch

Due to the fact that all the data and code for GameMaker games are inside a single binary file, it is basically impossible
to distribute patches for it in a manner that works with several mods applied at the same time.

Additionally, when a game updates, that update is of course pushed as a new data.win. So none of the old patches work for it, and for mods to update,
mod creators have to manually reconstruct the mod with the new data.win as a base.

There are tools that address this, for example, [YYToolkit](https://github.com/AurieFramework/YYToolkit) - but its approach is much less accessible to most people.

g3man attempts to solve these problems while staying firmly in GameMaker-Land, and only by patching the data.win.

1. A patching format

	From my understanding, a lot of mod creators in the past have kept all the changes they've done to a data.win in some text document, so that
	if the game they're modding updates in the future, they could reference the file and restore all their changes.

	This is basically what g3man's patch format is. It's a small list of instructions that say where to insert code.
	The patch format is designed to be incredibly simple, and to be as compatible as possible with other patches (so several patches can modify the same file).
	For documentation and examples, check TODO.

2. Data merging

	Mods don't just modify existing code, they need to add new code entries and new assets.
	g3man allows you to do this in a unique way: You make a GameMaker project for your mod!
	In it, you will add all the sprites, objects, and code files, etc. Then, after compiling it and copying the data.win to your mod,
	g3man will copy everything from your data.win to the base game's data.win, merging them.

	This solves a problem I've seen in many attempts at GameMaker mod loaders/patchers - 
	you have to add (and maintain!!) a way for people to add new assets.

	This approach also has several benefits:
	- You get to use the GameMaker IDE to define the new data
	- Depending on the nature of your mod, you can run it from the IDE, and use the GameMaker debugger/profiler
	- You can still distribute differential patches like .xdelta for your mod, for users who wish to run just your mod and don't want to download g3man

	A small downside is that your asset indices get offset. You have to use asset_get_index() to get their index every time (i.e. instead of `spr_player`, you have to reference it using `asset_get_index("spr_player")`), as the indices for each asset will only be known post-merge.


Since g3man operates on the data.win only, it does not work for games compiled with YYC (games where the code is transpiled to C++, compiled, then embedded into the executable). To my knowledge, the only option available for YYC games right now is YYToolkit.

	

## Compiling
TODO


## Contributing
Please contribute