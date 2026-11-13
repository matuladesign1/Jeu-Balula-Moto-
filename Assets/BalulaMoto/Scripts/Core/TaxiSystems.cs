// BALULA MOTO — cœur du jeu : clients, tarifs, négociation, courses, satisfaction, réputation.
using System;
using System.Collections.Generic;

namespace BalulaMoto.Core
{
    public sealed class RideContext
    {
        public bool Night;
        public bool Rain;
        public bool RushHour;
        public float ReputationStars = 3f;
    }

    public static class FareCalculator
    {
        /// <summary>Prix de référence d'une course (ce que "tout le monde" paie à peu près).</summary>
        public static long ReferenceFare(FareRules r, float distanceKm, VehicleKind kind, int passengers, float cargoKg, RideContext ctx)
        {
            double fare;
            if (kind == VehicleKind.Moto)
                fare = r.baseMoto + r.parKmMoto * distanceKm;
            else
                fare = (r.baseTricyclePassager + r.parKmTricyclePassager * distanceKm) * Math.Max(1, passengers);
            if (kind == VehicleKind.Moto && passengers >= 2) fare *= 1.7;
            if (ctx != null)
            {
                if (ctx.Night) fare *= 1.0 + r.majorationNuit;
                if (ctx.Rain) fare *= 1.0 + r.majorationPluie;
                if (ctx.RushHour) fare *= 1.0 + r.majorationPointe;
            }
            if (cargoKg > 0f) fare *= 1.0 + r.majorationMarchandises * Math.Min(1.0, cargoKg / 100.0);
            long rounded = Cdf.RoundTo(fare, Math.Max(1, r.arrondi));
            return Math.Max(r.minimum, rounded);
        }

        public static float RoadDistanceKm(FareRules r, PlaceRef a, PlaceRef b)
        {
            return (float)(a.DistanceTo(b) / 1000.0 * Math.Max(1f, r.facteurDetour));
        }

        public static float ExpectedMinutes(FareRules r, float distanceKm)
        {
            float speed = r.vitesseMoyenneKmh > 1f ? r.vitesseMoyenneKmh : 22f;
            return distanceKm / speed * 60f + 1.0f;
        }
    }

    public sealed class ClientRequest
    {
        public string Name;
        public bool Female;
        public string TypeId;
        public string TypeLabel;
        public string Outfit;
        public PlaceRef Origin;
        public PlaceRef Destination;
        public float DistanceKm;
        public int Passengers = 1;
        public float CargoKg;
        public bool IsDelivery;
        public long ReferenceFare;
        public long MaxPrice;
        public long TargetPrice;
        public int Patience;
        public float Stubbornness;
        public DialogueLanguage Language;
        public bool Night;
        public bool Rain;
        public bool RushHour;
    }

