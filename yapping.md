// THIS file will be copied to the wiki when the repo goes public


// i don't know where this would go

### A short essay on patches and intent

In a theoretical, perfect patching system - your mod would be conveyed in exact intent of what you would like to change. Then, during patching, something that is capable of understanding your intent would go inside the game and make those changes.

No such system exists, but I believe you can analyze every system that applies some "patch" ontop of an existing code structure as something that wants to get close to that ideal.

Take git, for example. Commits there are your "patches", and they contain your intent in the form of some precise modifications to the software's code.

If you happen to pull a new commit while you still haven't pushed your commit,
most of the time git will still be able to add your commit ontop with no issue. For example, if the new commit doesn't touch any of the files you do.

However, if, for example, the new commit does change some code your commit also changes - or, if, for example, it renames one of the functions you were using - you can't just apply the commit ontop. Either git doesn't know how to merge your changes, or the new combined software won't be functional. It cannot know your intent.

Only you do, and that's why you can come in and make the necessary changes to make the mod work again; For example, by solving the merge conflict in a way that maintains your mod's functionality.

Without the ideal patching system described earlier - one that would simply know what you were trying to do when you were writing the mod - the step described earlier must eventually happen if the software's "base" changes.

### g3man's patching system - "gmlp"
What g3man's patching system aims to do is to allow you to define your intent more explicitly compared to, say, a .diff file.

You can think of making each patch as programming some robot that tries to make changes to a code file.

You can tell it to use specific "markers" in the file - like looking where some statement is made and moving to its line number - rather than having hardcoded line numbers, as those could easily change.

You fill the robot with as much of your intent as you wish. If you do it well, each patch could likely survive a few updates even in code that gets changed.

You can also make the robot really stupid and just move to a specific line and write text there.

The goal is to give you the choice - for maximum robustness, you can define a patch to identify the code you want to change in ways that are more resistant to change. But if you just want to write a patch for some version of the game and you don't care about updates/don't mind maintaining it, you can make very small patches to do precisely that.


// main page

## Basics of gmlp

gmlp stands for GameMaker Line Patch. It could be considered a language of sorts.

gmlp patches are a list of instructions that are executed linearly. There are no branches in the language.
You have a "caret", which sits on a line, and the patch you write tells the caret to which lines it should move to, and what it should write.

We'll be using this code for the following examples:
```gml
a = 0
b = 0
c = 0
```

Here's an example:
```js
move_to(3)
write_before('show_message("Before c assignment")')
```
This program moves the caret to line 3 and writes something before it,
so the code becomes:
```gml
a = 0
b = 0
show_message("Before c assignment")
c = 0
```

This is technically sufficient for any patch you may want to make. However, doing patches like this is a really bad idea
if you want to maintain your patch in the future. **You should basically never use `move_to(line)`.**

Instead, most of the time you want to use `find_line_with(str)`. Here's an equivalent patch:
```js
find_line_with('c = 0')
write_before('show_message("Before c assignment")')
```
Now that is much more readable. Though the language has more than just this.

// use the github pages sidebar or whatever


## Good Principles for Compatibility

This page will contain a few tips on how to make your mod's patches as compatible with other patches as possible

### Overriding ifs
Say you have some if like:
```gml
if (condition) 
{
  // code here...
}
```
If you want to remove or override it entirely, you COULD use `write_replace`... But if another mod tries to do the same, there will be a patch conflict.

Instead, use `write_after`:

```js
find_line_with('if (condition)')
write_after('&& false') // if you want to remove the if
write_after('&& false || my_condition') // if you want to override the if entirely with your own condition
```
In case two mods try to override the if in this way, the one with the highest priority will win out.

### Extending else-if-else chains
Say you have some if like:
```gml
if (condition) 
{
  // code here...
}
```

If you would like to add an `else` or an `else if`, you COULD do something like:
```js
write_after('
else if (whatever) 
{
	// code...
}
')
```
But imagine that another mod's patch does a `write_after` on that line, and it happens to end up above your write. You may end up with a statement in-between the `if {}` and the `else`,
which would cause a patch error:
```gml
if (condition) 
{
  // ...
}
show_message("statement inserted by other mod")
else if (whatever) // <------------ can't have else here!
{ 
	// ...
}
```

To guarantee a safe order, you have two operations: `write_after_else` and `write_after_else_if`.
In short, if you use them, they guarantee that the case that was demonstrated cannot happen.

So instead of the patch snippet from earlier, you would do:
```js
write_after_else_if('(whatever) 
{
	// code...
}')
```

`write_after_else` specifically opens up an `else` block without an if, so it would end an if-else chain.
If two mods use `write_after_else`, it'll put them in the same `else` block (with respect patch priority/mod order).

You would use `write_after_else` like:
```js
write_after_else('
	// more code...
')
```
Combining those last two patches, no matter their priority, the result should look like this:

```gml
if (condition) 
{
  // code here...
}
else if (whatever)
{
	// code...
}
else
{
	// more code...
}
```