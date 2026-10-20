using System;
using System.Collections.Generic;
using System.Linq;
using Android.Content;
using Android.Content.PM;
using aura.Models;
using aura.Services;

namespace aura.Android;

public class AndroidAppListProvider : IAppListProvider
{
    private readonly Context _context;

    public AndroidAppListProvider(Context context)
    {
        _context = context.ApplicationContext ?? context;
    }

    public List<ProcessItem> GetApplications(bool includeSystemApps)
    {
        var result = new List<ProcessItem>();
        try
        {
            var pm = _context.PackageManager;
            if (pm == null) return result;

            var installedApps = pm.GetInstalledApplications(PackageInfoFlags.MatchAll);
            if (installedApps == null || installedApps.Count == 0)
            {
                installedApps = pm.GetInstalledApplications(0);
            }

            if (installedApps == null) return result;

            string myPackage = _context.PackageName ?? string.Empty;

            foreach (var appInfo in installedApps)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(appInfo.PackageName)) continue;
                    if (appInfo.PackageName.Equals(myPackage, StringComparison.OrdinalIgnoreCase)) continue;

                    bool isSystem = (appInfo.Flags & ApplicationInfoFlags.System) != 0;

                    // If user unchecked "Показывать системные", filter out all system packages
                    if (!includeSystemApps && isSystem)
                    {
                        continue;
                    }

                    string label = pm.GetApplicationLabel(appInfo) ?? appInfo.PackageName;
                    if (string.IsNullOrWhiteSpace(label))
                    {
                        label = appInfo.PackageName;
                    }

                    result.Add(new ProcessItem
                    {
                        Name = appInfo.PackageName,
                        DisplayName = label,
                        Path = appInfo.PackageName,
                        PackageName = appInfo.PackageName,
                        Category = isSystem ? "System Application" : "Installed Application",
                        IsSystemApp = isSystem
                    });
                }
                catch
                {
                    // Ignore permissions or dead package
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Instance.Log("android", "WARN", $"Error querying installed applications: {ex.Message}");
        }

        return result.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