    public static class ClientGenerator
    {
        /// <summary>
        /// Génère un client à "origin" pour le véhicule donné. Respecte la capacité réelle du véhicule
        /// et l'édit de Kinshasa (avril 2026) : pas de mineurs ni de femmes enceintes sur les taxis-motos.
        /// </summary>
        public static ClientRequest Generate(GameDatabase db, PlaceRef origin, IList<PlaceRef> places, VehicleDef vehicle, RideContext ctx, GameRandom rng)
        {
            if (places == null || places.Count < 2 || vehicle == null) return null;
            ClientsConfig cfg = db.Clients;
            FareRules rules = db.Economy.tarifs;

            // Type de client compatible avec le véhicule
            List<ClientType> types = new List<ClientType>();
            List<int> weights = new List<int>();
            foreach (ClientType t in cfg.types)
            {
                if (vehicle.IsMoto && !t.motoAutorisee) continue;
                types.Add(t);
                weights.Add(Math.Max(1, t.poids));
            }
            ClientType type = types.Count > 0 ? types[rng.WeightedIndex(weights)] : null;

            // Destination : pondérée par la popularité, ni trop près ni trop loin
            List<PlaceRef> candidates = new List<PlaceRef>();
            List<int> destWeights = new List<int>();
            foreach (PlaceRef p in places)
            {
                if (p == origin || p.Id == origin.Id) continue;
                double d = origin.DistanceTo(p);
                if (d < 250 || d > 30000) continue;
                candidates.Add(p);
                destWeights.Add(Math.Max(1, p.Weight));
            }
            if (candidates.Count == 0) return null;
            PlaceRef dest = candidates[rng.WeightedIndex(destWeights)];

            ClientRequest c = new ClientRequest();
            c.Female = rng.Chance(0.48f);
            string first = rng.Pick(c.Female ? cfg.prenomsFemmes : cfg.prenomsHommes) ?? "Client";
            string last = cfg.postnoms.Count > 0 ? " " + rng.Pick(cfg.postnoms) : "";
            c.Name = first + last;
            c.TypeId = type != null ? type.id : "standard";
            c.TypeLabel = type != null ? type.libelle : "Client";
            c.Outfit = type != null ? type.tenue : "civil";
            c.Origin = origin;
            c.Destination = dest;
            c.DistanceKm = FareCalculator.RoadDistanceKm(rules, origin, dest);
            c.Night = ctx != null && ctx.Night;
            c.Rain = ctx != null && ctx.Rain;
            c.RushHour = ctx != null && ctx.RushHour;

            // Passagers et marchandises selon la capacité réelle
            int maxPassengers = Math.Max(1, vehicle.passagers);
            if (type != null && maxPassengers > 1 && rng.Chance(type.probaGroupe))
                c.Passengers = rng.Range(2, Math.Min(maxPassengers, vehicle.IsMoto ? 2 : 5) + 1);
            if (type != null && vehicle.chargeUtileKg > 0f && rng.Chance(type.probaMarchandises))
            {
                float maxCargo = vehicle.IsMoto ? Math.Min(40f, vehicle.chargeUtileKg) : vehicle.chargeUtileKg * 0.8f;
                c.CargoKg = (float)Math.Round(rng.Range(10f, Math.Max(12f, maxCargo)));
                c.IsDelivery = vehicle.Category == VehicleCategory.TricycleMarchandises || vehicle.Category == VehicleCategory.TricycleGrandeCapacite || rng.Chance(0.3f);
                if (vehicle.Category == VehicleCategory.TricycleMarchandises || vehicle.Category == VehicleCategory.TricycleGrandeCapacite) c.Passengers = 1;
            }

            c.ReferenceFare = FareCalculator.ReferenceFare(rules, c.DistanceKm, vehicle.Kind, c.Passengers, c.CargoKg, ctx);
            float budget = type != null ? type.facteurBudget : 1f;
            float rep = ctx != null ? ctx.ReputationStars : 3f;
            // Calibré par simulation : annoncer le tarif juste aboutit presque toujours (≈ 0,9 × tarif obtenu),
            // surfacturer en insistant échoue deux fois sur trois.
            float willingness = budget * rng.Range(1.0f, 1.5f) * (1f + 0.05f * (rep - 3f));
            long step = Math.Max(1, rules.arrondi);
            c.MaxPrice = Math.Max(rules.minimum, Cdf.RoundTo(c.ReferenceFare * willingness, step));
            c.TargetPrice = Math.Max(rules.minimum, Cdf.RoundDownTo(c.MaxPrice * rng.Range(0.7f, 0.9f), step));
            if (c.TargetPrice >= c.MaxPrice) c.TargetPrice = Math.Max(rules.minimum, c.MaxPrice - step);
            c.Stubbornness = type != null ? MathUtil.Clamp01(type.obstination + rng.Range(-0.15f, 0.15f)) : 0.5f;
            c.Patience = type != null ? rng.Range(type.patienceMin, type.patienceMax + 1) : 3;
            int lang = rng.Range(0, 10);
            c.Language = lang < 4 ? DialogueLanguage.Francais : (lang < 7 ? DialogueLanguage.Lingala : DialogueLanguage.Mixte);
            return c;
        }
    }

