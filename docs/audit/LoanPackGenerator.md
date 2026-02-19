# Audit Report: LoanPackGenerator Module

**Module ID:** 13
**Audit Date:** 2026-02-17
**Auditor:** Domain Expert Review
**Module Status (Documented):** 🟢 Completed
**Audit Verdict:** ⚠️ Critical Feature Incomplete — File Storage Not Implemented

---

## 1. Executive Summary

The LoanPackGenerator module has a sophisticated PDF generation implementation using QuestPDF, with comprehensive sections covering the full corporate loan assessment. The content hashing and version control design is solid. However, a fundamental gap makes the module non-functional for its primary use case: **PDF files are generated in memory but never persisted to storage**. The download endpoint is a TODO. Without file persistence, generated loan packs cannot be distributed to committee members or archived for compliance.

---

## 2. Critical Implementation Gaps

### 2.1 File Storage Not Implemented (CRITICAL)

The module documentation explicitly states:
> "Current Implementation: PDF bytes are generated but file storage is not yet implemented."

The `LoanPack.StoragePath` field stores an intended path, but no file is ever written to it. Consequences:
- `GET /api/LoanPack/download/{id}` is a TODO — any download request will fail or return an error
- Generated loan packs cannot be sent by email
- Compliance requirement for document archival cannot be met
- Regenerating the PDF requires re-running the full data aggregation pipeline

**Recommendation:**
- Implement file storage using the configured adapter (S3-compatible: MinIO for dev, AWS S3/Azure Blob for prod)
- Use the `IFileStorage` abstraction (or create one) to keep the generator decoupled from the storage provider
- Store the file immediately after generation and before marking the `LoanPack` as `Generated`

### 2.2 Download Endpoint Is a TODO (CRITICAL)

```
GET /api/LoanPack/download/{id}
```

This endpoint is documented as "TODO: file storage." It cannot be used in any environment. Committee members who need to review the loan pack have no way to retrieve it.

**Recommendation:** Implement this endpoint with:
- Authorization check (only users with access to the loan application can download)
- Streaming the file from storage (do not load entire PDF into server memory)
- Generate a pre-signed URL with short expiry for S3-backed storage
- Audit log the download action in `DataAccessLog`

---

## 3. Performance Issues

### 3.1 PDF Generation in Synchronous Request Pipeline (HIGH)

PDF generation is triggered by:
```
POST /api/LoanPack/generate/{loanApplicationId}
```

The entire generation — data aggregation (parallel DB queries), PDF rendering (QuestPDF) — runs synchronously in the HTTP request pipeline. For complex applications (3 years financial data, 5 directors, 10 bureau reports), this could:
- Take 30–60 seconds
- Timeout at the HTTP gateway (default: 30 seconds)
- Block the thread pool under concurrent requests

**Recommendation:**
- Implement as a background job: the API returns `202 Accepted` with a `LoanPackId`
- A `IHostedService` processes the queue
- The client polls `GET /api/LoanPack/{id}` for status (`Generating` → `Generated`)
- Push notification to the requester when complete

### 3.2 Parallel Data Loading Still Blocks for Large Datasets

Even with parallel data loading (as documented), loading all directors' bureau reports, 3 years of financial statements, and full workflow history simultaneously may consume excessive memory for large applications. No memory pressure handling is documented.

---

## 4. Security Issues

### 4.1 No Access Control on Generation Endpoint (HIGH)

The `POST /api/LoanPack/generate/{loanApplicationId}` endpoint documentation shows no specific role restriction. Any authenticated user could potentially trigger PDF generation for any loan application.

**Recommendation:** Restrict to roles that should generate loan packs: `CreditOfficer`, `HOReviewer`, `SystemAdmin`.

### 4.2 Content Hash Not Verified on Download (MEDIUM)

`ContentHash` (SHA256) is stored on `LoanPack` but is not verified when the file is served via the download endpoint. If the file in storage was tampered with, the user would receive an undetected corrupt document.

**Recommendation:** On download, compute the SHA256 of the retrieved bytes and compare to `ContentHash`. Return a 409/integrity error if they don't match.

### 4.3 Old Version Files Not Cleaned Up (MEDIUM)

When a new version is generated, old versions are archived (status = `Archived`). However, the actual PDF files for archived versions remain in storage indefinitely. For compliance, archived versions should be retained (audit requirement), but for storage cost management, a configurable retention policy is needed.

---

## 5. Business Logic Issues

### 5.1 `LoanPackGeneratedEvent` Has No Documented Consumer (MEDIUM)

`LoanPackGeneratedEvent` is published but there is no documented handler that:
- Notifies the loan officer or requester that the pack is ready
- Sends the pack to committee members via email

**Recommendation:** Implement a `LoanPackGeneratedEventHandler` that:
- Creates a notification for the requesting user
- Optionally emails the pack link to committee members (if voting has started)

### 5.2 QuestPDF Community License Revenue Limit (LOW)

QuestPDF community license is free for businesses with less than $1M annual revenue. If the bank's revenue exceeds this threshold, a commercial license is required. This should be tracked and planned for.

---

## 6. Recommendations Summary

| Priority | Item |
|----------|------|
| CRITICAL | Implement file storage integration (S3/Azure Blob/MinIO) |
| CRITICAL | Implement download endpoint with authorization and audit logging |
| HIGH | Move PDF generation to background job (202 Accepted pattern) |
| HIGH | Add role-based authorization on generation endpoint |
| MEDIUM | Verify content hash on file download |
| MEDIUM | Implement `LoanPackGeneratedEventHandler` for notifications |
| MEDIUM | Define storage retention policy for archived versions |
| LOW | Plan for QuestPDF commercial license if revenue threshold exceeded |
