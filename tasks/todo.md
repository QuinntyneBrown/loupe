# Loupe acceptance checklist

Every criterion remains required. Execute in tasks/plan.md order; check only with evidence.

## L2-001: Upload and save personal photographs
- [ ] L2-001.1: Given an authenticated user and a valid image in each supported format, when the user uploads it, then one My Work record, its image, and a correctly oriented browser-compatible preview are saved and appear after reload.
- [ ] L2-001.2: Given upload sizes at 25,000,000 and 25,000,001 bytes and decoded dimensions at and above each shared limit, when each is submitted, then boundary-valid images are accepted and over-limit images receive the defined error without a saved photograph or retained upload file.
- [ ] L2-001.3: Given an upload with no title or brief, when it succeeds, then the default title is shown, brief fields are absent, and no critique request is created unless the user selected that action.
- [ ] L2-001.4: Given an interrupted transfer or storage failure, when the upload fails, then the UI offers retry, retains the brief, states if the file must be selected again, and shows no successfully saved photograph; abandoned bytes are cleaned under L2-032.
- [ ] L2-001.5: Given uploaded bytes still transferring, when progress is available, then the UI displays transferred bytes and total bytes; when total size is unavailable, then it shows indeterminate progress without inventing a percentage.

## L2-002: Record and edit the critique brief
- [x] L2-002.1: Given a saved photograph, when the user saves any valid combination of brief fields, then the exact normalized values survive reload and are shown on its detail screen.
- [x] L2-002.2: Given a field at its shared maximum and then one character above, or an experience value outside the allowed set, when saved, then the boundary value succeeds and invalid input produces a field error while preserving the previous brief.
- [ ] L2-002.3: Given a critique already queued, when the user edits the brief, then the job uses its original brief snapshot, and its displayed result identifies that snapshot rather than claiming it used the edited brief.
- [ ] L2-002.4: Given a saved brief, when the user clears all optional fields, then they are stored as absent and later critiques do not reuse cleared instructions.

## L2-003: Browse and revisit My Work
- [ ] L2-003.1: Given 25 photographs and a reference owned by the user, when My Work is opened and advanced through its results, then all 25 photographs appear once in the shared order and the reference never appears.
- [x] L2-003.2: Given photographs without a critique, with queued/running jobs, with successful critiques, and with failed replacements, when the list and detail are opened, then each status is accurate and failed replacements retain the previous successful critique.
- [ ] L2-003.3: Given a saved detail URL, when the owner opens it in a later authenticated session, then the saved content loads without a new upload or AI call.
- [ ] L2-003.4: Given an absent/deleted photograph or a failed list request, when opened, then the UI distinguishes unavailable detail from a retryable list failure and never represents either as an empty successful library.

## L2-004: Keep personal notes on photographs
- [ ] L2-004.1: Given a photograph with or without a critique, when valid notes are saved and the page reloads, then the notes appear unchanged after shared normalization and separately from AI text.
- [ ] L2-004.2: Given existing notes, when a critique is requested, retried, or replaced, then the notes are preserved.
- [x] L2-004.3: Given a failed save, when the user retries, then the editor retains the attempted text and does not claim it was saved before acknowledgment.
- [x] L2-004.4: Given saved notes, when an empty value is saved, then the notes are cleared; when 10,001 characters are submitted, then the shared field error is returned and saved notes remain intact.

## L2-005: Compare two saved attempts
- [x] L2-005.1: Given two eligible photographs, when the user selects Compare and the second photograph, then both saved attempts and their current critiques appear with unambiguous labels and no new AI job is created.
- [x] L2-005.2: Given fewer than two eligible photographs, when comparison is opened, then the UI explains that another critiqued photograph is needed and does not open an empty comparison.
- [x] L2-005.3: Given the same identifier twice or a photograph without a successful critique, when comparison is requested, then a validation error is shown; given an unavailable/foreign identifier, then the shared 404 behavior applies.
- [x] L2-005.4: Given a comparison already displayed and one selected photograph subsequently deleted, when the screen refreshes, then the remaining attempt stays visible and the missing side offers selection of another eligible photograph.
- [x] L2-005.5: Given comparison at widths below 768, when rendered, then attempts stack vertically; at widths of 768 or greater, then they appear in two labeled columns, with full-image fit and readable text under the shared accessibility rules.

## L2-006: Deliver a structured, actionable critique
- [ ] L2-006.1: Given a valid provider result covering all required sections, when processing completes, then each section is saved and displayed, with three improvements in priority order and one exercise.
- [ ] L2-006.2: Given an improvement, when its content is reviewed against the submitted image, then it identifies a visible observation, explains its effect, and specifies an action for the next shoot or edit; each exercise states an action and an observable way to compare the outcome.
- [ ] L2-006.3: Given a provider result missing a required section, containing blank explanations, or containing other than three priorities, when validated, then it is not marked successful or saved as the current critique, and the job follows L2-034.
- [ ] L2-006.4: Given an image where an aspect cannot be determined, when a critique is produced, then that aspect explicitly states uncertainty instead of supplying a fabricated observation.

## L2-007: Ground critique in evidence and stated intent
- [ ] L2-007.1: Given fixtures stating deliberate blur, shallow focus, or low-key intent, when critiqued, then the output acknowledges that intent and does not label its mere presence a technical error; recommendations explain how they support the intent or label an alternative as optional artistic exploration.
- [ ] L2-007.2: Given absent EXIF, when results are inspected, then no camera model, lens, aperture, shutter speed, ISO, location, or capture time is asserted as a measured fact; inferred lighting or motion causes are explicitly uncertain.
- [ ] L2-007.3: Given actual EXIF and the conflicting fixture, when critiqued, then cited EXIF values match supplied values exactly and conflicting visual hypotheses are identified as uncertain rather than replacing EXIF facts.
- [ ] L2-007.4: Given the evaluation set, when each fixture is run three times with the configured production model before release, then all 36 outputs contain the L2-006 sections and no prohibited factual claims; at least 33 outputs satisfy every fixture-specific evidence/actionability check, assessed by a named reviewer with recorded reasons.
- [ ] L2-007.5: Given a failed release evaluation or a changed model/prompt affecting critique behavior, when release validation runs, then the quality gate fails until the changed configuration passes the same evaluation; deterministic acceptance fixtures remain unchanged unless their expected behavior changes through specification review.

## L2-008: Request and replace the current critique
- [x] L2-008.1: Given a saved photograph without a critique, when Request critique is selected, then a durable asynchronous job is acknowledged and the UI shows its status without waiting for analysis.
- [x] L2-008.2: Given upload-and-critique selected, when the image persists but job admission fails, then the photograph remains saved, the UI states that critique was not queued, and the user can request it without uploading again.
- [x] L2-008.3: Given a current critique, when replacement is requested, then the UI first states that success replaces the current critique and keeps notes, and the old critique remains available while the job runs or fails.
- [ ] L2-008.4: Given a validated replacement, when it commits, then the new critique, generation timestamp, execution mode, and submitted brief snapshot become current together, with notes unchanged.
- [ ] L2-008.5: Given repeated requests for unchanged completed inputs, when submitted without explicit Regenerate, then the completed result is reused; explicit Regenerate creates one new job under L2-035.

## L2-009: Save an uploaded reference
- [x] L2-009.1: Given a valid uploaded image and optional metadata, when saved as a reference, then one inspiration record and preview appear after reload, with metadata intact and no entry in My Work.
- [x] L2-009.2: Given no source or attribution, when saved, then the UI shows that these are unknown without inventing a photographer or source.
- [ ] L2-009.3: Given a saved image reference, when AI metadata work is queued, delayed, or fails, then the user can still open the image, edit manual metadata, and assign boards.
- [ ] L2-009.4: Given invalid image or metadata input, when submitted, then the shared limits/errors apply and no partial reference is created.

## L2-010: Import a reference from a source URL
- [ ] L2-010.1: Given an allowed direct-image URL, when saved and imported, then the record retains its source, gains a stored validated image and preview, and reports import success.
- [ ] L2-010.2: Given an accessible HTML fixture containing the supported preview metadata, page title, and explicit author metadata, when imported, then the selected image, suggested title, and suggested attribution reflect those fields; the user can inspect and correct the metadata.
- [ ] L2-010.3: Given an HTML page with several candidate images, when imported, then selection follows the stated order; given no valid candidate, then the record stays link-only with the L2-011 fallback.
- [x] L2-010.4: Given the same normalized source already saved by this user, when submitted again, then the existing reference is returned with an Already saved message; another user's match is never disclosed. Normalization lowercases scheme/host, removes a default port and fragment, and preserves path and query without dropping tracking parameters.
- [ ] L2-010.5: Given an import finishing after manual edits, when applying its result, then it fills only still-empty fields and never overwrites a user-edited title, attribution, notes, or image.

