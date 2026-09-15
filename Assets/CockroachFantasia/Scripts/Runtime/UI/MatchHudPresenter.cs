using CockroachFantasia.Food;
using CockroachFantasia.Gameplay;
using CockroachFantasia.Networking;
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
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private Text resultsHeadlineLabel;
        [SerializeField] private Text resultsDetailLabel;
        [SerializeField] private Button rematchButton;
        [SerializeField] private Button returnButton;
        [SerializeField] private Text waitingForHostLabel;

        private int lastScore = -1;
        private uint lastImpactSequence;
        private float emphasisUntil;
        private bool resultPresented;

        public int ResultsPresentationCount { get; private set; }

        public void Configure(Text timer, Text score, Text announcement, GameObject roachPanel,
            Text carry, Text roachPrompt, Text respawn, GameObject humanHud, Text reticle, Text swatter,
            GameObject results, Text resultsHeadline, Text resultsDetail, Button rematch, Button returnToMenu,
            Text waitingForHost)
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
            resultsPanel = results;
            resultsHeadlineLabel = resultsHeadline;
            resultsDetailLabel = resultsDetail;
            rematchButton = rematch;
            returnButton = returnToMenu;
            waitingForHostLabel = waitingForHost;
        }

        private void OnEnable()
        {
            rematchButton?.onClick.AddListener(RequestRematch);
            returnButton?.onClick.AddListener(RequestReturnToMenu);
        }

        private void OnDisable()
        {
            rematchButton?.onClick.RemoveListener(RequestRematch);
            returnButton?.onClick.RemoveListener(RequestReturnToMenu);
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
            if (game.Phase == MatchPhase.Results)
            {
                ShowResults(game, localPlayer);
                return;
            }

            resultPresented = false;
            resultsPanel?.SetActive(false);
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
            resultsPanel?.SetActive(false);
        }

        private void ShowResults(NetworkGameManager game, NetworkObject localPlayer)
        {
            cockroachPanel?.SetActive(false);
            humanPanel?.SetActive(false);
            resultsPanel?.SetActive(true);
            if (!resultPresented)
            {
                resultPresented = true;
                ResultsPresentationCount++;
            }

            var identity = localPlayer != null ? localPlayer.GetComponent<NetworkRoleAvatar>() : null;
            var role = identity != null && identity.Seat == LobbySeat.Human
                ? PlayerRole.Human
                : PlayerRole.Cockroach;
            if (resultsHeadlineLabel != null)
                resultsHeadlineLabel.text = MatchResultsFormatter.Headline(game.Winner);
            if (resultsDetailLabel != null)
                resultsDetailLabel.text = $"{MatchResultsFormatter.RoleOutcome(game.Winner, role)}\n" +
                                          $"FINAL FOOD  {game.DepositedPoints} / {game.Rules.FoodQuotaPoints}";

            var isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
            if (rematchButton != null) rematchButton.gameObject.SetActive(isHost);
            if (returnButton != null) returnButton.gameObject.SetActive(isHost);
            if (waitingForHostLabel != null) waitingForHostLabel.gameObject.SetActive(!isHost);
        }

        private static void RequestRematch()
        {
            NetworkRoster.Instance?.RequestRematch();
        }

        private static void RequestReturnToMenu()
        {
            NetworkRoster.Instance?.RequestReturnToMenu();
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
