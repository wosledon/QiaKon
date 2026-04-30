using QiaKon.Contracts.DTOs;
using QiaKon.Llm;

namespace QiaKon.Shared;

public sealed class ConfiguredLlmModelResolver
{
    private readonly ILlmProviderService _llmProviderService;

    public ConfiguredLlmModelResolver(ILlmProviderService llmProviderService)
    {
        _llmProviderService = llmProviderService;
    }

    public IReadOnlyList<LlmModelDto> GetEnabledInferenceModels()
        => EnumerateModels()
            .Where(x => x.Model.ModelType == LlmModelType.Inference && x.Model.IsEnabled)
            .Select(x => x.Model)
            .ToList();

    internal ConfiguredLlmModelSelection? TryGetDefaultInferenceModel()
        => FindModel(LlmModelType.Inference, null);

    internal ConfiguredLlmModelSelection? TryGetDefaultChunkingModel()
        => FindModel(LlmModelType.Embedding, null);

    internal ConfiguredLlmModelSelection? TryGetInferenceModel(Guid modelId)
        => FindModel(LlmModelType.Inference, modelId);

    internal LlmOptions BuildOptions(ConfiguredLlmModelSelection selection)
        => new()
        {
            Provider = selection.Provider.InterfaceType switch
            {
                LlmInterfaceType.OpenAI => LlmProviderType.OpenAI,
                LlmInterfaceType.Anthropic => LlmProviderType.Anthropic,
                _ => throw new NotSupportedException($"Unsupported provider type: {selection.Provider.InterfaceType}")
            },
            Model = selection.Model.ActualModelName,
            BaseUrl = selection.Provider.BaseUrl,
            ApiKey = selection.Provider.ApiKey,
            TimeoutSeconds = selection.Provider.TimeoutSeconds,
            MaxRetries = selection.Provider.RetryCount,
            InferenceOptions = new LlmInferenceOptions
            {
                MaxTokens = selection.Model.MaxTokens
            }
        };

    private ConfiguredLlmModelSelection? FindModel(LlmModelType modelType, Guid? explicitModelId)
    {
        var candidates = EnumerateModels()
            .Where(x => x.Model.ModelType == modelType && x.Model.IsEnabled)
            .ToList();

        if (explicitModelId.HasValue)
        {
            return candidates.FirstOrDefault(x => x.Model.Id == explicitModelId.Value);
        }

        return candidates.FirstOrDefault(x => x.Model.IsDefault)
            ?? candidates.FirstOrDefault();
    }

    private IEnumerable<ConfiguredLlmModelSelection> EnumerateModels()
    {
        foreach (var provider in _llmProviderService.GetAll())
        {
            foreach (var model in provider.Models)
            {
                yield return new ConfiguredLlmModelSelection(provider, model);
            }
        }
    }
}

internal sealed record ConfiguredLlmModelSelection(LlmProviderDto Provider, LlmModelDto Model);