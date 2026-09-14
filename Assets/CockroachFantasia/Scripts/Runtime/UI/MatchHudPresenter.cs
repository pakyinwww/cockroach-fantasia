using CockroachFantasia.Food;
using CockroachFantasia.Gameplay;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CockroachFantasia.UI
{
    public sealed class MatchHudPresenter : MonoBehaviour
    {
        [SerializeField] private Text timerLabel;
        [SerializeField] private Text scoreLabel;
        [SerializeField] private Text announcementLabel;
        [SerializeField] private GameObject cockroachPanel;
        [SerializeField] private Text carryLabel;
        [SerializeField] private Text cockroachPromptLabel;
        [SerializeField] private Text respawnLabel;
        [SerializeField] private GameObject humanPanel;
        [SerializeField] private Text reticleLabel;
        [SerializeField] private Text swatterLabel;

        private int lastScore = -1;
        private uint lastImpactSequence;
        private float emphasisUntil;

        public void Configure(Text timer, Text score, Text announcement, GameObject roachPanel,
            Text carry, Text roachPrompt, Text respawn, GameObject humanHud, Text reticle, Text swatter)
        {
            timerLabel = timer;
            scoreLabel = score;
            announcementLabel = announcement;
            cockroachPanel = roachPanel;
            carryLabel = carry;
            cockroachPromptLabel = roachPrompt;
            respawnLabel = respawn;
            humanPanel = humanHud;
            reticleLabel = reticle;
            swatterLabel = swatter;
        }

        private void Update()
        {
            var game = NetworkGameManager.Instance;
            if (game == null)
            {
                SetWaitingState();
                return;
            }

            RefreshCommon(game);
            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            var carrier = localPlayer != null ? localPlayer.GetComponent<CockroachFoodCarrier>() : null;
            var respawn = localPlayer != null ? localPlayer.GetComponent<CockroachRespawn>() : null;
            var swatter = localPlayer != null ? localPlayer.GetComponent<SwatterAttack>() : null;
            cockroachPanel?.SetActive(carrier != null);
            humanPanel?.SetActive(swatter != null);
            if (carrier != null) RefreshCockroach(carrier, respawn);
            if (swatter != null) RefreshHuman(swatter, game);
        }

        private void RefreshCommon(NetworkGameManager game)
        {
            var quota = game.Rules != null ? game.Rules.FoodQuotaPoints : 12;
            if (scoreLabel != null)
            {
                scoreLabel.text = MatchHudFormatter.FormatScore(game.DepositedPoints, quota);
                if (lastScore >= 0 && game.DepositedPoints > lastScore)
                {
                    emphasisUntil = Time.unscaledTime + 0.8f;
                    if (announcementLabel != null)
                        announcementLabel.text = $"FOOD SECURED!  +{game.DepositedPoints - lastScore}";
                }
                scoreLabel.color = Time.unscaledTime < emphasisUntil
                    ? new Color(0.55f, 1f, 0.45f)
                    : Color.white;
            }
            lastScore = game.DepositedPoints;

            if (timerLabel != null)
            {
                timerLabel.text = game.Phase switch
                {
                    MatchPhase.Loading => "LOADING",
                    MatchPhase.Countdown => "GET READY",
                    MatchPhase.Playing => MatchHudFormatter.FormatTimer(game.RemainingPlayingSeconds),
                    _ => "0:00"
                };
                var urgent = game.Phase == MatchPhase.Playing && game.RemainingPlayingSeconds <= 10d;
                timerLabel.color = urgent ? new Color(1f, 0.3f, 0.24f) : Color.white;
                timerLabel.transform.localScale = urgent
                    ? Vector3.one * (1f + 0.08f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 7f)))
                    : Vector3.one;
            }

            if (announcementLabel != null && game.Phase == MatchPhase.Results)
                announcementLabel.text = game.Winner == MatchWinner.Cockroaches
                    ? "THE ROACHES FEAST!"
                    : "THE KITCHEN IS SAVED!";
            else if (announcementLabel != null && Time.unscaledTime >= emphasisUntil)
                announcementLabel.text = string.Empty;
        }

        private void RefreshCockroach(CockroachFoodCarrier carrier, CockroachRespawn respawn)
        {
            if (respawn != null && respawn.IsRespawning)
            {
                if (carryLabel != null) carryLabel.text = "CARGO DROPPED";
                if (cockroachPromptLabel != null) cockroachPromptLabel.text = "SQUASHED — HOLD TIGHT";
                if (respawnLabel != null)
                {
                    respawnLabel.gameObject.SetActive(true);
                    respawnLabel.text = MatchHudFormatter.FormatRespawn(respawn.RemainingRespawnSeconds);
                }
                return;
            }

            if (respawnLabel != null) respawnLabel.gameObject.SetActive(false);
            if (!carrier.IsCarrying || !TryResolveFood(carrier.CarriedFoodNetworkId, out var food))
            {
                if (carryLabel != null) carryLabel.text = "CARRY  EMPTY  •  SPEED 100%";
                if (cockroachPromptLabel != null) cockroachPromptLabel.text = "E  PICK UP NEARBY FOOD";
                return;
            }

            var definition = food.Definition;
            if (carryLabel != null)
                carryLabel.text = $"CARRY  {food.Size.ToString().ToUpperInvariant()}  •  {definition.Points} PT" +
                                  $"  •  SPEED {definition.CarrySpeedMultiplier * 100f:0}%";
            if (cockroachPromptLabel != null) cockroachPromptLabel.text = "E  DROP FOOD";
        }

        private void RefreshHuman(SwatterAttack swatter, NetworkGameManager game)
        {
            if (reticleLabel != null) reticleLabel.text = "+";
            if (swatterLabel != null)
                swatterLabel.text = game.AcceptsGameplayRequests && swatter.IsLocallyReady
                    ? "SWATTER READY"
                    : $"SWATTER  {swatter.LocalCooldownRemaining:0.0}s";
            if (swatter.ConfirmedImpactSequence != lastImpactSequence)
            {
                lastImpactSequence = swatter.ConfirmedImpactSequence;
                emphasisUntil = Time.unscaledTime + 0.8f;
                if (announcementLabel != null)
                    announcementLabel.text = $"WHOMP!  x{swatter.LastConfirmedHitCount}";
            }
            else if (announcementLabel != null && Time.unscaledTime >= emphasisUntil)
            {
                announcementLabel.text = string.Empty;
            }
        }

        private void SetWaitingState()
        {
            if (timerLabel != null) timerLabel.text = "LOADING";
            if (scoreLabel != null) scoreLabel.text = "FOOD  0 / 12";
            cockroachPanel?.SetActive(false);
            humanPanel?.SetActive(false);
        }

        private static bool TryResolveFood(ulong networkId, out FoodItem food)
        {
            food = null;
            var manager = NetworkManager.Singleton;
            return manager != null && manager.SpawnManager.SpawnedObjects.TryGetValue(networkId,
                       out var networkObject) && (food = networkObject.GetComponent<FoodItem>()) != null;
        }
    }
}
