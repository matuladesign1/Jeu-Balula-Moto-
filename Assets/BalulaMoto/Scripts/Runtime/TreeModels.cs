// BALULA MOTO — arbres du jeu : les modèles fournis par MATULA DESIGN (Blend Swap, licence CC0) remplacent tous les
// anciens arbres dessinés (cours des parcelles, espaces verts des boulevards, esplanades des monuments, ronds-points,
// piste d'essai). Quatre essences : arbre feuillu simple (« Low-Poly Tree », laser blaster), chêne, érable et pin
// (« Low-poly foliege », EugeneKiver). Les arbres d'une case de ville sont fusionnés par matériau (DecorBatch).
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public static class TreeModels
    {
        private static readonly string[] Paths = { "Decor/Arbre_Simple", "Decor/Arbre_Chene", "Decor/Arbre_Erable", "Decor/Arbre_Pin" };
        private static DecorModel[] models;

        private static void Load()
        {
            if (models != null) return;
            models = new DecorModel[Paths.Length];
            for (int i = 0; i < Paths.Length; i++) models[i] = DecorModels.Get(Paths[i]);
        }

        /// <summary>Vrai si au moins l'arbre simple est présent dans le jeu.</summary>
        public static bool Ready { get { Load(); return models[0] != null; } }

        /// <summary>Essence tirée au sort : surtout l'arbre simple (léger), quelques chênes, érables et pins.
        /// Qualité basse : arbre simple seulement.</summary>
        public static DecorModel Pick(System.Random rnd)
        {
            Load();
            if (GameSettings.Quality == 0 || rnd == null) return models[0];
            int roll = rnd.Next(100);
            int k = roll < 68 ? 0 : (roll < 79 ? 1 : (roll < 90 ? 2 : 3));
            return models[k] ?? models[0];
        }

        /// <summary>Hauteur naturelle du modèle (m).</summary>
        public static float NaturalHeight(DecorModel m)
        {
            return m != null ? Mathf.Max(0.5f, m.Bounds.max.y) : 7f;
        }

        /// <summary>Ajoute un arbre au lot de la case : pied au point donné, hauteur voulue, orientation au hasard.</summary>
        public static DecorModel Add(DecorBatch batch, Vector3 foot, float height, System.Random rnd)
        {
            if (batch == null) return null;
            DecorModel m = Pick(rnd);
            if (m == null) return null;
            float yaw = rnd != null ? (float)rnd.NextDouble() * 360f : 0f;
            float s = height / NaturalHeight(m);
            // Légère variation de silhouette (plus ou moins large)
            float w = rnd != null ? 0.9f + (float)rnd.NextDouble() * 0.25f : 1f;
            batch.Add(m, Matrix4x4.TRS(foot, Quaternion.Euler(0f, yaw, 0f), new Vector3(s * w, s, s * w)));
            return m;
        }

        /// <summary>Pose un arbre isolé (piste d'essai), avec un tronc solide.</summary>
        public static GameObject Place(Transform parent, Vector3 foot, float height, System.Random rnd)
        {
            DecorModel m = Pick(rnd);
            if (m == null) return null;
            float s = height / NaturalHeight(m);
            Matrix4x4 w = parent.localToWorldMatrix * Matrix4x4.TRS(foot, Quaternion.Euler(0f, rnd != null ? (float)rnd.NextDouble() * 360f : 0f, 0f), Vector3.one * s);
            GameObject g = DecorModels.Place(m, parent, w, "Arbre");
            if (g != null)
            {
                BoxCollider bc = new GameObject("Tronc").AddComponent<BoxCollider>();
                bc.transform.SetParent(parent, false);
                bc.transform.localPosition = foot + Vector3.up * 1.3f;
                bc.size = new Vector3(0.55f, 2.6f, 0.55f);
            }
            return g;
        }

        /// <summary>Rayon de la couronne (m) d'un arbre de cette hauteur (pour espacer les arbres).</summary>
        public static float CrownRadius(float height) { return height * 0.55f; }
    }
}
