using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Stgadgets.Cli.Dev;

/// <summary>
/// This process's own stdin, stdout and stderr on Windows. .NET starts every child with handle inheritance on, so a
/// child gets every inheritable handle this process has, even with its own standard handles redirected. A server
/// started in the background would then hold this command's output open for as long as it runs, and whatever reads
/// that output, such as a pipe, would wait for the server to stop instead of for this command to finish.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class StandardHandles
{
    private const int StdInput = -10;   // STD_INPUT_HANDLE
    private const int StdOutput = -11;  // STD_OUTPUT_HANDLE
    private const int StdError = -12;   // STD_ERROR_HANDLE
    private const uint HandleFlagInherit = 1;

    /// <summary>Stops children inheriting this process's standard handles. They still get the ones given to them.</summary>
    public static void StopInheritance()
    {
        foreach (var handle in (int[])[StdInput, StdOutput, StdError])
        {
            var value = GetStdHandle(handle);
            if (value != 0 && value != -1)   // none, or INVALID_HANDLE_VALUE
                _ = SetHandleInformation(value, HandleFlagInherit, 0);   // a handle that can't be changed is left as it is
        }
    }

    // DllImport rather than LibraryImport, which would need unsafe code allowed in the whole CLI.
    [DllImport("kernel32.dll")]
    private static extern nint GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll")]
    private static extern int SetHandleInformation(nint handle, uint mask, uint flags);
}
