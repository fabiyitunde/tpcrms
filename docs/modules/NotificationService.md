# NotificationService Module

## Overview

The NotificationService module provides multi-channel notification capabilities for the CRMS system. It supports email, SMS, and WhatsApp notifications with template-based message generation, delivery tracking, and retry logic.

## Key Features

- **Multi-Channel Support**: Email, SMS, WhatsApp, InApp, Push
- **Template-Based Messaging**: Reusable templates with variable substitution
- **Delivery Tracking**: Track notification status from creation to delivery/read
- **Retry Logic**: Automatic retries for failed notifications
- **Scheduled Notifications**: Send notifications at a specified time
- **Event-Driven**: Consumes domain events from Workflow, Committee, and LoanApplication
- **Background Processing**: Async notification processing via hosted service

## Notification Channels

| Channel | Provider (Mock) | Production Suggestion |
|---------|-----------------|----------------------|
| Email | MockEmailSender | SendGrid, Amazon SES |
| SMS | MockSmsSender | Africa's Talking, Termii |
| WhatsApp | MockWhatsAppSender | Twilio, 360dialog |
| InApp | - | WebSocket/SignalR |
| Push | - | Firebase FCM, OneSignal |

## Notification Types

| Type | Description | Typical Channels |
|------|-------------|------------------|
| ApplicationSubmitted | Loan application received | Email, SMS |
| ApplicationApproved | Loan approved | Email, SMS |
| ApplicationRejected | Loan rejected | Email |
| ApplicationDisbursed | Loan disbursed | Email, SMS |
| WorkflowAssigned | Task assigned to user | Email |
| WorkflowEscalated | Workflow escalated | Email |
| WorkflowSLAWarning | SLA deadline approaching | Email |
| WorkflowSLABreached | SLA deadline exceeded | Email |
| CommitteeVoteRequired | Committee member needs to vote | Email |
| CommitteeDecisionMade | Committee decision recorded | Email |
| DocumentRequired | Additional document needed | Email, SMS |
| PasswordReset | Password reset request | Email |

## Domain Model

### Notification Entity

| Property | Description |
|----------|-------------|
| Type | Notification type (enum) |
| Channel | Delivery channel (Email, SMS, etc.) |
| Priority | Low, Normal, High, Urgent |
| Status | Pending, Scheduled, Sending, Sent, Delivered, Read, Failed, Retry, Cancelled |
| RecipientUserId | Optional link to system user |
| RecipientName | Recipient display name |
| RecipientAddress | Email address, phone number, etc. |
| TemplateCode | Template used for rendering |
| Subject | Email subject line |
| Body | Plain text body |
| BodyHtml | Optional HTML body |
| LoanApplicationId | Optional context link |
| ScheduledAt | When to send (if scheduled) |
| SentAt | When sent |
| DeliveredAt | When confirmed delivered |
| ReadAt | When marked as read |
| FailedAt | When failed |
| FailureReason | Error message |
| RetryCount | Number of retry attempts |
| MaxRetries | Maximum retries (default: 3) |
| ExternalMessageId | Provider message ID |
| ProviderName | Sender provider name |

### NotificationTemplate Entity

| Property | Description |
|----------|-------------|
| Code | Unique template code |
| Name | Display name |
| Description | Template description |
| Type | Associated notification type |
| Channel | Target channel |
| Language | Language code (default: "en") |
| Subject | Email subject template |
| BodyTemplate | Plain text body template |
| BodyHtmlTemplate | Optional HTML body template |
| AvailableVariables | JSON schema of available variables |
| IsActive | Whether template is active |
| Version | Version number |

## Template Variable Substitution

Templates use `{{variableName}}` syntax for variable substitution:

```
Dear {{UserName}},

Your loan application {{ApplicationNumber}} for {{CustomerName}} has been approved.

Approved Amount: {{Amount}}
```

## Event Handlers

The NotificationService consumes domain events to automatically trigger notifications:

