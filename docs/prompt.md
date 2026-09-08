# Build Loupe: a photography development and inspiration platform

Build a web platform called **Loupe** that helps me develop my professional photography skills through AI critique, deliberate practice, and a curated visual reference library.

The core workflow is: **collect inspiration → understand what makes it effective → practice → upload my work → receive feedback → improve**.

## Product goals

- Give me specific, actionable feedback on my photographs, especially opportunities for technical improvement.
- Let me collect and organize inspiring photographs and photographers’ websites in a Pinterest-style library.
- Make that library searchable using both conventional filters and natural-language semantic search.
- Connect the inspiration I save with the skills I want to develop.

## Core workflows

### 1. Upload my work and get AI critique

Let me upload a photograph and optionally describe my intent, genre, experience level, and the feedback I want. Keep my own work distinct from saved reference images.

Provide a structured critique covering:

- What works well and why.
- Technical observations: exposure, focus, depth of field, motion, lighting, color, and processing.
- Composition and visual storytelling: framing, subject separation, balance, visual hierarchy, and mood.
- The three highest-priority improvements, with concrete suggestions for the next shoot or edit.
- A short practice exercise based on the feedback.

Ground feedback in visible evidence. Distinguish technical problems from stylistic choices, respect my stated intent, and acknowledge uncertainty. Use EXIF data when available; do not invent camera settings or shooting conditions. Favor useful explanations over generic praise or unexplained scores.

Save photographs and their critiques so I can revisit them, add notes, and compare later attempts.

### 2. Build a visual inspiration library

Let me save reference photographs by uploading an image or submitting a source URL. For each reference, retain the image or available preview, source link, photographer attribution when known, personal notes, tags, and board memberships.

Use AI to suggest an editable description and tags for subjects, genre, composition, lighting, palette, mood, and visible techniques. Clearly separate AI suggestions from my own notes.

Support creating, renaming, and deleting boards, and placing the same reference in multiple boards without duplicating the underlying item. Provide an image-first grid and a detail view with metadata, notes, and related references.

Preserve source attribution and respect site access restrictions. When a URL cannot be imported, offer a useful fallback: save the link and add notes or an image manually.

### 3. Collect photographers and their websites

Let me bookmark photographers’ portfolios with a name, URL, description, personal notes, and editable tags. Generate suggested summaries only from content the platform can actually access, and let me link saved references to a photographer.

Make these bookmarks searchable alongside reference photographs, with filters to distinguish the two types. Start with individual bookmarks and accessible page metadata; full-site crawling is outside the first version.

### 4. Search by meaning

Provide keyword search, tag and board filters, and vector-based semantic search over the inspiration library. Index image content or generated visual descriptions, together with relevant text metadata, so searches can express visual ideas rather than requiring exact tags.

Example searches:

- “Moody portraits with soft window light.”
- “Minimalist architecture with strong shadows.”
- “Photographers who use cinematic color and negative space.”

Return visually scannable results that open the saved item and its original source. Support combining a natural-language query with filters.

## Experience and design

Create a calm, polished, image-first interface suited to serious creative work. Use restrained typography, generous spacing, and neutral surfaces that let photographs stand out. Prioritize fast browsing, clear navigation, accessible controls, and responsive layouts.

The main areas should be **My Work**, **Inspiration**, **Photographers**, and **Search**. Make saving an image, requesting a critique, and finding a reference straightforward. Include thoughtful empty states, upload and processing progress, recoverable errors, and clear AI processing status.

## First-version scope

Deliver a working end-to-end application with persistent storage, image uploads, saved critiques, boards, photographer bookmarks, editable AI metadata, and semantic search. Keep personal uploads and critiques private by default. Protect provider credentials on the server, validate uploads, and provide deletion controls that also remove associated files and search records.

Choose a practical, maintainable stack that fits any existing repository conventions. Explain major architectural choices briefly. Process slow AI tasks asynchronously, expose failures and retries, and avoid repeating completed AI work unnecessarily.

If AI services require credentials that are unavailable, provide a clearly labeled demo mode and document how to enable the real integrations. Never present mock output as a real analysis or imply an integration works before it is connected.

Defer social feeds, public profiles, payments, complex team features, and full-site crawling. Progress tracking and personalized learning plans can follow once the core workflows work well.

## Completion criteria

A user should be able to:

1. Upload their own photograph, request a critique, and revisit the saved feedback.
2. Save a reference image, review and edit suggested tags, and add it to multiple boards.
3. Bookmark a photographer’s website with searchable metadata and notes.
4. Find relevant saved inspiration with natural-language queries and filters.
5. Edit or delete saved content and retain their library across sessions.

Build the platform, rather than stopping at a mockup or plan. Make reasonable implementation decisions, document assumptions, and verify the main workflows. At delivery, include setup instructions, required configuration, what was tested, and any remaining limitations.
