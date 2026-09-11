using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Domain.Photographers;

namespace Loupe.Application.PhotographerSummaries;

public static class PhotographerSuggestionReview
{
    public static void Apply(Photographer photographer, ReviewPhotographerSuggestionCommand decision)
    {
        var saved = photographer.SuggestionsJson is null ? null : JsonSerializer.Deserialize<SavedPhotographerSuggestions>(photographer.SuggestionsJson);
        if (saved is null || saved.OperationId != decision.OperationId || saved.SourceRevision != photographer.SourceRevision) throw new RevisionConflictException();
        if (decision.Target == "all")
        {
            if (saved.SummaryStatus != "pending" && saved.Tags.All(tag => tag.State != "pending")) throw new RevisionConflictException();
            if (saved.SummaryStatus == "pending") Apply(photographer, decision with { Target = "summary" });
            foreach (var tag in saved.Tags.Where(tag => tag.State == "pending")) Apply(photographer, decision with { Target = "tag", Name = tag.Name, Value = null, Category = null });
            return;
        }
        var accept = decision.Decision == "accept"; var state = accept ? "accepted" : "dismissed";
        if (decision.Target == "summary")
        {
            if (saved.SummaryStatus != "pending") throw new RevisionConflictException();
            if (accept)
            {
                var text = TextField.Normalize(decision.Value ?? saved.Summary, 4000, "summary") ?? throw new RequestValidationException("summary", "Enter a summary to accept.");
                photographer.Summary = text; photographer.SummaryProvenance = text == saved.Summary ? "ai-accepted" : "edited-ai";
            }
            saved = saved with { SummaryStatus = state };
        }
        else
        {
            var index = Array.FindIndex(saved.Tags, tag => tag.Name == decision.Name);
            if (index < 0 || saved.Tags[index].State != "pending") throw new RevisionConflictException();
            var suggestion = saved.Tags[index];
            if (accept)
            {
                var name = TextField.Normalize((decision.Value ?? suggestion.Name).Normalize(), 50, "tag") ?? throw new RequestValidationException("tag", "Enter a tag to accept.");
                var category = decision.Category ?? suggestion.Category;
                if (category is not ("subject" or "genre" or "lighting" or "composition" or "palette" or "mood" or "technique")) throw new RequestValidationException("category", "Choose a supported category.");
                var key = name.ToUpperInvariant();
                if (photographer.Tags.All(tag => tag.NormalizedName != key))
                {
                    if (photographer.Tags.Count >= 50) throw new RequestValidationException("tags", "A photographer can have at most 50 active tags. Remove a tag before accepting another.");
                    photographer.Tags.Add(new PhotographerTag { PhotographerId = photographer.Id, OwnerId = photographer.OwnerId, NormalizedName = key, Name = name, Category = category,
                        Provenance = name == suggestion.Name && category == suggestion.Category ? "ai-accepted" : "edited-ai" });
                }
            }
            saved.Tags[index] = suggestion with { State = state };
        }
        photographer.SuggestionsJson = JsonSerializer.Serialize(saved);
    }
}
