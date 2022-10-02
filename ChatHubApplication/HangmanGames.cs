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
            if (guess.Length == 1 && winWord.Contains(guess[0]))
            {
                var correctChar = guess[0];
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
            
            if (guess.Length > 1 && guess.Equals(winWord))
            {
                session.GuessingWord = session.WinWord;
                return true;
            }

            return false;
        }
    }
}
