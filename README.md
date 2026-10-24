# TerminalHub

**Your Canvas LMS to-dos and unread Gmail on one terminal screen, without opening a browser.**

I built this so I could check what's due and what's in my school inbox without opening Canvas, Gmail, or reaching for my phone while focusing. Type `hub`, and everything is on screen in about 0.2 seconds.

```
 TerminalHub  Briefing                                                                 Fri, Oct 24 · 5:03 PM
┏━Canvas · To-do (1/5)━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓╭─Gmail · Unread (3)──────────────────────────────────╮
┃ ▸ ANN Room change for Friday's lab                  ┃│   [CISCO] USC Days 2026: Event Schedule             │
┃     Overdue · Oct 23, 5:03 PM · Data Structures     ┃│     Student Council · 3:03 PM                       │
┃   ASG Submit thesis appeal letter                   ┃│   Appeal Letter: Thesis 2 - AUG 2026                │
┃     Today 8:03 PM · Thesis 2                        ┃│     Thesis 2 - AUG 2026 · Yesterday                 │
┃   QUIZ Quiz 3: Design Patterns                      ┃│   Enrollment reminder                               │
┃     Tomorrow 5:03 PM · Software Engineering         ┃│     Registrar · Oct 12                              │
┃   ASG Lab 7                                         ┃│                                                     │
┃     Nov 2, 5:03 PM · Data Structures                ┃│                                                     │
┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛╰─────────────────────────────────────────────────────╯

 ↑↓ move · Tab mail · ⏎ open · d done · a announcements · r refresh · s settings · ^L redraw · q quit
```

## Features

- **Briefing home screen:** Canvas to-dos (this week through the next 14 days) next to your 5 most recent unread emails. The panels stack on narrow terminals.
- **Color-coded due dates:** overdue, today, tomorrow, this week, later.
- **Read without leaving the terminal:** emails, assignments, announcements and discussions open in a scrollable view.
- **Act on things:** mark a to-do as done in Canvas, star or trash an email (with a y/n confirm). Opening an email marks it as read, like Gmail does.
- **Course announcements:** browse each of your courses' announcements.
- **Fast:**
  - It starts in about 0.2 s and immediately shows your last briefing from a local cache, then refreshes in the background.
  - Canvas and Gmail are fetched at the same time.
  - If one of them fails, the other still shows.
- **Terminal-friendly:**
  - It redraws automatically when the window is resized, which helps with tiling window managers like Hyprland.
  - `Ctrl+L` forces a redraw.
  - It uses the alternate screen, so your shell history is untouched when you quit.
- **Handles expired logins:** if Google signs you out, it opens the browser to sign in again and carries on.

## Requirements

- **Linux.** It's built and tested on Linux (Kitty and Foot). Other platforms are untested, and the publish settings target `linux-x64`.
- **[.NET 10 SDK](https://dotnet.microsoft.com/download)** to build it. The installed app doesn't need .NET.
- A **Canvas LMS** account at a school that uses Canvas.
- A **Google account** with Gmail, and access to the [Google Cloud Console](https://console.cloud.google.com).

## Setup

### 1. Canvas access token

In Canvas, go to **Account → Settings → Approved Integrations → + New Access Token**, and copy the token.

You'll also need your school's Canvas domain, e.g. `usc.instructure.com` (no `https://`).

### 2. Google OAuth client (for Gmail)

1. In the [Google Cloud Console](https://console.cloud.google.com), create a project (or pick an existing one).
2. Go to **APIs & Services → Library**, find **Gmail API**, and click **Enable**.
3. Go to **Google Auth Platform → Audience** and choose a user type:
   - **Internal:** only offered if the project belongs to a Google Workspace organization, e.g. your school. No extra steps, and logins don't expire on their own.
   - **External:** for personal Gmail accounts. Leave it in **Testing** and add your email under **Test users**. Google expires Testing logins after 7 days; when that happens, TerminalHub opens the browser so you can sign in again.
4. Go to **Google Auth Platform → Clients → Create client**, choose the application type **Desktop app**, and copy the **Client ID** and **Client secret**.

### 3. Configuration file

Create `~/.config/TerminalHub/.env`:

```env
CANVAS_DOMAIN=yourschool.instructure.com
CANVAS_TOKEN=your-canvas-token
GOOGLE_CLIENT_ID=your-client-id.apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=your-client-secret
```

Then make it readable only by you:

```sh
chmod 600 ~/.config/TerminalHub/.env
```

### 4. Build and install

```sh
git clone https://github.com/kin-kkoy/StudentCLI.git
cd StudentCLI
dotnet publish -c Release
install -m 755 bin/Release/net10.0/linux-x64/publish/CanvasMail ~/.local/bin/hub
```

This assumes `~/.local/bin` is on your `PATH`. On the first run, `hub` opens your browser once to sign in to Google.

## Usage

```sh
hub
```

| Key | Action |
|---|---|
| `↑` `↓` / `j` `k` | Move |
| `Tab` / `←` `→` | Switch between the to-do and mail panels |
| `Enter` | Open the selected item |
| `d` | Mark the selected to-do as done |
| `a` | Courses → announcements |
| `s` | Settings |
| `r` | Refresh |
| `s` / `t` (in an email) | Star / move to trash |
| `PgUp` `PgDn` / `g` `G` | Scroll / jump to the top or bottom |
| `Esc` | Back |
| `Ctrl+L` | Redraw the screen |
| `q` | Quit |

### Settings (`s`)

- **Semester start date:** courses created on or after this date count as "this semester" in the courses list. The default is 2026-08-01.
- **Sign in to Google again:** deletes the saved Google login and opens the browser to log in fresh.

## Where things are stored

| What | Where |
|---|---|
| App | `~/.local/bin/hub` |
| Canvas and Google keys | `~/.config/TerminalHub/.env` |
| Settings | `~/.config/TerminalHub/settings.json` |
| Google login (token) | `~/.config/TerminalHub/google-token/` |
| Cached briefing | `~/.cache/TerminalHub/briefing.json` |

The courses list shows your Canvas **dashboard (favorited) courses**.

## Development

```sh
dotnet run   # reads ./.env if ~/.config/TerminalHub/.env doesn't exist
```

```
Program.cs        startup: loads .env, connects, starts the UI
Services/         CanvasService (Canvas REST API), GmailClientService (Gmail API + OAuth)
Models/           records for Canvas/Gmail data
Controllers/      HubController: the app's actions (briefing, open email + mark read, ...)
UI/               full-screen terminal UI (Spectre.Console): screens, lists, cache, settings
Utility/          TextCleaner (HTML → readable terminal text)
```

Built with [Spectre.Console](https://spectreconsole.net/), [Google.Apis.Gmail.v1](https://www.nuget.org/packages/Google.Apis.Gmail.v1) and [DotNetEnv](https://github.com/tonerdo/dotnet-env).

## Credits

This was my first C# backend project, built as a just-in-time learning exercise. I wrote the backend: `Services/`, `Models/`, `Controllers/` and `Utility/`.

The terminal UI (`UI/`) and the startup and packaging work were written by **Claude** (Anthropic's AI assistant, via Claude Code), at my request. Claude also made some smaller changes inside the backend:
- the `<style>`/`<script>` stripping in `TextCleaner`
- parallel email fetching in `GmailClientService`
- the settings and sign-in-again plumbing in `HubController`

Claude also guided me step by step through the parts I wrote myself. The commit history shows who did what.
