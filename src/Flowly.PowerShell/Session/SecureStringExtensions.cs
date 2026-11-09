using System.Runtime.InteropServices;
using System.Security;

namespace Flowly.PowerShell.Session;

internal static class SecureStringExtensions
{
    public static string ToPlainText(this SecureString secureString)
    {
        var pointer = Marshal.SecureStringToGlobalAllocUnicode(secureString);

        try
        {
            return Marshal.PtrToStringUni(pointer) ?? string.Empty;
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(pointer);
        }
    }

    public static SecureString ToReadOnlyCopy(this SecureString secureString)
    {
        var copy = secureString.Copy();
        copy.MakeReadOnly();

        return copy;
    }
}