    public enum NegotiationOutcome { Accepted, CounterOffer, ClientLeft, Refused }

    public sealed class NegotiationStep
    {
        public NegotiationOutcome Outcome;
        public long Price;
        public string LineKey;
    }

    /// <summary>
    /// Négociation à la kinoise : le client demande "combien ?", le chauffeur annonce un prix,
    /// le client accepte, fait une contre-offre ("Fais-moi 1 500") ou s'en va.
    /// </summary>
    public sealed class Negotiation
    {
        private readonly GameRandom rng;
        private readonly long step;
        private readonly long minimum;
        public ClientRequest Client { get; private set; }
        public long ClientOffer { get; private set; }
        public long LastPlayerOffer { get; private set; }
        public int RoundsLeft { get; private set; }
        public bool Finished { get; private set; }
        public long AgreedPrice { get; private set; }
        public float RespectPenalty { get; private set; }
        public int Rounds { get; private set; }

        public Negotiation(ClientRequest client, FareRules rules, GameRandom random)
        {
            Client = client;
            rng = random;
            step = Math.Max(1, rules.arrondi);
            minimum = rules.minimum;
            RoundsLeft = Math.Max(1, client.Patience);
        }

        /// <summary>Prix proposés par défaut dans l'interface : bas, juste, haut.</summary>
        public long[] SuggestedOffers()
        {
            long r = Client.ReferenceFare;
            return new[] { Math.Max(minimum, r - step), r, r + step, r + 2 * step };
        }

        public NegotiationStep Propose(long offer)
        {
            NegotiationStep st = new NegotiationStep();
            if (Finished) { st.Outcome = NegotiationOutcome.Refused; return st; }
            offer = Math.Max(minimum, Cdf.RoundTo(offer, step));
            bool insisting = offer == LastPlayerOffer;
            LastPlayerOffer = offer;
            Rounds++;
            long target = Client.TargetPrice, max = Client.MaxPrice;

            if (offer <= target)
            {
                return Accept(offer, offer < Client.ReferenceFare * 0.85f ? "accepte_content" : "accepte");
            }
            if (offer > max * 1.6f)
            {
                RespectPenalty += 0.15f;
                if (rng.Chance(0.35f + 0.45f * Client.Stubbornness) || RoundsLeft <= 1)
                    return Leave("part_trop_cher");
            }
            float t = (offer - target) / (float)Math.Max(1, max - target);
            float pAccept = offer <= max ? (1f - t) * (1f - 0.5f * Client.Stubbornness) + 0.2f : 0.03f;
            if (insisting) pAccept += 0.12f;
            if (ClientOffer > 0 && offer - ClientOffer <= step) pAccept += 0.25f;
            if (rng.Chance(MathUtil.Clamp01(pAccept))) return Accept(offer, "accepte");

            RoundsLeft--;
            if (RoundsLeft <= 0) return Leave("part_patience");

            long counter;
            if (ClientOffer <= 0) counter = Cdf.RoundTo(target + (Math.Min(offer, max) - target) * 0.3f, step);
            else
            {
                float concession = MathUtil.Lerp(0.25f, 0.5f, 1f - Client.Stubbornness);
                counter = Cdf.RoundTo(ClientOffer + (Math.Min(offer, max) - ClientOffer) * concession, step);
                if (counter < ClientOffer) counter = ClientOffer;
            }
            counter = Math.Max(minimum, Math.Min(counter, max));
            if (counter >= offer) return Accept(offer, "accepte");
            ClientOffer = counter;
            st.Outcome = NegotiationOutcome.CounterOffer;
            st.Price = counter;
            st.LineKey = Rounds > 1 ? "contre_offre_insiste" : "contre_offre";
            return st;
        }

        public NegotiationStep AcceptClientOffer()
        {
            if (Finished || ClientOffer <= 0) return new NegotiationStep { Outcome = NegotiationOutcome.Refused };
            return Accept(ClientOffer, "accepte_monte");
        }

        public NegotiationStep RefuseRide()
        {
            Finished = true;
            return new NegotiationStep { Outcome = NegotiationOutcome.Refused, LineKey = "refus_chauffeur" };
        }

