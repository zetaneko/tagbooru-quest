using Android.Content;
using TagbooruQuest.Services;

namespace TagbooruQuest.Platforms.Android
{
    public class AndroidUrlLauncher : IUrlLauncher
    {
        public Task<bool> OpenUrlAsync(string url)
        {
            try
            {
                var context = global::Android.App.Application.Context;
                var uri = global::Android.Net.Uri.Parse(url);
                var intent = new Intent(Intent.ActionView, uri);
                intent.AddFlags(ActivityFlags.NewTask);
                context.StartActivity(intent);

                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }
    }
}