## L2-011: Respect source restrictions and preserve a fallback
- [ ] L2-011.1: Given access denied, a login/paywall response, robots disallow, or an unavailable robots check, when import is attempted, then no restricted image/content is imported and the user sees the reason with Save link/Add image/Edit notes actions.
- [ ] L2-011.2: Given timeout, unsupported content, missing preview metadata, or an oversized response, when import fails, then the saved source remains available as a link-only reference and a manual image can be added without creating a second record.
- [ ] L2-011.3: Given a source that later becomes unavailable, when a previously saved reference is opened, then its stored image and metadata remain viewable and the original source link remains present.
- [ ] L2-011.4: Given a restricted page, when a manual fallback is saved, then the system does not attempt alternate identities, credential forwarding, browser automation, or proxy routes to obtain the restricted content.

## L2-012: Browse, inspect, and edit references
- [ ] L2-012.1: Given image and link-only references, when the user browses the grid, then both types have accessible titles and open details; missing images use a labeled placeholder rather than a broken image.
- [ ] L2-012.2: Given a reference, when its title, source, attribution, or notes are edited, then shared validation and conflict handling apply and accepted edits persist without changing board memberships.
- [ ] L2-012.3: Given a source or photographer link, when activated, then the source opens the saved external URL and the photographer opens its owned detail; external links do not grant the destination access to the opener or send a private Loupe detail URL as referrer.
- [ ] L2-012.4: Given a full-size image of any accepted aspect ratio, when detail is rendered, then the complete oriented frame is available without cropping; grid thumbnails are not used as the only detail image.
- [ ] L2-012.5: Given no references, a failed list load, or an unavailable detail, when displayed, then each has the distinct recovery behavior in L2-043.

## L2-013: Add or replace a reference image
- [ ] L2-013.1: Given a link-only reference, when a valid image is added, then the same reference gains an image and preview and retains its identifier, source, notes, attribution, tags, and boards.
- [ ] L2-013.2: Given an existing image, when the user confirms replacement and uploads a valid new image, then only successful persistence switches the current image; a failed upload leaves the old image visible.
- [ ] L2-013.3: Given successful replacement, when the reference is opened, then old visual suggestions and embeddings are no longer treated as current, new processing is queued, and reviewed metadata remains intact with a notice to review its relevance.
- [ ] L2-013.4: Given a job for the previous image that finishes later, when it attempts to save, then it cannot replace suggestions, previews, or search data for the new image revision.
- [ ] L2-013.5: Given old image files no longer referenced after replacement, when cleanup runs, then they follow L2-032 without removing the current image.

## L2-014: Generate visual descriptions and categorized tags
- [ ] L2-014.1: Given a reference with a validated image, when analysis succeeds, then a nonempty description and zero or more tags per defined category are saved as unreviewed suggestions and displayed separately from notes and active tags.
- [ ] L2-014.2: Given a valid response with an empty category, when displayed, then the category is omitted or marked No suggestions and no tag is invented to fill it.
- [ ] L2-014.3: Given a link-only reference, when visual analysis is requested, then the UI explains that an image is needed and no image-analysis provider call is made.
- [ ] L2-014.4: Given an overlength description/tag, unsupported category, or malformed response, when processing validates it, then the response is rejected as a failed attempt instead of silently truncating it or accepting partial suggestions.
- [ ] L2-014.5: Given a generated description or tag naming a photographer or camera setting without supplied evidence, when the content evaluation fixtures run, then that output fails validation of factual grounding and cannot pass the release gate.
- [ ] L2-014.6: Given the 12 images from L2-007 with pre-labeled acceptable visual descriptions/tags, when each is analyzed once by the live configured model, then all outputs satisfy the response contract, none assert unsupported identity/settings, and at least 11 accurately describe the fixture's documented subject and visible lighting/composition; a named reviewer records each judgment before release or a model/prompt change.

## L2-015: Review and edit AI suggestions
- [ ] L2-015.1: Given pending suggestions, when a tag or description is accepted, then it becomes active metadata with AI-accepted provenance, and its pending state is removed after reload.
- [ ] L2-015.2: Given a pending suggestion, when the user edits and accepts it, then only the edited normalized value becomes active, with edited-AI provenance and no change to personal notes.
- [ ] L2-015.3: Given a pending tag, when it is rejected, then it is removed from the review queue and never becomes an active tag; given a rejected description, then it is also excluded from semantic input until the user explicitly requests a new generation or supplies a description.
- [ ] L2-015.4: Given an active tag with different casing from an accepted suggestion, when the suggestion is accepted, then no duplicate active tag is created and the existing display spelling is preserved.
- [ ] L2-015.5: Given 50 active tags, when another distinct tag is accepted, then a field error explains the limit and leaves the suggestion pending; accepting an already-active tag does not consume another slot.
- [ ] L2-015.6: Given a failed review save, when the UI displays the failure, then it retains the selected decision for retry and does not claim that active metadata changed.

## L2-016: Maintain manual metadata without losing user changes
- [ ] L2-016.1: Given an owned reference or photographer bookmark, when valid tags or a description/summary are manually saved, then they become active with manual provenance and persist; tag categories are optional for manual tags and otherwise use the L2-014 categories.
- [ ] L2-016.2: Given a manually edited description, removed tag, and personal notes, when AI regeneration completes, then all three changes remain intact and new output appears only as suggestions.
- [ ] L2-016.3: Given a metadata save or review decision, when subsequent reads occur, then keyword fields and tag filters reflect it immediately and semantic records follow the L2-028 freshness window.
- [ ] L2-016.4: Given concurrent manual editing and a completing AI job, when the result is committed, then it cannot overwrite the edit; given concurrent manual edits, then L2-030 conflict behavior applies.
- [ ] L2-016.5: Given an active description that is cleared, when saved, then it remains absent without restoring an old generated draft into semantic input; the user can explicitly generate a new suggestion later.

## L2-017: Create and rename boards
- [ ] L2-017.1: Given no matching board name, when a valid name is created, then an empty board appears in board navigation and pickers after reload.
- [ ] L2-017.2: Given an existing board named Window Light, when the same user submits ` window light `, then a name-conflict error is returned and no second board is created; a different user can use that name independently.
- [ ] L2-017.3: Given a board with references, when renamed to an available valid name, then its identifier and memberships are unchanged and all views show the new name immediately.
- [ ] L2-017.4: Given empty, whitespace-only, or 81-character names, when created or renamed, then a field error is returned and existing data remains unchanged; an 80-character valid name succeeds.

## L2-018: Organize references across boards
- [ ] L2-018.1: Given one reference and two owned boards, when both are selected in its board picker and saved, then the reference appears once in each board and once in the full inspiration library with the same identifier.
- [ ] L2-018.2: Given an existing membership, when Add is repeated, then membership and board counts remain unchanged; repeating Remove for an already-absent membership also succeeds without side effects.
- [ ] L2-018.3: Given a reference in two boards, when removed from one, then it remains in the other and in the full library with its image, tags, and notes intact.
- [ ] L2-018.4: Given a picker submission including an unavailable/foreign board or reference, when saved, then the request follows shared 404 behavior and makes no partial membership changes.
- [ ] L2-018.5: Given an empty board or a board with 25 references, when opened, then the UI shows an add-reference empty action or a complete paginated list respectively, with an accurate total membership count.

## L2-019: Delete a board without deleting references
- [ ] L2-019.1: Given a populated board, when Delete is selected, then the confirmation names the board and explains that its references will remain; cancellation changes nothing.
- [ ] L2-019.2: Given confirmed deletion, when it succeeds, then the board disappears from navigation, pickers, and every reference's memberships immediately, while references remain in other boards and in the full library.
- [ ] L2-019.3: Given the user is viewing the deleted board, when deletion completes, then navigation returns to Inspiration and announces success without leaving an unusable page.
- [ ] L2-019.4: Given a saved search URL containing a now-deleted board filter, when opened, then the UI explains that the board is unavailable and offers removal of that filter; it does not silently broaden the search.

