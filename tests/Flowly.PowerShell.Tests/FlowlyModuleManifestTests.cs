using System.Management.Automation;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Flowly.PowerShell.Tests;

public class FlowlyModuleManifestTests
{
    public class CmdletsToExport
    {
        [Fact]
        public void ListsEveryCmdletTheAssemblyDeclaresAndNothingElse()
        {
            var declared = typeof(Flowly.PowerShell.Cmdlets.ConnectFlowlyCommand).Assembly
                .GetTypes()
                .Select(type => type.GetCustomAttribute<CmdletAttribute>())
                .Where(cmdlet => cmdlet is not null)
                .Select(cmdlet => $"{cmdlet!.VerbName}-{cmdlet.NounName}")
                .Order()
                .ToArray();

            Assert.Equal(declared, ReadExportedCmdlets().Order());
        }

        [Fact]
        public void ExportsTheMessageStreamPartitionCmdlets()
        {
            var exported = ReadExportedCmdlets();

            Assert.Contains("Get-FlowlyMessageStreamPartition", exported);
            Assert.Contains("Resume-FlowlyMessageStreamPartition", exported);
        }

        private static string[] ReadExportedCmdlets()
        {
            var manifest = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Flowly.PowerShell.psd1"));
            var exports = Regex.Match(manifest, @"CmdletsToExport\s*=\s*@\((?<names>[^)]*)\)", RegexOptions.Singleline).Groups["names"].Value;

            return Regex.Matches(exports, @"'(?<name>[^']+)'").Select(match => match.Groups["name"].Value).ToArray();
        }
    }
}
