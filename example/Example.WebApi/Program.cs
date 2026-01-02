using System.Text.Json.Serialization;
using Configuration.Writable;
using Configuration.Writable.FormatProvider;
using Example.WebApi;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateSlimBuilder(args);

// Configuration.Writable
builder.Services.AddWritableOptions<SampleSetting>(conf =>
{
    conf.UseFile("appsettings.json");
    conf.SectionName = "MySetting";
    conf.FormatProvider = new JsonAotFormatProvider(SampleSettingSerializerContext.Default);
    conf.UseDataAnnotationsValidation = false;
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
builder.Services.AddOpenApi();

var app = builder.Build();
app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "v1");
    options.RoutePrefix = "";
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