## L2-020: Save and edit photographer bookmarks
- [ ] L2-020.1: Given a valid name and portfolio URL, when saved, then the bookmark is usable immediately even if metadata fetching is pending or unavailable, and all manually entered fields survive reload.
- [ ] L2-020.2: Given a duplicate normalized portfolio URL, when saved by its owner, then the existing bookmark is returned with an Already saved message; changing another bookmark's URL to that value produces a conflict and preserves both records.
- [ ] L2-020.3: Given a bookmark, when its name, URL, summary, notes, or tags are edited, then shared validation applies and references remain linked to the same identifier.
- [ ] L2-020.4: Given an empty name, invalid scheme, overlength value, or forbidden URL, when saved, then the relevant field error is displayed with no partial bookmark and no outbound fetch.
- [ ] L2-020.5: Given the portfolio URL changes, when saved, then old fetched suggestions are labeled as belonging to the previous URL, they are excluded from current machine-derived search input, and new suggestions cannot overwrite manual values.

## L2-021: Generate grounded photographer summaries
- [ ] L2-021.1: Given an accessible page fixture with documented biography/style statements, when summary generation succeeds, then the suggested summary and tags reflect only that captured content, identify the fetched URL and timestamp, and remain separate from notes.
- [ ] L2-021.2: Given inaccessible content, an empty page, or a login/blocked page, when generation runs, then the UI states Summary unavailable with the reason and manual editing remains available; no summary is invented from the URL alone.
- [ ] L2-021.3: Given page links to galleries or other pages, when generating a summary, then none of those links are fetched; an optional preview image follows the same bounded preview rules as reference import.
- [ ] L2-021.4: Given a generated summary and edited manual summary, when the suggested summary is accepted, then the UI shows the replacement before saving and the explicit acceptance changes only the summary, preserving notes and tags.
- [ ] L2-021.5: Given page content containing instructions to reveal secrets or ignore the task, when summarized, then those instructions are treated as source text under L2-041 and cannot cause tool use, data disclosure, or unauthorized changes.
- [ ] L2-021.6: Given three captured page fixtures with pre-labeled factual statements (biography, style description, and title-only content), when each is summarized three times with the live configured model, then all nine outputs contain only supported factual claims; title-only content explicitly reports insufficient information for a substantive summary, and a named reviewer records the evidence for every claim.

## L2-022: Link references to photographers
- [ ] L2-022.1: Given an owned reference and photographer, when linked from either detail screen, then the reference shows the photographer and appears once in the photographer's linked references after reload.
- [ ] L2-022.2: Given an already-linked reference, when a different photographer is selected, then the UI states that the association will change and a confirmed save replaces the old association atomically.
- [ ] L2-022.3: Given no textual attribution, when a photographer is linked, then the current photographer name is copied as attribution; given existing attribution, then it remains unchanged and is displayed separately if it differs.
- [ ] L2-022.4: Given the photographer is renamed, unlinked, or deleted, when the reference is read, then the textual attribution and source URL remain intact; a surviving link displays the current photographer name.
- [ ] L2-022.5: Given a foreign/deleted reference or photographer, when linking is attempted, then the shared 404 response is returned and no relationship changes occur.

## L2-023: Browse photographer portfolios and linked inspiration
- [ ] L2-023.1: Given 25 bookmarks, when the collection is paged, then all appear once using shared pagination with names and source hostnames, including bookmarks with no image or AI summary.
- [ ] L2-023.2: Given a photographer with linked references, when opened, then the saved fields and paginated linked references appear and each reference opens its detail.
- [ ] L2-023.3: Given a photographer without references or without an available preview, when opened, then the UI provides a Link references action and a named placeholder respectively without suggesting missing content was analyzed.
- [ ] L2-023.4: Given an external portfolio action, when activated, then it opens the saved URL under the external-link protections in L2-012; opening a bookmark detail itself triggers no new fetch or AI charge.

## L2-024: Search saved library text by keyword
- [ ] L2-024.1: Given fixtures containing tokens in different eligible fields, when a multi-token keyword query is submitted, then an item is returned only if every token matches some eligible field, regardless of case; pending AI tags/summaries do not create keyword matches.
- [ ] L2-024.2: Given matching references, photographer bookmarks, another user's records, and My Work photographs, when searched, then only the owner's references and bookmarks are returned.
- [ ] L2-024.3: Given an empty/whitespace query, when Keyword search is submitted, then all items satisfying filters are returned; given over 500 characters, then a field error occurs without querying the search service.
- [ ] L2-024.4: Given multiple matching items, when results are paged, then they use the shared creation-date/identifier order and expose item type, title/name, preview or placeholder, and original-source action.
- [ ] L2-024.5: Given no match, when results load successfully, then the UI states No results and offers editing the query or clearing filters; a service error has a separate retryable state.

## L2-025: Combine tag, board, and type filters
- [ ] L2-025.1: Given items tagged A, B, or both, when A and B are selected, then only items with both active tags remain; pending suggestions never satisfy filters.
- [ ] L2-025.2: Given references in board X, Y, or neither, when X and Y are selected, then only references in at least one selected board remain, without duplicates for references in both.
- [ ] L2-025.3: Given query text, selected tags, boards, and References type, when searched in either mode, then every result satisfies every filter group and the query's mode-specific rule.
- [ ] L2-025.4: Given Photographers type with a board selected, when searched, then the UI explains that boards contain references and returns no matches rather than ignoring either filter.
- [ ] L2-025.5: Given a foreign/deleted board ID, more than 10 tags or boards, or an invalid type, when searched, then the shared unavailable/validation response applies; an unknown but syntactically valid tag yields no matches.
- [ ] L2-025.6: Given active filters, when one is removed or Clear filters is selected, then results update with the query and mode retained, and filter state is restored by browser Back/Forward and page reload from the same search URL.

## L2-026: Find inspiration by meaning
- [ ] L2-026.1: Given current indexed fixtures and a nonempty query such as Moody portraits with soft window light, when Meaning search is submitted, then relevant image references can be returned even without exact query words in active tags, and each result opens the saved item and source.
- [ ] L2-026.2: Given a blank query, when Meaning search is submitted, then the UI requests a query rather than generating an empty embedding; Keyword mode still supports filtered browsing.
- [ ] L2-026.3: Given a valid query and combined filters, when candidates are ranked, then filtering occurs before pagination and results do not underfill a page while eligible candidates remain.
- [ ] L2-026.4: Given unavailable embedding/search service, when Meaning search fails, then the UI explicitly reports its unavailability and offers switching to Keyword; it never labels keyword matches or demo fixtures as live semantic results.
- [ ] L2-026.5: Given a frozen release corpus of 60 references and 20 photographer bookmarks, 12 queries with at least five pre-labeled relevant items each, and documented model/configuration, when live semantic evaluation runs, then at least four of the first five results are relevant for at least 10 queries and at least three are relevant for every query; the three product-brief example queries must be included.
- [ ] L2-026.6: Given equal similarity scores or a changed index generation during pagination, when subsequent pages are fetched, then ties are stable and a cursor from a different generation returns a refresh-required conflict instead of mixing generations.

## L2-027: Show related references
- [ ] L2-027.1: Given at least seven eligible related references, when detail loads, then the six most similar are shown in descending similarity with identifier tie-breaking, excluding the source itself.
- [ ] L2-027.2: Given fewer than six eligible matches, when displayed, then only those matches appear; given none, then a No related references yet state is displayed without unrelated filler.
- [ ] L2-027.3: Given no current vector or a temporarily unavailable similarity service, when detail loads, then the main detail remains usable and the related area states Indexing or Temporarily unavailable as applicable.
- [ ] L2-027.4: Given a related reference, when opened, then its owned detail appears; another user's or deleted reference never appears even if the vector store contains a stale candidate.

## L2-028: Keep search consistent with library changes
- [ ] L2-028.1: Given a new or edited item with all required description generation complete and healthy embedding/index dependencies, when persisted, then its current vector becomes searchable within 60 seconds; if visual analysis is needed, then the 60 seconds starts at that analysis's success and the prior wait is labeled Processing description.
- [ ] L2-028.2: Given an edited source field, removed/rejected tag, changed image, or changed portfolio URL, when acknowledged, then stale vectors are excluded immediately and the UI shows Updating search until the replacement is current.
- [ ] L2-028.3: Given changed notes, tags, photographer name, or board membership, when keyword/filter searches run immediately afterward, then they reflect the change; affected linked-reference text is refreshed when a photographer name changes.
- [ ] L2-028.4: Given indexing failure, when the job fails, then keyword search and library editing remain available, the item shows Search indexing failed with retry, and no stale vector is presented as current.
- [ ] L2-028.5: Given a deleted item and stale search candidate, when any keyword, semantic, or related-results request runs, then ownership/deletion checks remove it before results and counts are returned.
- [ ] L2-028.6: Given a configured embedding model change, when rebuilding search, then vectors from incompatible models are never compared; Meaning search remains on a coherent old generation until replacement is ready, or reports temporarily unavailable if its query model is unavailable.

