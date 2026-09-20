using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;

namespace LinuxDiskManager;

static class Program
{
    [STAThread]
    static void Main()
    {
        if (!IsAdministrator())
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath ?? Application.ExecutablePath,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(processInfo);
            }
            catch
            {
                // User declined the UAC prompt
                MessageBox.Show(
                    "Administrator privileges are required to access physical drives directly.",
                    "Elevation Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }

    private static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}