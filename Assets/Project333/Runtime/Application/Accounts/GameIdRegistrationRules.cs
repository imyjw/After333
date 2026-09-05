using System;

namespace Project333.Runtime.Application.Accounts
{
    public static class GameIdRegistrationRules
    {
        public const int GameIdMinLength = 6;
        public const int GameIdMaxLength = 30;
        public const int PasswordMinLength = 8;
        public const int PasswordMaxLength = 128;

        public static bool TryNormalizeGameId(
            string gameId,
            out string normalizedGameId,
            out string errorMessage)
        {
            normalizedGameId = gameId == null
                ? string.Empty
                : gameId.Trim().ToLowerInvariant();

            if (normalizedGameId.Length < GameIdMinLength ||
                normalizedGameId.Length > GameIdMaxLength)
            {
                errorMessage = $"Game ID는 {GameIdMinLength}~{GameIdMaxLength}자로 입력해주세요.";
                return false;
            }

            for (var i = 0; i < normalizedGameId.Length; i++)
            {
                var character = normalizedGameId[i];
                var isValid =
                    character is >= 'a' and <= 'z' ||
                    character is >= '0' and <= '9' ||
                    character == '.';
                if (!isValid)
                {
                    errorMessage = "Game ID에는 영문자, 숫자, 마침표(.)만 사용할 수 있습니다.";
                    return false;
                }
            }

            if (normalizedGameId[0] == '.' ||
                normalizedGameId[normalizedGameId.Length - 1] == '.' ||
                normalizedGameId.Contains(".."))
            {
                errorMessage = "Game ID는 마침표로 시작하거나 끝날 수 없고, 마침표를 연속으로 사용할 수 없습니다.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public static bool TryValidatePassword(string password, out string errorMessage)
        {
            if (string.IsNullOrEmpty(password) ||
                password.Length < PasswordMinLength ||
                password.Length > PasswordMaxLength)
            {
                errorMessage = $"비밀번호는 {PasswordMinLength}~{PasswordMaxLength}자로 입력해주세요.";
                return false;
            }

            if (password[0] == ' ' || password[password.Length - 1] == ' ')
            {
                errorMessage = "비밀번호의 처음과 끝에는 공백을 사용할 수 없습니다.";
                return false;
            }

            for (var i = 0; i < password.Length; i++)
            {
                var character = password[i];
                if (character < ' ' || character > '~')
                {
                    errorMessage = "비밀번호에는 영문자, 숫자, 공백 및 일반 기호만 사용할 수 있습니다.";
                    return false;
                }
            }

            if (IsCommonWeakPassword(password))
            {
                errorMessage = "너무 쉽게 추측할 수 있는 비밀번호입니다. 다른 비밀번호를 사용해주세요.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool IsCommonWeakPassword(string password)
        {
            switch (password.ToLowerInvariant())
            {
                case "password":
                case "password123":
                case "12345678":
                case "qwerty123":
                case "11111111":
                case "abcdefgh":
                    return true;
                default:
                    return false;
            }
        }
    }
}