## L2-029: Persist successful changes across sessions and restarts
- [ ] L2-029.1: Given saved photographs, critiques, references, boards, memberships, bookmarks, links, reviews, and notes, when the user signs out, all application/worker processes restart, and the user signs in through a new browser session, then every acknowledged value and relationship remains available.
- [ ] L2-029.2: Given a database or media-storage failure before commit, when a save is attempted, then no success is acknowledged and no half-saved item is readable; staged files are cleaned by L2-032.
- [ ] L2-029.3: Given a restart immediately after job admission is acknowledged, when workers resume, then the job is recovered under L2-033 and is not silently lost.
- [ ] L2-029.4: Given a deployment upgrade against a copy of the populated acceptance database, when its supported migration procedure completes, then all saved content remains readable and linked correctly; failure exits without serving an incompatible schema.

## L2-030: Prevent duplicate submissions and lost edits
- [ ] L2-030.1: Given the same user, operation key, and payload submitted concurrently or after a lost response, when processed within 24 hours, then only one operation is committed and all successful responses identify the same item/job.
- [ ] L2-030.2: Given reuse of a key with a different payload, when submitted within 24 hours, then a conflict is returned without applying the second payload; keys belonging to different users do not collide or reveal each other.
- [ ] L2-030.3: Given two editors loaded at revision N, when one saves and the other then saves against N, then the second receives a conflict, retains its attempted text, and can reload the latest value before intentionally submitting a new edit.
- [ ] L2-030.4: Given a file upload whose success response was lost, when retried with the same key and bytes, then the existing saved image is returned and no duplicate media or AI job remains.
- [ ] L2-030.5: Given a deleted record whose original create key is replayed, when the retained key is resolved, then it returns the unavailable/deleted result and does not recreate the record; after key expiry, a fresh submission is a new intentional operation subject to URL uniqueness rules.

## L2-031: Delete saved content and its dependent data
- [ ] L2-031.1: Given any deletable item, when Delete is selected and canceled, then nothing changes; when confirmed, then the item becomes unavailable immediately in lists, details, media access, counts, search, and comparison/pickers.
- [ ] L2-031.2: Given a photograph with a critique, notes, previews, EXIF, and active jobs, when deleted, then all associated records/files are scheduled for removal and its jobs cannot later restore content or commit results.
- [ ] L2-031.3: Given a reference in multiple boards with suggestions and vectors, when deleted, then all its memberships, suggestions, files, and search records are removed while the boards and linked photographer remain.
- [ ] L2-031.4: Given a photographer linked to references, when deleted, then its bookmark, fetched preview, suggestions, and search records are removed, references are unlinked, and their textual attribution, images, source URLs, boards, and notes remain intact.
- [ ] L2-031.5: Given repeated deletion by the original owner while the deletion record is retained, when requested, then the same completed/pending outcome is returned without new effects; an unknown/foreign identifier receives the shared 404 response.
- [ ] L2-031.6: Given a storage cleanup failure, when deletion is acknowledged, then the item remains inaccessible, cleanup remains retryable and visible as pending in the deletion operation, and success never falsely claims that physical cleanup has completed.

## L2-032: Complete file cleanup and prevent resurrection
- [ ] L2-032.1: Given completed deletion with healthy storage, when 24 hours elapse, then originals, previews, extracted metadata, content records, suggestions, and vectors are absent from active stores; only a minimal deletion record and redacted operational audit remain.
- [ ] L2-032.2: Given interrupted uploads or superseded files, when 24 hours elapse after abandonment/replacement, then unreferenced bytes are removed while currently referenced media remains readable.
- [ ] L2-032.3: Given a worker finishing after deletion, when it attempts to commit or publish output, then deletion takes precedence and any staged output is scheduled for cleanup without restoring the item.
- [ ] L2-032.4: Given storage unavailable for more than 24 hours, when cleanup remains overdue, then an operational alert is raised, the user can see cleanup is pending, and retries resume after recovery until removal succeeds.
- [ ] L2-032.5: Given a backup containing an item deleted afterward, when the backup is restored, then deletion records are replayed before readiness, the item remains inaccessible, and restored deleted bytes are cleaned within 24 hours; backup objects expire by day 30.

## L2-033: Run slow work durably in the background
- [ ] L2-033.1: Given a valid admitted request, when the API acknowledges it, then it supplies a durable operation identifier and status location without waiting for provider completion; the operation survives process termination immediately after acknowledgment.
- [ ] L2-033.2: Given workers running normally, when a job moves through its lifecycle, then an open detail screen reflects each persisted state within five seconds without full-page reload; after navigation away and back, it shows the durable current state.
- [ ] L2-033.3: Given a worker interrupted while Running, when it has not renewed ownership for 60 seconds, then another worker can recover the job, and only a worker still holding valid ownership can commit its result.
- [ ] L2-033.4: Given a job for an outdated image/URL revision or deleted item, when it starts or attempts to commit, then it becomes Canceled and cannot publish obsolete content.
- [ ] L2-033.5: Given an unsupported status read or an operation belonging to another user, when requested, then shared validation/404 behavior applies and no provider details or private input are returned.

## L2-034: Bound failures and support safe retries
- [ ] L2-034.1: Given transient failures followed by success, when the job runs, then attempt timing follows the policy, the UI shows waiting/running accurately, and only one successful result is committed.
- [ ] L2-034.2: Given repeated transient failures or a never-returning provider, when the attempt budget is exhausted, then the job becomes Failed with a safe reason and a manual Retry action; no indefinite Running state remains.
- [ ] L2-034.3: Given invalid structured output twice, unsupported input, access denied, or invalid provider credentials, when processed, then the job fails according to its retry class, preserves the user's saved content, and exposes no raw provider payload.
- [ ] L2-034.4: Given a failed job and a permitted manual retry, when selected repeatedly, then one new operation using the original input snapshot is created under L2-030; a changed current input instead requires a new request clearly labeled with the current brief/image.
- [ ] L2-034.5: Given a failed replacement with an earlier successful result, when Retry is offered, then the earlier result stays readable throughout retry and is replaced only on success.
- [ ] L2-034.6: Given a source disallowed by robots or an administratively disabled provider, when the user inspects the failure, then the UI explains the configuration/access issue and offers the applicable manual fallback instead of claiming immediate retry will fix it.

## L2-035: Reuse completed work and control admission
- [ ] L2-035.1: Given an equivalent queued/running operation, when requested again, then its identifier is returned; given an equivalent completed operation without Regenerate, then the saved result is returned without another provider call.
- [ ] L2-035.2: Given changed analysis inputs or explicit Regenerate, when no job of that type is active and quotas allow it, then one new job is created; if different inputs are submitted while that type is active, then a conflict explains that the current operation must finish first.
- [ ] L2-035.3: Given five active user-requested analysis/import operations, when a sixth new analysis/import operation is requested, then admission returns 429 with Retry-After of 30 seconds, creates no hidden job, and leaves saved content usable; equivalent-job reads and automatic index maintenance do not consume this admission quota.
- [ ] L2-035.4: Given many queued jobs, when workers execute them, then simultaneous provider calls never exceed the per-user and deployment caps, and users with queued jobs are selected in round-robin order.
- [ ] L2-035.5: Given model, prompt version, execution mode, or image revision changes, when work is requested, then a result generated for the incompatible inputs is not reused.
- [ ] L2-035.6: Given an uncertain provider outcome after a worker crash, when recovered, then a provider idempotency key is reused where supported; otherwise at most one recovery call is allowed within the total attempt budget, and the documented limitation acknowledges that duplicate external charges cannot be ruled out despite a single committed result.
- [ ] L2-035.7: Given content has persisted but its requested analysis cannot be admitted, when the save result is shown, then the content remains usable and the UI explicitly states Analysis not queued with a later request action; required index maintenance remains durably recorded and is not silently dropped because of user-request quotas.