        private NegotiationStep Accept(long price, string key)
        {
            Finished = true;
            AgreedPrice = price;
            return new NegotiationStep { Outcome = NegotiationOutcome.Accepted, Price = price, LineKey = key };
        }

        private NegotiationStep Leave(string key)
        {
            Finished = true;
            return new NegotiationStep { Outcome = NegotiationOutcome.ClientLeft, LineKey = key };
        }
    }

    /// <summary>Mesures relevées pendant la course par le monde 3D.</summary>
    public sealed class RideMetrics
    {
        public float DistanceKm;
        public float DurationSeconds;
        public int HarshBrakes;
        public int Collisions;
        public float CollisionSeverity;
        public int RedLights;
        public float SpeedingSeconds;
        public int Jolts;
        public int HardTurns;
        public float RainSeconds;
        public float DropOffDistanceM;
        public bool Crashed;
        public int Honks;
    }

    public sealed class RideResult
    {
        public float PriceScore;
        public float TimeScore;
        public float SafetyScore;
        public float DrivingScore;
        public float ComfortScore;
        public float RespectScore;
        public float Satisfaction;
        public int Stars;
        public long Agreed;
        public long Paid;
        public long Tip;
        public int Xp;
        public string CommentKey;
        public bool Accident;
        public float ExpectedMinutes;
        public float ActualMinutes;
    }

    public static class SatisfactionCalculator
    {
        public static RideResult Evaluate(ClientRequest c, long agreed, RideMetrics m, VehicleStats st, FareRules rules, float respectPenalty, GameRandom rng)
        {
            RideResult r = new RideResult();
            r.Agreed = agreed;

            float ratio = agreed / (float)Math.Max(1, c.ReferenceFare);
            r.PriceScore = MathUtil.Clamp01(1f - (ratio - 0.9f) * 1.2f);

            r.ExpectedMinutes = FareCalculator.ExpectedMinutes(rules, c.DistanceKm);
            r.ActualMinutes = m.DurationSeconds / 60f;
            float tr = r.ActualMinutes / Math.Max(0.5f, r.ExpectedMinutes);
            r.TimeScore = MathUtil.Clamp01(1.3f - 0.5f * tr);

            r.SafetyScore = MathUtil.Clamp01(1f - 0.3f * m.Collisions - 0.6f * m.CollisionSeverity - 0.15f * m.RedLights - 0.004f * m.SpeedingSeconds - (m.Crashed ? 0.8f : 0f));
            r.DrivingScore = MathUtil.Clamp01(1f - 0.08f * m.HarshBrakes - 0.05f * m.HardTurns - 0.03f * m.Jolts - 0.02f * m.Honks);
            float rainPenalty = st != null && st.Kind == VehicleKind.Moto ? MathUtil.Clamp01(m.RainSeconds / 180f) * 0.35f : 0f;
            float comfortBase = st != null ? st.Comfort01 : 0.6f;
            r.ComfortScore = MathUtil.Clamp01(comfortBase * (1f - 0.025f * m.Jolts) + 0.25f - rainPenalty);
            float dropPenalty = MathUtil.Clamp01((m.DropOffDistanceM - 25f) / 150f) * 0.5f;
            float overcharge = ratio > 1.4f ? (ratio - 1.4f) * 0.8f : 0f;
            r.RespectScore = MathUtil.Clamp01(1f - respectPenalty - dropPenalty - overcharge);

            r.Satisfaction = r.PriceScore * 0.22f + r.TimeScore * 0.18f + r.SafetyScore * 0.22f + r.DrivingScore * 0.14f + r.ComfortScore * 0.12f + r.RespectScore * 0.12f;
            r.Accident = m.Crashed || m.CollisionSeverity > 0.5f;
            if (r.Accident) r.Satisfaction = Math.Min(r.Satisfaction, 0.3f);
            r.Stars = Math.Max(1, Math.Min(5, (int)Math.Round(1f + 4f * r.Satisfaction)));

            r.Paid = agreed;
            long step = Math.Max(1, rules.arrondi);
            if (r.Satisfaction < 0.25f && rng.Chance(0.6f))
            {
                r.Paid = Math.Max(rules.minimum / 2, Cdf.RoundDownTo(agreed * 0.7f, step));
                r.CommentKey = "paie_moins";
            }
            else if (r.Satisfaction > 0.85f && rng.Chance(0.45f))
            {
                r.Tip = Math.Max(step, Cdf.RoundTo(agreed * 0.15f, step));
                r.CommentKey = "pourboire";
            }
            else r.CommentKey = r.Stars >= 4 ? "paiement_content" : (r.Stars <= 2 ? "paiement_mecontent" : "paiement");

            r.Xp = Math.Max(5, 10 + (int)(c.DistanceKm * 5f) + (r.Stars - 3) * 5 + (c.IsDelivery ? 5 : 0));
            return r;
        }
    }

