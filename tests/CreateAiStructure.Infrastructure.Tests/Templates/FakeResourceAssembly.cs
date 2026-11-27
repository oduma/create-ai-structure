using System.Reflection;
using System.Text;

namespace CreateAiStructure.Infrastructure.Tests.Templates;

/// <summary>
/// Deterministic, test-only <see cref="Assembly"/> whose manifest resources are fully controlled by the test:
/// it exposes exactly the registered resource names (in registration order) and serves a fresh
/// <see cref="MemoryStream"/> per request, or <see langword="null"/> for resources registered via
/// <see cref="WithNullStream"/>. Every stream handed out is recorded so tests can assert it was disposed.
/// </summary>
internal sealed class FakeResourceAssembly : Assembly
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly List<KeyValuePair<string, byte[]?>> _resources = [];
    private readonly List<MemoryStream> _servedStreams = [];
    private readonly List<string> _requestedNames = [];

    /// <summary>Gets every stream served by <see cref="GetManifestResourceStream(string)"/>.</summary>
    public IReadOnlyList<MemoryStream> ServedStreams => _servedStreams;

    /// <summary>Gets every manifest resource name requested via <see cref="GetManifestResourceStream(string)"/>.</summary>
    public IReadOnlyList<string> RequestedNames => _requestedNames;

    /// <summary>Registers a resource whose content is <paramref name="text"/> encoded as UTF-8 without a BOM.</summary>
    public FakeResourceAssembly With(string manifestName, string text) => With(manifestName, Utf8NoBom.GetBytes(text));

    /// <summary>Registers a resource with the exact <paramref name="bytes"/>.</summary>
    public FakeResourceAssembly With(string manifestName, byte[] bytes)
    {
        _resources.Add(new(manifestName, bytes));
        return this;
    }

    /// <summary>Registers a resource name whose stream is reported as <see langword="null"/>.</summary>
    public FakeResourceAssembly WithNullStream(string manifestName)
    {
        _resources.Add(new(manifestName, null));
        return this;
    }

    /// <inheritdoc />
    public override string[] GetManifestResourceNames() => _resources.Select(resource => resource.Key).ToArray();

    /// <inheritdoc />
    public override Stream? GetManifestResourceStream(string name)
    {
        _requestedNames.Add(name);

        foreach (var resource in _resources)
        {
            if (string.Equals(resource.Key, name, StringComparison.Ordinal))
            {
                if (resource.Value is null)
                {
                    return null;
                }

                var stream = new MemoryStream(resource.Value, writable: false);
                _servedStreams.Add(stream);
                return stream;
            }
        }

        return null;
    }
}