## L2-036: Separate demo and live integrations honestly
- [ ] L2-036.1: Given Demo mode, when any screen shows generated critique, descriptions, summaries, tags, or semantic results, then it visibly states Demo output or Demo search and explains that it is simulated; fixtures persist with demo provenance.
- [ ] L2-036.2: Given Live mode without required credentials, when an affected action is selected, then the UI reports Integration not configured, the API returns a safe capability error, and no mock result is produced; manual saving, browsing, and keyword search remain available.
- [ ] L2-036.3: Given configured Live mode, when each configured provider capability is exercised in a release smoke check, then the provider request and persisted non-demo result are verified; a successful mock acceptance run alone is never reported as this check.
- [ ] L2-036.4: Given a mode switch, when prior saved output is opened, then its original demo/live label remains; existing demo vectors are excluded from live Meaning results and can be replaced only by live indexing work.
- [ ] L2-036.5: Given Demo mode with real user uploads, when third-party AI network traffic is monitored during all AI actions, then none is emitted; URL imports still disclose and obey their independent external-fetch behavior.

## L2-037: Authenticate users and end sessions
- [ ] L2-037.1: Given a signed-out visitor, when a private route is opened, then sign-in is required and successful authentication returns to a validated local destination; direct unauthenticated API/media requests return 401 without content.
- [ ] L2-037.2: Given an invalid/tampered identity response, wrong state/nonce, untrusted issuer, or expired assertion, when the callback is processed, then no session is established and the user sees a retryable sign-in failure without tokens in the URL or error message.
- [ ] L2-037.3: Given a valid session, when idle time reaches 30 minutes or session age reaches 12 hours, then the next authenticated request is rejected and the UI requests sign-in again; immediately before either boundary, an otherwise valid request succeeds.
- [ ] L2-037.4: Given Sign out selected, when acknowledged, then the application session is invalidated server-side, browser-held private state is cleared, and Back/refresh or reuse of that session cannot retrieve private content.
- [ ] L2-037.5: Given production cookie authentication, when the session is created and used, then its cookie is Secure and HttpOnly with an appropriate SameSite policy, successful login rotates the session identifier, and sign-in permits password managers and paste without application-added cognitive puzzles.

## L2-038: Enforce ownership at every data boundary
- [ ] L2-038.1: Given users A and B with independent seeded libraries, when A substitutes B's identifiers in every read, edit, delete, retry, compare, link, board, media, or operation action, then each returns indistinguishable 404 and B's content is unchanged.
- [ ] L2-038.2: Given a client submits another owner ID in a create/update request, when validated, then it cannot create or reassign data to that user; authenticated ownership is enforced independently of the payload.
- [ ] L2-038.3: Given search and count queries as A, when matching text, vectors, or IDs exist only for B, then B contributes no results, previews, names, or counts.
- [ ] L2-038.4: Given a copied private image URL, when requested without the owner session, then no image bytes are returned; generated previews follow the same rule and are never served from a public bucket or an unauthenticated CDN URL.
- [ ] L2-038.5: Given a private page/API/media response, when inspected, then it prohibits shared/public caching; after sign-out or deletion the application does not restore that response from its own cache.

## L2-039: Validate files and untrusted input
- [ ] L2-039.1: Given supported image signatures and matching decodable content, when uploaded, then the shared size/pixel/frame limits are enforced; renamed executables, SVG scripts, MIME mismatches, corrupt files, and decompression-limit fixtures are rejected with the shared errors.
- [ ] L2-039.2: Given filenames with traversal sequences or absolute paths, when uploaded, then no file is written outside managed image storage and the display filename cannot influence its storage key.
- [ ] L2-039.3: Given HTML/script, SQL metacharacters, or template expressions in any editable field, imported metadata, EXIF string, or AI response, when saved and displayed/searched, then they remain inert text, execute no query/code, and do not change unrelated data.
- [ ] L2-039.4: Given a cookie-authenticated mutation with a missing/invalid antiforgery token or untrusted origin, when submitted, then it is rejected with 403 before side effects; allowed-origin requests with valid protections work.
- [ ] L2-039.5: Given invalid IDs, oversized collections, malformed JSON, or unexpected enum values, when submitted directly to the API, then bounded validation errors occur without a server crash or partial mutation.

## L2-040: Restrict outbound URL fetching
- [ ] L2-040.1: Given forbidden schemes, ports, credentials, literal internal IPs, or DNS resolving internally, when a source is saved for fetching, then a safe validation error occurs and a controlled network observer records no connection to the forbidden target.
- [ ] L2-040.2: Given an allowed URL redirecting or resolving again to a forbidden destination, including IPv4-mapped IPv6 and DNS-rebinding fixtures, when fetched, then the connection is blocked before private bytes are retrieved.
- [ ] L2-040.3: Given more than five redirects, a redirect loop, an excessive decoded response, or a fetch exceeding 30 seconds, when importing, then fetching stops within the budget and the link-only/manual fallback is offered.
- [ ] L2-040.4: Given a preview or robots URL with a forbidden target, when page import encounters it, then the same restrictions apply rather than trusting the original page's allowed host.
- [ ] L2-040.5: Given a source request, when its headers are inspected, then it identifies the Loupe importer but includes no user's session cookies, access tokens, provider credentials, or unrelated authorization headers; source scripts cannot initiate additional requests.

## L2-041: Protect credentials, image metadata, and AI boundaries
- [ ] L2-041.1: Given an image with GPS, serial number, owner name, and camera settings, when stored/previewed or submitted to AI, then GPS/serial/owner fields are removed from all retained image copies and extracted metadata, and the provider receives only permitted image/brief/EXIF fields.
- [ ] L2-041.2: Given a live AI action, when its request control is displayed, then nearby disclosure identifies the configured provider and the image/brief or source content sent; selecting the action authorizes that operation without a second approval dialog, and saving content without selecting it causes no critique transmission.
- [ ] L2-041.3: Given browser bundles, network responses, errors, and logs from all workflows, when inspected with planted credential/token values, then those values are absent and credentials are retrieved only through server configuration or the configured secret store.
- [ ] L2-041.4: Given hostile instructions embedded in images, page text, notes, or provider output, when processed, then they cannot invoke tools, fetch new URLs, execute code, expose secrets/other records, or mutate anything except the operation's validated result fields.
- [ ] L2-041.5: Given the production deployment, when an HTTP request reaches its public endpoint, then HTTPS is enforced; storage and backup encryption are enabled, production secrets are absent from source-controlled setup examples, and external-provider retention/configuration is accurately documented without promising unsupported deletion guarantees.

## L2-042: Bound abuse and report safe security failures
- [ ] L2-042.1: Given the quota boundary for each request class, when the next request arrives within its rolling window, then it returns 429 with the number of seconds until capacity becomes available and performs no side effect; a request after expiry succeeds.
- [ ] L2-042.2: Given requests distributed across two application instances, when their combined count exceeds a user's limit, then the same quota applies and another user's independent quota remains available.
- [ ] L2-042.3: Given a validation, authentication, authorization, dependency, or unexpected failure, when returned to a client, then it contains a safe error code, useful message, and correlation ID without stack traces, SQL, file paths, secrets, or other users' data.
- [ ] L2-042.4: Given failed sign-ins, denied access, quota rejection, deletion, or administrative provider/configuration changes, when recorded, then the security audit contains timestamp, event type, outcome, correlation ID, and a pseudonymous actor identifier without private payloads; audit records expire after 30 days.
- [ ] L2-042.5: Given deployment security review, when evaluated against the OWASP Top 10:2025 categories, then applicable attack paths have recorded behavioral checks or configuration evidence and no unresolved critical/high finding; this review must not add architecture or source-layout tests.

## L2-043: Provide clear navigation and recoverable states
- [ ] L2-043.1: Given an authenticated user, when any main area or owned detail URL is opened directly or reloaded, then it resolves correctly with the active navigation state and an appropriate page title; an unknown route provides a return-to-library action.
- [ ] L2-043.2: Given an empty area, when displayed, then My Work offers Upload photograph, Inspiration offers Save reference, Photographers offers Add photographer, and Search explains queries/filters without implying a request failed.
- [ ] L2-043.3: Given a failed list/detail request, when displayed, then a useful message and Retry action appear, already loaded content is retained where available, and retry success replaces the error without losing current filters.
- [ ] L2-043.4: Given an upload, save, import, or AI request in flight, when the user acts again, then the relevant submit control indicates work in progress and accidental duplicate activation cannot create another operation; unrelated navigation remains usable.
- [ ] L2-043.5: Given unsaved text, when the user attempts in-app navigation or closes its editor, then Keep editing preserves the draft and Discard abandons it; browser unload requests the browser's available unsaved-change protection without requiring custom browser text.
- [ ] L2-043.6: Given session expiry during editing, when re-authentication is required, then drafts are retained only in the current tab's memory and restored only after the same user signs in; sign-out, tab closure, or a different authenticated user clears them.

