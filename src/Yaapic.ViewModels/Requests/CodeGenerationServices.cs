using Yaapic.Core.CodeGeneration;
using Yaapic.Core.Preferences;
using Yaapic.UI.Abstractions;

namespace Yaapic.ViewModels.Requests;

/// <summary>What a request editor needs to offer code generation.</summary>
/// <param name="Registry">The available generators.</param>
/// <param name="Preferences">Stores the last chosen target.</param>
/// <param name="Clipboard">Receives the copied code.</param>
public sealed record CodeGenerationServices(CodeGeneratorRegistry Registry, IPreferencesStore Preferences, IDialogService Clipboard);
