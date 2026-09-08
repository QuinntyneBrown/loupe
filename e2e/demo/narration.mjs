// Shared caption/title-card overlay for demo-video recordings. Injected purely
// via the page's own DOM at runtime — no application source is touched, and the
// overlay never intercepts pointer events so it cannot change real behavior.
const STYLE_ID = '__demo_narration_style__';
const CAPTION_ID = '__demo_narration_caption__';
const CARD_ID = '__demo_narration_card__';

async function ensureOverlay(page) {
  await page.evaluate(({ styleId, captionId, cardId }) => {
    if (document.getElementById(styleId)) return;
    const style = document.createElement('style');
    style.id = styleId;
    style.textContent = `
      #${captionId} {
        position: fixed; left: 0; right: 0; bottom: 0; z-index: 2147483647;
        padding: 14px 24px; font: 600 17px/1.4 -apple-system, Segoe UI, sans-serif;
        background: linear-gradient(0deg, rgba(10,14,18,.92), rgba(10,14,18,.75));
        color: #fff; pointer-events: none; text-shadow: 0 1px 2px rgba(0,0,0,.6);
      }
      #${captionId} .sub { display: block; font-weight: 400; font-size: 14px; opacity: .85; margin-top: 3px; }
      #${cardId} {
        position: fixed; inset: 0; z-index: 2147483647; display: flex; align-items: center; justify-content: center;
        flex-direction: column; gap: 10px; background: #0b0f14; color: #fff; text-align: center;
        font: 600 34px/1.3 -apple-system, Segoe UI, sans-serif; pointer-events: none; padding: 40px;
      }
      #${cardId} .sub { font-weight: 400; font-size: 18px; color: #9aa7b4; max-width: 720px; }
    `;
    document.head.appendChild(style);
  }, { styleId: STYLE_ID, captionId: CAPTION_ID, cardId: CARD_ID });
}

export async function showTitleCard(page, title, subtitle = '') {
  await ensureOverlay(page);
  await page.evaluate(({ cardId, title, subtitle }) => {
    document.getElementById(cardId)?.remove();
    const card = document.createElement('div');
    card.id = cardId;
    card.innerHTML = `<div>${title}</div>${subtitle ? `<div class="sub">${subtitle}</div>` : ''}`;
    document.body.appendChild(card);
  }, { cardId: CARD_ID, title, subtitle });
}

export async function hideTitleCard(page) {
  await page.evaluate((cardId) => document.getElementById(cardId)?.remove(), CARD_ID);
}

export async function showCaption(page, text, subtitle = '') {
  await ensureOverlay(page);
  await page.evaluate(({ captionId, text, subtitle }) => {
    let el = document.getElementById(captionId);
    if (!el) { el = document.createElement('div'); el.id = captionId; document.body.appendChild(el); }
    el.innerHTML = `${text}${subtitle ? `<span class="sub">${subtitle}</span>` : ''}`;
  }, { captionId: CAPTION_ID, text, subtitle });
}

export async function hideCaption(page) {
  await page.evaluate((captionId) => document.getElementById(captionId)?.remove(), CAPTION_ID);
}

export async function reapplyOverlayAfterNavigation(page) {
  await ensureOverlay(page);
}
