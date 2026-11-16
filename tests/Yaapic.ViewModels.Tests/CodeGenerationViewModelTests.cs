using Yaapic.Core.CodeGeneration;
using Yaapic.Core.Http;
using Yaapic.Core.Preferences;
using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Tests;

public sealed class CodeGenerationViewModelTests
{
    private readonly InMemoryPreferencesStore _prefs = new();
    private readonly FakeDialogService _clipboard = new();
    private ResolvedRequest _resolved = new(HttpMethodKind.Get, "https://x.test/a", [], null, null, []);

    private sealed class FakeGenerator(string id, string language, Func<CodeRequest, string>? generate = null) : ICodeGenerator
    {
        public string Id => id;

        public string DisplayName => id.ToUpperInvariant();

        public string EditorLanguage => language;

        public string Generate(CodeRequest request) => generate?.Invoke(request) ?? $"{id}:{request.Method} {request.Url}\n";
    }

    private static CodeGeneratorRegistry Registry(params ICodeGenerator[] generators) => new(generators);

    private CodeGenerationViewModel Create(params ICodeGenerator[] generators) =>
        new(Registry(generators.Length == 0 ? [new FakeGenerator("curl", "shell"), new FakeGenerator("python", "python")] : generators), () => _resolved, _prefs, _clipboard);

    [Fact]
    public void Targets_come_from_the_registry_and_curl_is_the_default()
    {
        var vm = Create();

        Assert.Equal(["curl", "python"], vm.Targets.Select(t => t.Id));
        Assert.Equal("CURL", vm.Targets[0].DisplayName);
        Assert.Equal("curl", vm.SelectedTarget!.Id);
        Assert.Equal("curl:GET https://x.test/a\n", vm.Code);
        Assert.Equal("shell", vm.EditorLanguage);
    }

    [Fact]
    public void The_first_target_is_used_when_curl_is_missing() =>
        Assert.Equal("python", Create(new FakeGenerator("python", "python")).SelectedTarget!.Id);

    [Fact]
    public async Task The_saved_target_is_restored()
    {
        _prefs.Preferences = UserPreferences.Default with { CodegenTarget = "python" };
        var vm = Create();

        await vm.InitializeAsync();

        Assert.Equal("python", vm.SelectedTarget!.Id);
        Assert.StartsWith("python:", vm.Code, StringComparison.Ordinal);
        Assert.Equal("python", vm.EditorLanguage);
    }

    [Fact]
    public async Task An_unknown_saved_target_keeps_the_default()
    {
        _prefs.Preferences = UserPreferences.Default with { CodegenTarget = "cobol" };
        var vm = Create();

        await vm.InitializeAsync();

        Assert.Equal("curl", vm.SelectedTarget!.Id);
    }

    [Fact]
    public void Selecting_a_target_regenerates_and_persists_it()
    {
        var vm = Create();

        vm.SelectedTarget = vm.Targets[1];

        Assert.StartsWith("python:", vm.Code, StringComparison.Ordinal);
        Assert.Equal("python", vm.EditorLanguage);
        Assert.Equal("python", _prefs.Preferences.CodegenTarget);
    }

    [Fact]
    public void Refresh_picks_up_changes_of_the_request()
    {
        var vm = Create();

        _resolved = _resolved with { Url = "https://x.test/b" };
        vm.Refresh();

        Assert.Contains("/b", vm.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void A_generator_exception_becomes_an_error_and_an_empty_code()
    {
        var vm = Create(new FakeGenerator("curl", "shell", _ => throw new InvalidOperationException("boom")));

        Assert.Equal("", vm.Code);
        Assert.Contains("boom", vm.Error, StringComparison.Ordinal);
        Assert.False(vm.CopyCommand.CanExecute(null));
    }

    [Fact]
    public void The_error_is_cleared_by_a_successful_refresh()
    {
        var fail = true;
        var vm = Create(new FakeGenerator("curl", "shell", r => fail ? throw new InvalidOperationException("boom") : "ok\n"));

        fail = false;
        vm.Refresh();

        Assert.Null(vm.Error);
        Assert.Equal("ok\n", vm.Code);
    }

    [Fact]
    public void Unresolved_variables_are_listed()
    {
        _resolved = _resolved with { Unresolved = ["a", "b"] };
        var vm = Create();

        Assert.Contains("a, b", vm.UnresolvedWarning, StringComparison.Ordinal);
        Assert.NotEqual("", vm.Code);
    }

    [Fact]
    public void No_unresolved_variable_means_no_warning()
    {
        Assert.Null(Create().UnresolvedWarning);
        Assert.False(string.IsNullOrEmpty(Create().SecretWarning));
    }

    [Fact]
    public async Task Copy_puts_the_code_on_the_clipboard()
    {
        var vm = Create();

        Assert.True(vm.CopyCommand.CanExecute(null));
        await vm.CopyCommand.ExecuteAsync(null);

        Assert.Equal(vm.Code, Assert.Single(_clipboard.ClipboardTexts));
    }

    private static RequestEditorViewModel Editor(CodeGenerationServices? services, FakeVariableScopeProvider? scopes = null) =>
        RequestEditorViewModel.CreateNew(new RequestSender(new FakeHttpTransport((_, _) => throw new InvalidOperationException())), scopes ?? new FakeVariableScopeProvider(), codeGeneration: services);

    [Fact]
    public void Without_services_the_editor_cannot_generate_code()
    {
        var editor = Editor(null);

        Assert.False(editor.GenerateCodeCommand.CanExecute(null));
        Assert.Throws<InvalidOperationException>(editor.CreateCodeGeneration);
    }

    [Fact]
    public void With_services_the_command_raises_the_event_and_resolution_uses_unsaved_edits()
    {
        var environment = new EnvironmentDefinition(Guid.NewGuid(), "dev", [new KeyValueEntry("host", "api.test")]);
        var scopes = new FakeVariableScopeProvider { Scopes = new VariableScopes(environment, [], []) };
        var editor = Editor(new CodeGenerationServices(Registry(new FakeGenerator("curl", "shell")), _prefs, _clipboard), scopes);
        var raised = 0;
        editor.CodeGenerationRequested += (_, _) => raised++;
        editor.Url = "https://{{host}}/one";
        var vm = editor.CreateCodeGeneration();

        Assert.True(editor.GenerateCodeCommand.CanExecute(null));
        editor.GenerateCodeCommand.Execute(null);
        Assert.Equal(1, raised);
        Assert.Contains("https://api.test/one", vm.Code, StringComparison.Ordinal);

        editor.Url = "https://{{host}}/two?x={{missing}}";
        vm.Refresh();

        Assert.Contains("/two", vm.Code, StringComparison.Ordinal);
        Assert.Contains("missing", vm.UnresolvedWarning, StringComparison.Ordinal);
    }
}
