using System.Text.RegularExpressions;
using Flowly.PowerShell.Session;

namespace Flowly.PowerShell.Tests;

public class ModuleLayoutTests
{
    public class Manifest
    {
        [Fact]
        public void IsNamedAfterThePackageId()
        {
            Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "Flowly.PowerShell.psd1")));
        }

        [Fact]
        public void RootModule_ExistsNextToTheManifest()
        {
            var rootModule = ReadManifestValue("RootModule");

            Assert.False(Path.IsPathRooted(rootModule));
            Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, rootModule)));
        }

        [Fact]
        public void RootModule_IsTheAssemblyOfThisProject()
        {
            Assert.Equal(typeof(FlowlyConnection).Assembly.GetName().Name + ".dll", ReadManifestValue("RootModule"));
        }
    }

    private static string ReadManifestValue(string key)
    {
        var manifest = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Flowly.PowerShell.psd1"));

        return Regex.Match(manifest, $@"^\s*{key}\s*=\s*'([^']*)'", RegexOptions.Multiline).Groups[1].Value;
    }
}
