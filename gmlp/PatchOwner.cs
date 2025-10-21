using System.Collections.Generic;
using System.Diagnostics;

namespace gmlp;

public class PatchOwner(string name) {
	public virtual int IsHigherPriorityThan(PatchOwner other, List<PatchOwner> order) {
		int myIndex = order.IndexOf(this);
		int otherIndex = order.IndexOf(other);
		return int.Sign(myIndex - otherIndex);
	}
}
