using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace SplashEdit.EditorCode
{
    /// <summary>
    /// Installs the MIPS cross-compiler toolchain and GNU Make.
    /// Supports Windows and Linux only.
    /// </summary>
    public static class ToolchainInstaller
    {
        private static bool _installing;

        public static string MipsVersion = "14.2.0";

        /// <summary>
        /// Runs an external process and waits for it to exit.
        /// </summary>
        public static async Task RunCommandAsync(string fileName, string arguments, string workingDirectory = "")
        {
            if (fileName.Equals("mips", StringComparison.OrdinalIgnoreCase))
            {
                fileName = "powershell";
                string roamingPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string scriptPath = Path.Combine(roamingPath, "mips", "mips.ps1");
                arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" {arguments}";
            }

            var tcs = new TaskCompletionSource<int>();

            Process process = new Process();
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.UseShellExecute = true;

            if (!string.IsNullOrEmpty(workingDirectory))
                process.StartInfo.WorkingDirectory = workingDirectory;

            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) =>
            {
                tcs.SetResult(process.ExitCode);
                process.Dispose();
            };

            process.Start();

            int exitCode = await tcs.Task;
            if (exitCode != 0)
                throw new Exception($"Process '{fileName}' exited with code {exitCode}");
        }

        /// <summary>
        /// Installs the MIPS GCC cross-compiler for the current platform.
        /// </summary>
        public static async Task<bool> InstallToolchain()
        {
            if (_installing) return false;
            _installing = true;

            try
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    if (!ToolchainChecker.IsToolAvailable("mips"))
                    {
                        await RunCommandAsync("powershell",
                            "-c \"& { iwr -UseBasicParsing https://raw.githubusercontent.com/grumpycoders/pcsx-redux/main/mips.ps1 | iex }\"");
                        EditorUtility.DisplayDialog("Reboot Required",
                            "Installing the MIPS toolchain requires a reboot. Please reboot and try again.",
                            "OK");
                        return false;
                    }
                    else
                    {
                        await RunCommandAsync("mips", $"install {MipsVersion}");
                    }
                }
                else if (Application.platform == RuntimePlatform.LinuxEditor)
                {
                    if (ToolchainChecker.IsToolAvailable("apt"))
                        await RunCommandAsync("pkexec", "apt install g++-mipsel-linux-gnu -y");
                    else if (ToolchainChecker.IsToolAvailable("trizen"))
                        await RunCommandAsync("trizen", "-S cross-mipsel-linux-gnu-binutils cross-mipsel-linux-gnu-gcc");
                    else
                        throw new Exception("Unsupported Linux distribution. Install mipsel-linux-gnu-gcc manually.");
                }
                else
                {
                    throw new Exception("Only Windows and Linux are supported.");
                }
                return true;
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Error",
                    $"Toolchain installation failed: {ex.Message}", "OK");
                return false;
            }
            finally
            {
                _installing = false;
            }
        }

        /// <summary>
        /// Installs GNU Make. On Windows it is bundled with the MIPS toolchain.
        /// </summary>
        public static async Task InstallMake()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Install GNU Make",
                    "On Windows, GNU Make is included with the MIPS toolchain installer. Install the full toolchain?",
                    "Yes", "No");
                if (proceed) await InstallToolchain();
            }
            else if (Application.platform == RuntimePlatform.LinuxEditor)
            {
                if (ToolchainChecker.IsToolAvailable("apt"))
                    await RunCommandAsync("pkexec", "apt install build-essential -y");
                else
                    throw new Exception("Unsupported Linux distribution. Install 'make' manually.");
            }
            else
            {
                throw new Exception("Only Windows and Linux are supported.");
            }
        }
    }
}
