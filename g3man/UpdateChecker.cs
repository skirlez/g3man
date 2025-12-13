using System.Net.Http.Headers;
using System.Text.Json;

namespace g3man;

public class UpdateChecker(Action OnStarted, Action<int> OnCompletion) {
    private const string URL = "https://api.github.com/repos/skirlez/g3man/releases/latest";
    private volatile int Ready = 1;
    public void Check() {
        if (Interlocked.CompareExchange(ref Ready, 0, 1) == 0)
            return;
        OnStarted.Invoke();
        new Thread(async void () => {
            try {
                using HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
                client.DefaultRequestHeaders.Add("User-Agent", "g3man Update Checker");
                string result = await client.GetStringAsync(URL);
                Dictionary<string, object> dictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(result)!;
                string TagName = string.Join("", dictionary["tag_name"].ToString()!.Where(char.IsDigit));
                int version = int.Parse(TagName);
                OnCompletion.Invoke(version);
            }
            catch (Exception e) {
                OnCompletion.Invoke(0);
            }
            Ready = 1;
        }) {
            IsBackground = true
        }.Start();
    }
}