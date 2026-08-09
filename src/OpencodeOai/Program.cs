using OpencodeOai;
using OpencodeOai.Configuration;
using OpencodeOai.Options;

var builder = WebApplication.CreateSlimBuilder(args);

EnvConfiguration.Apply(builder.Configuration);

builder.Services
    .AddOptions<BridgeOptions>()
    .Bind(builder.Configuration.GetSection(BridgeOptions.SectionName))
    .ValidateOnStart();

builder.Services
    .AddOptions<OpenCodeOptions>()
    .Bind(builder.Configuration.GetSection(OpenCodeOptions.SectionName))
    .ValidateOnStart();

var bridge = builder.Configuration.GetSection(BridgeOptions.SectionName).Get<BridgeOptions>() ?? new BridgeOptions();
builder.WebHost.UseUrls($"http://0.0.0.0:{bridge.Port}");

builder.Services.AddOpenCodeClient();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
