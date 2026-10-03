# TerminalHub: Canvas & Gmail CLI Dashboard

A modern terminal interface to manage Canvas LMS academic feeds and Gmail communications.

---

## Phase 1: Canvas LMS Core Engine
- [x] **Project Setup & Base Architecture**
  - [x] Configure `HttpClient` with base URL, bearer authentication, and `User-Agent`.
  - [x] Secure sensitive tokens via `DotNetEnv` (`.env` integration).
- [x] **Dashboard Feed (Planner API)**
  - [x] Create DTO records (`PlannerItem`, `PlannerItemDetails`, `PlannerOverride`).
  - [x] Implement dynamic date windows (start of current week to $N$ days ahead).
  - [x] Integrate server-side filtering (`filter=incomplete_items`).
- [x] **Task Completion & Write-Back**
  - [x] Implement conditional `POST` (create) vs. `PUT` (update) for planner overrides.
  - [x] Sync "Mark as done" actions directly with Canvas servers.
- [ ] **Detail View & Content Extraction**
  - [x] Fetch single announcement body via `courses/{course_id}/discussion_topics/{topic_id}`. For viewing or getting an item in the feed via the dashboard feed.
  - [X] Fetch single assignment prompt/rubric via `courses/{course_id}/assignments/{assignment_id}`.
  - [ ] Build `TextCleaner` utility (HTML stripping, entity decoding, line break preservation).

---

## Phase 2: Gmail Core Engine
- [ ] **Google Cloud Project & Authentication**
  - [ ] Configure Google Cloud Console project and enable Gmail API.
  - [ ] Set up OAuth 2.0 Client credentials (`client_secret.json`).
  - [ ] Implement OAuth 2.0 user consent flow (local browser authentication callback).
  - [ ] Implement token caching and silent refresh (`token.json`).
- [ ] **Gmail Service Implementation**
  - [ ] Create email DTO records (`EmailSummary`, `EmailDetail`, `AttachmentMetadata`).
  - [ ] Implement `FetchUnreadEmailsAsync` (filtered inbox queries e.g., `is:unread category:primary`).
  - [ ] Implement `FetchEmailContentAsync` (fetch body, parse MIME multipart payloads, decode base64url).
  - [ ] Implement interaction actions (`MarkAsReadAsync`, `StarEmailAsync`, `TrashEmailAsync`).

---

## Phase 3: Application Orchestration & Business Logic
- [ ] **Service Orchestration**
  - [ ] Create an application controller / state manager to coordinate Canvas and Gmail services.
  - [ ] Implement unified error handling and network fault tolerance (graceful timeouts, rate-limit backoff).
- [ ] **Data Aggregation View**
  - [ ] Build an aggregated "Morning Briefing" routine (today's schedule + urgent emails).

---

## Phase 4: CLI Interface & Terminal Makeover (Frontend)
- [ ] **Command & Menu System**
  - [ ] Integrate terminal UI framework (e.g., `Spectre.Console`).
  - [ ] Build interactive navigation loops (arrow-key selection, hotkeys, breadcrumbs).
  - [ ] Implement detail inspector views with paginated or scrollable panels.
- [ ] **Visual Makeover & Polish**
  - [ ] Color-code priority items (due today vs. due next week, high scores, urgent tags).
  - [ ] Format tabular views with clean borders, aligned columns, and truncated text.
  - [ ] Add loading spinners and progress indicators for network requests.
  - [ ] Provide clear error banners and confirmation prompts (e.g., before trashing an email).

---

## Phase 5: Packaging & Distribution
- [ ] Add configuration validation on startup (missing secrets, broken tokens).
- [ ] Build self-contained native executable (`dotnet publish -c Release -r linux-x64 --self-contained`).
- [ ] Create global alias / shell shortcut (`hub` or `terminal-hub`).