using CockroachFantasia.Gameplay;
using CockroachFantasia.Networking;

namespace CockroachFantasia.UI
{
    public static class MatchResultsFormatter
    {
        public static string Headline(MatchWinner winner)
        {
            return winner == MatchWinner.Cockroaches ? "THE ROACHES FEAST!" : "THE KITCHEN IS SAVED!";
        }

        public static string RoleOutcome(MatchWinner winner, PlayerRole localRole)
        {
            var won = winner == MatchWinner.Human && localRole == PlayerRole.Human ||
                      winner == MatchWinner.Cockroaches && localRole == PlayerRole.Cockroach;
            return won ? "YOU WON — WHAT A GLORIOUS MESS!" : "YOU LOST — REVENGE TASTES DELICIOUS!";
        }
    }
}
