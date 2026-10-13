# ClaimDesk API (v1)

Base path `/api`. JSON bodies use camelCase. Enums travel as strings. Dates are ISO `yyyy-MM-dd`; timestamps are ISO 8601 UTC.

## Identity (demo only)

There is no login. Every request carries `X-User-Id: <id>` and the API looks the user up to get the role.
A missing or unknown id returns `401`. A role that may not perform the action returns `403`.

Seeded users:

| id | name  | role     |
|----|-------|----------|
| 1  | Asha  | Agent    |
| 2  | Ravi  | Adjuster |
| 3  | Meera | Adjuster |
| 4  | Kiran | Manager  |

## Errors

All errors are RFC 7807 problem details: `{ "type", "title", "status", "detail", "errors"? }`.

| status | when |
|--------|------|
| 400 | request validation failed (`errors` maps field name to messages) |
| 401 | missing or unknown `X-User-Id` |
| 403 | the role may not perform this action |
| 404 | resource not found |
| 409 | a business rule was broken, or the row changed since it was read (stale `rowVersion`) |

## Business rules

- Premium = sum insured x yearly rate. Rates: Motor 2.5%, Health 4.0%, Home 0.8%.
- A policy is issued `Active` for a one-year term starting on `startDate`.
- A claim can be filed only on an `Active` policy, with the incident date inside the term, and an amount greater than 0 and not above the remaining cover. Remaining cover = sum insured minus the total of `Approved` and `Paid` claims on that policy.
- Claim workflow: `Submitted` -> `UnderReview` -> `Approved` or `Rejected`; `Approved` -> `Paid`.
- Maker-checker: the user who approves a claim must not be the user who reviewed it. A claim above 100000 can be approved only by a Manager.
- Approve, reject and pay need the `rowVersion` last read by the client. A stale value returns `409`.
- Every status change is written to the claim audit trail by a SQL Server trigger, not by application code.

## Endpoints

| method | path | who | body | returns |
|--------|------|-----|------|---------|
| GET | `/api/users` | anyone (no header needed) | - | `User[]` |
| GET | `/api/customers` | any user | - | `Customer[]` |
| POST | `/api/customers` | Agent, Manager | `{ name, email }` | `201 Customer` |
| POST | `/api/policies/quote` | any user | `{ customerId, type, sumInsured, startDate }` | `Quote` (nothing is saved) |
| POST | `/api/policies` | Agent, Manager | `{ customerId, type, sumInsured, startDate }` | `201 Policy` |
| GET | `/api/policies?status=&customerId=` | any user | - | `Policy[]` |
| GET | `/api/policies/{id}` | any user | - | `Policy` |
| POST | `/api/policies/{id}/cancel` | Agent, Manager | - | `Policy` |
| POST | `/api/claims` | Agent | `{ policyId, incidentDate, amount, description }` | `201 Claim` |
| GET | `/api/claims?status=&policyId=` | any user | - | `Claim[]` |
| GET | `/api/claims/{id}` | any user | - | `ClaimDetail` |
| POST | `/api/claims/{id}/review` | Adjuster | - | `Claim` |
| POST | `/api/claims/{id}/approve` | Adjuster, Manager | `{ rowVersion }` | `Claim` |
| POST | `/api/claims/{id}/reject` | Adjuster, Manager | `{ reason, rowVersion }` | `Claim` |
| POST | `/api/claims/{id}/pay` | Manager | `{ rowVersion }` | `Claim` |
| GET | `/api/reports/claims-summary?from=&to=` | any user | - | `ClaimsSummaryRow[]` (from the stored procedure `usp_ClaimsSummary`) |

## Shapes

```
User             { id, name, role }                       role: "Agent" | "Adjuster" | "Manager"
Customer         { id, name, email }
Quote            { type, sumInsured, ratePercent, premium }
Policy           { id, policyNumber, customerId, customerName, type, sumInsured, premium,
                   startDate, endDate, status, remainingCover }
                   type:   "Motor" | "Health" | "Home"
                   status: "Active" | "Cancelled" | "Expired"
Claim            { id, claimNumber, policyId, policyNumber, policyType, incidentDate, amount,
                   description, status, filedBy, reviewedBy, approvedBy, rejectionReason,
                   createdAt, rowVersion }
                   status: "Submitted" | "UnderReview" | "Approved" | "Rejected" | "Paid"
                   filedBy / reviewedBy / approvedBy are user names (or null)
                   rowVersion is base64
ClaimDetail      Claim + { audit: AuditEntry[] }
AuditEntry       { oldStatus, newStatus, changedBy, changedAt }   oldStatus is null for the first entry
ClaimsSummaryRow { policyType, status, claimCount, totalAmount }
```

Validation: `name` 2-100 chars, `email` valid, `sumInsured` 10000 to 10000000, `amount` > 0, `description` 5-500 chars, `reason` 5-300 chars.

## Local development

- API: `http://localhost:5080`
- Web (Vite dev server): `http://localhost:5173`, proxying `/api` to the API
- SQL Server: `localhost,14333`, database `ClaimDesk`; the connection string comes from `ConnectionStrings__ClaimDesk`
