using System;
using System.IO;
using System.Threading;
using Project333.Runtime.Application.Services;
using Project333.Runtime.Infrastructure.Data;

namespace Project333.PvpServer.BattleSessions;

public static class PrototypeCardDefinitions
{
    private const string CardDatabaseFileName = "cards.json";
    private static readonly Lazy<JsonCardDefinitionDatabase> CachedCardDatabase = new(
        LoadCardDefinitionDatabaseFromDisk,
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static BattleFlowController CreateBattleFlowController(
        ICardDefinitionProvider? provider = null,
        ICardUpgradeLevelProvider? cardUpgradeLevelProvider = null)
    {
        provider ??= LoadCardDefinitionProvider();
        cardUpgradeLevelProvider ??= ZeroCardUpgradeLevelProvider.Instance;
        var commandProcessor = new BattleCommandProcessor(
            new PlayCardService(provider, new SummonService(), cardUpgradeLevelProvider),
            new SpellService(provider, cardUpgradeLevelProvider),
            new MoveService(),
            new AttackService(),
            new EndTurnService());

        return new BattleFlowController(
            new BattleSetupService(),
            new MulliganService(),
            new TurnStartService(provider),
            commandProcessor);
    }

    public static ICardDefinitionProvider LoadCardDefinitionProvider()
    {
        return LoadCardDefinitionDatabase().CreateProvider();
    }

    public static JsonCardDefinitionDatabase LoadCardDefinitionDatabase()
    {
        return CachedCardDatabase.Value;
    }

    private static JsonCardDefinitionDatabase LoadCardDefinitionDatabaseFromDisk()
    {
        var cardDatabasePath = ResolveCardDatabasePath();
        Console.WriteLine($"[cards] loading definitions from {cardDatabasePath}");
        var json = File.ReadAllText(cardDatabasePath);
        var validation = CardDatabaseValidator.ValidateJson(json);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"Card database validation failed for '{cardDatabasePath}':{Environment.NewLine}{string.Join(Environment.NewLine, validation.Errors)}");
        }

        var database = JsonCardDefinitionDatabase.FromJson(json);
        return database;
    }

    private static string ResolveCardDatabasePath()
    {
        var outputCandidate = Path.Combine(AppContext.BaseDirectory, "Data", CardDatabaseFileName);
        var projectCandidate = FindProjectCardDatabasePath();

        if (File.Exists(outputCandidate) && projectCandidate != null)
        {
            return File.GetLastWriteTimeUtc(projectCandidate) > File.GetLastWriteTimeUtc(outputCandidate)
                ? projectCandidate
                : outputCandidate;
        }

        if (File.Exists(outputCandidate))
        {
            return outputCandidate;
        }

        if (projectCandidate != null)
        {
            return projectCandidate;
        }

        throw new FileNotFoundException(
            $"Could not find {CardDatabaseFileName}. Expected it under the server output Data folder or Assets/Project333/Resources/Project333/Data.",
            CardDatabaseFileName);
    }

    private static string? FindProjectCardDatabasePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var projectCandidate = Path.Combine(
                directory.FullName,
                "Assets",
                "Project333",
                "Resources",
                "Project333",
                "Data",
                CardDatabaseFileName);

            if (File.Exists(projectCandidate))
            {
                return projectCandidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