| Event | Action |
|-------|--------|
| WorkflowAssignedEvent | Send email to assigned user |
| WorkflowSLABreachedEvent | Send alert to assigned user and escalation target |
| WorkflowEscalatedEvent | Send email to escalation target |
| CommitteeVotingStartedEvent | Send email to all committee members |
| LoanApplicationApprovedEvent | Send notification to initiating officer |

## API Endpoints

### Notification Management

| Method | Endpoint | Description | Access |
|--------|----------|-------------|--------|
| GET | /api/notification/{id} | Get notification by ID | Authenticated |
| GET | /api/notification/my | Get current user's notifications | Authenticated |
| GET | /api/notification/my/unread-count | Get unread count | Authenticated |
| GET | /api/notification/loan-application/{id} | Get notifications for loan | Authenticated |
| POST | /api/notification/{id}/mark-read | Mark as read | Authenticated |
| POST | /api/notification/send | Send notification | SystemAdministrator |

### Template Management

| Method | Endpoint | Description | Access |
|--------|----------|-------------|--------|
| GET | /api/notification/templates | Get all templates | SystemAdministrator |
| GET | /api/notification/templates/{id} | Get template by ID | SystemAdministrator |
| POST | /api/notification/templates | Create template | SystemAdministrator |
| PUT | /api/notification/templates/{id} | Update template | SystemAdministrator |
| POST | /api/notification/templates/{id}/deactivate | Deactivate template | SystemAdministrator |

## Background Processing

The `NotificationProcessingService` runs as a hosted service with a 30-second processing interval:

1. **Process Scheduled**: Send notifications where ScheduledAt <= now
2. **Process Pending**: Send notifications in Pending status
3. **Process Retries**: Retry failed notifications

## Notification Lifecycle

```
Create → Pending
         ↓
     Scheduled (if scheduledAt provided)
         ↓
     Sending
         ↓
     Sent → Delivered → Read
         ↓
     Failed → Retry (if retries available)
         ↓
     Failed (permanent) / Cancelled
```

## Files

### Domain Layer
- `Domain/Aggregates/Notification/Notification.cs` - Notification aggregate
- `Domain/Aggregates/Notification/NotificationTemplate.cs` - Template aggregate
- `Domain/Enums/NotificationEnums.cs` - NotificationChannel, NotificationType, NotificationStatus, NotificationPriority
- `Domain/Interfaces/INotificationRepository.cs` - Repository interfaces

### Application Layer
- `Application/Notification/Interfaces/INotificationSender.cs` - Sender interface and INotificationService
- `Application/Notification/Services/NotificationService.cs` - NotificationOrchestrator
- `Application/Notification/DTOs/NotificationDtos.cs` - DTOs
- `Application/Notification/Queries/NotificationQueries.cs` - Query handlers

### Infrastructure Layer
- `Infrastructure/Persistence/Configurations/Notification/NotificationConfigurations.cs` - EF configurations
- `Infrastructure/Persistence/Repositories/NotificationRepositories.cs` - Repository implementations
- `Infrastructure/ExternalServices/Notifications/MockEmailSender.cs` - Mock senders (Email, SMS, WhatsApp)
- `Infrastructure/Events/Handlers/NotificationEventHandlers.cs` - Domain event handlers
- `Infrastructure/BackgroundServices/NotificationProcessingService.cs` - Background processor

### API Layer
- `API/Controllers/NotificationController.cs` - REST endpoints

## Integration Points

- **WorkflowEngine**: Receives WorkflowAssigned, WorkflowSLABreached, WorkflowEscalated events
- **CommitteeWorkflow**: Receives CommitteeVotingStarted events
- **LoanApplication**: Receives LoanApplicationApproved events
- **AuditService**: Notification events can be audited

## Future Enhancements

1. **Real Provider Integrations**: Replace mock senders with real providers
2. **Notification Preferences**: User preferences for channels and frequency
3. **Batching**: Batch multiple notifications into digest emails
4. **In-App Notifications**: WebSocket push for real-time in-app notifications
5. **Read Receipts**: Email tracking pixels for read confirmation
6. **Unsubscribe**: GDPR-compliant unsubscribe handling
