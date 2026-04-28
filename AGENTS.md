# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Project Overview

Monorepo for **九游俱乐部 (J9 Club)** — a gaming/entertainment club platform with two sub-projects:

- **J9_Admin/** — .NET 8 backend: Blazor Server admin panel + REST API + Telegram bot
- **J9_APP_103/** — React Native (Expo) cross-platform mobile app

## Common Commands

### Backend (J9_Admin)

```bash
# Run in development
cd J9_Admin && dotnet run

# Run on a fixed LAN-accessible port via helper script
cd J9_Admin && ./dotnet.sh

# Build backend project / solution
cd /root/DD/J999 && dotnet build J9_Admin.sln
cd /root/DD/J999/J9_Admin && dotnet build J9_Admin.csproj

# Run tests in solution (when test projects exist)
cd /root/DD/J999 && dotnet test J9_Admin.sln

# Run a single test
cd /root/DD/J999 && dotnet test J9_Admin.sln --filter FullyQualifiedName~SomeNamespace.SomeTestClass.SomeTestMethod

# Docker（通用流程见 .cursor/skills/dotnet-aspnet-docker/；J9 端口与卷见该 skill 文末对照表）
cd J9_Admin
docker compose up
./docker-auto.sh               # build + down + up -d
# 端口 8015，容器名 j9-admin-backend，卷：Logs、wwwroot/uploads、keys、buyu.db
```

### Mobile App (J9_APP_103)

```bash
cd J9_APP_103

pnpm install
pnpm dev          # Expo dev server (clears cache)
pnpm dev:server   # Tunnel mode on port 8099 for phone testing
pnpm android
pnpm ios
pnpm web

# Type check
npx tsc --noEmit

# Build static web bundle
pnpm build:web

# Format
npx prettier --write .

# Clean rebuild
pnpm clean        # removes .expo and node_modules
pnpm install
```

Package manager is **pnpm**.

## Architecture

### Backend (J9_Admin)

- **Single ASP.NET Core 8 app**: `Program.cs` wires Blazor Server UI, AdminBlazor API exposure, FreeSql, Telegram hosted service, Serilog, CORS, response caching, static files, and seed initialization.
- **API surface model**: business services in `J9_Admin/API/` are exposed via `UseAdminOmniApi()` (endpoint style like `/api/login/@Login`). Core logic is service-centric, not controller+repository layering.
- **Persistence**: FreeSql + SQLite (`buyu.db`) with `UseAutoSyncStructure(true)`; schema evolves from entity definitions on first CRUD access (no migration pipeline).
- **Domain entities**: main data model is under `J9_Admin/Entities/Ddd/` (`DMember`, `DTransAction`, `DGame`, `DAgent`, `DNotice`, `DEvent`, etc.), used directly by services.
- **Cross-cutting base service**: `API/BaseService.cs` provides shared access patterns (FreeSql, scheduler, auth/user context helpers) for feature services.
- **Operational integration**: Telegram bot runs as hosted background service, and services reuse `TGMessageApi` for notifications. `Filters/ApiOperationLoggingFilter.cs` logs API in/out summaries with sensitive-field masking.
- **Startup specifics**: `/profile` version endpoint is mapped before `UseAdminOmniApi()`, and `Program.cs` ends with `public partial class Program` for `WebApplicationFactory<Program>` test hosting.

### Mobile App (J9_APP_103)

- **Routing**: Expo Router file-based routing under `app/`; tab shell is in `app/(tabs)/`, while auth and feature flows are sibling route files.
- **App composition**: `app/_layout.tsx` sets theme, toast, portal host, balance context, and unauthorized redirect callback.
- **Custom tab navigation**: `app/(tabs)/_layout.tsx` implements a custom bottom tab bar UI instead of default tabs.
- **API abstraction**: `lib/api/request.ts` centralizes base URL selection, AsyncStorage token handling, auth header injection, query param assembly, unauthorized cleanup/redirect trigger, and response normalization.
- **Feature API modules**: `lib/api/*.ts` are thin endpoint wrappers (auth, event, game, message, notice, transaction, invite, banner) built on top of the shared request helper.
- **Styling stack**: NativeWind + global CSS variables for theme; reusable primitives in `components/ui/`; TypeScript alias `@/*` is configured in `tsconfig.json`.

### J9 Brand Colors

Purple `#7B5CFF` · Pink `#FF5FA2` · Orange `#FF8A34` · Blue `#3CBAFF` · Green `#35D07F` · Yellow `#FFD84D` · Background `#FFF7F1` · Text primary `#2A184B` · Text secondary `#6B5A8E`

## Review Rules

- Code review defaults to Chinese output.
- If the user does not explicitly request another language, findings, summaries, verdicts, and explanations should all be written in Chinese.
- Keep code, file paths, API names, class names, and other identifiers in their original form; do not translate them.
- Even when the review schema or surrounding instructions are in English, the natural-language content should still default to Chinese.

## Important Notes

- The codebase uses Chinese (中文) in comments, UI text, and business naming.
- `buyu.db` (SQLite) is committed to the repo — avoid committing unintended local DB changes.
- Model validation is intentionally disabled (`SuppressModelStateInvalidFilter`); service methods enforce validation.
- CORS is configured to allow any origin in development.
- Mobile local dev defaults to `http://localhost:5231` unless `EXPO_PUBLIC_API_URL` is set; production mobile builds should set `EXPO_PUBLIC_API_URL_PROD` explicitly.
- For remote phone testing, set `J9_APP_103/.env` with `EXPO_PUBLIC_API_URL=http://<server-ip>:8015` and run `pnpm dev:server`.
