using System.Diagnostics;
using gmlp;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

namespace gmlpweb;

public class Program {
	private static void Main(string[] args) {
		init(args);
	}

	private static async void init(string[] args) {
		try {
			WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
			builder.Services.AddScoped(sp => new HttpClient
				{ BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

			WebAssemblyHost host = builder.Build();
			IJSRuntime js = host.Services.GetRequiredService<IJSRuntime>();
			await js.InvokeVoidAsync("onBlazorInitialized");
			
			await host.RunAsync();
		}
		catch (Exception e) {
			// need to do this (seemingly redundant) catch because of the async keyword, apparently
			Console.Error.WriteLine(e);
		}
	}
	
	[JSInvokable("patch")]
	public static string patch(string patch, string code) {
		PatchesRecord record = new PatchesRecord();
		CodeSource source = new SingleCodeSource(code);
		Language.ExecuteEntirePatch(patch, source, record, new PatchOwner(""));
		
		Language.ApplyPatches(record, source, []);

		CodeFile? result = source.GetCodeFile("");
		Debug.Assert(result is not null);
		return result.GetAsString();
	}
	
	
	/** gmlp expects to work with a class that can provide several code files, but
	 * we only need to work with 1.
	 */
	private class SingleCodeSource(string only) : CodeSource {
		private string only = only;
		public override CodeFile? GetCodeFile(string _) {
			return new StringCodeFile(only);
		}

		public override void Replace(string _, string code) {
			only = code;
		}
	}
}