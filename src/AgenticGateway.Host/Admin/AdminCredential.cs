using System.Security.Cryptography;
using System.Text;

namespace AgenticGateway.Host.Admin;

public sealed class AdminCredential
{
    private readonly byte[] _key;

    public AdminCredential(IConfiguration configuration, ILogger<AdminCredential> logger)
    {
        var configured = Environment.GetEnvironmentVariable("AGENTIC_GATEWAY_ADMIN_KEY")
            ?? configuration["Gateway:AdminApiKey"];
        KeyFilePath = Path.GetFullPath(configuration["Admin:KeyFilePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "data", "admin.key"));

        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (Encoding.UTF8.GetByteCount(configured) < 32)
            {
                throw new InvalidOperationException("AGENTIC_GATEWAY_ADMIN_KEY must contain at least 32 random bytes.");
            }
            _key = Encoding.UTF8.GetBytes(configured);
            IsFileBacked = false;
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(KeyFilePath)!);
        if (!File.Exists(KeyFilePath))
        {
            var generated = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            try
            {
                using var stream = new FileStream(KeyFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(stream);
                writer.Write(generated);
                writer.Flush();
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(KeyFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                logger.LogInformation("Created local admin key at {KeyFilePath}. Read it locally to sign in to /admin.", KeyFilePath);
            }
            catch (IOException) when (File.Exists(KeyFilePath))
            {
                // Another process created the key first.
            }
        }

        var value = File.ReadAllText(KeyFilePath).Trim();
        if (Encoding.UTF8.GetByteCount(value) < 32)
        {
            throw new InvalidOperationException("The local admin key file must contain at least 32 random bytes.");
        }
        _key = Encoding.UTF8.GetBytes(value);
        IsFileBacked = true;
    }

    public string KeyFilePath { get; }
    public bool IsFileBacked { get; }

    public bool Matches(string? supplied)
    {
        if (string.IsNullOrEmpty(supplied) || supplied.Length > 4_096) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), _key);
    }
}
