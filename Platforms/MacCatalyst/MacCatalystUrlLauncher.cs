using Foundation;
using TagbooruQuest.Services;
using UIKit;

namespace TagbooruQuest.Platforms.MacCatalyst
{
    public class MacCatalystUrlLauncher : IUrlLauncher
    {
        public async Task<bool> OpenUrlAsync(string url)
        {
            try
            {
                var nsUrl = new NSUrl(url);
                if (UIApplication.SharedApplication.CanOpenUrl(nsUrl))
                {
                    await UIApplication.SharedApplication.OpenUrlAsync(nsUrl, new UIApplicationOpenUrlOptions());
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}