## L2-044: Adapt every workflow to viewport size
- [ ] L2-044.1: Given populated grids at every matrix width and immediately around each breakpoint, when rendered, then the stated column counts apply and no card, label, or primary action overlaps adjacent content.
- [ ] L2-044.2: Given any main screen, detail, comparison, form, dialog, or picker at each matrix size, when the user completes its primary action, then all required controls are reachable without horizontal page scrolling, clipped text, or hover-only interactions.
- [ ] L2-044.3: Given dialogs at 375 x 667 and 844 x 390, when content exceeds available height or the virtual keyboard is open, then content scrolls, focused input and confirmation/cancel controls remain reachable, and the background does not intercept interactions.
- [ ] L2-044.4: Given a full image in portrait, panorama, or square format, when the viewport changes, then its aspect ratio is preserved and detail continues to provide the complete frame; image loading reserves display space without displacing focused controls.
- [ ] L2-044.5: Given compact navigation, when opened by keyboard or touch and a destination is selected, then it exposes every main area, closes predictably, and updates focus to the destination's main content.

## L2-045: Support keyboard and assistive technology
- [ ] L2-045.1: Given a keyboard-only user, when they sign in, upload, request critique, edit notes/metadata, manage boards, link a photographer, compare, search, and delete, then every action completes with logical focus order, visible unobscured focus, and no keyboard trap.
- [ ] L2-045.2: Given a modal dialog, when opened, then focus enters it and remains within it until closing; Escape performs cancellation unless an acknowledged operation is already executing, and closing restores focus to its trigger or the next relevant control if that trigger was deleted.
- [ ] L2-045.3: Given icon controls, inputs, selection state, field errors, and asynchronous status changes, when inspected with the supported screen reader, then names/roles/states are announced, errors identify their fields, and progress/success/failure announcements do not unexpectedly move focus.
- [ ] L2-045.4: Given an image or decorative icon, when read by assistive technology, then the image has a meaningful description from active metadata or a neutral title-based fallback and decorative icons are ignored; unknown visual details are never invented for alternative text.
- [ ] L2-045.5: Given the full acceptance screen/state set, when automated accessibility scans and documented manual checks run, then no applicable Level A/AA violation remains; contrast is at least 4.5:1 for normal text, 3:1 for large text, and 3:1 for required non-text control boundaries/indicators.
- [ ] L2-045.6: Given pointer interactions, when controls are measured and exercised, then targets are at least 24 x 24 CSS pixels, drag/drop has a non-drag alternative, and essential instructions or state are not conveyed only by color, position, or hover.

## L2-046: Support zoom, motion preferences, and browser variation
- [ ] L2-046.1: Given all primary workflows in each pinned browser engine, when acceptance tests run, then saving, editing, filtering, retrying, navigating, and viewing HEIC-derived previews behave consistently with the same fixtures.
- [ ] L2-046.2: Given desktop browser zoom at 200% and 400% on a 1280-pixel-wide viewport, when screens and dialogs are used, then content reflows without loss of controls or two-dimensional page scrolling; intended full-image inspection can use its own bounded viewport.
- [ ] L2-046.3: Given increased text spacing and long permitted titles/tags/URLs, when rendered, then content wraps or offers an accessible full-value view without overlapping controls or hiding required actions.
- [ ] L2-046.4: Given reduced-motion preference, when navigation, loading, and dialogs run, then nonessential motion is removed and progress remains understandable without animated movement.
- [ ] L2-046.5: Given the documented mobile browser and screen-reader matrix, when the primary workflows receive release checks, then file selection, responsive controls, focus management, and status announcements pass with recorded versions and findings.

## L2-047: Meet measurable browsing and API budgets
- [ ] L2-047.1: Given the baseline seed and profile, when measured, then each read category has p95 latency at most 500 ms, each write category at most 750 ms, and job admission at most 500 ms; unexpected 5xx/timeouts total less than 0.1% and valid acknowledged writes are never lost.
- [ ] L2-047.2: Given 10 concurrent users issuing one Meaning search every two seconds with a controlled 100 ms query-embedding response and production search storage, when run for 10 minutes, then p95 full semantic latency is at most 1,000 ms and no ownership/filter violation occurs.
- [ ] L2-047.3: Given each main area opened in a production browser build with 10 Mbps downstream, 1 Mbps upstream, 100 ms round-trip latency, a fourfold CPU slowdown, and an empty HTTP cache, when measured for 20 runs, then p75 Largest Contentful Paint is at most 2.5 seconds and p75 Cumulative Layout Shift at most 0.1; network errors and AI processing are not included as successful loads.
- [ ] L2-047.4: Given the 20-run browsing scenario, when cards, filters, and dialogs are exercised, then the p75 time from input to the next rendered UI acknowledgment is at most 200 ms; network-dependent completion is measured separately.
- [ ] L2-047.5: Given each valid image format and a 25 MB image, when all upload bytes have arrived on an otherwise idle baseline environment, then durable image/preview save acknowledgment takes at most five seconds at p95 over 20 uploads, excluding AI work.

## L2-048: Keep resource use and job recovery bounded
- [ ] L2-048.1: Given a library with 10,000 references, when the initial 24-card grid opens without scrolling, then no original images and no more than 48 grid previews are fetched; additional pages are fetched only on user pagination or approaching the next page while scrolling.
- [ ] L2-048.2: Given oversized page-size input above 100, when a list endpoint is called, then it returns a field error rather than loading an unbounded result; valid cursor traversal follows the shared list rules.
- [ ] L2-048.3: Given 20 concurrent maximum-supported uploads on the baseline worker allocation, when processed with enforced limits, then worker memory stays below its 8 GiB allocation, no process is terminated for memory exhaustion, and accepted operations complete or return a defined retryable capacity error without losing acknowledged content.
- [ ] L2-048.4: Given no queue backlog and healthy workers, when a job is acknowledged, then processing starts within five seconds; a fixture provider returning after two seconds produces a durable visible result within 10 seconds of admission.
- [ ] L2-048.5: Given two workers racing the same operation or one terminated while running, when recovery completes, then only one result becomes current, no acknowledged job is lost, and recovery starts within 90 seconds of the last valid ownership renewal.
- [ ] L2-048.6: Given an additional API or worker instance, when the same acceptance workload runs, then persisted ownership, rate limits, deduplication, and per-user/deployment provider caps remain correct across instances.

## L2-049: Diagnose failures and verify recovery
- [ ] L2-049.1: Given a controlled failed API request followed by a background retry failure, when operators inspect diagnostics using the displayed correlation/operation IDs, then they can identify stage, safe failure class, attempt count, and duration without seeing user images, briefs, notes, source-page bodies, secrets, or complete private URLs.
- [ ] L2-049.2: Given unavailable database, media storage, or durable queue, when readiness is queried, then it fails with 503 while liveness remains successful if the process responds; an unavailable AI provider reports a degraded AI capability while readiness permits manual library operations.
- [ ] L2-049.3: Given a job queued over five minutes, indexing lag over five minutes, cleanup older than 24 hours, or unexpected 5xx exceeding 1% over five minutes with at least 100 requests, when the monitoring window closes, then an alert event with the affected capability and safe diagnostic link is emitted.
- [ ] L2-049.4: Given a database/storage failure and restored dependencies, when failed operations are retried, then readiness recovers and persisted work resumes without manual database editing or duplicate results.
- [ ] L2-049.5: Given automated encrypted backups at least hourly, when the documented restore procedure is exercised on the baseline dataset, then library availability is restored within four hours of restoration start, at most one hour of acknowledged pre-incident changes is lost, and L2-032 deletion replay occurs before readiness.

## L2-050: Deliver reproducible setup and operational guidance
- [ ] L2-050.1: Given a clean supported development environment and the documented commands, when setup is followed using only the example configuration plus documented local identity provisioning, then API, worker, web client, and design-system site start and all five product-brief completion workflows succeed in clearly labeled Demo mode.
- [ ] L2-050.2: Given valid live provider and identity configuration, when the documented connection checks run, then they exercise critique, visual metadata, photographer summary, and embeddings against configured real services and report actual success or actionable configuration failures without exposing secrets.
- [ ] L2-050.3: Given a missing/invalid required setting or incompatible schema, when the affected process starts, then it exits with the setting name and safe corrective guidance; optional missing AI credentials follow L2-036 rather than preventing manual-library startup.
- [ ] L2-050.4: Given the documented test commands, when executed, then API integration acceptance, Playwright acceptance, relevant regression suites, production builds, accessibility/performance/AI evaluation procedures, and design-system checks can be reproduced with clearly separated automated and manual results.
- [ ] L2-050.5: Given a release, when its delivery notes are reviewed, then exact tested versions, configuration assumptions, deployment/rollback and restore procedures, executed checks with results, provider limitations, and any unverified integration are stated accurately; an unexecuted check is never described as passing.

