namespace Yaapic.Core.Import;

/// <summary>
/// Reads one foreign format into an <see cref="ImportBundle"/>. Importers are pure: they never touch the stores, the file
/// system or the network; <see cref="Sniff"/> never throws; <see cref="Import"/> reports every loss in the context report and
/// throws <see cref="ImportException"/> only when nothing at all can be read.
/// </summary>
public interface IImporter
{
    /// <summary>Gets the stable identifier of the format.</summary>
    string Id { get; }

    /// <summary>Gets the name shown to the user.</summary>
    string DisplayName { get; }

    /// <summary>Tells how likely the input is in this format, without importing it.</summary>
    /// <param name="input">The input to inspect.</param>
    /// <returns>The confidence; never throws.</returns>
    ImportConfidence Sniff(ImportInput input);

    /// <summary>Converts the input.</summary>
    /// <param name="input">The input to read.</param>
    /// <param name="context">The shared identifiers, options and report.</param>
    /// <returns>The bundle, carrying <see cref="ImportContext.Report"/>.</returns>
    /// <exception cref="ImportException">Nothing at all can be read from the input.</exception>
    ImportBundle Import(ImportInput input, ImportContext context);
}
