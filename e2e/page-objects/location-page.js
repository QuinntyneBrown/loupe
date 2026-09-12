import { expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { LocationLibrary } from "../fixtures/location-library.js";
import { SignInPage } from "./sign-in-page.js";

export class LocationPage {
  constructor(page) {
    this.page = page;
  }
  async configure(count) {
    this.library = new LocationLibrary(count);
    await this.library.attach(this.page);
  }
  async open(id) {
    await this.page.goto(`/locations/${id}`);
    await new SignInPage(this.page).continue();
  }
  async reload() {
    await this.page.reload();
    await new SignInPage(this.page).continue();
  }
  main() {
    return this.page.getByRole("main");
  }
  async expectTitle(name) {
    await expect(this.page).toHaveTitle("Location · Loupe");
    await expect(
      this.page.getByRole("heading", { level: 1, name, exact: true }),
    ).toBeVisible();
  }
  async expectAddress(lines) {
    const address = this.main().locator("address");
    await expect(address).toHaveText(lines.join("\n"), { useInnerText: true });
  }
  async expectAddressAbsent() {
    await expect(this.main().locator("address")).toHaveCount(0);
    await expect(
      this.main().getByText("Address not recorded", { exact: true }).first(),
    ).toBeVisible();
  }
  async expectDetail(term, value) {
    const definition = this.main()
      .locator("dl")
      .locator("dt", { hasText: new RegExp(`^${term}$`) })
      .locator("xpath=following-sibling::dd[1]");
    await expect(definition).toContainText(value);
  }
  async expectMeta(text) {
    await expect(
      this.main().getByText(text, { exact: true }).first(),
    ).toBeVisible();
  }
  async expectSetting(setting) {
    await expect(this.main().getByText(`Setting: ${setting}`)).toBeAttached();
  }
  async expectReportStatus(text) {
    await expect(
      this.main()
        .getByRole("region", { name: "Scouting report", exact: true })
        .getByText(text),
    ).toBeVisible();
  }
  gallery() {
    return this.main().getByRole("radiogroup", { name: "Images", exact: true });
  }
  async expectGallery(count) {
    await expect(this.gallery().getByRole("radio")).toHaveCount(count);
    await expect(
      this.gallery().getByRole("radio", { name: "Image 1, cover", exact: true }),
    ).toBeChecked();
    await expect(
      this.main().getByText("Image 1 · Cover", { exact: true }),
    ).toBeVisible();
  }
  async selectImage(index) {
    await this.gallery()
      .getByRole("radio", { name: new RegExp(`^Image ${index}(, cover)?$`) })
      .check();
  }
  async expectStageUncropped(name, index) {
    const image = this.main()
      .locator("figure")
      .getByRole("img", { name: `${name}, image ${index}`, exact: true });
    await expect(image).toBeVisible();
    const fit = await image.evaluate(
      (element) => getComputedStyle(element).objectFit,
    );
    expect(fit).toBe("contain");
    const box = await image.boundingBox();
    const natural = await image.evaluate((element) => ({
      width: element.naturalWidth,
      height: element.naturalHeight,
    }));
    expect(box.width).toBeGreaterThan(0);
    expect(natural.width).toBeGreaterThan(0);
  }
  async expectNoImages() {
    await expect(
      this.main().getByText("No images yet. Add up to ten; the scouting report works from them."),
    ).toBeVisible();
    await expect(
      this.main().getByRole("button", { name: "Add images", exact: true }),
    ).toBeVisible();
    await expect(this.gallery()).toHaveCount(0);
  }
  async expectCapacity(text) {
    await expect(this.main().getByText(text, { exact: true })).toBeVisible();
  }
  async expectUnavailable() {
    await expect(
      this.page.getByRole("alert").getByRole("heading", {
        name: "Couldn't load this location.",
        exact: true,
      }),
    ).toBeVisible();
    await expect(
      this.page.getByRole("link", { name: "Back to Locations", exact: true }),
    ).toHaveAttribute("href", "/locations");
  }
  async retry() {
    await this.page.getByRole("button", { name: "Try again", exact: true }).click();
  }
  async backToLocations() {
    await this.page.getByRole("link", { name: "Back to Locations", exact: true }).click();
    await expect(this.page).toHaveURL(/\/locations$/);
  }
  async openMenu() {
    await this.page.getByLabel("More actions", { exact: true }).click();
  }
  async expectMenuFocused() {
    await expect(this.page.getByLabel("More actions", { exact: true })).toBeFocused();
  }
  async openEdit() {
    await this.openMenu();
    await this.page.getByRole("button", { name: "Edit location", exact: true }).click();
    await expect(this.editDialog()).toBeVisible();
  }
  editDialog() {
    return this.page.getByRole("dialog", { name: "Edit location", exact: true });
  }
  field(label) {
    return this.editDialog().getByLabel(new RegExp("^" + label + "( optional.*)?$"));
  }
  async expectField(label, value) {
    await expect(this.field(label)).toHaveValue(value);
  }
  async expectNoField(label) {
    await expect(this.field(label)).toHaveCount(0);
  }
  async fill(values) {
    for (const [label, value] of Object.entries(values)) {
      if (label === "Setting") await this.field(label).selectOption(value);
      else await this.field(label).fill(value);
    }
  }
  async saveEdit() {
    await this.editDialog().getByRole("button", { name: "Save changes", exact: true }).click();
  }
  async expectEditClosed() {
    await expect(this.editDialog()).toHaveCount(0);
  }
  async expectEditConflict() {
    await expect(this.editDialog().getByRole("alert")).toContainText("This location changed");
    await expect(this.editDialog().getByRole("button", { name: "Save changes", exact: true })).toBeDisabled();
  }
  async reloadLatest() {
    await this.editDialog().getByRole("button", { name: "Reload latest", exact: true }).click();
  }
  async expectEditFieldError(label, message) {
    await expect(this.field(label)).toHaveAttribute("aria-invalid", "true");
    await expect(this.editDialog().getByText(message, { exact: true })).toBeVisible();
  }
  async escape() {
    await this.page.keyboard.press("Escape");
  }
  discardDialog() {
    return this.page.getByRole("dialog", { name: "Discard unsaved changes?", exact: true });
  }
  async expectDiscardChoice() {
    await expect(this.discardDialog()).toBeVisible();
  }
  async keepEditing() {
    await this.discardDialog().getByRole("button", { name: "Keep editing", exact: true }).click();
  }
  async discardChanges() {
    await this.discardDialog().getByRole("button", { name: "Discard", exact: true }).click();
  }
  textEditor(field) {
    return this.main().getByRole("region", {
      name: field === "scoutingBrief" ? "Scouting brief" : "Your notes",
      exact: true,
    });
  }
  async editText(field, value) {
    await this.textEditor(field).getByRole("textbox").fill(value);
  }
  async saveText(field) {
    await this.textEditor(field).getByRole("button", { name: "Save", exact: true }).click();
  }
  async expectText(field, value) {
    await expect(this.textEditor(field).getByRole("textbox")).toHaveValue(value);
  }
  async expectTextStatus(field, status) {
    await expect(this.textEditor(field).getByText(status, { exact: true })).toBeVisible();
  }
  async expectTextError(field, message) {
    await expect(this.textEditor(field).getByRole("alert")).toContainText(message);
  }
  tags() {
    return this.main().getByRole("region", { name: "Your tags", exact: true });
  }
  async addTag(name) {
    const input = this.tags().getByRole("textbox", { name: "Add a tag", exact: true });
    await input.fill(name);
    await input.press("Enter");
  }
  async removeTag(name) {
    await this.tags().getByRole("button", { name: `Remove tag ${name}`, exact: true }).click();
  }
  async saveTags() {
    await this.tags().getByRole("button", { name: "Save", exact: true }).click();
  }
  async expectTags(names) {
    await expect(this.tags().locator(".lp-tag")).toHaveText(names);
  }
  async expectTagStatus(status) {
    await expect(this.tags().getByText(status, { exact: true })).toBeVisible();
  }
  async openDelete() {
    await this.openMenu();
    await this.page.getByRole("button", { name: "Delete location", exact: true }).click();
    await expect(this.deleteDialog()).toBeVisible();
  }
  deleteDialog() {
    return this.page.getByRole("dialog", { name: /^Delete “.+”\?$/ });
  }
  async expectDeleteDialog(name, effects) {
    await expect(
      this.page.getByRole("dialog", { name: `Delete “${name}”?`, exact: true }),
    ).toBeVisible();
    await expect(this.deleteDialog()).toContainText(effects);
    await expect(this.deleteDialog().getByRole("button", { name: "Cancel", exact: true })).toBeFocused();
  }
  async cancelDelete() {
    await this.deleteDialog().getByRole("button", { name: "Cancel", exact: true }).click();
    await expect(this.deleteDialog()).toHaveCount(0);
  }
  async confirmDelete() {
    await this.deleteDialog().getByRole("button", { name: "Delete", exact: true }).click();
  }
  async expectDeleted() {
    await expect(this.page).toHaveURL(/\/locations$/);
    await expect(this.page.getByRole("status")).toContainText("Location deleted.");
  }
  async expectLayout(sideBySide) {
    const media = await this.main().locator(".lp-detail__media").boundingBox();
    const body = await this.main().locator(".lp-detail__body").boundingBox();
    if (sideBySide) {
      expect(body.x).toBeGreaterThanOrEqual(media.x + media.width - 1);
      expect(Math.abs(body.y - media.y)).toBeLessThan(media.height);
    } else {
      expect(Math.abs(body.x - media.x)).toBeLessThan(2);
      expect(body.y).toBeGreaterThanOrEqual(media.y + media.height - 1);
    }
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
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true);
  }
}
