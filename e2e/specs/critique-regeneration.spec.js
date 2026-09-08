import { test, expect } from "@playwright/test";
import { SignInPage } from "../page-objects/sign-in-page.js";
import { MyWorkPage } from "../page-objects/my-work-page.js";
import { PhotographDetailPage } from "../page-objects/photograph-detail-page.js";
import { savedCritique } from "../fixtures/saved-critique.js";
import { critiqueOperation } from "../fixtures/critique-operation.js";

async function open(page) {
  const work = new MyWorkPage(page);
  await work.configureCollection(1);
  const id = work.library.photos[0].id;
  const previous = savedCritique();
  work.library.critiques.set(id, previous);
  work.library.critiqueOperations.set(id, {
    ...critiqueOperation(id, "Succeeded"),
    id: previous.operationId,
    message: "Critique saved.",
  });
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.openPhotograph("Study 01");
  const detail = new PhotographDetailPage(page);
  await detail.expectCritique();
  return { work, detail, id, previous };
}

// Given a saved critique, when replacement is requested, then confirmation is
// explicit and the earlier result and notes remain until validated success.
test("L2-008.3: cancel named regeneration without admitting work or discarding notes", async ({
  page,
}) => {
  const { work, detail } = await open(page);
  await detail.editNotes("A private unfinished thought");
  await detail.regenerateCritique();
  await detail.expectRegenerationConfirmation();
  expect(work.library.critiqueRequests).toHaveLength(0);
  await detail.cancelRegeneration();
  await detail.expectRegenerationCancelled();
  await detail.expectNotes("A private unfinished thought");
  await detail.expectCritique();
});

test("L2-008.3/4: confirmed regeneration waits for acknowledgment and replaces only on success", async ({
  page,
}) => {
  await page.clock.install();
  const { work, detail, id, previous } = await open(page);
  await detail.editNotes("Keep this draft through replacement");
  await detail.regenerateCritique();
  await detail.expectRegenerationConfirmation();
  work.library.pause("requestCritique");
  await detail.confirmRegeneration();
  await detail.expectRegenerationPending();
  await detail.expectUnloadProtection(true);
  expect(work.library.critiques.get(id)).toEqual(previous);
  work.library.release("requestCritique");
  await detail.expectCritiqueStatus("Queued", "Waiting to start.");
  await detail.expectCritiqueStatusFocus();
  await detail.expectRegenerateDisabled();
  await detail.expectCritique();
  expect(work.library.critiqueRequests[0].regenerate).toBe(true);
  const operation = work.library.critiqueOperations.get(id);
  operation.status = "Succeeded";
  operation.message = "Critique saved.";
  const replacement = savedCritique();
  replacement.operationId = operation.id;
  replacement.content.strengths[0].explanation =
    "The new diagonal makes the subject clearer.";
  work.library.critiques.set(id, replacement);
  await page.clock.fastForward(5000);
  await detail.expectCritiqueText(
    "The new diagonal makes the subject clearer.",
  );
  await detail.expectNotes("Keep this draft through replacement");
});

test("L2-030/L2-008.3: an uncertain regeneration retry keeps one key and the earlier result", async ({
  page,
}) => {
  const { work, detail, id, previous } = await open(page);
  await detail.regenerateCritique();
  work.library.lostCritiqueResponses = 1;
  await detail.confirmRegeneration();
  await detail.expectRegenerationFailure();
  expect(work.library.critiques.get(id)).toEqual(previous);
  await detail.retryRegeneration();
  await detail.expectCritiqueStatus("Queued", "Waiting to start.");
  expect(work.library.critiqueRequests[1]).toEqual(
    work.library.critiqueRequests[0],
  );
  expect(work.library.critiqueReceipts.size).toBe(1);
  await detail.expectCritique();
});

test("L2-030/L2-008.3: a stale regeneration requires reviewing its saved brief", async ({
  page,
}) => {
  const { work, detail } = await open(page);
  await detail.regenerateCritique();
  work.library.photos[0].revision = 2;
  work.library.photos[0].brief.intent = "Preserve deliberate motion";
  await detail.confirmRegeneration();
  await detail.expectRegenerationFailure(
    "This photograph changed. Review its latest saved brief before requesting.",
  );
  await detail.reviewRegeneration();
  await detail.expectRegenerationBrief("Preserve deliberate motion");
  await detail.confirmReviewedRegeneration();
  await detail.expectCritiqueStatus("Queued", "Waiting to start.");
  expect(work.library.critiqueRequests[1]).toMatchObject({
    revision: 2,
    regenerate: true,
  });
  await detail.expectCritique();
});

test("L2-002/L2-008.3: unsaved brief edits block replacement until saved", async ({
  page,
}) => {
  const { detail } = await open(page);
  await detail.editBrief();
  await detail.fillBrief({ intent: "Retain the deep shadows" });
  await detail.expectRegenerateDisabled();
  await detail.saveBrief();
  await detail.regenerateCritique();
  await detail.expectRegenerationConfirmation();
});
