using System.Text;

namespace Artbrewify.Core;

public static class FileName
{
    public static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            sb.Append(invalid.Contains(c) || char.IsControl(c) ? '_' : c);
        }
        var result = sb.ToString().TrimEnd('.', ' ');
        return string.IsNullOrWhiteSpace(result) ? "untitled" : result;
    }

    public static string ExtensionFromUrl(string url)
    {
        var path = url.Split('?')[0];
        var dot = path.LastIndexOf('.');
        var slash = path.LastIndexOf('/');
        if (dot > slash && dot < path.Length - 1)
        {
            var ext = path[(dot + 1)..].ToLowerInvariant();
            if (ext.Length <= 5 && ext.All(char.IsLetterOrDigit))
                return ext;
        }
        return "jpg";
    }
}