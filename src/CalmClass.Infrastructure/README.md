## Infrastructure Project

In Clean Architecture, Infrastructure concerns are kept separate from the core business rules (or domain model in DDD).

The only project that should have code concerned with EF, Files, Email, Web Services, AWS, etc is Infrastructure.

Infrastructure should depend on Core where abstractions (interfaces) exist.

Infrastructure classes implement interfaces found in the Core project(s).
