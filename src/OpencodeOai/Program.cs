using Microsoft.AspNetCore.Authentication;
using OpencodeOai;
using OpencodeOai.Auth;
using OpencodeOai.Configuration;
using OpencodeOai.Endpoints;
using OpencodeOai.Options;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, OpencodeOai.Openai.OpenaiJsonContext.Default);
});

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
builder.Services.AddSingleton<OpencodeOai.Bridge.IChatCompletionService, OpencodeOai.Bridge.ChatCompletionService>();
builder.Services.AddSingleton<OpencodeOai.Bridge.IIdempotencyStore, OpencodeOai.Bridge.MemoryIdempotencyStore>();

builder.Services
    .AddAuthentication(ApiKeyAuthHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>(ApiKeyAuthHandler.SchemeName, _ => { });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(ApiKeyAuthHandler.SchemeName, p => p.RequireAuthenticatedUser());
});

var app = builder.Build();

app.Use(async (ctx, next) =>
{
    await next();
    if (ctx.Response.StatusCode == StatusCodes.Status401Unauthorized && !ctx.Response.HasStarted)
    {
        await ApiKeyChallenge.WriteAsync(ctx);
    }
});

app.UseAuthentication();
app.UseAuthorization();

EndpointRegistry.MapAll(app);

app.Run();
