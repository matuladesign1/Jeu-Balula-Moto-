// BALULA MOTO — utilitaires indépendants de Unity : monnaie, géographie, hasard, texte.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BalulaMoto.Core
{
    /// <summary>Franc congolais : toutes les transactions du jeu sont en CDF.</summary>
    public static class Cdf
    {
        public static string Format(long amount)
        {
            string s = Math.Abs(amount).ToString("N0", CultureInfo.InvariantCulture).Replace(",", " ");
            return (amount < 0 ? "-" : "") + s + " FC";
        }

        public static string FormatSigned(long amount)
        {
            return (amount > 0 ? "+" : "") + Format(amount);
        }

        public static string FormatUsd(double usd)
        {
            return "≈ " + Math.Round(usd).ToString("N0", CultureInfo.InvariantCulture).Replace(",", " ") + " $";
        }

        public static long RoundTo(double value, long step)
        {
            if (step <= 1) return (long)Math.Round(value);
            return (long)(Math.Round(value / step) * step);
        }

        public static long RoundUpTo(double value, long step)
        {
            if (step <= 1) return (long)Math.Ceiling(value);
            return (long)(Math.Ceiling(value / step) * step);
        }

        public static long RoundDownTo(double value, long step)
        {
            if (step <= 1) return (long)Math.Floor(value);
            return (long)(Math.Floor(value / step) * step);
        }
    }

    /// <summary>
    /// Projection locale (plan tangent équirectangulaire) : X = est, Z = nord, en mètres.
    /// Erreur inférieure à 0,1 % sur l'étendue de Kinshasa, suffisante pour la reconstruction.
    /// </summary>
    public static class GeoProjection
    {
        public const double MetersPerDegreeLat = 110574.0;
        public const double MetersPerDegreeLonAtEquator = 111320.0;

        public static void ToLocal(double lat, double lon, double originLat, double originLon, out double x, out double z)
        {
            z = (lat - originLat) * MetersPerDegreeLat;
            x = (lon - originLon) * MetersPerDegreeLonAtEquator * Math.Cos(originLat * Math.PI / 180.0);
        }

        public static void ToLatLon(double x, double z, double originLat, double originLon, out double lat, out double lon)
        {
            lat = originLat + z / MetersPerDegreeLat;
            lon = originLon + x / (MetersPerDegreeLonAtEquator * Math.Cos(originLat * Math.PI / 180.0));
        }

        /// <summary>Distance orthodromique (haversine) en mètres.</summary>
        public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000.0;
            double p1 = lat1 * Math.PI / 180.0, p2 = lat2 * Math.PI / 180.0;
            double dp = (lat2 - lat1) * Math.PI / 180.0, dl = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(dp / 2) * Math.Sin(dp / 2) + Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
            return 2 * R * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }

    /// <summary>Générateur aléatoire déterministe (pour les tests et la reproductibilité).</summary>
    public sealed class GameRandom
    {
        private readonly Random random;

        public GameRandom(int seed) { random = new Random(seed); }

        public float Value() { return (float)random.NextDouble(); }

        public float Range(float min, float max) { return min + (max - min) * (float)random.NextDouble(); }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return random.Next(minInclusive, maxExclusive);
        }

        public bool Chance(float probability) { return random.NextDouble() < probability; }

        public T Pick<T>(IList<T> list)
        {
            if (list == null || list.Count == 0) return default(T);
            return list[random.Next(list.Count)];
        }

        public int WeightedIndex(IList<int> weights)
        {
            int total = 0;
            for (int i = 0; i < weights.Count; i++) total += Math.Max(0, weights[i]);
            if (total <= 0) return weights.Count > 0 ? random.Next(weights.Count) : -1;
            int roll = random.Next(total);
            for (int i = 0; i < weights.Count; i++)
            {
                roll -= Math.Max(0, weights[i]);
                if (roll < 0) return i;
            }
            return weights.Count - 1;
        }
    }

    public static class TextUtil
    {
        /// <summary>Remplace {cle} par sa valeur. Paires : "cle", "valeur", "cle2", "valeur2"...</summary>
        public static string Fill(string template, params string[] keyValues)
        {
            if (string.IsNullOrEmpty(template) || keyValues == null) return template ?? "";
            StringBuilder sb = new StringBuilder(template);
            for (int i = 0; i + 1 < keyValues.Length; i += 2)
                sb.Replace("{" + keyValues[i] + "}", keyValues[i + 1] ?? "");
            return sb.ToString();
        }

        /// <summary>Minuscules sans accents, pour comparer des noms de lieux.</summary>
        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char ch in s.ToLowerInvariant())
            {
                switch (ch)
                {
                    case 'à': case 'â': case 'ä': case 'á': sb.Append('a'); break;
                    case 'é': case 'è': case 'ê': case 'ë': sb.Append('e'); break;
                    case 'î': case 'ï': case 'í': sb.Append('i'); break;
                    case 'ô': case 'ö': case 'ó': sb.Append('o'); break;
                    case 'ù': case 'û': case 'ü': case 'ú': sb.Append('u'); break;
                    case 'ç': sb.Append('c'); break;
                    case '\'': case '’': case '-': case '_': sb.Append(' '); break;
                    default: sb.Append(ch); break;
                }
            }
            return sb.ToString().Trim();
        }

        /// <summary>Étoiles en caractères sûrs pour la police par défaut d'Unity (pas d'emoji).</summary>
        public static string Stars(float value)
        {
            int full = (int)Math.Round(value);
            if (full < 0) full = 0;
            if (full > 5) full = 5;
            return new string('*', full) + new string('·', 5 - full);
        }

        public static string Rating(float value)
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + "/5";
        }

        public static string Clock(float minuteOfDay)
        {
            int m = (int)minuteOfDay;
            return (m / 60 % 24).ToString("00") + ":" + (m % 60).ToString("00");
        }
    }

    public static class MathUtil
    {
        public static float Clamp(float v, float min, float max) { return v < min ? min : (v > max ? max : v); }
        public static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
    }
}
