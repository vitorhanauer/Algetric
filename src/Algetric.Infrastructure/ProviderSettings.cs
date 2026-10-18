using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Algetric.Core;

namespace Algetric.Infrastructure;

public sealed class ProviderSettings(string directory)
{
    private string PathName => Path.Combine(directory, "provider.json");
    public async Task<TileProvider?> LoadAsync()
    {
        if (!File.Exists(PathName)) return null;
        var provider = JsonSerializer.Deserialize<TileProvider>(await File.ReadAllTextAsync(PathName));
        provider?.Validate(); return provider;
    }
    public async Task SaveAsync(TileProvider provider)
    {
        provider.Validate(); Directory.CreateDirectory(directory);
        var temporary = PathName + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(provider)); File.Move(temporary, PathName, true);
    }
    public static string Identifier(string template) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(template)))[..16].ToLowerInvariant();
}
public sealed class LinuxSecretStore(string executable = "secret-tool") : IProviderSecretStore
{
    public Task<string?> GetAsync(string provider) => RunAsync(["lookup", "application", "algetric", "provider", provider], null);
    public async Task SetAsync(string provider, string secret)
    { await RunAsync(["store", "--label=Algetric: mapas", "application", "algetric", "provider", provider], secret); }
    private async Task<string?> RunAsync(string[] arguments, string? input)
    {
        var info = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true, UseShellExecute = false };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        try
        {
            using var process = Process.Start(info) ?? throw new IOException("Não foi possível iniciar secret-tool.");
            if (input is not null) await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(); await error;
            if (process.ExitCode != 0 && input is not null) throw new IOException("Não foi possível guardar a credencial no serviço de segredos.");
            return process.ExitCode == 0 ? (await output).TrimEnd('\r', '\n') : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            if (input is null) return null;
            throw new IOException("Instale libsecret/secret-tool para usar credenciais de mapas.");
        }
    }
}
