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
- [X] **Detail View & Content Extraction**
  - [x] Fetch single announcement body via `courses/{course_id}/discussion_topics/{topic_id}`. For viewing or getting an item in the feed via the dashboard feed.
  - [X] Fetch single assignment prompt/rubric via `courses/{course_id}/assignments/{assignment_id}`.
  - [X] Build `TextCleaner` utility (HTML stripping, entity decoding, line break preservation).

---

## Phase 2: Gmail Core Engine
- [X] **Google Cloud Project & Authentication**
  - [X] Configure Google Cloud Console project and enable Gmail API.
  - [X] Set up OAuth 2.0 Client credentials (`client_secret.json`). Used `.env` instead hehe
  - [X] Implement OAuth 2.0 user consent flow (local browser authentication callback).
  - [X] Implement token caching and silent refresh (`token.json`).
- [X] **Gmail Service Implementation**
  - [X] Create email DTO records (`EmailSummary`, `EmailDetail`, `AttachmentMetadata`).
  - [X] Implement `FetchUnreadEmailsAsync` (filtered inbox queries e.g., `is:unread category:primary`).
  - [X] Implement `FetchEmailContentAsync` (fetch body, parse MIME multipart payloads, decode base64url).
  - [X] Implement interaction actions (`MarkAsReadAsync`, `StarEmailAsync`, `TrashEmailAsync`).

---

## Phase 3: Application Orchestration & Business Logic
> Phases 3 and 4 are built together: they're really one thing split in two. The controller (Phase 3) is the "backend of the frontend", so its methods get shaped by what each screen (Phase 4) needs. Add a controller method whenever a screen needs one.
- [X] **Service Orchestration**
  - [X] Create an application controller / state manager to coordinate Canvas and Gmail services.
  - [X] Opening an email auto-marks it as read (fetch content + `MarkAsReadAsync`).
  - [X] Implement unified error handling and network fault tolerance (friendly error messages instead of crashes, request timeouts).
    - Rate-limit backoff dropped: a personal tool doesn't come close to Canvas/Gmail limits, and Google's library already retries 503s.
- [X] **Data Aggregation View**
  - [X] Build an aggregated "Morning Briefing" routine (today's schedule + urgent emails).

---

## Phase 4: CLI Interface & Terminal Makeover (Frontend)
> Folder already created `UI/`
> Built together with Phase 3 (see the note there).
- [X] **Command & Menu System**
  - [X] Integrate terminal UI framework (e.g., `Spectre.Console`).
  - [X] Build interactive navigation loops (arrow-key selection, hotkeys, breadcrumbs).
  - [X] Implement detail inspector views with paginated or scrollable panels.
- [X] **Visual Makeover & Polish**
  - [X] Color-code priority items (due today vs. due next week, high scores, urgent tags). Color-coded by due date (overdue / today / tomorrow / this week).
  - [X] Format tabular views with clean borders, aligned columns, and truncated text.
  - [X] Add loading spinners and progress indicators for network requests.
  - [X] Provide clear error banners and confirmation prompts (e.g., before trashing an email).

---

## Phase 5: Packaging & Distribution
- [X] Add configuration validation on startup (missing secrets, broken tokens).
  - `.env` is read from `~/.config/TerminalHub/.env` (falls back to `./.env` for `dotnet run`); a missing value prints which one and where to put it.
  - An expired/revoked Google login is handled automatically: the old login is deleted and the browser opens to sign in again. (The OAuth app is "Internal" to the school's Google org, so Google's 7-day token limit for External/Testing apps doesn't apply.)
  - Settings screen (`s`): semester start date (saved to `~/.config/TerminalHub/settings.json`) and a manual "Sign in to Google again".
- [X] Build self-contained native executable (`dotnet publish -c Release -r linux-x64 --self-contained`).
  - Now just `dotnet publish -c Release`: the `.csproj` makes it one self-contained, ReadyToRun-compiled file (~0.2s startup).
  - Native AOT was tested and dropped: Google's auth library (Newtonsoft.Json) crashes under it.
- [X] Create global alias / shell shortcut (`hub` or `terminal-hub`).
  - Installed as `~/.local/bin/hub`: `install -m 755 bin/Release/net10.0/linux-x64/publish/CanvasMail ~/.local/bin/hub`
- [X] Instant start: the last briefing is cached in `~/.cache/TerminalHub/briefing.json` and shown immediately while fresh data loads in the background.
