using System.IO.Compression;

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}

if (args.Length < 2)
{
    return Fail("Unsupported unzip shim arguments.");
}

if (args[0] == "-Z1" && args.Length == 2)
{
    using var archive = ZipFile.OpenRead(args[1]);
    foreach (var entry in archive.Entries)
    {
        Console.Out.WriteLine(entry.FullName);
    }
    return 0;
}

if (args[0] == "-p" && args.Length == 3)
{
    using var archive = ZipFile.OpenRead(args[1]);
    var entry = archive.GetEntry(args[2]);
    if (entry is null)
    {
        return Fail($"Entry not found: {args[2]}");
    }

    await using var input = entry.Open();
    await using var output = Console.OpenStandardOutput();
    await input.CopyToAsync(output);
    return 0;
}

return Fail($"Unsupported unzip shim arguments: {string.Join(" ", args)}");
