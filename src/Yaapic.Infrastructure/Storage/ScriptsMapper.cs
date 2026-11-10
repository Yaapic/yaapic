using Yaapic.Core.Storage;
using Yaapic.Domain.Scripting;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Maps <see cref="ScriptSet"/> to and from the <c>scripts</c> section shared by request and folder files.</summary>
internal static class ScriptsMapper
{
    /// <summary>Maps a script set to its DTO.</summary>
    /// <param name="scripts">The scripts.</param>
    /// <returns>The DTO.</returns>
    public static ScriptsDto ToDto(ScriptSet scripts) => new()
    {
        PreRequest = ToDto(scripts.PreRequest),
        PostResponse = ToDto(scripts.PostResponse),
    };

    /// <summary>Maps a DTO to a script set; a missing section or block is empty.</summary>
    /// <param name="dto">The DTO, or <see langword="null"/>.</param>
    /// <returns>The scripts.</returns>
    /// <exception cref="StorageException">A block names an unknown language.</exception>
    public static ScriptSet FromDto(ScriptsDto? dto) =>
        new(ReadBlock(dto?.PreRequest), ReadBlock(dto?.PostResponse));

    private static ScriptBlockDto ToDto(ScriptBlock block) =>
        new() { Language = block.Language.ToText(), Source = new ScriptSourceText(block.Source) };

    private static ScriptBlock ReadBlock(ScriptBlockDto? dto)
    {
        if (dto is null)
        {
            return ScriptBlock.Empty;
        }

        var language = ScriptLanguage.JavaScript;
        if (dto.Language is not null && !ScriptLanguageExtensions.TryParse(dto.Language, out language))
        {
            throw new StorageException($"Unknown script language '{dto.Language}'");
        }

        return new ScriptBlock(language, dto.Source?.Value ?? "");
    }
}
