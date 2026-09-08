import { expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

export class PhotographUploadPage {
  constructor(page) { this.page = page; }
  async expectProgressState() {
    await expect(this.modal().getByRole('heading', { name: 'Uploading…', exact: true })).toBeVisible();
    await expect(this.modal().getByLabel('What were you trying to do?', { exact: true })).toBeHidden();
    await expect(this.modal().getByRole('button', { name: 'Cancel upload', exact: true })).toBeVisible();
  }
  async cancelTransfer() { await this.modal().getByRole('button', { name: 'Cancel upload', exact: true }).click(); }
  async expectCanceledNotice() {
    await expect(this.modal()).toHaveCount(0);
    await expect(this.page.getByRole('status')).toContainText('Transfer stopped. The photograph may already have been saved. Check My Work before uploading again.');
  }
  async expectMockForm() {
    await expect(this.modal().getByText('Drop a photograph here, or browse', { exact: true })).toBeVisible();
    await expect(this.modal().getByRole('button', { name: 'Upload only', exact: true })).toBeDisabled();
    await expect(this.modal().getByRole('button', { name: 'Upload and request critique', exact: true })).toBeDisabled();
    await expect(this.modal().getByLabel('Title (optional)', { exact: true })).toBeHidden();
    await expect(this.modal().getByLabel('Genre', { exact: true })).toHaveValue('');
    await expect(this.modal().getByLabel('Your experience', { exact: true })).toHaveValue('');
    for (const name of ['Technical', 'Composition', 'Colour and processing', 'Storytelling'])
      await expect(this.modal().getByRole('button', { name, exact: true })).toHaveAttribute('aria-pressed', 'false');
  }
  async toggleFeedback(name) { await this.modal().getByRole('button', { name, exact: true }).click(); }
  async saveOnly() { await this.modal().getByRole('button', { name: 'Upload only', exact: true }).click(); }
  async dropImages(count) {
    const transfer = await this.page.evaluateHandle(count => {
      const data = new DataTransfer();
      for (let index = 0; index < count; index++) data.items.add(new File(['image bytes'], `Dropped${index}.png`, { type: 'image/png' }));
      return data;
    }, count);
    await this.modal().locator('.lp-dropzone').dispatchEvent('drop', { dataTransfer: transfer });
    await transfer.dispose();
  }
  async expandOptionalDetails() {
    const details = this.modal().locator('details').filter({ has: this.page.getByText('Optional details', { exact: true }) });
    if (!(await details.getAttribute('open') === '')) await details.locator('summary').click();
  }
  modal() { return this.page.getByRole('dialog', { name: 'Upload photograph', exact: true }); }
  trigger(entry = 'header') { return this.page.getByRole('button', { name: 'Upload photograph', exact: true }).nth(entry === 'empty' ? 1 : 0); }
  async openFrom(entry) { await this.trigger(entry).click(); }
  async expectModal() {
    await expect(this.modal()).toBeVisible();
    await expect(this.page).toHaveURL(/\/my-work$/);
    expect(await this.modal().evaluate(dialog => dialog.matches(':modal'))).toBe(true);
    await expect(this.page.locator('main h1')).toHaveText('My Work');
  }
  async expectFocusContained() {
    for (let index = 0; index < 18; index++) {
      await this.page.keyboard.press('Tab');
      expect(await this.modal().evaluate(dialog => dialog.contains(document.activeElement))).toBe(true);
    }
  }
  async closeDialog() { await this.modal().getByRole('button', { name: 'Close', exact: true }).click(); }
  async expectClosedWithFocus(entry) {
    await expect(this.modal()).toHaveCount(0);
    await expect(this.trigger(entry)).toBeFocused();
  }
  async requestCritiqueAfterSaving() { this.critiqueAfterSaving = true; }
  async expectSavedBeforeCritique() { await expect(this.page.getByRole('heading', { name: 'Photograph saved', exact: true })).toBeVisible(); }
  async expectAccessibleSavedPhotograph() {
    await expect(this.page.getByRole('heading', { name: 'Photograph saved', exact: true })).toBeFocused();
    expect(await this.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const audit = await new AxeBuilder({ page: this.page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
    expect(audit.violations).toEqual([]);
  }
  async expectCritiquePending() { await expect(this.page.getByRole('status')).toHaveText('Requesting critique…'); }
  async expectCritiqueNotQueued() { await expect(this.page.getByText('Critique was not queued. Your photograph is saved.', { exact: true })).toBeVisible(); }
  async expectUncertainCritique() { await expect(this.page.getByRole('alert')).toHaveText('Critique admission was not confirmed. Retry to check the same request.'); }
  async retryCritique() { await this.page.getByRole('button', { name: 'Retry critique request', exact: true }).click(); }
  async viewSavedPhotograph() { await this.page.getByRole('link', { name: 'View saved photograph', exact: true }).click(); }
  async open() { this.critiqueAfterSaving = false; await this.openFrom('header'); }
  async expectOpen() { await expect(this.modal()).toBeVisible(); }
  async chooseImage(name = 'Morning.png') {
    await this.page.getByLabel('Photograph', { exact: true }).setInputFiles({ name, mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=', 'base64') });
  }
  async fill(values) {
    const labels = { title: 'Title (optional)', intent: 'What were you trying to do?', genre: 'Custom genre (optional)', requestedFeedback: 'Custom feedback (optional)' };
    for (const [field, value] of Object.entries(values)) {
      if (field === 'experience') await this.page.getByLabel('Your experience', { exact: true }).selectOption(value);
      else {
        if (field === 'genre') await this.page.getByLabel('Genre', { exact: true }).selectOption('Other');
        if (field === 'title' || field === 'requestedFeedback') await this.expandOptionalDetails();
        await this.page.getByLabel(labels[field], { exact: true }).fill(value);
      }
    }
  }
  async save() { await this.page.getByRole('button', { name: this.critiqueAfterSaving ? 'Upload and request critique' : 'Upload only', exact: true }).click(); }
  async chooseFile({ name = 'Morning.png', mimeType = 'image/png', size = 100 } = {}) {
    // Large in-memory fixtures cross Playwright's protocol before application validation starts.
    await this.page.getByLabel('Photograph', { exact: true }).setInputFiles({ name, mimeType, buffer: Buffer.alloc(size) }, { timeout: 15000 });
  }
  async expectFieldLimit(field, maximum) {
    const labels = { title: 'Title (optional)', intent: 'What were you trying to do?', genre: 'Custom genre (optional)', requestedFeedback: 'Custom feedback (optional)' };
    await expect(this.page.getByLabel(labels[field], { exact: true })).toHaveAttribute('aria-invalid', 'true');
    await expect(this.page.getByRole('alert')).toHaveText(`Use ${maximum.toLocaleString('en-US')} characters or fewer.`);
    await this.expectSaveDisabled();
  }
  async expectFileError(message) {
    await expect(this.page.getByLabel('Photograph', { exact: true })).toHaveAttribute('aria-invalid', 'true');
    await expect(this.page.getByRole('alert')).toHaveText(message);
    await this.expectSaveDisabled();
  }
  async expectSaveDisabled() {
    const buttons = this.modal().getByRole('button', { name: /^(Upload only|Upload and request critique|Retry upload)$/ });
    for (const button of await buttons.all()) await expect(button).toBeDisabled();
    if (await buttons.count() === 0) await expect(this.modal().getByRole('button', { name: /^Cancel( upload)?$/ })).toBeVisible();
  }
  async expectFailure(message = 'The upload was not confirmed. Your fields are still here. Retry to check whether it was saved.') {
    await expect(this.page.getByRole('alert')).toHaveText(message);
    await expect(this.page).toHaveURL(/\/my-work$/);
  }
  async expectDraft(values) {
    const labels = { title: 'Title (optional)', intent: 'What were you trying to do?', genre: 'Custom genre (optional)', experience: 'Your experience', requestedFeedback: 'Custom feedback (optional)' };
    for (const [field, value] of Object.entries(values)) {
      const control = field === 'genre' && !(await this.page.getByLabel(labels[field], { exact: true }).count())
        ? this.page.getByLabel('Genre', { exact: true }) : this.page.getByLabel(labels[field], { exact: true });
      await expect(control).toHaveValue(value);
    }
  }
  async expectFileRetained() { await expect(this.page.getByText('The selected file is still available.', { exact: true })).toBeVisible(); }
  async expectReselectionRequired() {
    await expect(this.page.getByText('Select the photograph again to retry.', { exact: true })).toBeVisible();
    await expect(this.page.getByRole('button', { name: 'Retry upload', exact: true })).toBeDisabled();
  }
  async retry() { await this.page.getByRole('button', { name: 'Retry upload', exact: true }).click(); }
  async expectProgress(transferred, total) {
    const progress = this.page.getByRole('progressbar', { name: 'Upload progress', exact: true });
    await expect(progress).toHaveJSProperty('value', transferred);
    await expect(progress).toHaveJSProperty('max', total);
    await expect(this.page.getByRole('status')).toHaveText(`${transferred.toLocaleString('en-US')} of ${total.toLocaleString('en-US')} bytes transferred.`);
  }
  async expectIndeterminate(transferred = null) {
    await expect(this.page.getByRole('progressbar', { name: 'Upload progress', exact: true })).toHaveJSProperty('position', -1);
    await expect(this.page.getByRole('status')).toHaveText(transferred === null ? 'Saving photograph…' : `${transferred.toLocaleString('en-US')} bytes transferred. Total size unavailable.`);
  }
  async expectNoProgress() { await expect(this.page.getByRole('progressbar', { name: 'Upload progress', exact: true })).toHaveCount(0); }
  async returnToLibrary() {
    const cancel = this.modal().getByRole('button', { name: 'Cancel upload', exact: true });
    if (await cancel.isVisible()) await cancel.click(); else await this.closeDialog();
  }
  discardDialog() { return this.page.getByRole('dialog', { name: 'Discard unsaved changes?', exact: true }); }
  async expectDiscardChoice() {
    await expect(this.discardDialog()).toBeVisible();
    await expect(this.discardDialog().getByRole('button', { name: 'Keep editing', exact: true })).toBeFocused();
  }
  async expectPendingWarning() { await expect(this.discardDialog()).toContainText('This upload may finish after you leave. Check My Work before uploading it again.'); }
  async keepEditing() { await this.discardDialog().getByRole('button', { name: 'Keep editing', exact: true }).click(); }
  async discardChanges() { await this.discardDialog().getByRole('button', { name: 'Discard', exact: true }).click(); }
  async escapeDiscard() { await this.page.keyboard.press('Escape'); }
  async expectNoDiscardChoice() { await expect(this.discardDialog()).toHaveCount(0); }
  async expectUnloadProtection(expected) {
    expect(await this.page.evaluate(() => !window.dispatchEvent(new Event('beforeunload', { cancelable: true })))).toBe(expected);
  }
  async expectEmptyDraft() {
    await this.expectDraft({ title: '', intent: '', genre: '', experience: '', requestedFeedback: '' });
    await expect(this.page.getByLabel('Photograph', { exact: true })).toHaveValue('');
    await this.expectSaveDisabled();
  }
  async expectAccessibleUpload() {
    expect(await this.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const audit = await new AxeBuilder({ page: this.page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
    expect(audit.violations).toEqual([]);
    for (const label of ['Photograph', 'Title (optional)', 'What were you trying to do?', 'Custom genre (optional)', 'Your experience', 'Custom feedback (optional)']) {
      const control = this.page.getByLabel(label, { exact: true });
      const box = await control.boundingBox();
      expect(box.width).toBeGreaterThanOrEqual(24);
      expect(box.height).toBeGreaterThanOrEqual(24);
    }
  }
  async expectDiscardKeyboardAndLayout() {
    await this.expectDiscardChoice();
    await this.page.keyboard.press('Tab');
    await expect(this.discardDialog().getByRole('button', { name: 'Discard', exact: true })).toBeFocused();
    await this.page.keyboard.press('Tab');
    await expect(this.discardDialog().getByRole('button', { name: 'Close', exact: true })).toBeFocused();
    await this.page.keyboard.press('Shift+Tab');
    await expect(this.discardDialog().getByRole('button', { name: 'Discard', exact: true })).toBeFocused();
    const box = await this.discardDialog().boundingBox();
    const viewport = this.page.viewportSize();
    expect(box.x).toBeGreaterThanOrEqual(0);
    expect(box.y).toBeGreaterThanOrEqual(0);
    expect(box.x + box.width).toBeLessThanOrEqual(viewport.width);
    expect(box.y + box.height).toBeLessThanOrEqual(viewport.height);
    const audit = await new AxeBuilder({ page: this.page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
    expect(audit.violations).toEqual([]);
  }
  async expectSaving() {
    await expect(this.page.getByRole('status')).toContainText('Saving photograph');
    await this.expectSaveDisabled();
    await expect(this.page).toHaveURL(/\/my-work$/);
  }
}
