using System.Diagnostics;
using System.Runtime.InteropServices;
using g3man.Core.Util;
using PatchCommon;

namespace g3man.Core.Git;

public static class GitPatch {
	public static void FindIntentions(string diffText, string filename, PatchIntentionAggregate<GitRecord> aggregate) {
		string codeFilename = GetTargetFilename(diffText);
		aggregate.AddIntention(false, new PatchIntention<GitRecord>(codeFilename, filename, critical: true, failFast: true, 
			action: (record, source, info) => {
				CodeFile? file = source.GetCodeFile(codeFilename);
				string original = file.GetAsString();
				string applied = ApplyDiff(file.GetAsString(), diffText);
				if (record.NewContent is null)
					record.NewContent = applied;
				else
					record.NewContent = ThreeWayMerge(original, record.NewContent, applied);
				record.Success = true;
			}));
	}
	
	
	
		
	[DllImport(C.LIBG3MAN_NAME)]
	private static extern IntPtr create_diff(string original, nint original_size, string modified, nint modified_size, string filename);
	
	[DllImport(C.LIBG3MAN_NAME)]
	private static extern IntPtr apply_diff(string original, nint original_size, string diff, nint diff_size);
	
	[DllImport(C.LIBG3MAN_NAME)]
	private static extern IntPtr get_string_from_git_buf(IntPtr buf, out IntPtr ptr, out nint string_size);
	
	[DllImport(C.LIBG3MAN_NAME)]
	private static extern IntPtr free_git_buf(IntPtr buf);
	
	[DllImport(C.LIBG3MAN_NAME)]
	private static extern IntPtr three_way_merge(string basis, nint basis_size, string ours, nint ours_size, string theirs, nint theirs_size, out int automerged);

	[DllImport(C.LIBG3MAN_NAME)]
	private static extern IntPtr diff_get_target_filename(string diff, nint diff_size);
	
	[DllImport(C.LIBG3MAN_NAME)]
	private static extern IntPtr get_last_git_error();
	
	public static string CreateDiff(string filename, string original, string modified) {
		IntPtr buf = create_diff(original, original.Length,  modified, modified.Length, filename);
		return ConsumeGitBufOrThrow(buf);
	}
	public static string ApplyDiff(string text, string diff) {
		IntPtr buf = apply_diff(text, text.Length, diff, diff.Length);
		return ConsumeGitBufOrThrow(buf);
	}
	public static string GetTargetFilename(string diff) {
		IntPtr buf = diff_get_target_filename(diff, diff.Length);
		return ConsumeGitBufOrThrow(buf);
	}
	public static string? ThreeWayMerge(string basis, string ours, string theirs) {
		IntPtr buf = three_way_merge(basis, basis.Length, ours, ours.Length, theirs, theirs.Length, out int automerged);
		string result = ConsumeGitBufOrThrow(buf);
		if (automerged != 0) {
			return null;
		}
		return result;
	}
	
	private static string getLastGitError() {
		IntPtr buf = get_last_git_error();
		Debug.Assert(buf != IntPtr.Zero);
		return ConsumeGitBufOrThrow(buf);
	}

	private static string ConsumeGitBufOrThrow(IntPtr buf) {
		if (buf == IntPtr.Zero)
			throw new LibGit2Exception(getLastGitError());
		get_string_from_git_buf(buf, out IntPtr ptr, out nint size);
		string resultString = Marshal.PtrToStringUTF8(ptr, (int)size);
		free_git_buf(buf);
		return resultString;
	}

	public class LibGit2Exception(string message) : Exception(message);

	public static PatchResults Apply(RecordAggregate<GitRecord> recordAggregate, CodeSource source) {
		PatchResults results = new();
		foreach (KeyValuePair<string, GitRecord> pair in recordAggregate.GetChanges()) {
			string file = pair.Key;
			results.AddResult(file,pair.Value.NewContent!);
		}
		return results;
	}
}