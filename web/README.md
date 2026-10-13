# ClaimDesk web

The React front end of ClaimDesk. It talks to the API described in [`../docs/API.md`](../docs/API.md).

## Run it

Start the API on `http://localhost:5080` first, then:

```bash
npm install
npm run dev      # http://localhost:5173, with /api proxied to the API
```

| command | what it does |
|---------|--------------|
| `npm run dev` | dev server with hot reload |
| `npm run test` | unit and component tests (Vitest + React Testing Library, `fetch` mocked) |
| `npm run e2e` | one end-to-end test in Microsoft Edge (Playwright) against the running API |
| `npm run screenshots` | retakes the pictures in `../docs/screenshots` |
| `npm run lint` | oxlint |
| `npm run build` | type-check, then a production build in `dist/` |

## How it is organised

```
src/
  api/         types.ts (every shape in the contract), client.ts (typed fetch wrapper, ApiError), session.ts
  auth/        UserProvider + useCurrentUser (the "acting as" user), permissions.ts (what each role may do)
  components/  Button, Field, StatusBadge, DataTable, Panel, Feedback (loading / error / empty states), Layout
  hooks/       useAsync (load data, with loading, error and reload)
  lib/         format.ts (rupees, dates)
  pages/       Dashboard, Policies, Claims, Claim detail, and the forms they use
  test/        test setup and helpers (mockApi, renderAs)
e2e/           Playwright: the claim flow from filing to payment, and the screenshot script
```

## Things worth knowing

- **No login.** The header has an "Acting as" switcher. The chosen user id is kept in `localStorage`
  and `api/client.ts` sends it as `X-User-Id` with every request.
- **Roles.** `auth/permissions.ts` decides which buttons are shown. This is only for convenience:
  the API enforces every rule, and the pages show its `403` and `409` messages when it refuses.
- **Stale data.** Approve, reject and pay send the claim's `rowVersion`. If someone else changed the
  claim first, the API answers `409`; the claim page shows the message and reloads the claim.
- **Errors.** Every failed request becomes an `ApiError` with the status, the server's message and
  any field errors, so forms can show a validation message next to the field it belongs to.

## End-to-end test

`npm run e2e` drives the real stack, so start the API first. Playwright uses the Microsoft Edge that is already
installed (`channel: 'msedge'` in `playwright.config.ts`), and starts the Vite dev server if it is not running.
Every run adds one policy and one claim to the development database.

## Tool versions

Vite 6, oxlint 1.16 and jsdom 26 are used because they run on Node 22.11. Their newer major versions
need Node 22.12 or later.
