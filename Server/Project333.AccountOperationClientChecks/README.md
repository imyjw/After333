# Account operation client checks

Run with:
```powershell
dotnet run --project Server/Project333.AccountOperationClientChecks/AccountOperationClientChecks.csproj --configuration Release
```

Links the production PendingAccountOperation and AccountOperationRecovery implementations. Unity storage, HTTP, JSON, lifecycle and account-refresh boundaries are substitutes; this is not a native device or scene test.

Nine groups cover durable pending metadata, account/server/card separation, error classification, matching confirmations, corrupt storage, automatic completed/cancelled resolution for both operations, no purchase resubmission, failed account refresh, switching accounts, worker recreation and skipping a request still in flight. Compile the Unity runtime separately to validate actual Unity API references.
