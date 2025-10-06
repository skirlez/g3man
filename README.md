# g3man
**G**ame**M**aker **M**od **Man**ager

A mod manager and mod patcher for GameMaker games.

Depends on [UndertaleModLib](https://github.com/UnderminersTeam/UndertaleModTool) and [Underanalyzer](https://github.com/UnderminersTeam/Underanalyzer).

## Background

UndertaleModTool has existed for a long time, yet a general solution for loading several GameMaker mods at a time
never really came about. Due to the fact that all the data and code for GameMaker games is inside a single binary file, it is basically impossible
to distribute patches for it in a manner that works with several mods applied at the same time.

Additionally, when a game updates, that update is of course pushed as a new data.win. So none of the old patches work for it, and for mods to update,
mod creators have to manually reconstruct the mod with the new data.win as a base.

These are the problems g3man aims to solve. At its core, g3man provides two things:

1. A patching format

	From my understanding, a lot of mod creators in the past have kept all the changes they've done to a data.win in some text document, so that
	if the game they're modding updates in the future, they could reference the file and restore all their changes.

	This is basically what g3man's patch format is. It's a small list of instructions that say where to insert code.
	The patch format is designed to be incredibly simple, and to be as compatible as possible with other patches (so several patches can modify the same file).

2. Data merging

	Mods don't just modify existing code, they need to add new code entries and new assets.
	g3man allows you to do this in a unique way: You make a GameMaker project for your mod!
	In it, you will add all the sprites, objects, and code files. Then, after compiling it and copying the data.win to your mod,
	g3man will copy everything that you added to the base game's data.win, merging them.

	This solves a problem I've seen in many attempts at GameMaker mod loaders - 
	you have to add (and maintain!! god that would suck) a way for people to add new data.

	This approach also has several benefits:
	You get to use the GameMaker IDE to define the new data
	Depending on the nature of your mod, you can run it from the IDE, and use the GameMaker debugger/profiler
	

	

## Compiling
TODO

## Contributing
Please contribute