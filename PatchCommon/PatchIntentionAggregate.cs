using System;
using System.Collections.Generic;
using common;

namespace PatchCommon;

/**
* An aggregate of several patches' intentions; used to hold all patch intentions coming from a mod that are of some type.
 * It also is responsible for associating patch intentions with a name.
*/
public class PatchIntentionAggregate<T>() where T : new() {
	private readonly List<PatchIntention<T>> FirstIntentions = [];
	private readonly List<PatchIntention<T>> LastIntentions = [];
	private readonly List<string> errors = [];
	
	public void AddStepsIfNecessary<E>(List<PatchStep<E>> firstPatchSteps, List<PatchStep<E>> lastPatchSteps, CodeSource source, E owner, Func<RecordAggregate<T>, CodeSource, PatchResults> apply) {
		AddStepIfNecessary(FirstIntentions, firstPatchSteps, source, owner, apply);
		AddStepIfNecessary(LastIntentions, lastPatchSteps, source, owner, apply);
	}

	private static void AddStepIfNecessary<E>(List<PatchIntention<T>> intentions, List<PatchStep<E>> steps, CodeSource source, E owner, Func<RecordAggregate<T>, CodeSource, PatchResults> apply) {
		if (intentions.Count == 0)
			return;

		Dictionary<string, List<PatchInfo>> blameMap = new();
		foreach (PatchIntention<T> intention in intentions) {
			string target = intention.Target;
			

			if (blameMap.ContainsKey(target))
				blameMap[target].Add(intention.Info);
			else
				blameMap.Add(target, [intention.Info]);
		}
		
		steps.Add(new PatchStep<E>(() => {
			RecordAggregate<T> aggregate = new();
			RealizeAll(intentions, aggregate, source);
			
			return apply(aggregate, source);

		}, blameMap, owner));
	}

	public RecordAggregate<T> RealizeAll(CodeSource source) {
		RecordAggregate<T> aggregate = new();
		RealizeAll(FirstIntentions, aggregate, source);
		RealizeAll(LastIntentions, aggregate, source);
		return aggregate;
	}

	private static void RealizeAll(List<PatchIntention<T>> intentions, RecordAggregate<T> aggregate, CodeSource source) {
		foreach (PatchIntention<T> intention in intentions) {
			T record = aggregate.GetOrCreate(intention.Target);
			
			intention.Realize(record, source);
		}
	}

	public void AddIntention(bool last, PatchIntention<T> patchIntention) {
		if (last)
			LastIntentions.Add(patchIntention);
		else
			FirstIntentions.Add(patchIntention);
	}

	public void AddError(string error) {
		errors.Add(error);
	}

	public bool HasErrors() {
		return errors.Count != 0;
	}

	public List<string> GetAllErrors() {
		return errors;
	}
}
/**
 * Patches have intentions; an intention is a procedure belonging to a patch
 * that can be run ("realized") which appends information to the record on where and what to patch.
 *
 * Example: gmlp's record keeps track of which operations will be applied to which lines.
 */
public readonly struct PatchIntention<T>(string target, string filename, bool critical, Action<T, CodeSource> action) where T : new() {
	public readonly string Target = target;
	public readonly PatchInfo Info = new PatchInfo(filename, critical);
	
	public void Realize(T record, CodeSource source) {
		action(record, source);
	}
}

public readonly struct PatchInfo(string filename, bool critical) {
	public readonly bool Critical = critical;
	public readonly string Filename = filename;
}

