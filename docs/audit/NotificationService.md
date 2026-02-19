# Audit Report: NotificationService Module

**Module ID:** 14
**Audit Date:** 2026-02-17
**Auditor:** Domain Expert Review
**Module Status (Documented):** 🟢 Completed
**Audit Verdict:** ⚠️ Security and Reliability Issues — Not Production Ready

---

## 1. Executive Summary

The NotificationService is well-designed with a clean template system, retry logic, delivery tracking, and event-driven triggering. The background processor pattern is appropriate for asynchronous notification dispatch. However, there are critical gaps: the template rendering is not safe against injection, the background processor can send duplicate notifications in multi-instance deployments, email/phone validation is missing, and the `mark-read` endpoint has a broken ownership check. InApp and Push channels have no real implementation.

---

## 2. Security Issues

### 2.1 Template Variable Injection Risk (MEDIUM-HIGH)

The template system uses `{{variableName}}` substitution via simple string replacement:
```
Dear {{UserName}},
Your application {{ApplicationNumber}} has been approved.
```

If a `UserName` value itself contains `{{...}}` patterns (e.g., a maliciously crafted user name stored in the database), the substitution could produce unexpected output. This is especially relevant if the template body is later rendered as HTML.

**Recommendation:**
- Sanitize all variable values before substitution (strip `{{` and `}}` characters from values)
- Consider using a proven template engine (e.g., Scriban or Fluid for .NET) that handles escaping
- For HTML bodies, ensure all substituted values are HTML-encoded

### 2.2 `MarkAsRead` Does Not Verify Ownership (HIGH)

```
POST /api/notification/{id}/mark-read
```

An authenticated user can mark any notification as read if they know the notification ID (a GUID). There is no check that the notification belongs to the calling user.

**Recommendation:**
```csharp
// In MarkAsReadHandler:
if (notification.RecipientUserId != currentUserId)
    return ApplicationResult.Failure("Access denied");
```

---

## 3. Reliability Issues

### 3.1 Background Processor Causes Duplicate Notifications in Multi-Instance Deployment (CRITICAL)

The `NotificationProcessingService` runs every 30 seconds and processes all `Pending` notifications. In a horizontally scaled environment (multiple API instances), multiple instances will simultaneously query and process the same `Pending` notifications, resulting in duplicate emails/SMS being sent.

**Impact:** A user could receive the same "Your loan has been approved" email 3 times if 3 API instances are running.

**Recommendation:**
- Implement a distributed lock (e.g., using Redis `SETNX` or SQL row locking with `SELECT FOR UPDATE`) before processing notifications
- Alternative: Use a message queue (RabbitMQ/Azure Service Bus) to dispatch notifications — a queue guarantees single delivery
- Use a `Processing` status with a timestamp to detect and release stuck notifications (e.g., stuck for > 5 minutes)

### 3.2 Retry Logic Has No Backoff Delay (MEDIUM)

The retry mechanism attempts up to 3 retries but does not document any delay between retries. If an email provider is temporarily unavailable (rate limit, network issue), immediate retries will likely fail for the same reason and exhaust retries within seconds.

**Recommendation:**
- Implement exponential backoff: retry 1 after 1 minute, retry 2 after 5 minutes, retry 3 after 30 minutes
- Store `NextRetryAt` on the `Notification` entity
- Only process retries where `NextRetryAt <= now`

### 3.3 SLA Breach Notifications May Fire Repeatedly (MEDIUM)

`WorkflowSLABreachedEvent` triggers a breach notification. However, if the SLA monitoring background service runs every 15 minutes and the workflow item remains overdue for 2 hours, the event may fire multiple times (once per monitoring cycle), sending the same alert 8 times.

**Recommendation:** After the first SLA breach notification is sent, record a flag `SLABreachNotificationSentAt` on `WorkflowInstance`. Subsequent breach checks should not send another notification unless the escalation level increases.

---

## 4. Missing Validations

### 4.1 No Email Address Validation

The `RecipientAddress` for email channel is not validated. An invalid email address will cause delivery failures that consume retry budget without resolution.

**Recommendation:** Validate email format (RFC 5322 basic validation) before creating the notification.

### 4.2 No Phone Number Validation for SMS

Nigerian phone numbers should follow `+234XXXXXXXXXX` (E.164 format) or `0XXXXXXXXXX` (local format). No validation is performed. Invalid numbers will fail at the SMS provider level.

**Recommendation:** Normalize phone numbers to E.164 format before dispatch.

---

## 5. Missing Features

### 5.1 InApp and Push Channels Have No Implementation

`NotificationChannel.InApp` and `NotificationChannel.Push` are defined in the enum but have no sender implementations. Notifications created for these channels will fail silently.

**Recommendation:** Either implement InApp (WebSocket/SignalR) and Push (FCM) or clearly filter these channels from processing until implemented.

### 5.2 No Notification Preferences per User

All notifications are sent to all configured channels. Users cannot opt out of channels (e.g., a user who prefers SMS over email). For NDPA compliance, users must be able to manage communication preferences.

### 5.3 No Unsubscribe Mechanism

Particularly for the Customer Portal (Phase 2), marketing or regulatory communications must include an unsubscribe mechanism. This is required for NDPA compliance.

---

## 6. Operational Issues

### 6.1 No Monitoring for Notification Queue Backlog

If the notification processing service falls behind (e.g., provider outage), there is no alert for a growing backlog of pending notifications. A long backlog means critical notifications (e.g., committee vote requests) are delayed.

**Recommendation:** Add a health metric: if `Pending` notification count exceeds a threshold (e.g., 100), trigger an alert.

### 6.2 `ExternalMessageId` Not Populated in Mock Senders

Mock senders do not set `ExternalMessageId` or `ProviderName`. When switching to real providers, these fields must be populated for delivery tracking and dispute resolution.

---

## 7. Recommendations Summary

| Priority | Item |
|----------|------|
| CRITICAL | Implement distributed lock to prevent duplicate notifications in multi-instance deployment |
| HIGH | Fix `MarkAsRead` — verify notification ownership before marking |
| HIGH | Implement retry backoff delay (exponential) |
| HIGH | Sanitize template variables before substitution |
| MEDIUM | Add email address and phone number validation |
| MEDIUM | Prevent repeated SLA breach notifications without escalation |
| MEDIUM | Disable processing of InApp/Push channels until implemented |
| MEDIUM | Add notification queue backlog monitoring |
| LOW | Implement user notification preferences |
| LOW | Plan unsubscribe mechanism for Phase 2 Customer Portal |
