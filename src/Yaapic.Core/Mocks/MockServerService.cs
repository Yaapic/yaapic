using Yaapic.Core.Storage;
using Yaapic.Domain.Mocks;
using Yaapic.Localization;

namespace Yaapic.Core.Mocks;

/// <summary>A request logged by a mock server.</summary>
/// <param name="ServerId">The server that answered.</param>
/// <param name="Entry">The log entry.</param>
public sealed record MockLogged(Guid ServerId, MockLogEntry Entry);

/// <summary>Loads, saves, starts and stops mock servers and keeps their request logs (ADR 0020).</summary>
/// <param name="store">The server store.</param>
/// <param name="host">The HTTP host that listens on the ports.</param>
/// <param name="time">The clock.</param>
public sealed class MockServerService(IMockServerStore store, IMockHttpHost host, TimeProvider time) : IAsyncDisposable
{
    private const int LogCapacity = 500;

    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, MockServerDefinition> _definitions = [];
    private readonly Dictionary<Guid, IMockHttpServer> _running = [];
    private readonly Dictionary<Guid, Queue<MockLogEntry>> _logs = [];
    private List<Guid> _order = [];

    /// <summary>Raised after the servers were loaded, saved or deleted.</summary>
    public event EventHandler? ServersChanged;

    /// <summary>Raised after a server started or stopped; the argument is its identifier.</summary>
    public event EventHandler<Guid>? RunningChanged;

    /// <summary>Raised after a request was logged.</summary>
    public event EventHandler<MockLogged>? RequestLogged;

    /// <summary>Gets the servers, in store order.</summary>
    public IReadOnlyList<MockServerDefinition> Servers
    {
        get
        {
            lock (_gate)
            {
                return [.. _order.Select(id => _definitions[id])];
            }
        }
    }

    /// <summary>Gets the number of running servers.</summary>
    public int RunningCount
    {
        get
        {
            lock (_gate)
            {
                return _running.Count;
            }
        }
    }

