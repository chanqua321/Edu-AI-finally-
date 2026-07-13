namespace EduAI.BusinessLogic.Helpers;

public static class DocumentPathHelper
{
    public static string ResolvePhysicalPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return filePath;

        if (Path.IsPathRooted(filePath))
            return Path.GetFullPath(filePath);

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), filePath));
    }

    public static string ResolveUploadRoot(string? uploadPath)
    {
        var root = string.IsNullOrWhiteSpace(uploadPath) ? "uploads" : uploadPath.Trim();
        return ResolvePhysicalPath(root);
    }
}
