using System.Text.Json;

namespace EduAI.BusinessLogic.Helpers;

public static class VectorHelper
{
    public static string Serialize(float[] vector) =>
        JsonSerializer.Serialize(vector);

    public static bool TryDeserialize(string? value, out float[] vector)
    {
        vector = Array.Empty<float>();
        if (string.IsNullOrWhiteSpace(value) || !value.TrimStart().StartsWith('['))
            return false;

        try
        {
            var parsed = JsonSerializer.Deserialize<float[]>(value);
            if (parsed is { Length: > 0 })
            {
                vector = parsed;
                return true;
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }

    public static double CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        if (left.Count == 0 || right.Count == 0 || left.Count != right.Count)
            return 0;

        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;

        for (var i = 0; i < left.Count; i++)
        {
            dot += left[i] * right[i];
            leftNorm += left[i] * left[i];
            rightNorm += right[i] * right[i];
        }

        if (leftNorm == 0 || rightNorm == 0)
            return 0;

        return dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
    }
}
