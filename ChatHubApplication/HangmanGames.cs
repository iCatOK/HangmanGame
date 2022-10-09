using System.Text;

namespace ChatHubApplication
{
    public class HangmanGameHelper
    {
        public static bool CheckGuess(string guess, GameSession session)
        {
            if (session == null) return false;

            var guessingWord = session.GuessingWord;
            var winWord = session.WinWord;
            
            var guessWordBuilder = new StringBuilder(guessingWord);

            // отгадано слово
            if (guess.Length > 1 && guess.Equals(winWord))
            {
                session.GuessingWord = session.WinWord;
                return true;
            }

            // отгадана буква
            if (guess.Length == 1 && winWord.Contains(guess[0]))
            {
                var correctChar = guess[0];

                // звёздочки заменяются отгаданной буквой
                for (int i = 0; i < winWord.Length; i++)
                {
                    if (winWord[i] == correctChar)
                    {
                        guessWordBuilder[i] = correctChar;
                    }
                }
                session.GuessingWord = guessWordBuilder.ToString();
                return true;
            }

            return false;
        }
    }
}
