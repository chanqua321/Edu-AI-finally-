using EduAI.Model.DTOs;
using EduAI.Model.Entities;

namespace EduAI.BusinessLogic.Helpers;

public static class AiCostCalculator
{
    public static decimal EstimateGenerationCostUsd(AiTokenUsageDto usage, SystemSettings settings) =>
        usage.PromptTokens * settings.InputTokenPricePerMillion / 1_000_000m
        + usage.CompletionTokens * settings.OutputTokenPricePerMillion / 1_000_000m;

    public static decimal EstimateEmbeddingCostUsd(int tokenCount, SystemSettings settings) =>
        tokenCount * settings.EmbeddingPricePerMillion / 1_000_000m;
}
