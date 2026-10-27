## 1. EmailService ILogger Injection

- [x] 1.1 Add ILogger<EmailService> parameter to EmailService constructor
- [x] 1.2 Store ILogger as private readonly field
- [x] 1.3 Update DI registration in Program.cs to pass ILogger instance

## 2. Fix LogAndEmail Exception Handling

- [x] 2.1 Replace "catch (Exception ex) { return false; }" with "catch (Exception ex) { _logger.LogError(ex, "DB persistence failed..."); throw; }"
- [x] 2.2 Remove return type bool, make return type void (or Task if async)
- [x] 2.3 Verify signature change doesn't break existing code

## 3. Fix Public Methods to Throw

- [x] 3.1 EmailCartAndLog: remove return type check (LogAndEmail now throws), exception propagates to handler
- [x] 3.2 LogOrderPlaced: same as 3.1
- [x] 3.3 RegisterUserEmailAndLog: same as 3.1

## 4. Add Null-Check in EmailCartAndLog Loop

- [x] 4.1 Iterate CartDetails, check if item.Product != null before accessing Product.Name
- [x] 4.2 Skip null items (don't append to HTML message)
- [x] 4.3 Track count of skipped items, append "[X products could not be included]" if count > 0
- [x] 4.4 Ensure message completes without throwing NullReferenceException

## 5. Verify & Test

- [x] 5.1 Build solution (Mango.Services.EMailAPI compiles)
- [x] 5.2 Run any existing tests for EmailService
- [x] 5.3 Manual test: publish CartDto with null Product item, verify message logs with note and no crash
- [x] 5.4 Manual test: simulate DB failure, verify exception logged and message not completed
