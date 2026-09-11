import { test } from '@playwright/test';
import { SignInPage } from '../page-objects/sign-in-page.js';
import { MyWorkPage } from '../page-objects/my-work-page.js';
import { PhotographDetailPage } from '../page-objects/photograph-detail-page.js';
import { savedCritique } from '../fixtures/saved-critique.js';

const title = `DSC_20260910_${'PhotographyStudy'.repeat(10)}_original.jpg`;
const longText = 'Describe the quiet morning light and preserve the clear outline of the subject. '.repeat(12);

for (const width of [320, 375, 768, 991, 992, 1024, 1440, 1920]) {
  for (const orientation of ['landscape', 'portrait']) {
    test(`L2-053: long photograph content fits ${width}px with a ${orientation} image`, async ({ page }) => {
      // Given a saved photograph with a filename-style title, long context, and a critique.
      await page.setViewportSize({ width, height: 900 });
      await page.emulateMedia({ reducedMotion: 'reduce' });
      const work = new MyWorkPage(page);
      await work.configureCollection(1);
      const photograph = work.library.photos[0];
      photograph.title = title;
      photograph.exif.camera = 'VeryLongCameraManufacturerAndModel'.repeat(4);
      photograph.exif.lens = 'ProfessionalTelephotoLensWithImageStabilization'.repeat(3);
      photograph.brief.intent = longText;
      photograph.brief.requestedFeedback = longText;
      photograph.notes = longText;
      if (orientation === 'portrait') {
        photograph.width = 600;
        photograph.height = 800;
        photograph.imageUrl = 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="600" height="800"><rect width="600" height="800" fill="#e0e0e0"/><path d="M0 800 350 0h100L100 800" fill="#9a9a9a"/></svg>');
        photograph.previewUrl = photograph.imageUrl;
      }
      const critique = savedCritique();
      critique.content.strengths[0].explanation = longText;
      work.library.critiques.set(photograph.id, critique);
      const signIn = new SignInPage(page);
      await signIn.openPrivateDestination();
      await signIn.continue();

      // When the photograph is opened, all content fits without horizontal scrolling.
      await work.openPhotograph(title);
      const detail = new PhotographDetailPage(page);
      await detail.expectCritiqueText(longText);
      await detail.expectContentFitsViewport(title);

      // Then expanding the brief editor and editing notes also keeps controls in view.
      await detail.editBrief();
      await detail.fillBrief({ intent: longText, requestedFeedback: longText });
      await detail.editNotes(longText + 'Try a second viewpoint.');
      await detail.expectContentFitsViewport(title);
    });
  }
}
