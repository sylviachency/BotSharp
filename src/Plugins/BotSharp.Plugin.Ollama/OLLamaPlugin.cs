namespace BotSharp.Plugin.Ollama;

public class OLLamaPlugin : IBotSharpPlugin
{
    public string Id => "49e398b8-14ea-9ca2-f86f-df4688233a3a";
    public string Name => "OLLama";
    public SettingsMeta Settings => new SettingsMeta("OLLama");

    public object GetNewSettingsInstance()
    {
        return new OLlamaSettings();
    }

    public void RegisterDI(IServiceCollection services, IConfiguration config)
    {
        services.AddScoped(provider =>
        {
            var settingService = provider.GetRequiredService<ISettingService>();
            return settingService.Bind<OLlamaSettings>("OLLama");
        });
        services.AddScoped<IChatCompletion, ChatCompletionProvider>();
    }
}
