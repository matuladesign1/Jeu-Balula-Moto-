// BALULA MOTO — déroulement du jeu : clients, négociation, courses, carburant, usure, police, missions scénarisées.
using System.Collections.Generic;
using BalulaMoto.Core;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public enum RideState { Libre, Negociation, EnCourse }

    public class GameplayDirector : MonoBehaviour
    {
        public static GameplayDirector Instance;

        public GameSession Session;
        public WorldContext World;
        public RaycastVehicle Player;
        public VehicleAudio PlayerAudio;
        public WorldEnvironment Env;
        public CameraRig Cam;
        public TrafficManager Traffic;

        public RideState State = RideState.Libre;
        public ClientRequest Client;
        public Negotiation Nego;
        public long AgreedPrice;
        public RideMetrics Metrics;
        public string OpeningLine;
        public InteractionPoint NearbyInteraction;
        public WaitingClient NearbyClient;
        public string ActionLabel;
        public PlaceRef CurrentPlace;
        public VehicleStats Stats;
        public bool AtDestination;

        private WaitingClient negotiatingWith;
        private DestinationMarker marker;
        private readonly List<GameObject> passengers = new List<GameObject>();
        private float wearTimer, statsTimer, scanTimer, commentCooldown, stoppedAtDest, stuckTimer, hardTurnCooldown, crashTimer;
        private bool arrivalSaid, lowFuelWarned;
        private Vector3 prevPos;
        private string pendingInfraction;
        private float policeStopTimer, chaseEscapeTimer, docCheckCooldown = 30f;
        private bool policeChase, policePanelOpen;
        // Agent à pied (poste de police d'un carrefour) qui a vu l'infraction et qui arrête le joueur
        private Transform stopOfficer;
        private bool stopByPost;
        private float bumpCooldown;
        /// <summary>Piétons et agents bousculés depuis le début de la partie (photos de contrôle).</summary>
        public int PedestrianBumps { get; private set; }
        /// <summary>Vrai pendant un arrêt de police (sifflet donné, en attente que le joueur se range).</summary>
        public bool PoliceStopPending { get { return pendingInfraction != null; } }
        public string PendingInfraction { get { return pendingInfraction; } }
        private AIVehicle escort;
        private float followSeconds, followLost;

        private UIManager UI { get { return UIManager.Instance; } }

        public void Begin(GameSession session, WorldContext world, WorldEnvironment env, CameraRig cam, TrafficManager traffic)
        {
            Instance = this;
            Session = session;
            World = world;
            Env = env;
            Cam = cam;
            Traffic = traffic;
            State = RideState.Libre;
            SpawnPlayer(world.SpawnPosition, world.SpawnRotation);
        }

        // ==================================================================
        // Véhicule du joueur
        // ==================================================================

        public void SpawnPlayer(Vector3 pos, Quaternion rot)
        {
            if (Player != null)
            {
                Player.Impact -= OnImpact;
                Player.HardBrake -= OnHardBrake;
                Player.Jolt -= OnJolt;
                Player.Crash -= OnCrash;
                Destroy(Player.gameObject);
            }
            VehicleDef def = Session.ActiveDef;
            OwnedVehicle ov = Session.ActiveVehicle;
            Stats = Session.ActiveStats();
            Player = VehicleBuilder.BuildPlayer(def, ov, Stats, pos, rot);
            PlayerAudio = Player.gameObject.AddComponent<VehicleAudio>();
            PlayerAudio.Setup(Player);
            Player.Impact += OnImpact;
            Player.HardBrake += OnHardBrake;
            Player.Jolt += OnJolt;
            Player.Crash += OnCrash;
            Player.EngineCut = ov.carburantL <= 0f;
            Player.HeadlightsOn = Session.IsNight;
            if (Cam != null) Cam.SetTarget(Player);
            if (Traffic != null) Traffic.Player = Player;
            prevPos = Player.transform.position;
        }

        public void RespawnAtGarage()
        {
            if (State == RideState.EnCourse) AbortRide("changement de véhicule");
            SpawnPlayer(World.SpawnPosition, World.SpawnRotation);
        }

        /// <summary>Reconstruit le véhicule sur place (après une personnalisation).</summary>
        public void RebuildInPlace()
        {
            if (Player == null) return;
            Vector3 p = Player.transform.position;
            Vector3 f = Vector3.ProjectOnPlane(Player.transform.forward, Vector3.up);
            if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
            bool riding = State == RideState.EnCourse;
            if (riding) ClearPassengers();
            SpawnPlayer(p + Vector3.up * 0.2f, Quaternion.LookRotation(f.normalized));
            if (riding) SeatPassengers();
        }

        /// <summary>Fin de partie (retour au menu, changement de partie) : nettoie sans pénaliser le joueur.</summary>
        public void Shutdown()
        {
            EndRideCleanup();
            if (escort != null && Traffic != null) Traffic.Despawn(escort);
            escort = null;
            if (World != null && World.Checkpoint != null) World.Checkpoint.ReturnBus();
            if (Player != null) Destroy(Player.gameObject);
            Player = null;
            if (Traffic != null) Traffic.Player = null;
            if (Instance == this) Instance = null;
        }

        public void RefreshStats()
        {
            Stats = Session.ActiveStats();
            if (Player != null) Player.ApplyStats(Stats);
        }

        // ==================================================================
        // Boucle principale
        // ==================================================================

        /// <summary>Commandes imposées par les essais automatiques (photos de contrôle) ; null en jeu normal.</summary>
        public static DriveInput? TestInput;
        private bool wasNight;

        private void Update()
        {
            if (Session == null || Player == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            bool modal = UI != null && UI.AnyModalOpen;
            recentSpeedKmh = Mathf.Max(Player.SpeedKmh, recentSpeedKmh - 25f * dt);
            Player.Controls = modal ? new DriveInput() : (TestInput.HasValue ? TestInput.Value : PlayerControls.Read(Player.Kind == VehicleKind.Moto));
            // Les phares s'allument tout seuls à la tombée de la nuit (le joueur peut toujours les éteindre)
            bool nightNow = Session.IsNight;
            if (nightNow && !wasNight) Player.HeadlightsOn = true;
            wasNight = nightNow;
            if (PlayerAudio != null) PlayerAudio.HornPressed = !modal && PlayerControls.HornHeld();
            if (!modal) HandleKeys();
            Player.Wetness = Env != null ? Env.Wetness : 0f;

            wearTimer += dt;
            if (wearTimer >= 0.5f) { wearTimer = 0f; ApplyWearAndFuel(); }
            statsTimer += dt;
            if (statsTimer >= 4f) { statsTimer = 0f; RefreshStats(); }
            scanTimer += dt;
            if (scanTimer >= 0.2f) { scanTimer = 0f; Scan(); }
            commentCooldown -= dt;
            hardTurnCooldown -= dt;
            UpdateClientSpawning(dt);
            if (State == RideState.EnCourse) UpdateRide(dt);
            UpdatePolice(dt);
            UpdateFollowMission(dt);
            if (Player.Crashed)
            {
                crashTimer += dt;
                if (crashTimer > 2.5f) { crashTimer = 0f; Player.Recover(); }
            }
            prevPos = Player.transform.position;
        }

        private void HandleKeys()
        {
            if (PlayerControls.Pressed(GameKey.Action)) OnAction();
            if (PlayerControls.Pressed(GameKey.Camera)) CycleCamera();
            if (PlayerControls.Pressed(GameKey.Lights)) ToggleLights();
            if (PlayerControls.Pressed(GameKey.IndicatorLeft)) ToggleIndicator(-1);
            if (PlayerControls.Pressed(GameKey.IndicatorRight)) ToggleIndicator(1);
            if (PlayerControls.Pressed(GameKey.Reverse)) ToggleReverse();
            if (PlayerControls.Pressed(GameKey.Menu) && UI != null && UI.LastEscapeFrame != Time.frameCount) UI.OpenPause();
            if (PlayerControls.Pressed(GameKey.Missions) && UI != null) UI.OpenMissions();
        }

        public void CycleCamera()
        {
            if (Cam == null) return;
            CameraMode m = Cam.NextMode();
            if (UI != null) UI.Toast(CameraRig.Label(m));
        }

        public void ToggleLights() { Player.HeadlightsOn = !Player.HeadlightsOn; }

        public void ToggleIndicator(int side)
        {
            Player.Indicator = Player.Indicator == side ? 0 : side;
        }

        public void ToggleHazards() { Player.Indicator = Player.Indicator == 2 ? 0 : 2; }

        public void ToggleReverse()
        {
            if (Player == null) return;
            if (!Player.ReverseGear && Player.SpeedKmh > 4f) { if (UI != null) UI.Toast("Arrêtez-vous avant de reculer."); return; }
            Player.ReverseGear = !Player.ReverseGear;
            bool moto = Player.Kind == VehicleKind.Moto;
            if (UI != null) UI.Toast(Player.ReverseGear ? (moto ? "Marche arrière : recul aux pieds (GAZ pour reculer)" : "Marche arrière (GAZ pour reculer)") : "Marche avant");
        }

        // ==================================================================
        // Carburant et usure
        // ==================================================================

        private void ApplyWearAndFuel()
        {
            OwnedVehicle ov = Session.ActiveVehicle;
            if (ov == null || Stats == null) return;
            float throttle, idle, brakeLoss;
            float km = Player.TakeDistanceKm(out throttle, out idle, out brakeLoss);
            EconomyConfig eco = Session.Db.Economy;
            VehicleWear.ApplyDistance(ov, eco, km, Player.CurrentSurface, Env != null && Env.Wetness > 0.3f, throttle);
            VehicleWear.ApplyBraking(ov, eco, brakeLoss);
            if (!Player.EngineCut) Session.Fuel.Consume(ov, Stats, km, throttle, idle);
            if (State == RideState.EnCourse && Metrics != null) Metrics.DistanceKm += km;
            if (ov.carburantL <= 0f && !Player.EngineCut)
            {
                Player.EngineCut = true;
                Player.PushMode = false;
                Say("Vous", Session.Dialogue.Line("panne_seche", ""));
                if (UI != null) UI.OpenEmptyTank();
            }
            else if (ov.carburantL > 0f && Player.EngineCut)
            {
                Player.EngineCut = false;
                Player.PushMode = false;
            }
            float frac = Session.Fuel.Fraction(ov, Stats);
            if (frac < 0.12f && !lowFuelWarned && !Player.EngineCut) { lowFuelWarned = true; if (UI != null) UI.Toast("Réserve de carburant : trouvez une station ou un Kadafi."); }
            if (frac > 0.2f) lowFuelWarned = false;
        }

        public void CallKadafiDelivery()
        {
            RefuelResult r = Session.Fuel.EmergencyDelivery(Session.ActiveVehicle, Stats, 2f, 2000);
            if (UI != null) UI.Toast(r.Message);
            if (r.Success) { Player.EngineCut = false; Player.PushMode = false; }
        }

        public void StartPushing()
        {
            Player.PushMode = true;
            if (UI != null) UI.Toast("Vous poussez le véhicule (accélérateur = pousser). Trouvez une station ou un Kadafi.");
        }

        // ==================================================================
        // Détection : clients, services, lieux
        // ==================================================================

        private void Scan()
        {
            Vector3 p = Player.transform.position;
            float speed = Player.SpeedKmh;
            NearbyClient = null;
            NearbyInteraction = null;
            if (State == RideState.Libre && speed < 10f)
            {
                float best = 8f * 8f;
                foreach (ClientStop s in ClientStop.All)
                    foreach (WaitingClient c in s.Clients)
                    {
                        if (c == null) continue;
                        float d = (c.transform.position - p).sqrMagnitude;
                        if (d < best) { best = d; NearbyClient = c; }
                    }
            }
            if (speed < 10f)
            {
                float best = float.MaxValue;
                foreach (InteractionPoint ip in InteractionPoint.All)
                {
                    float d = (ip.transform.position - p).sqrMagnitude;
                    if (d < ip.Radius * ip.Radius && d < best) { best = d; NearbyInteraction = ip; }
                }
            }
            AtDestination = false;
            if (State == RideState.EnCourse && Client != null)
            {
                Vector3 dest = WorldGeo.ToWorld(Client.Destination, 0f);
                AtDestination = Flat(dest - p) < 22f;
            }
            if (Player.Crashed) ActionLabel = "Relever le véhicule";
            else if (Player.EngineCut && !Player.PushMode) ActionLabel = "Panne sèche : solutions";
            else if (AtDestination) ActionLabel = "Déposer le client";
            else if (NearbyClient != null) ActionLabel = "Parler au client — " + NearbyClient.Request.Name;
            else if (NearbyInteraction != null) ActionLabel = NearbyInteraction.ActionLabel();
            else ActionLabel = null;

            PlaceRef near = World.NearestPlace(p, 30f);
            if (near != CurrentPlace)
            {
                CurrentPlace = near;
                if (near != null) Session.Missions.OnPlaceReached(near);
            }
        }

        public void OnAction()
        {
            if (Player.Crashed) { Player.Recover(); crashTimer = 0f; return; }
            if (Player.EngineCut && !Player.PushMode) { if (UI != null) UI.OpenEmptyTank(); return; }
            if (AtDestination && State == RideState.EnCourse) { FinishRide(); return; }
            if (NearbyClient != null && State == RideState.Libre) { BeginNegotiation(NearbyClient); return; }
            if (NearbyInteraction == null || UI == null) return;
            switch (NearbyInteraction.Kind)
            {
                case InteractionKind.StationService:
                case InteractionKind.PointVente:
                case InteractionKind.Kadafi:
                    UI.OpenFuel(NearbyInteraction.VendorId);
                    break;
                case InteractionKind.Atelier: UI.OpenWorkshop(); break;
                case InteractionKind.Garage: UI.OpenGarage(); break;
                case InteractionKind.Concession: UI.OpenShop(); break;
            }
        }

        private void UpdateClientSpawning(float dt)
        {
            if (Player == null) return;
            Vector3 p = Player.transform.position;
            float rep = Session.Profile.Reputation();
            foreach (ClientStop s in ClientStop.All)
            {
                s.Cleanup();
                s.Timer -= dt;
                if (s.Timer > 0f) continue;
                float baseInterval = Session.IsRushHour ? 12f : (Session.IsNight ? 45f : 22f);
                s.Timer = Random.Range(baseInterval * 0.7f, baseInterval * 1.4f) / Mathf.Lerp(0.8f, 1.3f, (rep - 1f) / 4f);
                if (s.Clients.Count >= s.MaxClients) continue;
                float d = Flat(s.transform.position - p);
                if (d > 320f || d < 20f) continue;
                ClientRequest req = Session.Taxi.GenerateClient(s.Place, World.ZonePlaces(s.Place.Zone), Env != null && Env.IsRaining);
                if (req != null) s.AddClient(req, Random.Range(0, 100000));
            }
        }

        // ==================================================================
        // Négociation (appelée par le panneau de négociation)
        // ==================================================================

        public DialogueLanguage ClientLanguage()
        {
            DialogueLanguage pref = Session.Dialogue.Preference;
            if (pref != DialogueLanguage.Mixte || Client == null) return pref;
            return Client.Language;
        }

        public static string SpeechPrice(long amount) { return Cdf.Format(amount).Replace(" FC", ""); }

        private string Line(string key, params string[] kv)
        {
            string district = Client != null ? Client.Destination.District + " " + Client.Destination.Name : "";
            return Session.Dialogue.Line(key, district, ClientLanguage(), kv);
        }

        private void BeginNegotiation(WaitingClient wc)
        {
            negotiatingWith = wc;
            Client = wc.Request;
            Nego = Session.Taxi.StartNegotiation(Client);
            State = RideState.Negociation;
            string key = Client.CargoKg > 0f ? "demande_prix_marchandises" : (Client.Passengers > 1 ? "demande_prix_groupe" : "demande_prix");
            OpeningLine = Line(key, "dest", Client.Destination.Name, "n", Client.Passengers.ToString());
            if (UI != null) UI.OpenNegotiation();
        }

        public NegotiationStep Propose(long price, out string driverLine, out string clientLine)
        {
            driverLine = Line("chauffeur_propose", "prix", SpeechPrice(price));
            NegotiationStep st = Nego.Propose(price);
            clientLine = Line(st.LineKey, "prix", SpeechPrice(st.Price));
            if (st.Outcome == NegotiationOutcome.Accepted) StartRide();
            else if (st.Outcome == NegotiationOutcome.ClientLeft) ClientLeaves();
            return st;
        }

        public NegotiationStep AcceptCounter(out string driverLine)
        {
            NegotiationStep st = Nego.AcceptClientOffer();
            driverLine = Line("accepte_monte");
            if (st.Outcome == NegotiationOutcome.Accepted) StartRide();
            return st;
        }

        public string RefuseClient()
        {
            if (Nego != null) Nego.RefuseRide();
            string line = Line("refus_chauffeur");
            if (negotiatingWith != null) negotiatingWith.Lifetime = Mathf.Min(negotiatingWith.Lifetime, 8f);
            negotiatingWith = null;
            Client = null;
            State = RideState.Libre;
            return line;
        }

        private void ClientLeaves()
        {
            if (negotiatingWith != null && negotiatingWith.Stop != null) negotiatingWith.Stop.RemoveClient(negotiatingWith);
            negotiatingWith = null;
            Client = null;
            State = RideState.Libre;
        }

        // ==================================================================
        // Course
        // ==================================================================

        private void StartRide()
        {
            AgreedPrice = Nego.AgreedPrice;
            Metrics = new RideMetrics();
            if (negotiatingWith != null && negotiatingWith.Stop != null) negotiatingWith.Stop.RemoveClient(negotiatingWith);
            negotiatingWith = null;
            SeatPassengers();
            Player.SetExtraMass(Client.Passengers * 70f + Client.CargoKg);
            if (marker != null) Destroy(marker.gameObject);
            marker = DestinationMarker.Create(WorldGeo.ToWorld(Client.Destination, 0f), new Color(1f, 0.82f, 0.1f), Client.Destination.Name);
            State = RideState.EnCourse;
            stoppedAtDest = 0f;
            arrivalSaid = false;
            Say("Vous", Line("monte"));
        }

        private void SeatPassengers()
        {
            ClearPassengers();
            if (Client == null || Player == null || Player.Rig == null) return;
            int n = Mathf.Min(Client.Passengers, Player.Rig.Seats.Count);
            for (int i = 0; i < n; i++)
            {
                GameObject h = VehicleBuilder.BuildHuman(Player.Rig.Seats[i], Vector3.zero, i == 0 ? Client.Outfit : "civil", true, Client.Name.GetHashCode() + i, false);
                Prims.SetLayerRecursive(h, RaycastVehicle.PlayerLayer);
                passengers.Add(h);
            }
            if (Client.CargoKg > 0f && Player.Rig.CargoAnchor != null)
            {
                float s = Mathf.Clamp(Client.CargoKg / 120f, 0.35f, 1.1f);
                GameObject box = Prims.Box(Player.Rig.CargoAnchor, "Marchandises", new Vector3(0f, s * 0.35f, 0f), new Vector3(s, s * 0.7f, s), new Color(0.55f, 0.4f, 0.25f));
                Prims.SetLayerRecursive(box, RaycastVehicle.PlayerLayer);
                passengers.Add(box);
            }
        }

        private void ClearPassengers()
        {
            foreach (GameObject g in passengers) if (g != null) Destroy(g);
            passengers.Clear();
        }

        private void UpdateRide(float dt)
        {
            if (Client == null || Metrics == null) return;
            Metrics.DurationSeconds += dt;
            float speed = Player.SpeedKmh;
            bool moto = Player.Kind == VehicleKind.Moto;
            if (speed > World.SpeedLimitKmh + 5f) Metrics.SpeedingSeconds += dt;
            if (moto && Env != null && Env.IsRaining)
            {
                Metrics.RainSeconds += dt;
                if (commentCooldown <= 0f && Metrics.RainSeconds > 20f && Random.value < 0.01f) Comment("pluie_moto");
            }
            if (speed > (moto ? 58f : 48f) && commentCooldown <= 0f) Comment("conduite_rapide");
            if (moto && Mathf.Abs(Player.LeanAngle) > 36f && hardTurnCooldown <= 0f) { hardTurnCooldown = 3f; Metrics.HardTurns++; }
            stuckTimer = speed < 3f ? stuckTimer + dt : 0f;
            if (stuckTimer > 14f && commentCooldown <= 0f) { stuckTimer = 0f; Comment("embouteillage"); }

            Vector3 dest = WorldGeo.ToWorld(Client.Destination, 0f);
            float d = Flat(dest - Player.transform.position);
            if (d < 70f && !arrivalSaid) { arrivalSaid = true; Say(Client.Name, Line("arrivee")); }
            if (d < 22f && speed < 4f)
            {
                stoppedAtDest += dt;
                if (stoppedAtDest > 1.2f) FinishRide();
            }
            else stoppedAtDest = 0f;
        }

        private void FinishRide()
        {
            if (Client == null || Metrics == null) return;
            Metrics.DropOffDistanceM = Flat(WorldGeo.ToWorld(Client.Destination, 0f) - Player.transform.position);
            ClientRequest c = Client;
            RideResult r = Session.Taxi.CompleteRide(c, AgreedPrice, Metrics, Nego != null ? Nego.RespectPenalty : 0f);
            string line = Line(r.CommentKey, "prix", SpeechPrice(r.Paid + r.Tip));
            EndRideCleanup();
            if (PlayerAudio != null) PlayerAudio.PlayOneShot(AudioSynth.Cash, 0.6f);
            if (UI != null) UI.OpenRideSummary(r, c, line);
            if (BalulaGame.Instance != null) BalulaGame.Instance.Save();
        }

        public void AbortRide(string reason)
        {
            if (Client != null) Session.Taxi.AbortRide(Client, reason);
            EndRideCleanup();
        }

        private void EndRideCleanup()
        {
            ClearPassengers();
            if (marker != null) Destroy(marker.gameObject);
            marker = null;
            if (Player != null) Player.SetExtraMass(0f);
            Client = null;
            Metrics = null;
            State = RideState.Libre;
            AtDestination = false;
        }

        // ==================================================================
        // Événements physiques
        // ==================================================================

        private float recentSpeedKmh;

        private void OnImpact(float dv, Collider other)
        {
            float sev = VehicleWear.ApplyImpact(Session.ActiveVehicle, dv, Stats != null ? Stats.Resistance01 : 0.6f);
            RefreshStats();
            if (State == RideState.EnCourse && Metrics != null)
            {
                Metrics.Collisions++;
                Metrics.CollisionSeverity = Mathf.Max(Metrics.CollisionSeverity, sev);
                Comment("collision");
            }
            // Accrochage d'une autre voiture : infraction si un agent l'a vu
            // (seulement si le joueur roulait : pas d'amende quand un autre véhicule heurte la moto à l'arrêt)
            if (other != null && other.GetComponentInParent<AIVehicle>() != null && dv > 2f && recentSpeedKmh > 12f) Infraction(PoliceSystem.Collision);
            if (UI != null && sev > 0.15f) UI.Toast("Choc ! Carrosserie : " + Session.ActiveVehicle.carrosserie.ToString("0") + " %");
        }

        private void OnHardBrake(float decel)
        {
            if (State == RideState.EnCourse && Metrics != null)
            {
                Metrics.HarshBrakes++;
                Comment("freinage_brusque");
            }
        }

        private void OnJolt(float speedKmh)
        {
            VehicleWear.ApplyPothole(Session.ActiveVehicle, Session.Db.Economy, speedKmh);
            if (State == RideState.EnCourse && Metrics != null)
            {
                Metrics.Jolts++;
                if (speedKmh > 25f) Comment("nids_de_poule");
            }
        }

        private void OnCrash()
        {
            if (UI != null) UI.Toast(Player.Kind == VehicleKind.Moto ? "Chute ! Le véhicule va être relevé." : "Tricycle renversé ! Il va être relevé.");
            OwnedVehicle ov = Session.ActiveVehicle;
            VehicleWear.ApplyImpact(ov, 7f, Stats != null ? Stats.Resistance01 : 0.6f);
            if (State == RideState.EnCourse && Metrics != null)
            {
                Metrics.Crashed = true;
                Comment("collision");
                AbortRide("accident");
            }
            crashTimer = 0f;
        }

        // ==================================================================
        // Commentaires des passagers et des personnages
        // ==================================================================

        private void Comment(string key)
        {
            if (commentCooldown > 0f || Client == null) return;
            commentCooldown = 6f;
            string text = Line(key);
            if (!string.IsNullOrEmpty(text)) Say(Client.Name, text);
        }

        public void Say(string speaker, string text)
        {
            if (UI != null && !string.IsNullOrEmpty(text)) UI.ShowComment(speaker, text);
        }

        // ==================================================================
        // Police : infractions, contrôles, poursuite
        // ==================================================================

        private void UpdatePolice(float dt)
        {
            Vector3 pos = Player.transform.position;
            if (World.Junction != null && World.Junction.CrossedOnRed(prevPos, pos))
            {
                if (State == RideState.EnCourse && Metrics != null) { Metrics.RedLights++; Comment("feu_rouge"); }
                Infraction(PoliceSystem.RedLight);
            }
            CheckPedestrianBumps(dt);
            PoliceCheckpoint cp = World.Checkpoint;
            PolicePatrol patrol = PolicePatrol.Instance;
            float dist = cp != null ? Flat(cp.transform.position - pos) : float.MaxValue;
            bool nearCp = cp != null && dist < cp.Radius;
            // Agents à pied des carrefours : ils voient la vitesse et les phares éteints de près
            bool nearAgent = patrol != null && patrol.NearestOfficer(pos, 40f) != null;
            if ((nearCp || nearAgent) && Player.SpeedKmh > World.SpeedLimitKmh + 12f) Infraction(PoliceSystem.Speeding);
            if ((nearCp || nearAgent) && Session.IsNight && !Player.HeadlightsOn && Player.SpeedKmh > 10f) Infraction(PoliceSystem.NoLights);
            docCheckCooldown -= dt;
            if (cp != null && pendingInfraction == null && !policeChase && !policePanelOpen && dist < 18f && Player.SpeedKmh < 30f && docCheckCooldown <= 0f)
            {
                docCheckCooldown = 150f;
                if (Random.value < 0.35f || Session.Profile.rechercheParPolice) Infraction(Session.Profile.rechercheParPolice ? PoliceSystem.Fleeing : "controle");
            }
            if (pendingInfraction != null && !policeChase && !policePanelOpen)
            {
                policeStopTimer += dt;
                float d = stopByPost && stopOfficer != null ? Flat(stopOfficer.position - pos) : dist;
                if (Player.SpeedKmh < 3f)
                {
                    policePanelOpen = true;
                    // L'agent du carrefour vient au véhicule ; recherché pour refus d'obtempérer : amende plus lourde
                    bool fled = stopByPost && Session.Profile.rechercheParPolice;
                    if (stopByPost && stopOfficer != null && patrol != null) patrol.Approach(stopOfficer, Player.transform);
                    if (UI != null) UI.OpenPolice(pendingInfraction, fled);
                }
                else if (policeStopTimer > 12f && d > 60f)
                {
                    if (!stopByPost && cp != null && cp.Bus != null) StartChase();
                    else RefuseStop();
                }
            }
            if (policeChase && cp != null && cp.Bus != null)
            {
                float d = Flat(cp.Bus.transform.position - pos);
                if (d < 7f && Player.SpeedKmh < 8f)
                {
                    policeChase = false;
                    policePanelOpen = true;
                    if (UI != null) UI.OpenPolice(pendingInfraction ?? PoliceSystem.Fleeing, true);
                }
                else
                {
                    chaseEscapeTimer = d > 220f ? chaseEscapeTimer + dt : 0f;
                    if (chaseEscapeTimer > 15f)
                    {
                        Session.Profile.rechercheParPolice = true;
                        if (UI != null) UI.Toast("Vous avez semé la police... pour l'instant. Le prochain contrôle sera plus sévère.");
                        EndChase();
                        pendingInfraction = null;
                    }
                }
            }
        }

        /// <summary>Piétons (et agents) bousculés par le véhicule du joueur : ils tombent et se relèvent, la police verbalise.</summary>
        private void CheckPedestrianBumps(float dt)
        {
            bumpCooldown -= dt;
            if (bumpCooldown > 0f || Player.SpeedKmh < 6f) return;
            Transform pt = Player.transform;
            bool moto = Player.Kind == VehicleKind.Moto;
            float hw = moto ? 0.45f : 0.75f, hl = moto ? 1.05f : 1.5f;
            Vector3 where = Vector3.zero;
            bool hit = CrowdSystem.Instance != null && CrowdSystem.Instance.Bump(pt.position, pt.forward, hw, hl, out where);
            Transform agent;
            if (!hit && PolicePatrol.Instance != null && PolicePatrol.Instance.Bump(pt.position, pt.forward, hw, hl, out agent))
            {
                hit = true;
                where = agent.position;
            }
            if (!hit) return;
            bumpCooldown = 2f;
            PedestrianBumps++;
            // Le choc ralentit le véhicule
            if (Player.Body != null) Compat.SetVelocity(Player.Body, Compat.GetVelocity(Player.Body) * 0.6f);
            Say("Piéton", Session.Dialogue.Line("pieton_bouscule", "", Session.Dialogue.Preference));
            if (UI != null) UI.Toast("Vous avez bousculé un piéton !");
            if (State == RideState.EnCourse && Metrics != null)
            {
                Metrics.Collisions++;
                Comment("collision");
            }
            Infraction(PoliceSystem.Pedestrian, where);
        }

        /// <summary>Refus de s'arrêter devant un agent à pied : noté, le prochain contrôle sera plus sévère.</summary>
        private void RefuseStop()
        {
            Session.Profile.rechercheParPolice = true;
            Say("Police", Session.Dialogue.Line("police_fuite", ""));
            if (UI != null) UI.Toast("Refus d'obtempérer : l'agent a relevé votre plaque. Le prochain contrôle sera plus sévère.");
            pendingInfraction = null;
            stopOfficer = null;
            stopByPost = false;
        }

        private void Infraction(string id) { Infraction(id, Player.transform.position); }

        /// <summary>Infraction vue par la police (agent d'un carrefour à moins de 75 m, ou contrôle routier) : sifflet, arrêt.</summary>
        private void Infraction(string id, Vector3 where)
        {
            if (pendingInfraction != null || policeChase || policePanelOpen) return;
            PoliceCheckpoint cp = World.Checkpoint;
            bool cpNear = cp != null && Flat(cp.transform.position - Player.transform.position) <= cp.Radius;
            Transform officer = PolicePatrol.Instance != null ? PolicePatrol.Instance.NearestOfficer(where, PolicePatrol.SightRadius) : null;
            if (!cpNear && officer == null) return;
            stopOfficer = cpNear ? null : officer;
            stopByPost = !cpNear;
            pendingInfraction = id;
            policeStopTimer = 0f;
            Say("Police", Session.Dialogue.Line("police_stop", "", Session.Dialogue.Preference));
            if (AmbientAudio.Instance != null) AmbientAudio.Instance.Play(AudioSynth.Whistle, 0.6f);
            if (UI != null && stopByPost) UI.Toast("Coup de sifflet : un agent vous arrête (" + Session.Police.InfractionLabel(id).ToLowerInvariant() + "). Rangez-vous.");
        }

        private void StartChase()
        {
            PoliceCheckpoint cp = World.Checkpoint;
            if (cp == null || cp.Bus == null) return;
            policeChase = true;
            chaseEscapeTimer = 0f;
            cp.Bus.Pursuing = true;
            cp.Bus.PursuitTarget = Player.transform;
            cp.Bus.Sirens = true;
            Say("Police", Session.Dialogue.Line("police_fuite", ""));
            if (UI != null) UI.Toast("Refus d'obtempérer : le bus Kabasele vous poursuit !");
        }

        private void EndChase()
        {
            policeChase = false;
            if (World.Checkpoint != null) World.Checkpoint.ReturnBus();
        }

        /// <summary>Appelé par le panneau de police une fois le contrôle terminé.</summary>
        public void PoliceResolved()
        {
            pendingInfraction = null;
            policePanelOpen = false;
            // L'agent retourne à son poste
            if (stopByPost && PolicePatrol.Instance != null) PolicePatrol.Instance.Release();
            stopOfficer = null;
            stopByPost = false;
            EndChase();
        }

        /// <summary>Essais automatiques (photos de contrôle) : simule un piéton bousculé à cet endroit.</summary>
        public void TestPedestrianBump(Vector3 where)
        {
            PedestrianBumps++;
            Say("Piéton", Session.Dialogue.Line("pieton_bouscule", "", Session.Dialogue.Preference));
            Infraction(PoliceSystem.Pedestrian, where);
        }

        // ==================================================================
        // Mission scénarisée : suivre le bus Kabasele
        // ==================================================================

        private void UpdateFollowMission(float dt)
        {
            MissionDef m = Session.Missions.ActiveFollowMission();
            if (m == null)
            {
                if (escort != null) { Traffic.Despawn(escort); escort = null; }
                return;
            }
            if (escort == null)
            {
                if (World.Checkpoint == null || World.FollowPath == null || Traffic == null) return;
                if (Flat(World.Checkpoint.transform.position - Player.transform.position) < 45f && Player.SpeedKmh < 20f)
                {
                    float s = World.FollowPath.ClosestS(World.Checkpoint.transform.position);
                    escort = Traffic.Spawn(AIVehicleType.PoliceKabasele, World.FollowPath, s, 11f);
                    escort.Special = true;
                    escort.IgnoreLights = true;
                    escort.Sirens = true;
                    followSeconds = 0f;
                    followLost = 0f;
                    Say("Agent", "Suis le bus Kabasele, ne le perds pas de vue !");
                }
                return;
            }
            float d = Flat(escort.transform.position - Player.transform.position);
            if (d < 60f) { followSeconds += dt; followLost = 0f; }
            else followLost += dt;
            Session.Missions.OnFollowProgress((int)followSeconds);
            if (followLost > 8f)
            {
                Session.Missions.OnFollowFailed();
                Traffic.Despawn(escort);
                escort = null;
            }
        }

        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
    }
}
