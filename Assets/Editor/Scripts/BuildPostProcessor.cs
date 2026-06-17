using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;

public static class BuildPostProcessor
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64)
            return;

        // Path to your built executable
        string exePath = pathToBuiltProject;
        string manifestPath = Path.Combine(Path.GetDirectoryName(exePath), "temp_admin.manifest");

        // 1. Create a manifest requesting administrator rights and UIAccess
        string manifestContent = @"<?xml version='1.0' encoding='UTF-8' standalone='yes'?>
<assembly xmlns='urn:schemas-microsoft-com:asm.v1' manifestVersion='1.0'>
  <trustInfo xmlns='urn:schemas-microsoft-com:asm.v3'>
    <security>
      <requestedPrivileges>
        <requestedExecutionLevel level='requireAdministrator' uiAccess='true' />
      </requestedPrivileges>
    </security>
  </trustInfo>
</assembly>";

        try
        {
            File.WriteAllText(manifestPath, manifestContent);

            string mtPath = ResolveMtPath();
            UnityEngine.Debug.Log($"[BuildPostProcessor] Embedding administrator manifest with UIAccess using Tool: {mtPath} on target: {exePath}");

            // 2. Embed the manifest into the built exe using mt.exe (Windows SDK tool)
            var psi = new ProcessStartInfo
            {
                FileName = mtPath,
                Arguments = $"-manifest \"{manifestPath}\" -outputresource:\"{exePath}\";#1",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using (var process = Process.Start(psi))
            {
                process?.WaitForExit();
                if (process != null && process.ExitCode != 0)
                {
                    UnityEngine.Debug.LogError($"[BuildPostProcessor] mt.exe failed with exit code: {process.ExitCode}");
                }
            }

            // 3. Sign the executable with the UIAccess certificate
            SignExecutable(exePath);
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[BuildPostProcessor] Failed to process standalone manifest and code-signing: {ex.Message}");
        }
        finally
        {
            // Clean up temporary manifest file
            if (File.Exists(manifestPath))
                File.Delete(manifestPath);
        }
    }

    private static void SignExecutable(string exePath)
    {
        string psCommand = $"$cert = Get-ChildItem Cert:\\LocalMachine\\My | Where-Object {{ $_.Subject -eq 'CN=RobitUiAutomationUIAccess' }} | Select-Object -First 1; if ($cert) {{ Set-AuthenticodeSignature -FilePath '{exePath}' -Certificate $cert }} else {{ Write-Error 'UIAccess code-signing certificate not found in LocalMachine Store' }}";
        
        var signPsi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        try
        {
            using (var process = Process.Start(signPsi))
            {
                process?.WaitForExit();
                string output = process?.StandardOutput.ReadToEnd();
                string error = process?.StandardError.ReadToEnd();
                
                if (process == null || process.ExitCode != 0)
                {
                    UnityEngine.Debug.LogError($"[BuildPostProcessor] Code-signing failed: {error} {output}");
                }
                else
                {
                    UnityEngine.Debug.Log("[BuildPostProcessor] Built game signed successfully with UIAccess certificate.");
                }
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[BuildPostProcessor] Failed to execute code-signing script: {ex.Message}");
        }
    }

    private static string ResolveMtPath()
    {
        // 1. Try system PATH first
        if (IsInPath("mt.exe"))
            return "mt.exe";

        // 2. Search common Windows SDK directories
        string sdkRoot = @"C:\Program Files (x86)\Windows Kits";
        if (Directory.Exists(sdkRoot))
        {
            // Try Windows 10/11 Kits
            string kits10 = Path.Combine(sdkRoot, "10", "bin");
            if (Directory.Exists(kits10))
            {
                var subDirs = Directory.GetDirectories(kits10);
                // Subdirectories are version names like 10.0.19041.0, 10.0.22621.0, etc.
                System.Array.Sort(subDirs);
                System.Array.Reverse(subDirs);

                foreach (var dir in subDirs)
                {
                    string x64Path = Path.Combine(dir, "x64", "mt.exe");
                    if (File.Exists(x64Path))
                        return x64Path;
                    
                    string x86Path = Path.Combine(dir, "x86", "mt.exe");
                    if (File.Exists(x86Path))
                        return x86Path;
                }
            }

            // Try Windows 8.1 Kits
            string kits81 = Path.Combine(sdkRoot, "8.1", "bin", "x64", "mt.exe");
            if (File.Exists(kits81))
                return kits81;
        }

        // Fallback to "mt.exe" and let it throw if missing
        return "mt.exe";
    }

    private static bool IsInPath(string fileName)
    {
        var values = System.Environment.GetEnvironmentVariable("PATH");
        if (values == null) return false;
        foreach (var path in values.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(path, fileName);
            if (File.Exists(fullPath))
                return true;
        }
        return false;
    }
}