## L2-051: Publish an independent design-system reference site
- [ ] L2-051.1: Given the documented design-system-only install/build commands, when run and their output served on a static host with the application/API stopped, then every reference page and local navigation route works without application data or service requests.
- [ ] L2-051.2: Given the published site, when a designer browses its token reference, then token names, values, roles, and visible examples are available for every listed token category, with readable contrast and keyboard navigation.
- [ ] L2-051.3: Given component examples, when viewed, then buttons, form inputs/selects/text areas, cards, tags/status pills, navigation, dialogs, progress indicators, notices, and empty/error states each show their relevant default, hover, focus, disabled, loading, selected, and error states.
- [ ] L2-051.4: Given the shared viewport/accessibility matrix, when the site is exercised, then examples and documentation remain usable under L2-044 through L2-046; grids demonstrating app layouts show the documented breakpoints.
- [ ] L2-051.5: Given the standalone site deployment, when network activity and visible content are inspected, then no personal images, user records, runtime API calls, provider credentials, or application login dependency are present.

## L2-052: Apply the authoritative visual system consistently
- [ ] L2-052.1: Given approved component examples in the design-system site, when equivalent application controls render in the same state and viewport, then their observable typography, color, spacing, radius, focus, and disabled/error appearance match the examples in visual review.
- [ ] L2-052.2: Given approved visual baselines for My Work, critique, comparison, Inspiration, reference, Photographers, photographer detail, and Search at 375, 768, and 1440 pixels, when the application is rendered with deterministic content, then visual comparison shows no unexplained layout or styling regression; review records deliberate baseline updates.
- [ ] L2-052.3: Given photographs with dark, bright, saturated, and monochrome content, when viewed in cards and details, then surrounding controls remain readable, no tint/filter is applied to the displayed photograph, and the complete detail image follows L2-012/L2-044.
- [ ] L2-052.4: Given keyboard focus, validation error, processing, and selected states, when displayed on each application screen, then the same state meaning and appearance match the documented reference and remain accessible without color alone.

## Azure OpenAI critique integration
- [x] L2-036.6–7: Configure Azure and verify the Responses request using failing-first API acceptance tests.
- [x] L2-036.8–9: Verify saved-result compatibility and Azure failure handling.
- [x] L2-041.2 / L2-050: Name Azure in request disclosures; update setup and run regression checks.

## L2-055: Save and edit a location's details
- [x] L2-055.1: Given an authenticated user and only a valid name, when Add location is saved, then one location record appears in Locations after reload showing the name, Address not recorded, No images, and No scouting report, and it never appears in My Work, Inspiration, Photographers, or Inspiration Search.
- [ ] L2-055.2: Given every optional field supplied with valid values including both coordinates, when saved and the detail is reloaded, then the exact normalized values are displayed, the address appears line by line in the entered order with empty lines omitted, coordinates show six decimal places, and setting, brief, notes, and tags are shown in their own labeled areas.
- [ ] L2-055.3: Given each text field at its maximum length and then one character above it, a latitude of 90.000000 and 90.000001, a longitude of -180.000000 and -180.000001, a setting outside Indoor, Outdoor, or Mixed, or a latitude without a longitude, when saved, then boundary-valid values succeed and each invalid value returns a field error naming that field while the previously saved values remain unchanged.
- [ ] L2-055.4: Given a location with images, a scouting report, and tags, when only the name, address, coordinates, setting, or notes are edited and saved, then the images, image order, cover, report, report status, and tags are unchanged, and keyword search reflects the edit immediately while semantic search follows L2-062.
- [ ] L2-055.5: Given a location with a current scouting report, when the scouting brief is edited and saved, then the report remains current and readable, its display identifies the brief snapshot it used, and the detail offers Regenerate to apply the new brief under L2-060.
- [ ] L2-055.6: Given two editors loaded at the same revision, when both save, then the second receives the L2-030 conflict, retains its attempted values, and can reload before resubmitting; no partial field update is applied.
- [ ] L2-055.7: Given HTML, script, SQL metacharacters, or template expressions in any address line, locality, or name, when saved, displayed, and searched, then they remain inert text under L2-039 and match keyword tokens as plain text.
- [ ] L2-055.8: Given the Add location or Edit location dialog at every shared viewport, when opened with unsaved changes and closed by Close, Escape, backdrop, or navigation, then the L2-043 unsaved-change protection applies; Discard clears the draft and Keep editing preserves it.

## L2-056: Upload and manage location images
- [ ] L2-056.1: Given a location with no images and a valid image in each supported format, when each is uploaded, then each becomes a location image with a correctly oriented browser-compatible preview, the images are listed in upload order after reload, and the first uploaded image is the cover.
- [ ] L2-056.2: Given a location with ten images, when an eleventh upload is submitted directly to the API, then a field error states the ten-image limit and no bytes are retained; the UI shows Add images disabled with the reason and the remaining capacity is displayed while fewer than ten exist.
- [ ] L2-056.3: Given a selection of several files whose count is at most the remaining capacity, when Upload is confirmed, then each file transfers as its own operation with its own progress, a failure or invalid file affects only that file and offers retry or removal for it, successfully uploaded images remain saved, and no file is silently skipped.
- [ ] L2-056.4: Given a selection whose count exceeds the remaining capacity, when the files are chosen, then the UI rejects the selection before any transfer, states the remaining capacity, and keeps the existing images unchanged.
- [ ] L2-056.5: Given a location with three images and a current scouting report, when one image is removed and the removal is confirmed, then the image disappears from the gallery immediately, the other two keep their order, the cover moves to the next image if the cover was removed, the removed bytes follow L2-032, and the report is labeled Outdated under L2-060.
- [ ] L2-056.6: Given a location with several images, when a different image is chosen as cover and the page reloads, then the grid card and detail show that image as cover and the image order is unchanged.
- [ ] L2-056.7: Given a foreign or deleted location identifier, when an image upload, cover change, or removal is submitted, then the shared 404 response is returned and no file is written.
- [ ] L2-056.8: Given a location image containing GPS, serial number, and owner name EXIF, when uploaded, then those fields are absent from every retained copy and from any extracted metadata, and the location's coordinates remain exactly as the user entered them or absent.
- [ ] L2-056.9: Given an upload in progress, when the user cancels it, then the transfer is aborted, no partial image appears in the gallery, and the notice offers Refresh to reconcile a later server commit as in L2-001.

## L2-057: Browse, inspect, and delete locations
- [ ] L2-057.1: Given 25 locations, a photograph, and a reference owned by the user, when Locations is opened and advanced through its results, then all 25 locations appear once in the shared order with a cover image or a labeled placeholder, name, locality when present, image count, and report status, and the photograph and reference never appear.
- [ ] L2-057.2: Given a location with images, when its detail is opened directly by URL or reloaded, then the gallery offers every image with the complete oriented frame available without cropping, the address block, coordinates, setting, brief, notes, tags, report or report status, and created and updated timestamps are shown, and no thumbnail is the only available detail image.
- [ ] L2-057.3: Given a location with no images, when its detail is opened, then a labeled placeholder and an Add images action are shown, and the scouting report area explains that an image is needed instead of showing a failure.
- [ ] L2-057.4: Given no locations, a failed list request, or an unavailable or foreign detail, when displayed, then the empty state offers Add location and the failed list and unavailable detail have the distinct recovery behaviors in L2-043.
- [ ] L2-057.5: Given populated Locations at every shared viewport width, when rendered, then the grid uses the L2-044 column rule for image grids, the detail gallery and details stack below 992 CSS pixels and sit alongside each other from 992 upward, and the gallery is operable by keyboard with the selected image announced.
- [ ] L2-057.6: Given a location, when Delete is selected, then the confirmation names the location and states that its images and scouting report will be removed; cancellation changes nothing; confirmation makes the location unavailable immediately in the grid, detail, media, Find a location, and operations, and cleanup follows L2-032.
- [ ] L2-057.7: Given repeated deletion of the same location by its owner or deletion of a foreign identifier, when requested, then L2-031 criterion 5 applies.

