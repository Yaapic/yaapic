using Yaapic.Core.Http;
using Yaapic.Core.Secrets;
using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Editing;
using Yaapic.ViewModels.Environments;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Vault;

namespace Yaapic.ViewModels.Tests;

public sealed class SecretRowsTests
{
    private readonly InMemorySecretVault _vault = new();
    private readonly InMemoryEnvironmentStore _inner = new();
    private readonly FakeDialogService _dialogs = new();

    private static KeyValueEditorViewModel Editor(VaultState state, KeyValueEntry[] entries)
    {
        var editor = new KeyValueEditorViewModel { AllowSecrets = true };
        editor.Load(entries);
        editor.SetVaultState(state);
        return editor;
    }

    [Fact]
    public void ToggleSecretMarksTheRowWhenTheVaultIsUnlocked()
    {
        var row = Editor(VaultState.Unlocked, [new("token", "abc")]).Rows[0];

        row.ToggleSecretCommand.Execute(null);

        Assert.True(row.IsSecret);
        Assert.Null(row.LastError);
    }

    [Theory]
    [InlineData(VaultState.Locked)]
    [InlineData(VaultState.NotCreated)]
    public void ToggleSecretIsRefusedWhenTheVaultIsNotUnlocked(VaultState state)
    {
        var row = Editor(state, [new("token", "abc")]).Rows[0];

        row.ToggleSecretCommand.Execute(null);

        Assert.False(row.IsSecret);
        Assert.Equal("Unlock the vault to mark a variable as secret", row.LastError);
    }

    [Fact]
    public void TurningSecretOffKeepsThePlainValueAndShowsTheHint()
    {
        var row = Editor(VaultState.Unlocked, [new("token", "abc", true, true)]).Rows[0];

        row.ToggleSecretCommand.Execute(null);

        Assert.False(row.IsSecret);
        Assert.Equal("abc", row.Value);
        Assert.Equal("abc", row.DisplayValue);
        Assert.Equal("This value will be stored in the workspace file.", row.Hint);
    }

    [Fact]
    public void SecretValuesAreMaskedUntilRevealedAndEmptyOnesAreNot()
    {
        var editor = Editor(VaultState.Unlocked, [new("a", "abc", true, true), new("b", "", true, true)]);
        var (a, b) = (editor.Rows[0], editor.Rows[1]);

        Assert.Equal("••••••••", a.DisplayValue);
        Assert.Equal("", b.DisplayValue);
        a.ToggleRevealCommand.Execute(null);
        Assert.Equal("abc", a.DisplayValue);
    }

    [Fact]
    public void LockingHidesRevealedValuesAndMakesSecretRowsReadOnly()
    {
        var editor = Editor(VaultState.Unlocked, [new("a", "abc", true, true), new("b", "plain")]);
        var (secret, plain) = (editor.Rows[0], editor.Rows[1]);
        secret.ToggleRevealCommand.Execute(null);
        Assert.True(secret.CanEditValue);

        editor.SetVaultState(VaultState.Locked);

        Assert.False(secret.IsRevealed);
        Assert.Equal("••••••••", secret.DisplayValue);
        Assert.False(secret.CanEditValue);
        Assert.True(plain.CanEditValue);
        Assert.False(editor.SecretsAvailable);
    }

