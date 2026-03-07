using UpdateCpmVersions;

// dotnet CLI does this too
// https://github.com/dotnet/sdk/blob/5bb7e315bf797af120abf7ed17b7cbd6145b48f6/src/Cli/dotnet/Program.cs#L48
Console.OutputEncoding = System.Text.Encoding.UTF8;

return await CliCommand.Build().Parse(args).InvokeAsync();
