using System.Diagnostics;
using g3man.GMLP;
using g3man.Models;

public abstract class PatchOwner() {
	public abstract int IsHigherPriorityThan(PatchOwner other, List<Mod> mods);
}

public class ModPatchOwner(Mod mod, string path) : PatchOwner {
	private Mod mod = mod;
	private string path = path;
	public override int IsHigherPriorityThan(PatchOwner other, List<Mod> mods) {
		if (other is not ModPatchOwner otherModPatchOwner) throw new UnreachableException();
		
		int myIndex = mods.IndexOf(mod);
		int otherIndex = mods.IndexOf(otherModPatchOwner.mod);
		return int.Sign(myIndex - otherIndex);
	}
}

public class TestPatchOwner(int index, string name) : PatchOwner {
	private int index = index;
	private string name = name;
	public override int IsHigherPriorityThan(PatchOwner other, List<Mod> mods) {
		if (other is not TestPatchOwner otherTestPatchOwner) throw new UnreachableException();

		return int.Sign(otherTestPatchOwner.index - index);
	}
}