    [Fact]
    public void RevealingDoesNotRaiseChanged()
    {
        var editor = Editor(VaultState.Unlocked, [new("a", "abc", true, true)]);
        var raised = 0;
        editor.Changed += (_, _) => raised++;

        editor.Rows[0].ToggleRevealCommand.Execute(null);
        editor.SetVaultState(VaultState.Locked);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void EditorsThatDoNotAllowSecretsNeverExposeTheFlag()
    {
        var editor = new KeyValueEditorViewModel();
        editor.Load([new("a", "1", true, true)]);

        Assert.False(editor.Rows[0].IsSecret);
        Assert.False(editor.ToEntries()[0].IsSecret);
    }

    [Fact]
    public void EntriesKeepTheSecretFlagOnEditorsThatAllowIt()
    {
        var editor = Editor(VaultState.Unlocked, [new("a", "1", true, true), new("b", "2")]);

        Assert.Equal([true, false], editor.ToEntries().Select(e => e.IsSecret));
    }

    [Fact]
    public async Task SecretFlagRoundTripsThroughTheEnvironmentsSaveAndTheVault()
    {
        _vault.Arrange(VaultState.Unlocked);
        var store = new SecretAwareEnvironmentStore(_inner, _vault);
        var source = new FakeVaultState(VaultState.Unlocked);
        var vm = new EnvironmentsViewModel(store, _dialogs, source);
        vm.AddCommand.Execute(null);
        var item = vm.EditedEnvironment!;
        item.Variables.Rows[0].Key = "token";
        item.Variables.Rows[0].Value = "s3cret";
        item.Variables.Rows[0].ToggleSecretCommand.Execute(null);
        Assert.True(item.Variables.Rows[0].IsSecret);

        await vm.SaveCommand.ExecuteAsync(item);

        var stored = Assert.Single(await _inner.ListAsync(CancellationToken.None)).Variables.Single();
        Assert.Equal(new KeyValueEntry("token", "", true, true), stored);
        Assert.True(_vault.TryGet(new SecretRef(SecretOwnerKind.Environment, item.Id, "token"), out var held));
        Assert.Equal("s3cret", held);

        await vm.LoadAsync(item.Id);
        Assert.Equal("s3cret", vm.Environments[0].Variables.Rows[0].Value);
        Assert.True(vm.Environments[0].Variables.Rows[0].IsSecret);

        _vault.Lock();
        source.Set(VaultState.Locked);
        await vm.LoadAsync(item.Id);
        var locked = vm.Environments[0].Variables.Rows[0];
        Assert.Equal("", locked.Value);
        Assert.True(locked.IsSecret);
        Assert.False(locked.CanEditValue);
    }

    [Fact]
    public void VaultStateChangesReachTheLoadedEnvironments()
    {
        var source = new FakeVaultState(VaultState.Locked);
        var vm = new EnvironmentsViewModel(_inner, _dialogs, source);
        vm.AddCommand.Execute(null);
        var variables = vm.Environments[0].Variables;
        Assert.False(variables.SecretsAvailable);

        source.Set(VaultState.Unlocked);

        Assert.True(variables.SecretsAvailable);
    }

    [Fact]
    public void ScopesReportWhetherTheWinningRowIsSecret()
    {
        var scopes = new VariableScopes(null, [new("k", "plain")], [new("k", "hidden", true, true), new("g", "x")]);

        Assert.True(scopes.TryResolve("k", out _, out var kind, out var secret));
        Assert.Equal((VariableScopeKind.Collection, false), (kind, secret));
        Assert.True(scopes.TryResolve("g", out _, out _, out secret));
        Assert.False(secret);
        var onlyGlobals = new VariableScopes(null, [], [new("k", "hidden", true, true)]);
        Assert.True(onlyGlobals.TryResolve("k", out var value, out _, out secret));
        Assert.Equal(("hidden", true), (value, secret));
    }

    private static VariableContext ContextWith(string value)
    {
        var environment = new EnvironmentDefinition(Guid.NewGuid(), "e", [new("token", value, true, true)]);
        return new(Guid.NewGuid(), new FakeVariableScopeProvider { Scopes = new VariableScopes(environment, [], []) });
    }

    [Fact]
    public void HintsFlagSecretVariables()
    {
        var hint = ContextWith("abc").Describe("token");

        Assert.True(hint.IsSecret);
        Assert.False(new VariableContext(Guid.NewGuid(), new FakeVariableScopeProvider()).Describe("x").IsSecret);
    }

    [Fact]
    public void PopupMasksASecretValueUntilRevealedAndNeverPutsItInTheForm()
    {
        var popup = ContextWith("abc").CreatePopup("token");

        Assert.True(popup.IsSecret);
        Assert.Equal("••••••••", popup.DisplayValue);
        Assert.Equal("", popup.EditableValue);
        Assert.False(popup.CanEdit);
        popup.RevealCommand.Execute(null);
        Assert.Equal("abc", popup.DisplayValue);
    }

    [Fact]
    public async Task SendWarnsWhenASecretVariableIsEmptyBecauseTheVaultIsLocked()
    {
        var environment = new EnvironmentDefinition(Guid.NewGuid(), "e", [new("token", "", true, true)]);
        var ok = new HttpResponseData(200, "OK", [], [], TimeSpan.Zero, "https://x.test");
        var transport = new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Success(ok)));
        var editor = RequestEditorViewModel.CreateNew(
            new RequestSender(transport), new FakeVariableScopeProvider { Scopes = new VariableScopes(environment, [], []) });
        editor.Url = "https://x.test/{{token}}";

        await editor.SendCommand.ExecuteAsync(null);

