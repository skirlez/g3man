### A short essay on patches and intent

In a theoretical, perfect patching system - your mod would be conveyed in exact intent of what you would like to change. Then, during patching, something that is capable of understanding your intent would go inside the game and make those changes.

No such system exists, but I believe you can analyze every system that applies some "patch" ontop of an existing code structure as something that wants to get close to that ideal.

Take git, for example. Commits there are your "patches", and they contain your intent in the form of some precise modifications to the software's code.

If you happen to pull a new commit while you still haven't pushed your commit,
most of the time git will still be able to add your commit ontop with no issue. For example, if the new commit doesn't touch any of the files you do.

However, if, for example, the new commit does change some code your commit also changes - or, if, for example, it renames one of the functions you were using - you can't just apply the commit ontop. Either git doesn't know how to merge your changes, or the new combined software won't be functional. It cannot know your intent.

Only you do, and that's why you can come in and make the necessary changes to make the mod work again; For example, by solving the merge conflict in a way that maintains your mod's functionality.

Without the ideal patching system described earlier - one that would simply know what you were trying to do when you were writing the mod - the step described earlier must eventually happen if the software's "base" changes.

### g3man's patching system
What g3man's patching system aims to do is to allow you to define your intent more explicitly compared to, say, a .diff file.

You can think of making each patch as programming some robot that tries to make changes to a code file.

You can tell it to use specific "markers" in the file - like looking where some statement is made and moving to its line number - rather than having hardcoded line numbers, as those could easily change.

You fill the robot with as much of your intent as you wish. If you do it well, each patch could likely survive a few updates even in code that gets changed.

You can also make the robot really stupid and just move to a specific line and write text there.

The goal is to give you the choice - for maximum robustness, you can define a patch to identify the code you want to change in ways that are more resistant to change. But if you just want to write a patch for some version of the game and you don't care about updates/don't mind maintaining it, you can make very small patches to do precisely that.
