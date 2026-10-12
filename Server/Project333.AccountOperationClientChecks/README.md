# Account operation client checks

Run with:
```powershell
dotnet run --project Server/Project333.AccountOperationClientChecks/AccountOperationClientChecks.csproj --configuration Release
```

Links the production `PendingAccountOperation`, `AccountOperationRecovery`, `AccountHttpTransport`, `PendingCardPackPresentation` and `ServerShopClient` implementations. Unity storage, transport, account DTOs, lifecycle and `/me` application boundaries are substitutes; this is not a native device, Unity scene, database or real network test.

The original nine purchase/upgrade recovery groups and four HTTP deadline/cancellation groups remain. The card-pack checks cover durable request/unopened markers before HTTP, response loss and storage reload, in-flight duplicate rejection, exact ten-copy order and duplicates, authoritative `/me` collection application, invalid receipts/refreshes, account/token/endpoint/cancellation guards, GET-only unread result restoration, scoped/matching acknowledgement, corrupt storage, completed/cancelled common recovery, original in-flight work, and definitive business rejection. Recovery never resends a purchase POST.

Compile the Unity runtime separately to validate actual Unity APIs and account DTOs. For an isolated run, copy this harness and its linked production files into a throwaway project tree so `bin/obj` stay outside the primary server directories. Set `PROJECT333_CLIENT_CHECK_PROOF` to a writable JSON path for individual card-pack case results and exact boundary limitations.