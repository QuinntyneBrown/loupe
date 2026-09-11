import { expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { SignInPage } from "./sign-in-page.js";
export class PhotographerPage {
  constructor(page) {
    this.page = page;
  }
  summaryPanel() {return this.page.getByRole('region',{name:'Summary',exact:true});}
  async expectSummaryState(text) {await expect(this.summaryPanel()).toContainText(text);}
  async retrySummary() {await this.summaryPanel().getByRole('button',{name:'Try again',exact:true}).click();}
  async expectSummarySource(url) {await expect(this.summaryPanel().getByRole('link',{name:'Source page',exact:true})).toHaveAttribute('href',url);}
  async editSuggestedSummary(value) {await this.summaryPanel().getByRole('textbox',{name:'Suggested summary',exact:true}).fill(value);}
  async expectSuggestedSummary(value) {await expect(this.summaryPanel().getByRole('textbox',{name:'Suggested summary',exact:true})).toHaveValue(value);}
  async summaryAction(name) {await this.summaryPanel().getByRole('button',{name,exact:true}).click();}
  async expectSummaryNotice(text) {await expect(this.summaryPanel().getByRole('status')).toContainText(text);}
  async expectSummaryError(text) {await expect(this.summaryPanel().getByRole('alert')).toContainText(text);}
  async editSummaryTag(name,value,category) {await this.summaryAction('Edit tag '+name);await this.summaryPanel().getByLabel('Suggested tag',{exact:true}).fill(value);await this.summaryPanel().getByRole('combobox',{name:'Suggested category',exact:true}).selectOption(category);}
  linkDialog() {
    return this.page.getByRole("dialog", {
      name: "Link references",
      exact: true,
    });
  }
  async submitLinks(count) {
    await this.linkDialog()
      .getByRole("button", { name: `Link ${count} references`, exact: true })
      .click();
  }
  async expectLinkFailure(text = "Couldn't link") {
    await expect(this.linkDialog().getByRole("alert")).toContainText(text);
  }
  async reviewLink() {
    await this.linkDialog()
      .getByRole("button", { name: "Review latest reference", exact: true })
      .click();
  }
  async openLinkPicker() {
    await this.page
      .getByRole("button", { name: "Link references", exact: true })
      .first()
      .click();
  }
  async chooseReference(title) {
    await this.linkDialog()
      .getByRole("checkbox", { name: new RegExp("^" + title + " ") })
      .check();
  }
  async expectAlreadyLinked(title) {
    const checkbox = this.linkDialog().getByRole("checkbox", {
      name: new RegExp("^" + title + " "),
    });
    await expect(checkbox).toBeChecked();
    await expect(checkbox).toBeDisabled();
  }
  async searchReferences(query) {
    await this.linkDialog()
      .getByRole("searchbox", { name: "Search your references", exact: true })
      .fill(query);
  }
  async expectSelected(count) {
    await expect(
      this.linkDialog().getByText(
        `${count} selected. Ticking a reference that has a photographer moves it here.`,
        { exact: true },
      ),
    ).toBeVisible();
  }
  async confirmLinks(count) {
    await this.linkDialog()
      .getByRole("button", { name: `Link ${count} references`, exact: true })
      .click();
    await expect(this.linkDialog()).not.toBeVisible();
  }
  async cancelLinkPicker() {
    await this.linkDialog()
      .getByRole("button", { name: "Cancel", exact: true })
      .click();
    await expect(this.linkDialog()).not.toBeVisible();
  }
  async unlink(title) {
    await this.page
      .getByRole("article")
      .filter({
        has: this.page.getByRole("link", {
          name: "Open " + title,
          exact: true,
        }),
      })
      .getByRole("button", {
        name: "Unlink from this photographer",
        exact: true,
      })
      .click();
  }
  async undoUnlink() {
    await this.page.getByRole("button", { name: "Undo", exact: true }).click();
  }
  async retryUnlink() {
    await this.page
      .getByRole("button", { name: "Retry unlink", exact: true })
      .click();
  }
  async expectUnlinkFailure() {
    await expect(this.page.getByRole("alert")).toContainText("Couldn't unlink");
  }
  async expectUndoFailure() {
    await expect(this.page.getByRole("alert")).toContainText(
      "Couldn't restore",
    );
  }
  async expectReferenceFocused(title) {
    await expect(
      this.page.getByRole("link", { name: "Open " + title, exact: true }),
    ).toBeFocused();
  }
  async expectUnlinked() {
    await expect(this.page.getByRole("status")).toContainText(
      "Reference unlinked",
    );
  }
  async expectUndoConflict() {
    await expect(this.page.getByRole("alert")).toContainText("changed");
  }
  async expectActionsFocused() {
    await expect(
      this.page.getByLabel("More actions", { exact: true }),
    ).toBeFocused();
  }
  async backToCollection() {
    await this.page
      .getByRole("main")
      .getByRole("link", { name: "Photographers", exact: true })
      .click();
  }
  async keepEditing() {
    await this.page
      .getByRole("dialog", { name: "Discard unsaved changes?", exact: true })
      .getByRole("button", { name: "Keep editing", exact: true })
      .click();
  }
  async discardNotes() {
    await this.page
      .getByRole("dialog", { name: "Discard unsaved changes?", exact: true })
      .getByRole("button", { name: "Discard", exact: true })
      .click();
    await expect(this.page).toHaveURL(/\/photographers$/);
  }
  tagsPanel() {
    return this.page.getByRole("region", { name: "Tags", exact: true });
  }
  async addTag(name) {
    const input = this.tagsPanel().getByRole("textbox", {
      name: "Add a tag",
      exact: true,
    });
    await input.fill(name);
    await input.press("Enter");
  }
  async removeTag(name) {
    await this.tagsPanel()
      .getByRole("button", { name: `Remove tag ${name}`, exact: true })
      .click();
  }
  async expectTag(name) {
    await expect(
      this.tagsPanel().getByRole("button", {
        name: `Remove tag ${name}`,
        exact: true,
      }),
    ).toBeVisible();
  }
  async expectNoTag(name) {
    await expect(
      this.tagsPanel().getByRole("button", {
        name: `Remove tag ${name}`,
        exact: true,
      }),
    ).toHaveCount(0);
  }
  async expectTagError(text) {
    await expect(this.tagsPanel().getByRole("alert")).toContainText(text);
  }
  async retryTag() {
    await this.tagsPanel()
      .getByRole("button", { name: "Retry", exact: true })
      .click();
  }
  async reviewTags() {
    await this.tagsPanel()
      .getByRole("button", { name: "Review latest tags", exact: true })
      .click();
  }
  notesPanel() {
    return this.page.getByRole("region", { name: "Your notes", exact: true });
  }
  async writeNotes(text) {
    await this.page
      .getByRole("textbox", { name: "Your notes", exact: true })
      .fill(text);
  }
  async saveNotes() {
    await this.notesPanel()
      .getByRole("button", { name: "Save", exact: true })
      .click();
  }
  async expectNotesSaved(text) {
    await expect(
      this.page.getByRole("textbox", { name: "Your notes", exact: true }),
    ).toHaveValue(text);
    await expect(
      this.notesPanel().getByText("Saved", { exact: true }),
    ).toBeVisible();
  }
  async expectNotesFailure() {
    await expect(this.notesPanel().getByRole("alert")).toBeVisible();
  }
  async reviewNotes() {
    await this.notesPanel()
      .getByRole("button", { name: "Review latest value", exact: true })
      .click();
  }
  async edit() {
    await this.page.getByLabel("More actions", { exact: true }).click();
    await this.page
      .getByRole("button", { name: "Edit details", exact: true })
      .click();
  }
  dialog() {
    return this.page.getByRole("dialog", { name: "Edit details", exact: true });
  }
  async deleteBookmark() {
    await this.page.getByLabel("More actions", { exact: true }).click();
    await this.page
      .getByRole("button", { name: "Delete photographer", exact: true })
      .click();
  }
  deletion() {
    return this.page.getByRole("dialog", { name: /^Delete / });
  }
  async expectDeletion(name, count) {
    await expect(this.deletion()).toContainText(name);
    await expect(this.deletion()).toContainText(`${count} linked references`);
    await expect(
      this.deletion().getByRole("button", { name: "Cancel", exact: true }),
    ).toBeFocused();
  }
  async cancelDeletion() {
    await this.deletion()
      .getByRole("button", { name: "Cancel", exact: true })
      .click();
    await expect(this.deletion()).not.toBeVisible();
  }
  async confirmDeletion() {
    await this.deletion()
      .getByRole("button", { name: "Delete", exact: true })
      .click();
  }
  async expectDeleted() {
    await expect(this.page).toHaveURL(/\/photographers$/);
    await expect(this.page.getByRole("status")).toContainText(
      "Photographer deleted. Their references are still in your library.",
    );
  }
  async reviewDeletion() {
    await this.deletion()
      .getByRole("button", { name: "Review latest photographer", exact: true })
      .click();
  }
  async changeDetails(name, url, description) {
    await this.dialog()
      .getByRole("textbox", { name: "Name", exact: true })
      .fill(name);
    await this.dialog()
      .getByRole("textbox", { name: "Website", exact: true })
      .fill(url);
    await this.dialog()
      .getByRole("textbox", { name: "Description", exact: true })
      .fill(description);
  }
  async saveDetails() {
    await this.dialog()
      .getByRole("button", { name: "Save", exact: true })
      .click();
  }
  async cancelDetails() {
    await this.dialog()
      .getByRole("button", { name: "Cancel", exact: true })
      .click();
  }
  async expectDetailsClosed() {
    await expect(this.dialog()).not.toBeVisible();
  }
  async expectSaveFailure() {
    await expect(this.dialog().getByRole("alert")).toContainText(
      "Couldn't save",
    );
  }
  async retrySave() {
    await this.dialog()
      .getByRole("button", { name: "Retry", exact: true })
      .click();
  }
  async reviewLatest() {
    await this.dialog()
      .getByRole("button", { name: "Review latest", exact: true })
      .click();
  }
  async open(id = "photographer-1") {
    await this.page.goto("/photographers/" + id);
    await new SignInPage(this.page).continue();
  }
  async expectProfile(name, summary, notes) {
    await expect(
      this.page.getByRole("heading", { name, exact: true, level: 1 }),
    ).toBeVisible();
    await expect(
      this.page.getByRole("region", { name: "Summary", exact: true }),
    ).toContainText(summary);
    await expect(
      this.page.getByRole("textbox", { name: "Your notes", exact: true }),
    ).toHaveValue(notes);
  }
  async expectEmpty() {
    await expect(
      this.page.getByRole("heading", {
        name: "No references linked yet.",
        exact: true,
      }),
    ).toBeVisible();
  }
  async expectReferences(count) {
    await expect(
      this.page
        .getByRole("region", { name: "References", exact: true })
        .getByRole("article"),
    ).toHaveCount(count);
  }
  async more() {
    await this.page
      .getByRole("button", { name: "Load more", exact: true })
      .click();
  }
  async expectError() {
    await expect(
      this.page.getByRole("heading", {
        name: "Couldn't load this photographer.",
        exact: true,
      }),
    ).toBeVisible();
  }
  async retry() {
    await this.page
      .getByRole("button", { name: "Try again", exact: true })
      .click();
  }
  async expectSource(url, current = true) {
    await expect(
      this.page.getByRole("link", {
        name: current ? "Captured page" : "Previous captured page",
        exact: true,
      }),
    ).toHaveAttribute("href", url);
  }
  async expectAccessible() {
    expect(
      (
        await new AxeBuilder({ page: this.page })
          .withTags(["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"])
          .analyze()
      ).violations,
    ).toEqual([]);
    expect(
      await this.page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
  }
}
