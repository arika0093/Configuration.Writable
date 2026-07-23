using System.Text.Json.Serialization;
using Configuration.Writable;
using Configuration.Writable.FormatProvider;
using Example.WebApi;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NSwag.AspNetCore;

var builder = WebApplication.CreateSlimBuilder(args);

// Configuration.Writable
builder.Services.AddWritableOptions<SampleSetting>(conf =>
{
    conf.UseFile("appsettings.json");
    conf.SectionName = "MySetting";
    conf.FormatProvider = new JsonAotFormatProvider(SampleSettingSerializerContext.Default);
    conf.WithValidator<SampleSettingValidator>();
});

// default Configuration
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(
        0,
        SampleSettingSerializerContext.Default
    );
});
builder.Services.AddOpenApiDocument();

var app = builder.Build();
app.UseOpenApi();
app.UseSwaggerUi(options =>
{
    options.Path = "";
});

var configApi = app.MapGroup("/config");
configApi.MapGet(
    "/get",
    (IReadOnlyOptions<SampleSetting> options) =>
    {
        var settings = options.CurrentValue;
        return settings;
    }
);

configApi.MapGet(
    "/set/{name}",
    async ([FromServices] IWritableOptions<SampleSetting> options, string name) =>
    {
        try
        {
            await options.SaveAsync(setting => setting.Name = name);
            return Results.Ok();
        }
        catch (Exception ex)
        {
            return Results.Text(content: ex.Message, statusCode: 500);
        }
    }
);

app.Run();
