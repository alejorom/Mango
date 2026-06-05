You are assisting in a .NET 8 (Core) C# project structured under a Microservices Architecture.
Stack: .NET 8+, C#, ASP.NET Core Web API, Minimal APIs, MassTransit / RabbitMQ, EF Core / Dapper.

## Rules
- Diagnose before editing. Return root cause first unless told to skip.
- Microservice Isolation: Maintain strict domain boundaries. No shared databases or direct cross-service DB coupling.
- Modern C# Standards: Embrace modern features (file-scoped namespaces, primary constructors, records, pattern matching).
- API-First Approach: Build clean RESTful Web APIs or Minimal APIs with proper HTTP status codes and structured JSON.
- Async Communication: Favor event-driven patterns for inter-service communication via message brokers.
- Show only modified method, endpoint, class, or configuration block – not full file rewrites.
- No comments, JSDoc, or XML documentation tags unless asked.
- No suggestions beyond the task scope. State change. Show fix. Stop.
- Limit analysis to max 6 bullets.

## Critical Constraints (Token Savers)
- NEVER rewrite full files. Output ONLY the exact modified method, endpoint, or config block.
- Leverage .NET 8+ Extensions: Use native dependency injection, System.Text.Json, and modern configuration patterns.
- Distributed Resilience: Include proper error handling, retries, or distributed tracing integration where applicable.
- No explanations, no pleasantries, no markdown commentary. Code or strict fixes only.
- No comments, JSDoc, XML tags, or TODOs in code unless explicitly asked.
- Max 3 bullet points for root-cause analysis. If the fix is obvious, skip analysis entirely.

## Output
- Drop filler: no "Sure!", "Happy to help", "Of course", "Certainly".
- Zero filler. Do NOT say "Sure!", "Here is the fix", "Hope this helps", or "Certainly".
- Start your response directly with the fix or the code block.
- Short synonyms: fix > "implement a solution for", bug > "issue".
- Short terms only: use "fix" instead of "implement a solution for", "bug" instead of "issue".
- Ensure code blocks are 100% copy-paste safe.
- No em-dashes or decorative Unicode.

## Model selection
- Mechanical tasks (rename, boilerplate, serialization settings): use smallest available model.
- Simple tasks (syntax, routing, DTO updates): Use smallest/fastest model.
- Exploration and synthesis (diagnose, microservice refactor, explain): use standard model.
- Architecture decisions, complex event-driven integration: use the most capable model only if needed.
- Logic & Debugging (distributed transactions, async flows): Use standard model.
- Complex architectural choices only: Use most capable model.