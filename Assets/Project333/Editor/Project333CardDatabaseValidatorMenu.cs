using System;
using System.IO;
using System.Text;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;
using UnityEngine;

namespace Project333.Editor
{
    public static class Project333CardDatabaseValidatorMenu
    {
        private const string CardDatabaseAssetPath = "Assets/Project333/Resources/Project333/Data/cards.json";

        [MenuItem("Tools/Project333/Data/Validate cards.json")]
        public static void ValidateCardsJson()
        {
            var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), CardDatabaseAssetPath);
            if (!File.Exists(absolutePath))
            {
                var message = $"cards.json was not found at {CardDatabaseAssetPath}.";
                Debug.LogError(message);
                EditorUtility.DisplayDialog("After333 cards.json", message, "OK");
                return;
            }

            try
            {
                var result = CardDatabaseValidator.ValidateJson(File.ReadAllText(absolutePath));
                var details = FormatResult(result);

                if (result.IsValid)
                {
                    Debug.Log(details);
                    EditorUtility.DisplayDialog("After333 cards.json", "cards.json validation passed.", "OK");
                }
                else
                {
                    Debug.LogError(details);
                    EditorUtility.DisplayDialog("After333 cards.json", "cards.json validation failed. Check the Console.", "OK");
                }
            }
            catch (Exception exception)
            {
                var message = $"cards.json validation crashed: {exception.Message}";
                Debug.LogError(message);
                EditorUtility.DisplayDialog("After333 cards.json", message, "OK");
            }
        }

        private static string FormatResult(CardDatabaseValidationResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine(result.IsValid
                ? "After333 cards.json validation passed."
                : "After333 cards.json validation failed.");
            builder.AppendLine($"Errors: {result.Errors.Count}");
            foreach (var error in result.Errors)
            {
                builder.AppendLine($"- {error}");
            }

            builder.AppendLine($"Warnings: {result.Warnings.Count}");
            foreach (var warning in result.Warnings)
            {
                builder.AppendLine($"- {warning}");
            }

            return builder.ToString();
        }
    }
}
