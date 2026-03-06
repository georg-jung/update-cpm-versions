using System.Xml;
using System.Xml.Linq;

using NuGet.Versioning;

namespace UpdateCpmVersions;

record PackageEntry(string Id, NuGetVersion Version, XElement Element);

static class PackagePropsParser
{
    private const string FileName = "Directory.Packages.props";

    public static string FindFile(string? path)
    {
        if (path is not null)
        {
            if (File.Exists(path) &&
                Path.GetFileName(path).Equals(FileName, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(path);
            }

            if (Directory.Exists(path))
            {
                var filePath = Path.Combine(path, FileName);
                if (File.Exists(filePath))
                {
                    return Path.GetFullPath(filePath);
                }
            }

            throw new FileNotFoundException($"{FileName} not found at '{path}'.");
        }

        var dir = Directory.GetCurrentDirectory();
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, FileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException(
            $"{FileName} not found in current directory or any parent.");
    }

    public static (XDocument Document, List<PackageEntry> Packages) Parse(string filePath)
    {
        var doc = XDocument.Load(filePath, LoadOptions.PreserveWhitespace);
        var packages = new List<PackageEntry>();

        foreach (var element in doc.Descendants())
        {
            if (element.Name.LocalName is "PackageVersion" or "GlobalPackageReference")
            {
                var id = element.Attribute("Include")?.Value;
                var versionStr = element.Attribute("Version")?.Value;
                if (id is not null
                    && versionStr is not null
                    && NuGetVersion.TryParse(versionStr, out var version))
                {
                    packages.Add(new PackageEntry(id, version, element));
                }
            }
        }

        return (doc, packages);
    }

    public static void UpdateVersion(PackageEntry entry, NuGetVersion newVersion)
    {
        entry.Element.SetAttributeValue("Version", newVersion.ToNormalizedString());
    }

    public static void Save(XDocument doc, string filePath)
    {
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = doc.Declaration is null,
            Indent = false,
            NewLineHandling = NewLineHandling.None,
        };
        using var writer = XmlWriter.Create(filePath, settings);
        doc.Save(writer);
    }
}