        Assert.Equal("Vault is locked: secret variables are not available.", editor.Response.UnresolvedWarning);
    }

    public static TheoryData<string> LockedSecretPlaces => ["query", "header", "bearer", "basic-password", "api-key", "form", "raw-body"];

    [Theory]
    [MemberData(nameof(LockedSecretPlaces))]
    public async Task SendWarnsWhenALockedSecretIsUsedOutsideTheUrl(string place)
    {
        const string Ref = "{{token}}";
        var none = RequestAuth.None;
        var definition = HttpRequestDefinition.Create("r") with
        {
            Method = HttpMethodKind.Post,
            Url = "https://x.test/",
            QueryParams = place == "query" ? [new("q", Ref)] : [],
            Headers = place == "header" ? [new("X-Key", Ref)] : [],
            Auth = place switch
            {
                "bearer" => none with { Kind = AuthKind.Bearer, Token = Ref },
                "basic-password" => none with { Kind = AuthKind.Basic, Username = "u", Password = Ref },
                "api-key" => none with { Kind = AuthKind.ApiKey, Key = "k", Value = Ref },
                _ => none,
            },
            Body = place switch
            {
                "form" => RequestBody.UrlEncoded([new FormField("f", Ref)]),
                "raw-body" => RequestBody.Raw(RawContentType.Json, "{\"t\":\"" + Ref + "\"}"),
                _ => RequestBody.None,
            },
        };
        var environment = new EnvironmentDefinition(Guid.NewGuid(), "e", [new("token", "", true, true)]);
        var ok = new HttpResponseData(200, "OK", [], [], TimeSpan.Zero, "https://x.test");
        var transport = new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Success(ok)));
        var editor = RequestEditorViewModel.FromDefinition(
            definition, new RequestSender(transport), new FakeVariableScopeProvider { Scopes = new VariableScopes(environment, [], []) });

        await editor.SendCommand.ExecuteAsync(null);

        Assert.Equal(RequestEditorViewModel.VaultLockedWarning, editor.Response.UnresolvedWarning);
    }

    [Fact]
    public async Task SendDoesNotWarnWhenTheSecretHasAValue()
    {
        var environment = new EnvironmentDefinition(Guid.NewGuid(), "e", [new("token", "abc", true, true)]);
        var ok = new HttpResponseData(200, "OK", [], [], TimeSpan.Zero, "https://x.test");
        var transport = new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Success(ok)));
        var definition = HttpRequestDefinition.Create("r") with { Url = "https://x.test/", QueryParams = [new("q", "{{token}}")] };
        var editor = RequestEditorViewModel.FromDefinition(
            definition, new RequestSender(transport), new FakeVariableScopeProvider { Scopes = new VariableScopes(environment, [], []) });

        await editor.SendCommand.ExecuteAsync(null);

        Assert.Null(editor.Response.UnresolvedWarning);
    }

    [Theory]
    [InlineData(VaultState.Locked, true, true)]
    [InlineData(VaultState.Unlocked, true, false)]
    [InlineData(VaultState.NotCreated, true, false)]
    [InlineData(VaultState.Locked, false, false)]
    public void LockedCaptionShowsOnlyForSecretRowsOfALockedVault(VaultState state, bool secretRow, bool expected)
    {
        var editor = Editor(state, [new("token", "abc", true, secretRow)]);

        Assert.Equal(expected, editor.ShowLockedCaption);
    }

    [Fact]
    public void LockedCaptionFollowsTheSecretFlagAndTheVaultState()
    {
        var editor = Editor(VaultState.Unlocked, [new("token", "abc", true, true)]);
        var raised = new List<string?>();
        editor.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        editor.SetVaultState(VaultState.Locked);
        Assert.True(editor.ShowLockedCaption);

        editor.SetVaultState(VaultState.Unlocked);
        editor.Rows[0].ToggleSecretCommand.Execute(null);

        Assert.False(editor.ShowLockedCaption);
        Assert.Contains(nameof(KeyValueEditorViewModel.ShowLockedCaption), raised);
    }

    [Fact]
    public void LockedSecretRowsAreMaskedEvenWithoutAValue()
    {
        var editor = Editor(VaultState.Locked, [new("token", "", true, true)]);
        var row = editor.Rows[0];

        Assert.True(row.IsMasked);
        Assert.Equal(KeyValueRowViewModel.Mask, row.DisplayValue);

        editor.SetVaultState(VaultState.Unlocked);

        Assert.False(row.IsMasked);
        Assert.Equal("", row.DisplayValue);
    }

    [Fact]
    public void SecretToggleIsOfferedToSecretRowsAndToUnlockedVaultsOnly()
    {
        var editor = Editor(VaultState.Locked, [new("a", "1"), new("b", "2", true, true)]);

        Assert.False(editor.Rows[0].CanToggleSecret);
        Assert.Equal("Unlock the vault to use secrets", editor.Rows[0].SecretToggleTooltip);
        Assert.True(editor.Rows[1].CanToggleSecret);

        editor.SetVaultState(VaultState.Unlocked);

        Assert.True(editor.Rows[0].CanToggleSecret);
        Assert.Equal("Store this value in the vault", editor.Rows[0].SecretToggleTooltip);
    }

    [Fact]
    public void VariableEditorsGetTheUnlockCommandOfTheVault()
    {
        var vaultState = new FakeVaultState(VaultState.Locked);
        var globals = new GlobalsViewModel(new InMemoryGlobalsStore(), vaultState);

        Assert.Same(vaultState.RequestUnlockCommand, globals.Variables.UnlockCommand);
    }

    private sealed class FakeVaultState(VaultState state) : IVaultStateSource
    {
        public event EventHandler? StateChanged;

        public VaultState State { get; private set; } = state;

        public System.Windows.Input.ICommand RequestUnlockCommand { get; } = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });

        public void Set(VaultState value)
        {
            State = value;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