## L2-058: Deliver a structured scouting report
- [ ] L2-058.1: Given a valid provider result covering every section for a location with three images, when processing completes, then each section is saved and displayed under its own heading, every entry shows its cited images, and the overview, suitability, time of day, techniques, group size, and cautions appear in that order.
- [ ] L2-058.2: Given a valid result, when its suitability section is inspected, then it contains exactly one entry for each of Portraits, Family portraits, Headshots, Engagement, and Events, each with a rating from the shared set and a nonblank reason.
- [ ] L2-058.3: Given a valid result, when its time-of-day section is inspected, then it contains exactly one entry for each shared period, at least one period is Recommended unless every period is Unknown with a reason, and each entry states whether its basis is Visible or Inferred.
- [ ] L2-058.4: Given a valid result, when its techniques section is inspected, then it contains one to twelve distinct techniques from the shared set, each with an explanation of where and how it applies at this location; techniques outside the set are rejected.
- [ ] L2-058.5: Given a valid result, when its group size is inspected, then it is either a minimum and maximum satisfying the shared bounds with a reason or Cannot assess with a reason.
- [ ] L2-058.6: Given a provider result missing a section, containing a rating or period outside the shared sets, a blank reason, a duplicated or missing shoot type, more than three overview strengths, an overlength text field, or a cited image identifier outside the analysed image set, when validated, then it is rejected as invalid structured output under L2-034, is not saved, and the location's current report is unchanged.
- [ ] L2-058.7: Given a rendered report, when inspected, then it contains no numeric score, star rating, or percentage.

## L2-059: Ground the scouting report in visible evidence
- [ ] L2-059.1: Given fixtures whose briefs contain no logistics, when reports are produced, then no entry asserts permits, fees, opening hours, ownership, parking, crowd levels, compass orientation, exact sun position, distances, or a place name or address as fact; a brief that supplies such detail can be echoed only as stated by the user.
- [ ] L2-059.2: Given time-of-day entries, when reviewed against the images, then each Recommended or Avoid entry names the visible basis such as shade, open sky, window direction, reflective surfaces, or artificial lighting; a period the images cannot support is Unknown with a reason rather than a guess.
- [ ] L2-059.3: Given a location with an address, coordinates, notes, tags, and a brief, when a scouting report executes under a controlled provider, then the recorded request contains only the current images, the brief, and allowlisted EXIF, and none of the address, coordinates, notes, tags, or any other location's data.
- [ ] L2-059.4: Given hostile instructions embedded in an image or the brief, when processed, then they are treated as content under L2-041 and cannot invoke tools, fetch URLs, expose other records, or change anything except the validated report fields.
- [ ] L2-059.5: Given the evaluation set, when each fixture is run three times with the configured production model before release, then all 24 outputs satisfy the L2-058 contract and contain no prohibited claim, and at least 22 satisfy every fixture-specific evidence and actionability check, assessed by a named reviewer with recorded reasons.
- [ ] L2-059.6: Given a failed release evaluation or a changed model or prompt affecting scouting behavior, when release validation runs, then the quality gate fails until the changed configuration passes the same evaluation; deterministic acceptance fixtures remain unchanged unless their expected behavior changes through specification review.

## L2-060: Request, regenerate, and outdate the scouting report
- [ ] L2-060.1: Given a location with at least one image and no report, when Request scouting report is selected, then a durable operation is acknowledged with a status location, the detail shows Queued or Running without waiting for the provider, browsing and editing remain available, and the nearby disclosure names the configured provider and states that the images, brief, and camera settings are sent.
- [ ] L2-060.2: Given a location with no images, when a report is requested through the UI or directly through the API, then the UI explains that an image is needed, the API returns a field error, and no provider call is made.
- [ ] L2-060.3: Given a current report, when an image is added or removed, then the report is labeled Outdated with the image count and timestamp it used, it stays readable, and Regenerate is offered; the report remains current for search until a replacement commits.
- [ ] L2-060.4: Given a validated regenerated report, when it commits, then the new report, generation timestamp, execution mode, model provenance, brief snapshot, and image-set revision become current together, and personal notes are unchanged; while regeneration is running or after it fails, the previous report remains visible.
- [ ] L2-060.5: Given repeated requests for an unchanged image set, brief, model, prompt version, and execution mode, when submitted without explicit Regenerate, then the completed report is reused without a provider call; explicit Regenerate creates one new job under L2-035, and a second request while that job is active returns the active job.
- [ ] L2-060.6: Given transient, invalid-output, unsupported-input, or credential failures, when the job runs, then L2-034 retry classes apply, a failed job shows a safe reason with Retry, and any earlier report stays readable.
- [ ] L2-060.7: Given missing live credentials, when a report is requested, then the API reports Integration not configured without queuing work and the location remains editable; the Azure configuration rules in L2-036 apply to scouting reports.
- [ ] L2-060.8: Given a report is displayed, when inspected, then it is labeled AI generated with its timestamp, model provenance, brief snapshot, and image-set revision, and it is rendered separately from personal notes.

## L2-061: Find locations for a shoot idea
- [ ] L2-061.1: Given indexed location fixtures and the query Golden hour engagement session with leading lines for two people, when Meaning search is submitted, then relevant locations are returned even without exact query words in their tags, and each result shows the cover or placeholder, name, locality when present, Recommended periods, group range, the rating for each selected shoot type, and opens the location detail.
- [ ] L2-061.2: Given locations rated Well suited, Workable, and Not recommended for Engagement and Events, when both shoot types are selected, then only locations rated Well suited or Workable for both remain.
- [ ] L2-061.3: Given locations with group ranges of 1-2, 2-8, and 20-100 and one with Cannot assess, when a people count of 6 is entered, then only the 2-8 location remains; clearing the count restores all four to eligibility.
- [ ] L2-061.4: Given locations with Golden hour Recommended, Blue hour Recommended, and neither, when Golden hour and Blue hour are selected, then locations with at least one selected period Recommended remain, without duplicates.
- [ ] L2-061.5: Given locations without a report, when Meaning or Keyword search runs, then they are findable through their manual fields and labeled No scouting report, and they are excluded whenever a shoot type, people count, or time-of-day filter is active.
- [ ] L2-061.6: Given a blank query in Meaning mode, when submitted, then the UI requests a query rather than generating an empty embedding; Keyword mode with filters and no query returns all locations satisfying the filters.
- [ ] L2-061.7: Given an unavailable embedding or search service, when Meaning search fails, then the UI explicitly reports its unavailability and offers switching to Keyword, never presenting keyword matches as semantic results.
- [ ] L2-061.8: Given equal similarity scores or a changed index generation during pagination, when later pages are fetched, then ties are stable and a cursor from a different generation returns the refresh-required conflict in L2-026.
- [ ] L2-061.9: Given a frozen release corpus of 30 locations with reports, 8 queries with at least three pre-labeled relevant locations each, and documented model and configuration, when live semantic evaluation runs, then at least three of the first five results are relevant for at least six queries and at least two are relevant for every query, assessed by a named reviewer with recorded reasons.

## L2-062: Search locations by keyword and keep results current
- [ ] L2-062.1: Given fixtures containing tokens in different eligible fields, when a multi-token keyword query is submitted, then a location is returned only if every token matches some eligible field, case-insensitively after NFC normalization; a location without a report matches only through its manual fields.
- [ ] L2-062.2: Given query text, selected shoot types, a people count, times of day, setting, and tags, when Keyword search runs, then every result satisfies every filter and the keyword rule; more than ten tags, an unknown shoot type, period, or setting, or a people count outside 1-500 returns the shared validation response.
- [ ] L2-062.3: Given matching locations, references, photographer bookmarks, photographs, and another user's locations, when Find a location runs in either mode, then only the owner's locations are returned; when Inspiration Search runs, then no location is returned.
- [ ] L2-062.4: Given a saved or edited location with healthy embedding and index dependencies, when persisted, then its vector becomes searchable within 60 seconds of the save, or within 60 seconds of a scouting report's success when one was requested with it, and the prior wait is labeled Processing report.
- [ ] L2-062.5: Given an edited eligible field, a removed tag, an image change, or a regenerated report, when acknowledged, then the stale vector is excluded immediately and the location shows Updating search until the replacement is current; keyword results reflect the edit immediately.
- [ ] L2-062.6: Given an indexing failure, when the job fails, then keyword search and editing remain available, the location shows Search indexing failed with Retry, and no stale vector is presented as current.
- [ ] L2-062.7: Given a deleted location and a stale search candidate, when any keyword or meaning request runs, then ownership and deletion checks remove it before results and counts are returned.
- [ ] L2-062.8: Given active query, mode, and filters, when the browser reloads or uses Back and Forward on the search URL, then the same state is restored; No results offers editing the query or clearing filters, and a service error has a separate retryable state.