    public sealed class TaxiSystem
    {
        private readonly GameSession s;
        public TaxiSystem(GameSession session) { s = session; }

        public RideContext CurrentContext(bool raining)
        {
            return new RideContext { Night = s.IsNight, Rain = raining, RushHour = s.IsRushHour, ReputationStars = s.Profile.Reputation() };
        }

        public ClientRequest GenerateClient(PlaceRef origin, IList<PlaceRef> places, bool raining)
        {
            VehicleDef d = s.ActiveDef;
            if (d == null) return null;
            return ClientGenerator.Generate(s.Db, origin, places, d, CurrentContext(raining), s.Rng);
        }

        public Negotiation StartNegotiation(ClientRequest c)
        {
            return new Negotiation(c, s.Db.Economy.tarifs, s.Rng);
        }

        /// <summary>Fin de course : paiement, note, réputation, expérience et missions.</summary>
        public RideResult CompleteRide(ClientRequest c, long agreed, RideMetrics m, float respectPenalty)
        {
            VehicleStats st = s.ActiveStats();
            RideResult r = SatisfactionCalculator.Evaluate(c, agreed, m, st, s.Db.Economy.tarifs, respectPenalty, s.Rng);
            PlayerProfile p = s.Profile;

            string label = (c.IsDelivery ? "Livraison " : "Course ") + c.Origin.Name + " → " + c.Destination.Name;
            s.Economy.Earn(r.Paid, c.IsDelivery ? LedgerCategories.Livraison : LedgerCategories.Course, label);
            if (r.Tip > 0) s.Economy.Earn(r.Tip, LedgerCategories.Pourboire, c.Name);

            p.notes.Add(r.Stars);
            if (p.notes.Count > 50) p.notes.RemoveAt(0);
            p.nbCourses++;
            p.coursesDuJour++;
            if (c.IsDelivery) p.nbLivraisons++;
            bool accident = r.Accident || m.Collisions > 0;
            if (accident)
            {
                p.nbAccidents++;
                p.serieSansAccident = 0;
            }
            else
            {
                p.serieSansAccident++;
                if (p.serieSansAccident > p.meilleureSerie) p.meilleureSerie = p.serieSansAccident;
            }
            if (!string.IsNullOrEmpty(c.Destination.Id) && !p.lieuxVisites.Contains(c.Destination.Id))
            {
                p.lieuxVisites.Add(c.Destination.Id);
                s.Missions.OnLocationVisited(c.Destination.Id);
            }
            s.Career.AddXp(r.Xp);
            s.Missions.OnRideCompleted(c, r, c.RushHour);
            return r;
        }

        /// <summary>Le client descend en route (accident grave, refus) : pas de paiement, mauvaise note.</summary>
        public void AbortRide(ClientRequest c, string reason)
        {
            PlayerProfile p = s.Profile;
            p.notes.Add(1);
            if (p.notes.Count > 50) p.notes.RemoveAt(0);
            p.serieSansAccident = 0;
            s.Notify(c.Name + " est descendu(e) sans payer : " + reason);
        }
    }
}
