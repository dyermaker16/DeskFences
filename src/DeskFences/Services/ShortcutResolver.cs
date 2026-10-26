namespace DeskFences.Services;

public static class ShortcutResolver
{
    // Returns the target of a .lnk shortcut, or null if it has none (some store apps have no target).
    public static string? GetTarget(string lnkPath)
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type == null)
                return null;

            dynamic shell = Activator.CreateInstance(type)!;
            dynamic link = shell.CreateShortcut(lnkPath);
            string target = link.TargetPath;
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
