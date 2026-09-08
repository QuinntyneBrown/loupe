# Save a photograph and its brief

## Overview

Loupe separates personal photographs from inspiration references. A **photograph** — private image record submitted for practice and critique — belongs to My Work. A **brief** — optional statement of intent, genre, experience, and requested feedback — supplies context for a later critique. Saving a photograph does not itself request analysis.

This is a proposed design for the defined requirements. Existing files are illustrative HTML mockups; the repository contains no corresponding production implementation.

## Description

`MyWorkPage` lives in `frontend/projects/loupe` and owns routing and dialogs. `PhotographUploadPanel` lives in `frontend/projects/domain` and consumes `IPhotographService` through `PHOTOGRAPH_SERVICE`. The contract and token share `photograph.service.contract.ts` in `frontend/projects/api`. `PhotographService` is the separate production HTTP adapter. Composition substitutes a mock implementation under Playwright. State uses signals, and HTTP and observable conversion stay inside the adapter. Presentational controls in `components` consume inputs and emit outputs. Component class, template, and styles occupy separate files.

`PhotographsController` lives in `backend/src/Loupe.Api/Controllers` under `Loupe.Api.Controllers`. It binds requests, dispatches MediatR **12.5.0**, and returns results. Requests, handlers, and validators live in the matching feature namespace in `Loupe.Application`. `Photograph` lives in `Loupe.Domain`; the domain references no other project. `ILibraryStore` and capability ports belong to Application. Infrastructure implements persistence and external adapters. Each type occupies its own named file.

`PhotographUploadPanel` owns form and transfer signals. `IPhotographService.upload()` reports transferred and total bytes when available; otherwise the panel shows indeterminate progress. A failed attempt retains non-file fields and indicates whether file reselection is necessary.

`UploadPhotographCommandHandler` validates normalized fields before accepting the image. `IImageIngestor` stages bytes, validates the declared format against decoded content, applies EXIF orientation, and produces a browser-compatible preview. Accepted input contains exactly one still JPEG, PNG, HEIC, or WebP image. Limits are 25,000,000 bytes, 100,000,000 decoded pixels, and 20,000 pixels on either edge, inclusive. Multi-image containers, animation, RAW, TIFF, SVG, and GIF receive the shared failure response.

The handler writes immutable image objects before committing the photograph and file references in one database transaction. Staged objects remain inaccessible without an owned record. Failed persistence deletes staged objects where possible and records abandoned work for cleanup. Cleanup removes abandoned bytes within 24 hours under healthy storage, with overdue alerts and durable retries under `L2-032`. A saved record never points to an incomplete preview.

An absent title defaults to the filename without extension, capped at 200 Unicode scalar values; an empty result becomes Untitled photograph. `CritiqueBrief` contains nullable intent, genre, experience, and requested feedback. `ExperienceLevel` permits Beginner, Intermediate, Advanced, and Professional. Clearing optional values stores absence. `UpdatePhotographBriefCommandHandler` performs an owner-scoped conditional update against `Version`. Already queued critiques retain immutable brief snapshots.

Upload-and-critique performs job admission after saving the photograph, through [request-critique](../../critique/request-critique/README.md). An admission failure therefore preserves the upload and offers Request critique. The client distinguishes the saved photograph from the unqueued critique.

The following contract surface names the proposed operations. Background commands run in the worker after a separate admission request; local evaluation commands run in the evaluation project.

| Application request | Entry point | Input |
| --- | --- | --- |
| `UploadPhotographCommand` | `POST /api/photographs` | `image, title?, brief?` |
| `UpdatePhotographBriefCommand` | `PUT /api/photographs/{id}/brief` | `intent?, genre?, experience?, requestedFeedback?, version` |

All operations inherit [shared contracts and open dependencies](../../README.md). Owner identity comes from authenticated server context, never from trusted client input. Mutations validate complete input before side effects and use conditional writes. API errors use the shared status mapping in [L2.md](../../../specs/L2.md#shared-acceptance-definitions). Visual implementation uses mirrored `--lp-` tokens and the specification's viewport matrix.

Implementation proceeds one behavior at a time using the linked Given-When-Then criteria. Each acceptance test first fails for its expected missing behavior. API integration tests cover persistence and failures. Playwright tests use one page object per screen and injected service mocks. Real-provider release checks supplement deterministic fixtures where applicable. Each acceptance file identifies L2 coverage and each test names its criterion. Regression checks pass before the next behavior starts; no architecture tests are introduced.

## Requirements

The table preserves source wording verbatim, including its use of “must”. Quoted requirements are the sole exception to the design prose's shall/should/may convention. Each linked source section also supplies the acceptance criteria; deployment choices and unexecuted release evidence remain explicitly identified.

| L2 ID | Refines (L1) | Requirement |
| --- | --- | --- |
| `L2-001` | `L1-001` | Users must be able to save a valid image to My Work with an optional title and brief, independently of requesting a critique. |
| `L2-002` | `L1-001` | A photograph's brief must support optional intent, genre, experience level, and requested feedback. Experience is absent or one of Beginner, Intermediate, Advanced, or Professional. |

Acceptance criteria: [L2-001](../../../specs/L2.md#l2-001-upload-and-save-personal-photographs), [L2-002](../../../specs/L2.md#l2-002-record-and-edit-the-critique-brief).

## Diagrams

The context view places this capability within the owner's private Loupe library. External services appear only where the slice uses provider or source content.

![Context: Save a photograph and its brief](diagrams/c4-context.png)

The container view separates Angular interaction, the .NET API, and authoritative persistence. Durable background work appears where processing continues after admission.

![Containers: Save a photograph and its brief](diagrams/c4-container.png)

The component view shows dispatch into Application handlers and inward-defined ports. Infrastructure supplies the persistence and provider implementations.

![Components: Save a photograph and its brief](diagrams/c4-component.png)

The class view names the proposed state, requests, handlers, and interface-bound frontend service. Typed associations and dependencies show which element owns behavior.

![Classes: Save a photograph and its brief](diagrams/class-structure.png)

The upload and save personal photographs sequence traces `L2-001` through its enforcing operations. The accompanying Description defines state changes and significant recovery paths.

![L2-001: Upload and save personal photographs](diagrams/sequence-001.png)

The record and edit the critique brief sequence traces `L2-002` through its enforcing operations. The accompanying Description defines state changes and significant recovery paths.

![L2-002: Record and edit the critique brief](diagrams/sequence-002.png)
