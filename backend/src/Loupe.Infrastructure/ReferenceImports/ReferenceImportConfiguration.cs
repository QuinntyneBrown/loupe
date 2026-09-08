using Loupe.Application.Common;
using Loupe.Application.ReferenceImports;
using Loupe.Domain.Operations;
using Microsoft.Extensions.Options;
namespace Loupe.Infrastructure.ReferenceImports;

public sealed class ReferenceImportConfiguration(IOptions<ReferenceImportOptions> options) : IReferenceImportConfiguration
{
    public ExecutionMode GetMode() => options.Value.Mode switch
    {
        "Demo" => ExecutionMode.Demo,
        "Live" => ExecutionMode.Live,
        _ => throw new IntegrationNotConfiguredException()
    };
}
