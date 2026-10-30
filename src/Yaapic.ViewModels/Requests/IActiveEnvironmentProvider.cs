using Yaapic.Domain.Environments;

namespace Yaapic.ViewModels.Requests;

/// <summary>Exposes the environment whose variables are applied when a request is sent.</summary>
public interface IActiveEnvironmentProvider
{
    /// <summary>Gets the active environment, or <see langword="null"/> when none is selected.</summary>
    EnvironmentDefinition? Current { get; }
}
