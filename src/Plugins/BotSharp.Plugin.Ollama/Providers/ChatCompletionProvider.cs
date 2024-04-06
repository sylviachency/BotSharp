
using BotSharp.Abstraction.Agents;
using BotSharp.Abstraction.Loggers;
using OllamaSharp.Models.Chat;
using System.Threading;

namespace BotSharp.Plugin.Ollama.Providers;

public class ChatCompletionProvider : IChatCompletion
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;
    private readonly OLlamaSettings _settings;
    private string _model;

    public ChatCompletionProvider(IServiceProvider services,
        ILogger<ChatCompletionProvider> logger,
        OLlamaSettings settings)
    {
        _services = services;
        _logger = logger;
        _settings = settings;
    }

    public string Provider => "ollama";

    public async Task<RoleDialogModel> GetChatCompletions(Agent agent, List<RoleDialogModel> conversations)
    {
        var hooks = _services.GetServices<IContentGeneratingHook>().ToList();

        // Before chat completion hook
        foreach (var hook in hooks)
        {
            await hook.BeforeGenerating(agent, conversations);
        }
        var ollama = new OllamaApiClient(_settings.ModelUrl, _settings.ChatModelName); // (uri);
        var chat = new Chat(ollama, _ => { });

        var agentService = _services.GetRequiredService<IAgentService>();

        var prompt = string.Join("\r\n", conversations.Select(x => $"{x.Role}: {x.Content}")).Trim();
        prompt += $"\r\n{AgentRole.Assistant}: ";
        
        if (!string.IsNullOrEmpty(agent.Instruction))
        {
            var instruction = agentService.RenderedInstruction(agent);
            prompt = instruction + "\r\n" + prompt;
            await chat.SendAs(ChatRole.System, instruction);
        }
        foreach (var message in conversations)
        {           
            if (message.Role == "user")
            {
                await chat.SendAs(ChatRole.User, message.Content);
            }
            else if (message.Role == "assistant")
            {
                await chat.SendAs(ChatRole.Assistant, message.Content);
            }
        }

        var convSetting = _services.GetRequiredService<ConversationSetting>();
        if (convSetting.ShowVerboseLog)
        {
            _logger.LogInformation(prompt);
        }

        var lastMessage = conversations.LastOrDefault();

        string question = lastMessage?.Content;
        var chatResponse = "";
        var history = (await chat.Send(question, CancellationToken.None)).ToArray();

        var last = history.Last();
        chatResponse = last.Content;

        var msg = new RoleDialogModel(AgentRole.Assistant, chatResponse)
        {
            CurrentAgentId = agent.Id
        };

        // After chat completion hook
        foreach (var hook in hooks)
        {
            await hook.AfterGenerated(msg, new TokenStatsModel
            {
                Prompt = prompt,
                Provider = Provider,
                Model = _model
            });
        }

        return msg;
    }

    public async Task<bool> GetChatCompletionsAsync(Agent agent, List<RoleDialogModel> conversations, Func<RoleDialogModel, Task> onMessageReceived, Func<RoleDialogModel, Task> onFunctionExecuting)
    {
        var hooks = _services.GetServices<IContentGeneratingHook>().ToList();

        // Before chat completion hook
        foreach (var hook in hooks)
        {
            await hook.BeforeGenerating(agent, conversations);
        }

        var ollama = new OllamaApiClient(_settings.ModelUrl, _settings.ChatModelName); // (uri);
        var chat = new Chat(ollama, _ => { });

        var agentService = _services.GetRequiredService<IAgentService>();

        var prompt = string.Join("\r\n", conversations.Select(x => $"{x.Role}: {x.Content}")).Trim();
        prompt += $"\r\n{AgentRole.Assistant}: ";

        if (!string.IsNullOrEmpty(agent.Instruction))
        {
            var instruction = agentService.RenderedInstruction(agent);
            prompt = instruction + "\r\n" + prompt;
            await chat.SendAs(ChatRole.System, instruction);
        }
        foreach (var message in conversations)
        {
            if (message.Role == "user")
            {
                await chat.SendAs(ChatRole.User, message.Content);
            }
            else if (message.Role == "assistant")
            {
                await chat.SendAs(ChatRole.Assistant, message.Content);
            }
        }

        var convSetting = _services.GetRequiredService<ConversationSetting>();
        if (convSetting.ShowVerboseLog)
        {
            _logger.LogInformation(prompt);
        }

        var lastMessage = conversations.LastOrDefault();

        string question = lastMessage?.Content;
        var chatResponse = "";
        var history = (await chat.Send(question, CancellationToken.None)).ToArray();

        var last = history.Last();
        chatResponse = last.Content;

        var msg = new RoleDialogModel(AgentRole.Assistant, chatResponse)
        {
            CurrentAgentId = agent.Id
        };

        // Text response received
        await onMessageReceived(msg);

        return true;
    }

    public async Task<bool> GetChatCompletionsStreamingAsync(Agent agent, List<RoleDialogModel> conversations, Func<RoleDialogModel, Task> onMessageReceived)
    {
        string totalResponse = "";
        ChatResponseStream answerFromAssistant = null;
        //var content = string.Join("\r\n", conversations.Select(x => $"{x.Role}: {x.Content}")).Trim();
        //content += $"\r\n{AgentRole.Assistant}: ";

        var ollama = new OllamaApiClient(_settings.ModelUrl, _settings.ChatModelName); // (uri);
        var chat = new Chat(ollama, answer => answerFromAssistant = answer);

        var convSetting = _services.GetRequiredService<ConversationSetting>();
        if (convSetting.ShowVerboseLog)
        {
            _logger.LogInformation(agent.Instruction);
        }

        foreach (var response in await chat.Send(agent.Instruction, CancellationToken.None))  
        {
            await onMessageReceived(new RoleDialogModel(AgentRole.Assistant, response.Content)
            {
                CurrentAgentId = agent.Id
            });
        }

        return true;
    }

    public void SetModelName(string model)
    {
        _model = model;
    }

}
