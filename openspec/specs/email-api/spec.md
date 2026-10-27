## Purpose

EmailAPI consumes events from Azure Service Bus and maintains an audit log of notifications generated.

## Requirements

### Requirement: Consume cart notification messages
The system SHALL listen to `EmailShoppingCartQueue` and process cart notification requests. When a cart email is requested, the system SHALL construct an HTML message containing cart items and totals, skipping any items with null Product reference.

#### Scenario: Cart message received
- **WHEN** a CartDto is published to EmailShoppingCartQueue with non-null Product references
- **THEN** system deserializes CartDto, builds HTML message with all items, and logs to database

#### Scenario: Cart message received with null products
- **WHEN** a CartDto contains items with null Product (e.g., from ShoppingCartAPI bug or partial deploy)
- **THEN** system skips null items and includes only valid products in HTML message
- **AND** system appends note: "[Some products could not be included]"
- **AND** message is processed and logged without throwing NullReferenceException

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
The system SHALL persist every notification event to an EmailLogger table, recording email address, message content, and timestamp. If database persistence fails, the system SHALL log the exception and propagate an error to the message handler.

#### Scenario: Event logged successfully
- **WHEN** a message is consumed
- **THEN** system creates EmailLogger entry with Email, Message, and EmailSent timestamp

#### Scenario: Logging failure (silently swallowed)
- **WHEN** database persistence fails (exception in AppDbContext.SaveChangesAsync)
- **THEN** EmailService.LogAndEmail logs exception via ILogger<EmailService>
- **AND** EmailService.LogAndEmail throws exception (propagates to handler)
- **AND** handler does NOT call CompleteMessageAsync
- **AND** message remains unacknowledged, Service Bus retries or dead-letters per configuration

### Requirement: No REST API exposure
The system SHALL NOT expose any HTTP endpoints. All interaction is via Service Bus message consumption.

#### Scenario: Service startup
- **WHEN** EMailAPI starts
- **THEN** Service Bus processors are initialized and listening (no controllers or routes registered)
