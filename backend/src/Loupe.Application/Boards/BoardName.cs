using System.Text;
using Loupe.Application.Common;

namespace Loupe.Application.Boards;

public static class BoardName
{
    public static string Validate(string? value) => TextField.Normalize(value?.Normalize(NormalizationForm.FormC), 80, "name")
        ?? throw new RequestValidationException("name", "Enter a board name.");
}
