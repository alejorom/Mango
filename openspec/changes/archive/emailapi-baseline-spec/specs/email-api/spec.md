## Purpose

EmailAPI consumes events from Azure Service Bus and maintains an audit log of notifications generated.

## ADDED Requirements

### Requirement: Consume cart notification messages
The system SHALL listen to `EmailShoppingCartQueue` and process cart notification requests. When a cart email is requested, the system SHALL construct an HTML message containing cart items and totals.

#### Scenario: Cart message received
- **WHEN** a CartDto is published to EmailShoppingCartQueue
- **THEN** system deserializes CartDto, builds HTML message, and logs to database

### Requirement: Consume user registration notifications
The system SHALL listen to `RegisterUserQueue` and process user registration confirmations. When a user registers, the system SHALL construct a notification message with the email address.

#### Scenario: Registration message received
- **WHEN** an email string is published to RegisterUserQueue
- **THEN** system deserializes email, constructs notification, and logs to database

### Requirement: Consume order placement notifications
The system SHALL listen to `OrderCreatedTopic/OrderCreated_Email_Subscription` and process order placement events. When an order is placed, the system SHALL construct an HTML message with the order ID.

#### Scenario: Order placed message received
- **WHEN** a RewardsMessage is published to OrderCreatedTopic subscription
- **THEN** system deserializes RewardsMessage, builds notification message, and logs to database

### Requirement: Audit log persistence
The system SHALL persist every notification event to an EmailLogger table, recording email address, message content, and timestamp.

#### Scenario: Event logged successfully
- **WHEN** a message is consumed
- **THEN** system creates EmailLogger entry with Email, Message, and EmailSent timestamp

#### Scenario: Logging failure (silently swallowed)
- **WHEN** database persistence fails (exception in AppDbContext.SaveChangesAsync)
- **THEN** EmailService.LogAndEmail catches, returns false, and does NOT throw or log
- **AND** callers (EmailCartAndLog, LogOrderPlaced, RegisterUserEmailAndLog) ignore return value
- **AND** message is CompleteMessageAsync'd despite persistence failure (data loss)

### Requirement: No REST API exposure
The system SHALL NOT expose any HTTP endpoints. All interaction is via Service Bus message consumption.

#### Scenario: Service startup
- **WHEN** EMailAPI starts
- **THEN** Service Bus processors are initialized and listening (no controllers or routes registered)
