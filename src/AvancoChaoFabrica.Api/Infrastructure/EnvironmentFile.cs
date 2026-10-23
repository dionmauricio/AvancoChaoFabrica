namespace AvancoChaoFabrica.Api.Infrastructure;

public static class EnvironmentFile
{
    public static void Load()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, ".env");
            if (File.Exists(path))
            {
                Load(path);
                return;
            }

            directory = directory.Parent;
        }
    }

    public static void Load(string path)
    {
        foreach (var (line, lineNumber) in File.ReadLines(path).Select((line, index) => (line, index + 1)))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
            {
                throw new InvalidDataException(
                    $"Invalid .env entry at line {lineNumber}; expected KEY=VALUE.");
            }

            var key = trimmed[..separator].Trim();
            var value = trimmed[(separator + 1)..].Trim();
            if (key.Length == 0 || key.Any(character =>
                    !char.IsAsciiLetterOrDigit(character) && character != '_'))
            {
                throw new InvalidDataException($"Invalid .env variable name at line {lineNumber}.");
            }

            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') ||
                 (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
