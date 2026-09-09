import { test, expect } from "@playwright/test";
import { MyWorkPage } from "../page-objects/my-work-page.js";
import { SignInPage } from "../page-objects/sign-in-page.js";
import { PhotographUploadPage } from "../page-objects/photograph-upload-page.js";
import { PhotographDetailPage } from "../page-objects/photograph-detail-page.js";

// Acceptance: L2-001.8, L2-043.3/.7. Late responses cannot overwrite newer UI or operate on a departed page.
test("a late pagination response cannot append stale cards after upload refresh", async ({
  page,
}) => {
  const work = new MyWorkPage(page);
  await work.observeListCompletion();
  await work.configureCollection(25);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.expectPhotographs(24);
  work.library.pause("list");
  work.library.gates.list.snapshot = structuredClone(work.library.photos);
  const oldRequest = work.library.gates.list;
  await work.loadMore();
  await expect
    .poll(() => work.library.calls.filter((call) => call === "list").length)
    .toBe(2);
  delete work.library.gates.list;
  const upload = new PhotographUploadPage(page);
  await upload.open();
  await upload.chooseImage();
  await upload.save();
  await upload.expectCompleted();
  await work.expectListSettled(2);
  oldRequest.release();
  await work.expectListSettled(3);
  await work.expectPhotographs(24);
  await work.expectPhotographCard("Morning");
  await work.loadMore();
  await work.expectPhotographs(26);
  work.expectNoRuntimeErrors();
});

test("following View while collection refresh is pending leaves no late focus error", async ({
  page,
}) => {
  const work = new MyWorkPage(page);
  await work.observeListCompletion();
  await work.configureCollection(0);
  const signIn = new SignInPage(page);
  await signIn.openPrivateDestination();
  await signIn.continue();
  await work.expectEmpty();
  work.library.pause("list");
  const upload = new PhotographUploadPage(page);
  await upload.open();
  await upload.chooseImage();
  await upload.save();
  await upload.viewCompletedPhotograph();
  await new PhotographDetailPage(page).expectImage("Morning");
  work.library.release("list");
  await work.expectListSettled(2);
  work.expectNoRuntimeErrors();
});
