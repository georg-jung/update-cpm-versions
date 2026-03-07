using NuGet.Versioning;

using UpdateCpmVersions;

namespace UpdateCpmVersions.Tests;

public class PackagePropsParserTests
{
    private static string WriteTempFile(string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, "Directory.Packages.props");
        File.WriteAllText(filePath, content);
        return filePath;
    }

    [Test]
    public async Task Parse_ExtractsPackageVersions()
    {
        var path = WriteTempFile("""
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
                <PackageVersion Include="Serilog" Version="3.1.1" />
              </ItemGroup>
            </Project>
            """);

        var (_, packages) = PackagePropsParser.Parse(path);

        await Assert.That(packages).Count().IsEqualTo(2);
        await Assert.That(packages[0].Id).IsEqualTo("Newtonsoft.Json");
        await Assert.That(packages[0].Version).IsEqualTo(NuGetVersion.Parse("13.0.3"));
        await Assert.That(packages[1].Id).IsEqualTo("Serilog");
    }

    [Test]
    public async Task Parse_ExtractsGlobalPackageReferences()
    {
        var path = WriteTempFile("""
            <Project>
              <ItemGroup>
                <GlobalPackageReference Include="SomeAnalyzer" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var (_, packages) = PackagePropsParser.Parse(path);

        await Assert.That(packages).Count().IsEqualTo(1);
        await Assert.That(packages[0].Id).IsEqualTo("SomeAnalyzer");
    }

    [Test]
    public async Task Parse_SkipsEntriesWithoutVersion()
    {
        var path = WriteTempFile("""
            <Project>
              <ItemGroup>
                <PackageVersion Include="NoVersion" />
                <PackageVersion Include="WithVersion" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var (_, packages) = PackagePropsParser.Parse(path);

        await Assert.That(packages).Count().IsEqualTo(1);
        await Assert.That(packages[0].Id).IsEqualTo("WithVersion");
    }

    [Test]
    public async Task Parse_HandlesMultipleItemGroups()
    {
        var path = WriteTempFile("""
            <Project>
              <ItemGroup>
                <PackageVersion Include="A" Version="1.0.0" />
              </ItemGroup>
              <ItemGroup>
                <PackageVersion Include="B" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """);

        var (_, packages) = PackagePropsParser.Parse(path);

        await Assert.That(packages).Count().IsEqualTo(2);
    }

    [Test]
    public async Task UpdateVersion_ChangesVersionAttribute()
    {
        var path = WriteTempFile("""
            <Project>
              <ItemGroup>
                <PackageVersion Include="Foo" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var (doc, packages) = PackagePropsParser.Parse(path);
        PackagePropsParser.UpdateVersion(packages[0], NuGetVersion.Parse("1.2.0"));
        PackagePropsParser.Save(doc, path);

        var (_, reloaded) = PackagePropsParser.Parse(path);
        await Assert.That(reloaded[0].Version).IsEqualTo(NuGetVersion.Parse("1.2.0"));
    }

    [Test]
    public async Task Save_PreservesInlineComments()
    {
        var content = "<Project>\n"
            + "  <ItemGroup>\n"
            + "    <PackageVersion Include=\"Foo\" Version=\"1.0.0\" /> <!-- pinned for compat -->\n"
            + "    <PackageVersion Include=\"Bar\" Version=\"2.0.0\" /> <!-- another comment -->\n"
            + "  </ItemGroup>\n"
            + "</Project>\n";
        var path = WriteTempFile(content);

        var (doc, packages) = PackagePropsParser.Parse(path);
        PackagePropsParser.UpdateVersion(packages[0], NuGetVersion.Parse("1.2.0"));
        PackagePropsParser.Save(doc, path);

        var result = File.ReadAllText(path);
        var expected = content.Replace("Version=\"1.0.0\"", "Version=\"1.2.0\"");
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task FindFile_FindsInDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, "Directory.Packages.props");
        File.WriteAllText(filePath, "<Project />");

        var found = PackagePropsParser.FindFile(dir);

        await Assert.That(found).IsEqualTo(Path.GetFullPath(filePath));
    }

    [Test]
    public async Task FindFile_FindsDirectFilePath()
    {
        var path = WriteTempFile("<Project />");

        var found = PackagePropsParser.FindFile(path);

        await Assert.That(found).IsEqualTo(Path.GetFullPath(path));
    }

    [Test]
    public async Task FindFile_ThrowsWhenNotFound()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);

        await Assert.That(() => PackagePropsParser.FindFile(dir))
            .Throws<FileNotFoundException>();
    }

    [Test]
    public async Task FindFile_FindsInCurrentDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, "Directory.Packages.props");
        File.WriteAllText(filePath, "<Project />");

        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);
            var found = PackagePropsParser.FindFile(null);
            await Assert.That(found).IsEqualTo(filePath);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    [Test]
    public async Task FindFile_WalksUpDirectoryTree()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var child = Path.Combine(root, "child");
        Directory.CreateDirectory(child);
        var filePath = Path.Combine(root, "Directory.Packages.props");
        File.WriteAllText(filePath, "<Project />");

        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(child);
            var found = PackagePropsParser.FindFile(null);
            await Assert.That(found).IsEqualTo(filePath);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    [Test]
    public async Task FindFile_ThrowsWhenNotFoundInAnyParent()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);

        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);
            await Assert.That(() => PackagePropsParser.FindFile(null))
                .Throws<FileNotFoundException>();
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }
}
