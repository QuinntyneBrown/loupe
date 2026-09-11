using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Domain.References;

namespace Loupe.Application.ReferenceAnalysis;

public static class ReferenceSuggestionReview
{
    public static void Apply(Reference reference, ReviewReferenceSuggestionCommand decision)
    {
        var saved = reference.SuggestionsJson is null ? null : JsonSerializer.Deserialize<SavedReferenceSuggestions>(reference.SuggestionsJson);
        if (saved is null || saved.OperationId != decision.OperationId || saved.ImageRevision != reference.ImageRevision) throw new RevisionConflictException();
        if (decision.Target == "all")
        {
            if (saved.DescriptionState != "pending" && saved.Tags.All(tag => tag.State != "pending")) throw new RevisionConflictException();
            if (saved.DescriptionState == "pending") Apply(reference, decision with { Target = "description" });
            foreach (var tag in saved.Tags.Where(tag => tag.State == "pending"))
                Apply(reference, decision with { Target = "tag", Name = tag.Name, Value = null, Category = null });
            return;
        }
        var accept = decision.Decision == "accept";
        var state = accept ? "accepted" : "dismissed";
        if (decision.Target == "description")
        {
            if (saved.DescriptionState != "pending") throw new RevisionConflictException();
            if (accept)
            {
                var text = TextField.Normalize(decision.Value ?? saved.Description, 4000, "description")
                    ?? throw new RequestValidationException("description", "Enter a description to accept.");
                reference.Description = text;
                reference.DescriptionProvenance = text == saved.Description ? "ai-accepted" : "edited-ai";
            }
            saved = saved with { DescriptionState = state };
        }
        else
        {
            var index = Array.FindIndex(saved.Tags, tag => tag.Name == decision.Name);
            if (index < 0 || saved.Tags[index].State != "pending") throw new RevisionConflictException();
            var suggestion = saved.Tags[index];
            if (accept)
            {
                var name = TextField.Normalize((decision.Value ?? suggestion.Name).Normalize(), 50, "tag")
                    ?? throw new RequestValidationException("tag", "Enter a tag to accept.");
                var category = decision.Category ?? suggestion.Category;
                if (category is not ("subject" or "genre" or "lighting" or "composition" or "palette" or "mood" or "technique"))
                    throw new RequestValidationException("category", "Choose a supported category.");
                var key = name.ToUpperInvariant();
                if (reference.Tags.All(tag => tag.NormalizedName != key))
                {
                    if (reference.Tags.Count >= 50) throw new RequestValidationException("tags", "A reference can have at most 50 active tags. Remove a tag before accepting another.");
                    reference.Tags.Add(new ReferenceTag { ReferenceId = reference.Id, OwnerId = reference.OwnerId, NormalizedName = key,
                        Name = name, Category = category, Provenance = name == suggestion.Name && category == suggestion.Category ? "ai-accepted" : "edited-ai" });
                }
            }
            saved.Tags[index] = suggestion with { State = state };
        }
        reference.SuggestionsJson = JsonSerializer.Serialize(saved);
    }
}
