using Loupe.Application.Common;

namespace Loupe.Application.Comparisons;

public static class GetComparisonQueryValidator
{
    public static void Validate(GetComparisonQuery query)
    {
        if (query.FirstId == Guid.Empty) throw new RequestValidationException("firstId", "Choose a photograph.");
        if (query.SecondId == Guid.Empty) throw new RequestValidationException("secondId", "Choose a photograph.");
        if (query.FirstId == query.SecondId) throw new RequestValidationException("secondId", "Choose two different photographs.");
    }
}