    /// <summary>Loads the servers from the store.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the servers are loaded.</returns>
    /// <exception cref="StorageException">The store cannot be read.</exception>
    public async Task LoadAsync(CancellationToken ct)
    {
        var items = await store.ListAsync(ct).ConfigureAwait(false);
        lock (_gate)
        {
            _definitions.Clear();
            foreach (var s in items)
            {
                _definitions[s.Id] = s;
            }

            _order = [.. items.Select(s => s.Id)];
        }

        ServersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Validates and stores a server; a running server picks it up at its next request.</summary>
    /// <param name="server">The server to save.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The validation issues; empty when the server was saved.</returns>
    /// <exception cref="MockPortInUseException">The server runs, its port changed and the new port cannot be used; it is then stopped.</exception>
    /// <exception cref="StorageException">The store cannot be written.</exception>
    public async Task<IReadOnlyList<MockValidationIssue>> SaveAsync(MockServerDefinition server, CancellationToken ct)
    {
        var issues = MockServerValidator.Validate(server);
        if (issues.Count > 0)
        {
            return issues;
        }

        await store.SaveAsync(server, ct).ConfigureAwait(false);
        bool restart;
        lock (_gate)
        {
            var known = _definitions.TryGetValue(server.Id, out var old);
            _definitions[server.Id] = server;
            if (!known)
            {
                _order.Add(server.Id);
            }

            restart = known && _running.ContainsKey(server.Id) && old!.Port != server.Port;
        }

        ServersChanged?.Invoke(this, EventArgs.Empty);
        if (restart)
        {
            await StopAsync(server.Id).ConfigureAwait(false);
            await StartAsync(server.Id, ct).ConfigureAwait(false);
        }

        return [];
    }

    /// <summary>Stops a server if it runs, deletes it and drops its log.</summary>
    /// <param name="id">The server identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the server is deleted.</returns>
    /// <exception cref="StorageException">The store cannot be written.</exception>
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await StopAsync(id).ConfigureAwait(false);
        await store.DeleteAsync(id, ct).ConfigureAwait(false);
        lock (_gate)
        {
            _definitions.Remove(id);
            _order.Remove(id);
            _logs.Remove(id);
        }

        ServersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Starts a server; a no-op when it already runs.</summary>
    /// <param name="id">The server identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the server listens.</returns>
    /// <exception cref="InvalidOperationException">The server is unknown or has validation issues.</exception>
    /// <exception cref="MockPortInUseException">The port is already in use.</exception>
    public async Task StartAsync(Guid id, CancellationToken ct)
    {
        MockServerDefinition definition;
        lock (_gate)
        {
            if (_running.ContainsKey(id))
            {
                return;
            }

            definition = _definitions.TryGetValue(id, out var d) ? d : throw new InvalidOperationException(Strings.MockInvalid);
            if (MockServerValidator.Validate(definition).Count > 0)
            {
                throw new InvalidOperationException(Strings.MockInvalid);
            }

            if (_running.Keys.Any(other => _definitions[other].Port == definition.Port))
            {
                throw new MockPortInUseException(definition.Port);
            }
        }

        var server = await host.StartAsync(definition.Port, (request, _) => HandleAsync(id, request), ct).ConfigureAwait(false);
        lock (_gate)
        {
            _running[id] = server;
        }

        RunningChanged?.Invoke(this, id);
    }

    /// <summary>Stops a server; a no-op when it does not run.</summary>
    /// <param name="id">The server identifier.</param>
    /// <returns>A task that completes when the server stopped.</returns>
    public async Task StopAsync(Guid id)
    {
        IMockHttpServer? server;
        lock (_gate)
        {
            _running.Remove(id, out server);
        }

        if (server is null)
        {
            return;
        }

        await server.DisposeAsync().ConfigureAwait(false);
        RunningChanged?.Invoke(this, id);
    }

    /// <summary>Tells whether a server runs.</summary>
    /// <param name="id">The server identifier.</param>
    /// <returns><see langword="true"/> when it runs.</returns>
    public bool IsRunning(Guid id)
    {
        lock (_gate)
        {
            return _running.ContainsKey(id);
        }
    }

    /// <summary>Stops every running server.</summary>
    /// <returns>A task that completes when all servers stopped.</returns>
    public async Task StopAllAsync()
    {
        Guid[] ids;
        lock (_gate)
        {
            ids = [.. _running.Keys];
        }

        foreach (var id in ids)
        {
            await StopAsync(id).ConfigureAwait(false);
        }
    }

    /// <summary>Gets the log of a server, newest first (at most 500 entries).</summary>
    /// <param name="id">The server identifier.</param>
    /// <returns>The entries.</returns>
    public IReadOnlyList<MockLogEntry> GetLog(Guid id)
    {
        lock (_gate)
        {
            return _logs.TryGetValue(id, out var log) ? [.. log.Reverse()] : [];
        }
    }

    /// <summary>Empties the log of a server.</summary>
    /// <param name="id">The server identifier.</param>
    public void ClearLog(Guid id)
    {
        lock (_gate)
        {
            _logs.Remove(id);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await StopAllAsync().ConfigureAwait(false);

    private Task<MockHttpResponse> HandleAsync(Guid id, MockHttpRequest request)
    {
        var start = time.GetTimestamp();
        MockServerDefinition definition;
        lock (_gate)
        {
            definition = _definitions[id];
        }

        var result = MockResponder.Respond(definition, request);
        var entry = MockLogEntry.From(
            request, result.Response, result.RouteName, time.GetUtcNow(),
            time.GetElapsedTime(start) + result.Response.Delay);
        lock (_gate)
        {
            if (!_logs.TryGetValue(id, out var log))
            {
                _logs[id] = log = new Queue<MockLogEntry>();
            }

            log.Enqueue(entry);
            while (log.Count > LogCapacity)
            {
                log.Dequeue();
            }
        }

        RequestLogged?.Invoke(this, new MockLogged(id, entry));
        return Task.FromResult(result.Response);
    }
}
